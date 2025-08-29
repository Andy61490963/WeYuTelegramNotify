using System;
using System.Net;
using System.Net.Mail;
using System.Text;
using WeYuTelegramNotify.interfaces;
using WeYuTelegramNotify.Models;
using WeYuTelegramNotify.Options;
using Microsoft.Extensions.Options;
using WeYuTelegramNotify.Helper;
using WeYuTelegramNotify.Repositories;

namespace WeYuTelegramNotify.Services;

public class EmailNotifyService : IEmailNotifyService
{
    private const string ApiSecret = "WeYuTelegraiApi";
    private readonly EmailSettingOptions _settings;
    private readonly IEmailGroupRepository _groupRepository;

    public EmailNotifyService(IOptions<EmailSettingOptions> options, IEmailGroupRepository groupRepository)
    {
        _settings = options.Value;
        _groupRepository = groupRepository;
    }

    public async Task<EmailSendResult> SendAsync(EmailNotifyRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!request.SecretKey.MatchesSecret(ApiSecret))
                return new EmailSendResult { Success = false, Error = "未授權" };

            if (string.IsNullOrWhiteSpace(request.Email))
                return new EmailSendResult { Success = false, Error = "Email is required." };

            if (string.IsNullOrWhiteSpace(request.Subject))
                return new EmailSendResult { Success = false, Error = "Subject is required." };

            if (string.IsNullOrWhiteSpace(request.Body))
                return new EmailSendResult { Success = false, Error = "Body is required." };

            var (smtpClient, mailMessage) = CreateMailComponents(request.Subject, request.Body);
            mailMessage.To.Add(new MailAddress(request.Email));
            await smtpClient.SendMailAsync(mailMessage, cancellationToken).ConfigureAwait(false);

            return new EmailSendResult { Success = true };
        }
        catch (Exception ex)
        {
            return new EmailSendResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<GroupEmailSendResult> SendGroupAsync(GroupEmailNotifyRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!request.SecretKey.MatchesSecret(ApiSecret))
                return new GroupEmailSendResult { Success = false, Error = "未授權" };

            if (request.GroupId == Guid.Empty)
                return new GroupEmailSendResult { Success = false, Error = "GroupId is required." };

            if (string.IsNullOrWhiteSpace(request.Subject))
                return new GroupEmailSendResult { Success = false, Error = "Subject is required." };

            if (string.IsNullOrWhiteSpace(request.Body))
                return new GroupEmailSendResult { Success = false, Error = "Body is required." };

            var emails = await _groupRepository.GetEmailsByGroupAsync(request.GroupId, cancellationToken).ConfigureAwait(false);
            var emailList = emails.ToList();
            if (emailList.Count == 0)
                return new GroupEmailSendResult { Success = false, Error = "No active emails found." };

            var (smtpClient, mailMessage) = CreateMailComponents(request.Subject, request.Body);
            foreach (var email in emailList)
            {
                mailMessage.Bcc.Add(new MailAddress(email));
            }

            await smtpClient.SendMailAsync(mailMessage, cancellationToken).ConfigureAwait(false);

            return new GroupEmailSendResult { Success = true, SentCount = emailList.Count };
        }
        catch (Exception ex)
        {
            return new GroupEmailSendResult { Success = false, Error = ex.Message };
        }
    }

    private (SmtpClient Client, MailMessage Message) CreateMailComponents(string subject, string body)
    {
        var smtpClient = new SmtpClient(_settings.ExternalSMTP, _settings.Port)
        {
            Credentials = new NetworkCredential(_settings.From, _settings.Sw),
            EnableSsl = _settings.EnableSSL
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(_settings.From),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            Body = body,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = true,
            Priority = MailPriority.Normal
        };

        return (smtpClient, mailMessage);
    }
}
