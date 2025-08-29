using System;
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
    private readonly EmailSettingOptions _settings;
    private readonly IEmailGroupRepository _groupRepository;
    private readonly IEmailLogRepository _logRepository;

    public EmailNotifyService(IOptions<EmailSettingOptions> options, IEmailGroupRepository groupRepository, IEmailLogRepository logRepository)
    {
        _settings = options.Value;
        _groupRepository = groupRepository;
        _logRepository = logRepository;
    }

    public async Task<EmailSendResult> SendAsync(EmailNotifyRequest request, CancellationToken cancellationToken = default)
    {
        Guid? logId = null;
        try
        {
            if (!request.SecretKey.MatchesSecret(ApiSecret))
                return new EmailSendResult { Success = false, Error = "未授權" };

            if (string.IsNullOrWhiteSpace(request.Email))
                return new EmailSendResult { Success = false, Error = "Email is required." };

            if (string.IsNullOrWhiteSpace(request.Subject))
                return new EmailSendResult { Success = false, Error = "Subject is required." };

            if (string.IsNullOrWhiteSpace(request.Body))
                return new EmailSendResult { Success = false, Error = "Body is required." };

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

            var (smtpClient, mailMessage) = CreateMailComponents(request.Subject, request.Body);
            mailMessage.To.Add(new MailAddress(request.Email));
            await smtpClient.SendMailAsync(mailMessage, cancellationToken).ConfigureAwait(false);

            await _logRepository.UpdateLogStatusAsync(logId.Value, (byte)SendStatus.Success, null, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);

            return new EmailSendResult { Success = true, LogId = logId };
        }
        catch (Exception ex)
        {
            if (logId.HasValue)
            {
                try
                {
                    await _logRepository.UpdateLogStatusAsync(logId.Value, (byte)SendStatus.Failed, ex.Message, null, cancellationToken).ConfigureAwait(false);
                }
                catch { /* ignore secondary failures */ }
            }

            return new EmailSendResult { Success = false, Error = ex.Message, LogId = logId };
        }
    }

    public async Task<GroupEmailSendResult> SendGroupAsync(GroupEmailNotifyRequest request, CancellationToken cancellationToken = default)
    {
        Guid? logId = null;
        try
        {
            if (!request.SecretKey.MatchesSecret(ApiSecret))
                return new GroupEmailSendResult { Success = false, Error = "未授權" };

            if (request.GroupId == Guid.Empty)
                return new GroupEmailSendResult { Success = false, Error = "GroupId is required." };

            if (string.IsNullOrWhiteSpace(request.Subject))
                return new GroupEmailSendResult { Success = false, Error = "Subject is required." };

            if (string.IsNullOrWhiteSpace(request.Body))
                return new GroupEmailSendResult { Success = false, Error = "Body is required." };

            var emails = await _groupRepository.GetEmailsByGroupAsync(request.GroupId, cancellationToken).ConfigureAwait(false);
            var emailList = emails.ToList();
            if (emailList.Count == 0)
                return new GroupEmailSendResult { Success = false, Error = "No active emails found." };

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

            var (smtpClient, mailMessage) = CreateMailComponents(request.Subject, request.Body);
            foreach (var email in emailList)
            {
                mailMessage.Bcc.Add(new MailAddress(email));
            }

            await smtpClient.SendMailAsync(mailMessage, cancellationToken).ConfigureAwait(false);

            await _logRepository.UpdateLogStatusAsync(logId.Value, (byte)SendStatus.Success, null, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);

            return new GroupEmailSendResult { Success = true, SentCount = emailList.Count, LogId = logId };
        }
        catch (Exception ex)
        {
            if (logId.HasValue)
            {
                try
                {
                    await _logRepository.UpdateLogStatusAsync(logId.Value, (byte)SendStatus.Failed, ex.Message, null, cancellationToken).ConfigureAwait(false);
                }
                catch { /* ignore */ }
            }
            return new GroupEmailSendResult { Success = false, Error = ex.Message, LogId = logId };
        }
    }

    private (SmtpClient Client, MailMessage Message) CreateMailComponents(string subject, string body)
    {
        var smtpClient = new SmtpClient(_settings.ExternalSMTP, _settings.Port)
        {
            Credentials = new NetworkCredential(_settings.From, _settings.Sw),
            EnableSsl = _settings.EnableSSL
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(_settings.From),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            Body = body,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = true,
            Priority = MailPriority.Normal
        };

        return (smtpClient, mailMessage);
    }
}
