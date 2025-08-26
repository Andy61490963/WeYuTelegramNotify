using WeYuTelegramNotify.Enum;
using WeYuTelegramNotify.Models;
using WeYuTelegramNotify.Repositories;

namespace WeYuTelegramNotify.Services;

public class TelegramNotifyService : ITelegramNotifyService
{
    private const int MaxMessageLength = 4096;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITelegramRepository _repository;

    public TelegramNotifyService(IHttpClientFactory httpClientFactory, ITelegramRepository repository)
    {
        _httpClientFactory = httpClientFactory;
        _repository = repository;
    }

    /// <summary>
    /// 發送訊息
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="HttpRequestException"></exception>
    public async Task SendAsync(TelegramNotifyRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _repository.GetSingleOrGroup(request.Id, cancellationToken);

        if (result == null)
        {
            return;
        }

        var log = new TelegramMessageLog
        {
            TELEGRAM_USER_ID = result.ID,
            TELEGRAM_MESSAGE_TEMPLATE_ID = request.TemplateId,
            SUBJECT = request.Subject,
            BODY = request.Body,
            STATUS = SendStatus.Queued,
            CREATED_AT = DateTime.Now,
        };
        
        var logId = await _repository.InsertLogAsync(log, cancellationToken).ConfigureAwait(false);

        try
        {
            var client = _httpClientFactory.CreateClient("Telegram");
            var header = $"<b>{request.Subject}</b>\n\n";
            var maxBodyLength = MaxMessageLength - header.Length;
            foreach (var chunk in SplitMessage(request.Body, maxBodyLength))
            {
                var payload = new Dictionary<string, string>
                {
                    ["chat_id"] = result.CHAT_ID.ToString(),
                    ["text"] = header + chunk
                };

                using var content = new FormUrlEncodedContent(payload);
                using var response = await client.PostAsync("sendMessage", content, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    throw new HttpRequestException($"Telegram API error: {response.StatusCode} - {body}");
                }
            }

            await _repository.UpdateLogStatusAsync(logId, 1, null, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _repository.UpdateLogStatusAsync(logId, 2, ex.Message, null, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// 訊息太長，截斷發兩次
    /// </summary>
    /// <param name="text"></param>
    /// <param name="chunkSize"></param>
    /// <returns></returns>
    private static IEnumerable<string> SplitMessage(string text, int chunkSize)
    {
        for (var i = 0; i < text.Length; i += chunkSize)
        {
            yield return text.Substring(i, Math.Min(chunkSize, text.Length - i));
        }
    }
}

