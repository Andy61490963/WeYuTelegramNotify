using WeYuTelegramNotify.Models;

namespace WeYuTelegramNotify.interfaces;

public interface ITelegramNotifyService
{
    Task<TelegramSendResult> SendAsync(TelegramNotifyRequest request, CancellationToken cancellationToken = default);
}

