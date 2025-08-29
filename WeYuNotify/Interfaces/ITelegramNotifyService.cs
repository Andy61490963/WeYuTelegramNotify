using WeYuNotify.Models;

namespace WeYuNotify.interfaces;

public interface ITelegramNotifyService
{
    Task<TelegramSendResult> SendAsync(TelegramNotifyRequest request, CancellationToken cancellationToken = default);
}

