using System.ComponentModel.DataAnnotations;

namespace WeYuTelegramNotify.Models;

public class TelegramNotifyRequest
{
    [Required]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    [Required]
    public Guid Id { get; set; }

    public Guid TemplateId { get; set; }
}

