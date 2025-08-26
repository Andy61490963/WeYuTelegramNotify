using System;

namespace WeYuTelegramNotify.Models;

/// <summary>
/// TELEGRAM_MESSAGE_TEMPLATE table representation.
/// </summary>
public class TelegramMessageTemplate
{
    public Guid ID { get; set; }

    /// <summary>Unique template code.</summary>
    public string CODE { get; set; } = string.Empty;

    public string? SUBJECT { get; set; }

    public string BODY { get; set; } = string.Empty;
}
