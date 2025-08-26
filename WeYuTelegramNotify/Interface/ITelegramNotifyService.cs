using WeYuTelegramNotify.Models;

namespace WeYuTelegramNotify.Services;

public interface ITelegramNotifyService
{
    Task SendAsync(TelegramNotifyRequest request, CancellationToken cancellationToken = default);
}

