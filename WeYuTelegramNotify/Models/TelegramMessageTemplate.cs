using System;

namespace WeYuTelegramNotify.Models;

/// <summary>
/// TELEGRAM_MESSAGE_TEMPLATE table representation.
/// </summary>
public class TelegramMessageTemplate
{
    public Guid Id { get; set; }

    /// <summary>Unique template code.</summary>
    public string Code { get; set; } = string.Empty;

    public string? Subject { get; set; }

    public string Body { get; set; } = string.Empty;
}
