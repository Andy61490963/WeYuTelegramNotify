using System;
using WeYuTelegramNotify.Enum;

namespace WeYuTelegramNotify.Models;

/// <summary>
/// TELEGRAM_MESSAGE_LOG table representation.
/// </summary>
public class EmailLog
{
    public Guid ID { get; set; }

    public Guid EMAIL_GROUP_ID { get; set; }

    public string SUBJECT { get; set; } = string.Empty;

    public string BODY { get; set; } = string.Empty;

    /// <summary>0 = Queued, 1 = Success, 2 = Failed.</summary>
    public SendStatus STATUS { get; set; }

    public string? ERROR_MESSAGE { get; set; }

    public int RETRY_COUNT { get; set; }

    public DateTime CREATED_AT { get; set; }
}
