using System.ComponentModel.DataAnnotations;
using System.Globalization;
using WeYuTelegramNotify.Enum;
using WeYuTelegramNotify.interfaces;
using WeYuTelegramNotify.Models;
using WeYuTelegramNotify.Repositories;

namespace WeYuTelegramNotify.Services;
public class TelegramNotifyService : ITelegramNotifyService
{
    private const int MaxMessageLength = 4096; // Telegram text hard limit
    private readonly ITelegramRepository _repository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITemplateRendererService _templateRenderer;

    public TelegramNotifyService(
        ITelegramRepository repository,
        IHttpClientFactory httpClientFactory,
        ITemplateRendererService templateRenderer)
    {
        _repository = repository;
        _httpClientFactory = httpClientFactory;
        _templateRenderer = templateRenderer;
    }

    /// <summary>
    /// 發送訊息（會以模板 + Tokens 渲染，parse_mode=HTML，安全分段）
    /// </summary>
    public async Task<TelegramSendResult> SendAsync(TelegramNotifyRequest request, CancellationToken cancellationToken = default)
    {
        FailureStage stage = FailureStage.Validation;
        Guid? logId = null;

        try
        {
            // ── 驗證：Body 與 TemplateId 至少一個要有
            bool hasBody = !string.IsNullOrWhiteSpace(request.Body);
            bool hasTpl = request.TemplateId.HasValue && request.TemplateId.Value != Guid.Empty;

            if (!hasBody && !hasTpl)
                return new TelegramSendResult { Success = false, Stage = FailureStage.Validation, Error = "Body 或 TemplateId 需至少提供一個。" };

            // ── 1) 取目標
            stage = FailureStage.TargetLookup;
            var target = await _repository.GetSingleOrGroupAsync(request.Id, cancellationToken).ConfigureAwait(false);
            if (target is null || !target.IS_ACTIVE)
                return new TelegramSendResult { Success = false, Stage = FailureStage.TargetLookup, Error = "Target 不存在或未啟用。" };

            // ── 2) 取模板（僅在有 TemplateId 時）
            TelegramMessageTemplate? template = null;
            if (hasTpl)
            {
                stage = FailureStage.TemplateLookup;
                template = await _repository.GetTemplateByIdAsync(request.TemplateId, cancellationToken).ConfigureAwait(false);
                if (template is null)
                    return new TelegramSendResult { Success = false, Stage = FailureStage.TemplateLookup, Error = "TemplateId 無效或不存在。" };
            }

            // ── 3) Tokens
            var tokens = new Dictionary<string, string?>(request.Tokens, StringComparer.OrdinalIgnoreCase)
            {
                ["Now"]          = DateTime.UtcNow.ToString(),
                ["DisplayName"]  = target.DISPLAY_NAME,
                ["ChatId"]       = target.CHAT_ID,
                ["TemplateCode"] = template?.CODE
            };

            // ── 4) 渲染
            stage = FailureStage.Render;
            var subjectTpl = !string.IsNullOrWhiteSpace(request.Subject) ? request.Subject : template?.SUBJECT;
            var bodyTpl    = hasBody ? request.Body! : template!.BODY;
            if (string.IsNullOrWhiteSpace(bodyTpl))
                return new TelegramSendResult { Success = false, Stage = FailureStage.Render, Error = "找不到可用的訊息內容（Body 與模板皆為空）。" };

            var (subject, body) = _templateRenderer.Render(
                subjectTemplate: subjectTpl,
                bodyTemplate:    bodyTpl,
                data: tokens,
                culture: CultureInfo.GetCultureInfo("zh-TW"),
                htmlEncodeValues: false
            );

            // ── 5) 寫入 Log（Queued）
            stage = FailureStage.DbWrite;
            var log = new TelegramMessageLog
            {
                TELEGRAM_USER_ID = target.ID, // 若你同時支援群組，這裡可依 TYPE 改成 TELEGRAM_GROUP_ID
                TELEGRAM_MESSAGE_TEMPLATE_ID = template?.ID,
                SUBJECT = subject,
                BODY = body,
                STATUS = SendStatus.Queued,
                CREATED_AT = DateTime.UtcNow,
                RETRY_COUNT = 0
            };
            logId = await _repository.InsertLogAsync(log, cancellationToken).ConfigureAwait(false);

            // ── 6) 傳送
            stage = FailureStage.HttpSend;
            var client = _httpClientFactory.CreateClient("Telegram");
            var header = string.IsNullOrWhiteSpace(subject) ? string.Empty : $"<b>{subject}</b>\n\n";
            var maxBodyLength = Math.Max(0, MaxMessageLength - header.Length);

            foreach (var chunk in SplitMessageSafe(body, maxBodyLength))
            {
                var payload = new Dictionary<string, string>
                {
                    ["chat_id"] = target.CHAT_ID.ToString(),
                    ["text"] = header + chunk,
                    ["parse_mode"] = "HTML",
                    ["disable_web_page_preview"] = "true"
                };

                using var content  = new FormUrlEncodedContent(payload);
                using var response = await client.PostAsync("sendMessage", content, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var resp = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    // 回寫失敗
                    await _repository.UpdateLogStatusAsync(logId.Value, (byte)SendStatus.Failed, $"{(int)response.StatusCode} {response.StatusCode} - {resp}", null, cancellationToken).ConfigureAwait(false);

                    return new TelegramSendResult
                    {
                        Success = false,
                        Stage = FailureStage.HttpSend,
                        Error = $"Telegram API error: {(int)response.StatusCode} {response.StatusCode}",
                        HttpStatus = (int)response.StatusCode,
                        LogId = logId,
                        Subject = subject,
                        Body = body
                    };
                }
            }

            // 成功回寫
            stage = FailureStage.DbWrite;
            await  _repository.UpdateLogStatusAsync(logId.Value, (byte)SendStatus.Success, null, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);

            return new TelegramSendResult
            {
                Success = true,
                LogId = logId,
                Subject = subject,
                Body = body
            };
        }
        catch (ValidationException vex)
        {
            return new TelegramSendResult { Success = false, Stage = FailureStage.Validation, Error = vex.Message, LogId = logId };
        }
        catch (Exception ex)
        {
            try
            {
                if (logId.HasValue)
                    await _repository.UpdateLogStatusAsync(logId.Value, (byte)SendStatus.Failed, ex.Message, null, cancellationToken).ConfigureAwait(false);
            }
            catch { /* swallow */ }

            return new TelegramSendResult
            {
                Success = false,
                Stage = stage == FailureStage.Validation ? FailureStage.Unknown : stage,
                Error = ex.Message,
                LogId = logId
            };
        }
    }

    /// <summary>
    /// 安全切段：盡量以 \n 或 空白切；最後使用 StringInfo 防止切壞 surrogate pair。
    /// </summary>
    private static IEnumerable<string> SplitMessageSafe(string input, int maxLen)
    {
        if (string.IsNullOrEmpty(input))
            yield break;

        if (maxLen <= 0)
        {
            yield return string.Empty;
            yield break;
        }

        var start = 0;
        while (start < input.Length)
        {
            var len = Math.Min(maxLen, input.Length - start);
            var end = start + len;

            if (end < input.Length)
            {
                // 優先找最近的換行
                var nl = input.LastIndexOf('\n', end - 1, len);
                if (nl >= start) end = nl + 1;
                else
                {
                    // 再找空白
                    var sp = input.LastIndexOf(' ', end - 1, len);
                    if (sp >= start) end = sp + 1;
                    else
                    {
                        // 確保不切壞 Unicode text element
                        end = AdjustToTextElementBoundary(input, start, end);
                    }
                }
            }

            yield return input[start..end];
            start = end;
        }
    }

    private static int AdjustToTextElementBoundary(string s, int start, int end)
    {
        // 盡量往前移到完整的 text element 邊界
        if (end <= start) return end;
        // 簡化處理：若位於低代理，往前一位
        var c = s[end - 1];
        if (char.IsLowSurrogate(c) && end - 2 >= start && char.IsHighSurrogate(s[end - 2]))
            return end - 1;
        return end;
    }
}
