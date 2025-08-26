using System;

namespace WeYuTelegramNotify.Models;

/// <summary>
/// TELEGRAM_GROUP table representation.
/// </summary>
public class TelegramGroup
{
    public Guid Id { get; set; }

    /// <summary>Chat identifier (negative for groups).</summary>
    public long ChatId { get; set; }

    public string? GroupName { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}
