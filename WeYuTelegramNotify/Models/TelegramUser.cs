using System;

namespace WeYuTelegramNotify.Models;

/// <summary>
/// TELEGRAM_USER table representation.
/// </summary>
public class TelegramUser
{
    /// <summary>Primary key.</summary>
    public Guid ID { get; set; }

    /// <summary>Telegram chat identifier.</summary>
    public string CHAT_ID { get; set; }

    public string? DISPLAY_NAME { get; set; }

    public int TYPE { get; set; }

    /// <summary>Record active flag.</summary>
    public bool IS_ACTIVE { get; set; }

    public DateTime CREATED_AT { get; set; }
}
