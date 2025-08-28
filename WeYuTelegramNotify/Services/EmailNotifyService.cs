using System.Net;
using System.Net.Mail;
using System.Text;
using WeYuTelegramNotify.interfaces;
using WeYuTelegramNotify.Models;
using WeYuTelegramNotify.Options;
using Microsoft.Extensions.Options;
using WeYuTelegramNotify.Helper;

namespace WeYuTelegramNotify.Services;

public class EmailNotifyService : IEmailNotifyService
{
    private readonly EmailSettingOptions _settings;

    public EmailNotifyService(IOptions<EmailSettingOptions> options)
    {
        _settings = options.Value;
    }

    public async Task<EmailSendResult> SendAsync(EmailNotifyRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!request.SecretKey.MatchesSecret("WeYuTelegraiApi"))
                return new EmailSendResult { Success = false, Error = "未授權" };

            if (string.IsNullOrWhiteSpace(request.Email))
                return new EmailSendResult { Success = false, Error = "Email is required." };

            if (string.IsNullOrWhiteSpace(request.Subject))
                return new EmailSendResult { Success = false, Error = "Subject is required." };

            if (string.IsNullOrWhiteSpace(request.Body))
                return new EmailSendResult { Success = false, Error = "Body is required." };

            using var smtpClient = new SmtpClient(_settings.ExternalSMTP, _settings.Port)
            {
                Credentials = new NetworkCredential(_settings.From, _settings.Sw),
                EnableSsl = _settings.EnableSSL
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(_settings.From),
                Subject = request.Subject,
                SubjectEncoding = Encoding.UTF8,
                Body = request.Body,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = true,
                Priority = MailPriority.Normal
            };

            mailMessage.To.Add(new MailAddress(request.Email));
            await smtpClient.SendMailAsync(mailMessage, cancellationToken).ConfigureAwait(false);

            return new EmailSendResult { Success = true };
        }
        catch (Exception ex)
        {
            return new EmailSendResult { Success = false, Error = ex.Message };
        }
    }
}
