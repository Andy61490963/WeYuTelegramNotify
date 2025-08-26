using System;

namespace WeYuTelegramNotify.Models;

/// <summary>
/// TELEGRAM_MESSAGE_LOG table representation.
/// </summary>
public class TelegramMessageLog
{
    public Guid Id { get; set; }

    public string? TemplateCode { get; set; }

    public Guid? TelegramUserId { get; set; }

    public Guid? TelegramGroupId { get; set; }

    public Guid? TelegramMessageTemplateId { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    /// <summary>0 = Queued, 1 = Success, 2 = Failed.</summary>
    public byte Status { get; set; }

    public string? ErrorMessage { get; set; }

    public int RetryCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? SentAt { get; set; }
}
