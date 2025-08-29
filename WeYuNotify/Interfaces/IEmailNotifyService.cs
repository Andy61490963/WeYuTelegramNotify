using WeYuNotify.Models;

namespace WeYuNotify.interfaces;

public interface IEmailNotifyService
{
    Task<EmailSendResult> SendAsync(EmailNotifyRequest request, CancellationToken cancellationToken = default);

    Task<GroupEmailSendResult> SendGroupAsync(GroupEmailNotifyRequest request, CancellationToken cancellationToken = default);
}
