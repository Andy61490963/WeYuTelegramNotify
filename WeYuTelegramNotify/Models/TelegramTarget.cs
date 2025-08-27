using System;
using System.Collections.Generic;

namespace WeYuTelegramNotify.Models;

/// <summary>
/// TELEGRAM table and its associated target chat identifiers.
/// </summary>
public class TelegramTarget
{
    /// <summary>Primary key for TELEGRAM.</summary>
    public Guid ID { get; set; }

    /// <summary>Display name for this target set.</summary>
    public string? DISPLAY_NAME { get; set; }

    /// <summary>Active flag.</summary>
    public bool IS_ACTIVE { get; set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CREATED_AT { get; set; }

    /// <summary>Collection of chat identifiers.</summary>
    public List<string> CHAT_IDS { get; } = new();
}

