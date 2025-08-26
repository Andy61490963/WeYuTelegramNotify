using System.Net.Http.Json;
using WeYuTelegramNotify.Models;

namespace WeYuTelegramNotify.Services;

public class TelegramNotifyService : ITelegramNotifyService
{
    private const int MaxMessageLength = 4096;
    private readonly IHttpClientFactory _httpClientFactory;

    public TelegramNotifyService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task SendAsync(TelegramNotifyRequest request, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("Telegram");
        var header = $"<b>{request.Subject}</b>\n\n";
        var maxBodyLength = MaxMessageLength - header.Length;
        foreach (var chunk in SplitMessage(request.Body, maxBodyLength))
        {
            var payload = new Dictionary<string, string>
            {
                ["chat_id"] = request.GroupId.ToString(),
                ["text"] = header + chunk,
                ["parse_mode"] = request.ParseMode
            };

            using var content = new FormUrlEncodedContent(payload);
            using var response = await client.PostAsync("sendMessage", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException($"Telegram API error: {response.StatusCode} - {body}");
            }
        }
    }

    private static IEnumerable<string> SplitMessage(string text, int chunkSize)
    {
        for (var i = 0; i < text.Length; i += chunkSize)
        {
            yield return text.Substring(i, Math.Min(chunkSize, text.Length - i));
        }
    }
}

