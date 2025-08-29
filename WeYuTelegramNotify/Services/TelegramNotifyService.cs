using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using WeYuTelegramNotify.Enum;
using WeYuTelegramNotify.Helper;
using WeYuTelegramNotify.interfaces;
using WeYuTelegramNotify.Models;
using WeYuTelegramNotify.Repositories;

namespace WeYuTelegramNotify.Services;
public class TelegramNotifyService : ITelegramNotifyService
{
    private const int MaxMessageLength = 4096; // Telegram text hard limit
    private readonly ITelegramRepository _repository;
    private readonly IHttpClientFactory _httpClientFactory;

    // {{Key}}
    private static readonly Regex TokenRegex =
        new(@"\{\{(?<raw>[^}]+)\}\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
            if (!request.SecretKey.MatchesSecret("WeYuTelegraiApi"))
            {
                return new TelegramSendResult
                {
                    Success = false,
                    Stage = FailureStage.Validation,
                    Error = "未授權"
                };
            }
            
            if (string.IsNullOrWhiteSpace(request.SecretKey))
                return new TelegramSendResult { Success = false, Stage = FailureStage.Validation, Error = "SecretKey is required." };
            
            if (string.IsNullOrWhiteSpace(request.Body))
                return new TelegramSendResult { Success = false, Stage = FailureStage.Validation, Error = "Body is required." };

            if (string.IsNullOrWhiteSpace(request.ChatId))
                return new TelegramSendResult { Success = false, Stage = FailureStage.Validation, Error = "ChatId is required." };

            if (string.IsNullOrWhiteSpace(request.BotToken))
                return new TelegramSendResult { Success = false, Stage = FailureStage.Validation, Error = "BotToken is required." };
            
            // ── Token 替換
            var tokens = new Dictionary<string, string?>(request.Tokens, StringComparer.OrdinalIgnoreCase)
            {
                ["Now"]    = DateTime.UtcNow.ToString("O"),
                ["ChatId"] = request.ChatId
            };

            var subject = ReplaceTokens(request.Subject, tokens);
            var body    = ReplaceTokens(request.Body, tokens);

            stage = FailureStage.DbWrite;
            var log = new TelegramMessageLog
            {
                CHAT_ID = request.ChatId,
                SUBJECT = subject,
                BODY = body,
                STATUS = SendStatus.Queued,
                CREATED_AT = DateTime.UtcNow,
                RETRY_COUNT = 0
            };
            logId = await _repository.InsertLogAsync(log, cancellationToken).ConfigureAwait(false);

            stage = FailureStage.HttpSend;
            var client = _httpClientFactory.CreateClient("Telegram");
            
            // 不設定 BaseAddress，改用完整 URL（避免多 Token 併發踩到）
            var url = $"https://api.telegram.org/bot{request.BotToken}/sendMessage";
            
            var header = string.IsNullOrWhiteSpace(subject) ? string.Empty : $"<b>{subject}</b>\n\n";
            var maxBodyLength = Math.Max(0, MaxMessageLength - header.Length);

            // 因為 4096 關係 所以才分段
            foreach (var chunk in SplitMessageSafe(body, maxBodyLength))
            {
                var payload = new Dictionary<string, string>
                {
                    ["chat_id"] = request.ChatId,
                    ["text"] = header + chunk,
                    ["parse_mode"] = "HTML",
                    ["disable_web_page_preview"] = "true"
                };

                using var content  = new FormUrlEncodedContent(payload);
                using var response = await client.PostAsync(url, content, cancellationToken).ConfigureAwait(false);

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
                        Subject = subject,
                        Body = body
                    };
                }
            }

            stage = FailureStage.DbWrite;
            await _repository.UpdateLogStatusAsync(logId.Value, (byte)SendStatus.Success, null, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);

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

    private static string ReplaceTokens(string? template, IReadOnlyDictionary<string, string?> data)
    {
        if (string.IsNullOrEmpty(template) || data.Count == 0)
            return template ?? string.Empty;

        return TokenRegex.Replace(template, m =>
        {
            var raw = m.Groups["raw"].Value;
            var pipe = raw.IndexOf('|');
            var key = (pipe >= 0 ? raw[..pipe] : raw).Trim();
            return data.TryGetValue(key, out var val) && val is not null ? val : m.Value;
        });
    }
}
