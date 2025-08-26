using System;

namespace WeYuTelegramNotify.Models;

/// <summary>
/// TELEGRAM_USER table representation.
/// </summary>
public class TelegramUser
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Telegram chat identifier.</summary>
    public long ChatId { get; set; }

    public string? Username { get; set; }

    public string? DisplayName { get; set; }

    /// <summary>Record active flag.</summary>
    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}
