using System.ComponentModel.DataAnnotations;
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

    public TelegramNotifyService(
        ITelegramRepository repository,
        IHttpClientFactory httpClientFactory)
    {
        _repository = repository;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// 發送訊息（直接使用提供的內容，parse_mode=HTML，安全分段）
    /// </summary>
    public async Task<TelegramSendResult> SendAsync(TelegramNotifyRequest request, CancellationToken cancellationToken = default)
    {
        FailureStage stage = FailureStage.Validation;
        Guid? logId = null;

        try
        {
            if (string.IsNullOrWhiteSpace(request.Body))
                return new TelegramSendResult { Success = false, Stage = FailureStage.Validation, Error = "Body is required." };

            stage = FailureStage.DbWrite;
            var log = new TelegramMessageLog
            {
                CHAT_ID = request.ChatId,
                SUBJECT = request.Subject ?? string.Empty,
                BODY = request.Body,
                STATUS = SendStatus.Queued,
                CREATED_AT = DateTime.UtcNow,
                RETRY_COUNT = 0
            };
            logId = await _repository.InsertLogAsync(log, cancellationToken).ConfigureAwait(false);

            stage = FailureStage.HttpSend;
            var client = _httpClientFactory.CreateClient("Telegram");
            var header = string.IsNullOrWhiteSpace(request.Subject) ? string.Empty : $"<b>{request.Subject}</b>\n\n";
            var maxBodyLength = Math.Max(0, MaxMessageLength - header.Length);

            foreach (var chunk in SplitMessageSafe(request.Body, maxBodyLength))
            {
                var payload = new Dictionary<string, string>
                {
                    ["chat_id"] = request.ChatId,
                    ["text"] = header + chunk,
                    ["parse_mode"] = "HTML",
                    ["disable_web_page_preview"] = "true"
                };

                using var content  = new FormUrlEncodedContent(payload);
                using var response = await client.PostAsync("sendMessage", content, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var resp = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    await _repository.UpdateLogStatusAsync(logId.Value, (byte)SendStatus.Failed, $"{(int)response.StatusCode} {response.StatusCode} - {resp}", null, cancellationToken).ConfigureAwait(false);

                    return new TelegramSendResult
                    {
                        Success = false,
                        Stage = FailureStage.HttpSend,
                        Error = $"Telegram API error: {(int)response.StatusCode} {response.StatusCode}",
                        HttpStatus = (int)response.StatusCode,
                        LogId = logId,
                        Subject = request.Subject,
                        Body = request.Body
                    };
                }
            }

            stage = FailureStage.DbWrite;
            await _repository.UpdateLogStatusAsync(logId.Value, (byte)SendStatus.Success, null, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);

            return new TelegramSendResult
            {
                Success = true,
                LogId = logId,
                Subject = request.Subject,
                Body = request.Body
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
