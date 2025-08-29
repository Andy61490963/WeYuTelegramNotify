using System.Net;
using System.Net.Mail;
using System.Text;
using WeYuTelegramNotify.interfaces;
using WeYuTelegramNotify.Models;
using WeYuTelegramNotify.Options;
using Microsoft.Extensions.Options;
using WeYuTelegramNotify.Helper;
using WeYuTelegramNotify.Repositories;
using WeYuTelegramNotify.Enum;

namespace WeYuTelegramNotify.Services;

public class EmailNotifyService : IEmailNotifyService
{
    private const string ApiSecret = "WeYuTelegraiApi";

    // ---- 參數 ----
    private const int SmtpTimeoutMs = 30_000;       // 連線/送信逾時
    private const int MaxRetry = 3;                 // 重試次數（遇暫時性錯誤）
    private const int BaseBackoffMs = 500;          // 退避起始（平方級：0.5s, 2s, 4.5s）
    private const int PerRecipientDelayMs = 120;    // 每封之間的節流（避免供應商限速）

    private readonly EmailSettingOptions _settings;
    private readonly IEmailGroupRepository _groupRepository;
    private readonly IEmailLogRepository _logRepository;
    private readonly ILogger<EmailNotifyService> _logger;

    public EmailNotifyService(
        IOptions<EmailSettingOptions> options,
        IEmailGroupRepository groupRepository,
        IEmailLogRepository logRepository,
        ILogger<EmailNotifyService> logger)
    {
        _settings = options.Value;
        _groupRepository = groupRepository;
        _logRepository = logRepository;
        _logger = logger;
    }

    /// <summary>
    /// 單發
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<EmailSendResult> SendAsync(EmailNotifyRequest request, CancellationToken cancellationToken = default)
    {
        Guid? logId = null;
        var started = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // 紀錄 LOG 摘要
            _logger.LogInformation("Email single: validating to={To}, subjectLen={SubjectLen}, bodyLen={BodyLen}",
                request.Email,
                request.Subject,
                request.Body);
            
            // --- 基本檢查 ---
            if (!request.SecretKey.MatchesSecret(ApiSecret))
                return new EmailSendResult { Success = false, Error = "未授權" };
            if (string.IsNullOrWhiteSpace(request.Email))
                return new EmailSendResult { Success = false, Error = "Email is required." };
            if (string.IsNullOrWhiteSpace(request.Subject))
                return new EmailSendResult { Success = false, Error = "Subject is required." };
            if (string.IsNullOrWhiteSpace(request.Body))
                return new EmailSendResult { Success = false, Error = "Body is required." };

            // --- Email 標頭防護、格式驗證 ---
            GuardHeader(request.Subject);
            var normalizedEmail = NormalizeEmail(request.Email);
            var to = new MailAddress(normalizedEmail); // 會拋格式錯
            
            // --- 建立主 Log ---
            var log = new EmailLog
            {
                EMAIL_GROUP_ID = Guid.Empty,
                SUBJECT = request.Subject,
                BODY = request.Body,
                STATUS = SendStatus.Queued,
                CREATED_AT = DateTime.UtcNow,
                RETRY_COUNT = 0
            };
            logId = await _logRepository.InsertLogAsync(log, cancellationToken).ConfigureAwait(false);

            using var scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["logId"] = logId
            });
            _logger.LogInformation("Email single queued. logId={LogId}", logId);
            
            // --- 重用同一個 SMTP 連線 ---
            using var smtp = CreateSmtpClient();

            // --- 送信 ---
            await SendOneAsync(smtp, to, request.Subject, request.Body, cancellationToken).ConfigureAwait(false);
            
            _logger.LogInformation("Email single success to={To} elapsedMs={Elapsed}",
                to.Address, started.Elapsed.TotalMilliseconds);
            
            await _logRepository.UpdateLogStatusAsync(logId.Value, (byte)SendStatus.Success, null, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);
            return new EmailSendResult { Success = true, LogId = logId };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email single failed. logId={LogId}, to={To}", logId, request.Email);
            if (logId.HasValue)
            {
                try
                {
                    await _logRepository.UpdateLogStatusAsync(logId.Value, (byte)SendStatus.Failed, ex.Message, null, cancellationToken).ConfigureAwait(false);
                }
                catch { /* */ }
            }
            return new EmailSendResult { Success = false, Error = ex.Message, LogId = logId };
        }
    }

    /// <summary>
    /// 群發(逐發)
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<GroupEmailSendResult> SendGroupAsync(GroupEmailNotifyRequest request, CancellationToken cancellationToken = default)
    {
        Guid? logId = null;
        var started = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Email group: validating groupId={GroupId}, subjectLen={SubjectLen}, bodyLen={BodyLen}",
                request.GroupId, request.Subject, request.Body);
            
            if (!request.SecretKey.MatchesSecret(ApiSecret))
                return new GroupEmailSendResult { Success = false, Error = "未授權" };
            if (request.GroupId == Guid.Empty)
                return new GroupEmailSendResult { Success = false, Error = "GroupId is required." };
            if (string.IsNullOrWhiteSpace(request.Subject))
                return new GroupEmailSendResult { Success = false, Error = "Subject is required." };
            if (string.IsNullOrWhiteSpace(request.Body))
                return new GroupEmailSendResult { Success = false, Error = "Body is required." };

            GuardHeader(request.Subject);

            // 抓名單去重、格式驗證
            var emails = (await _groupRepository.GetEmailsByGroupAsync(request.GroupId, cancellationToken).ConfigureAwait(false))
                .Select(NormalizeEmail)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var validRecipients = emails
                .Select(e =>
                {
                    try { return new MailAddress(e); }
                    catch { return null; }
                })
                .Where(x => x != null)
                .Cast<MailAddress>()
                .ToList();

            _logger.LogInformation("Email group: recipients total={Total}, valid={Valid}", 
                (await _groupRepository.GetEmailsByGroupAsync(request.GroupId, cancellationToken))?.Count() ?? 0,
                validRecipients.Count);
            
            if (validRecipients.Count == 0)
                return new GroupEmailSendResult { Success = false, Error = "No active/valid emails found." };

            // 建立群發 Log
            var log = new EmailLog
            {
                EMAIL_GROUP_ID = request.GroupId,
                SUBJECT = request.Subject,
                BODY = request.Body,
                STATUS = SendStatus.Queued,
                CREATED_AT = DateTime.UtcNow,
                RETRY_COUNT = 0
            };
            logId = await _logRepository.InsertLogAsync(log, cancellationToken).ConfigureAwait(false);
            
            using var scope = _logger.BeginScope(new Dictionary<string, object?> {
                ["logId"] = logId, ["groupId"] = request.GroupId
            });
            _logger.LogInformation("Email group queued. logId={LogId}, groupId={GroupId}", logId, request.GroupId);
            
            int sent = 0, failed = 0;
            using var smtp = CreateSmtpClient();

            foreach (var addr in validRecipients)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogDebug("Email Send start -> {To}", addr.Address);
                try
                {
                    await SendOneAsync(smtp, addr, request.Subject, request.Body, cancellationToken).ConfigureAwait(false);
                    sent++;
                    _logger.LogDebug("Email Send ok -> {To}", addr.Address);
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogWarning(ex, "Email Send fail -> {To}", addr.Address);
                }

                // 簡單節流，不要對 Smtp server 瘋狂請求
                await Task.Delay(PerRecipientDelayMs, cancellationToken).ConfigureAwait(false);
            }

            // 更新 Log 狀態（全部失敗才算失敗；有成功就算成功）
            var status = (sent > 0) ? SendStatus.Success : SendStatus.Failed;
            var note = (failed > 0 && sent > 0) ? $"Partial failed: {failed}." : (failed > 0 ? "All failed." : null);

            _logger.LogInformation("Email group done. sent={Sent}, failed={Failed}, elapsedMs={Elapsed}",
                sent, failed, started.Elapsed.TotalMilliseconds);
            
            await _logRepository.UpdateLogStatusAsync(
                logId.Value, (byte)status, note, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);

            return new GroupEmailSendResult
            {
                Success = sent > 0,
                SentCount = sent,
                Error = (sent == 0) ? "All recipients failed." : null,
                LogId = logId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email group failed. logId={LogId}, groupId={GroupId}", logId, request.GroupId);
            if (logId.HasValue)
            {
                try
                {
                    await _logRepository.UpdateLogStatusAsync(logId.Value, (byte)SendStatus.Failed, ex.Message, null, cancellationToken).ConfigureAwait(false);
                }
                catch { /* */ }
            }
            return new GroupEmailSendResult { Success = false, Error = ex.Message, LogId = logId };
        }
    }

    /// <summary>
    /// 單封寄送
    /// </summary>
    /// <param name="smtp"></param>
    /// <param name="to"></param>
    /// <param name="subject"></param>
    /// <param name="body"></param>
    /// <param name="ct"></param>
    private async Task SendOneAsync(SmtpClient smtp, MailAddress to, string subject, string body, CancellationToken ct)
    {
        GuardHeader(to.Address);

        for (int i = 0; ; i++)
        {
            using var msg = BuildMessage(subject, body);
            msg.To.Add(to);

            try
            {
                await smtp.SendMailAsync(msg, ct).ConfigureAwait(false);
                return; // success
            }
            catch (SmtpFailedRecipientException ex)
            {
                _logger.LogWarning(ex, "Recipient failed (will{WillRetry}) to={To}, attempt={Attempt}/{Max}",
                    i < MaxRetry - 1 ? " retry" : " not retry",
                    to.Address, i + 1, MaxRetry);
                
                // 單一收件者層級錯誤
                if (i >= MaxRetry - 1) throw;
                // 少數收件端會回暫時性 4xx，但 SmtpFailedRecipientException 沒有狀態碼區分
                await Task.Delay(BackoffDelay(i), ct).ConfigureAwait(false);
            }
            catch (SmtpException ex) when (i < MaxRetry - 1 && IsTransient(ex.StatusCode))
            {
                var delay = BackoffDelay(i).TotalMilliseconds;
                _logger.LogWarning(ex, "Transient SMTP error: status={Status}, to={To}, attempt={Attempt}/{Max}, backoffMs={Delay}",
                    ex.StatusCode, to.Address, i + 1, MaxRetry, delay);
                
                await Task.Delay(BackoffDelay(i), ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 遇到錯誤 等一下再繼續發
    /// </summary>
    /// <param name="attemptIndex"></param>
    /// <returns></returns>
    private static TimeSpan BackoffDelay(int attemptIndex)
        => TimeSpan.FromMilliseconds(BaseBackoffMs * (attemptIndex + 1) * (attemptIndex + 1)); // 0.5s, 2s, 4.5s

    private static bool IsTransient(SmtpStatusCode code)
        => code is SmtpStatusCode.MailboxBusy
               or SmtpStatusCode.MailboxUnavailable
               or SmtpStatusCode.InsufficientStorage
               or SmtpStatusCode.ServiceNotAvailable
               or SmtpStatusCode.TransactionFailed;

    /// <summary>
    /// 建立連線
    /// </summary>
    /// <returns></returns>
    private SmtpClient CreateSmtpClient()
    {
        return new SmtpClient(_settings.ExternalSMTP, _settings.Port)
        {
            Credentials = new NetworkCredential(_settings.From, _settings.Sw),
            EnableSsl = _settings.EnableSSL,
            Timeout = SmtpTimeoutMs,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };
    }

    /// <summary>
    /// 建立訊息
    /// </summary>
    /// <param name="subject"></param>
    /// <param name="body"></param>
    /// <param name="isHtml"></param>
    /// <param name="priority"></param>
    /// <param name="replyTo"></param>
    /// <returns></returns>
    private MailMessage BuildMessage(
        string subject,
        string body,
        bool isHtml = true,
        MailPriority priority = MailPriority.Normal,
        string? replyTo = null)
    {
        GuardHeader(subject);
        if (!string.IsNullOrWhiteSpace(replyTo))
        {
            var rt = new MailAddress(NormalizeEmail(replyTo));
            GuardHeader(rt.Address);
        }
        
        var msg = new MailMessage
        {
            From = new MailAddress(_settings.From),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            Body = body,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = isHtml,
            Priority = priority
        };

        if (!string.IsNullOrWhiteSpace(replyTo))
            msg.ReplyToList.Add(new MailAddress(NormalizeEmail(replyTo)));

        return msg;
    }

    /// <summary>
    /// 防止郵件標頭注入（Header Injection)
    /// </summary>
    /// <param name="s"></param>
    /// <exception cref="InvalidOperationException"></exception>
    private static void GuardHeader(string? s)
    {
        if (string.IsNullOrEmpty(s)) return;
        if (s.Contains("\r") || s.Contains("\n"))
            throw new InvalidOperationException("Header injection suspected.");
    }

    /// <summary>
    /// 正規化
    /// </summary>
    /// <param name="email"></param>
    /// <returns></returns>
    private static string NormalizeEmail(string email)
        => email.Trim().ToLowerInvariant();
}
