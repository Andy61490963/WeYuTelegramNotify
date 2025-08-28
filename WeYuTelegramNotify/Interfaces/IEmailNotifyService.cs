using System.Threading;
using WeYuTelegramNotify.Models;

namespace WeYuTelegramNotify.interfaces;

public interface IEmailNotifyService
{
    Task<EmailSendResult> SendAsync(EmailNotifyRequest request, CancellationToken cancellationToken = default);
}
