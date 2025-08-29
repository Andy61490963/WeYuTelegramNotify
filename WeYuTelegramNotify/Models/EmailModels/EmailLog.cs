using System;
using WeYuTelegramNotify.Enum;

namespace WeYuTelegramNotify.Models;

/// <summary>
/// EMAIL_LOG table representation.
/// </summary>
public class EmailLog
{
    /// <summary>主鍵。</summary>
    public Guid ID { get; set; }

    /// <summary>
    /// 群組 ID；若為單一收件者則為 <see cref="Guid.Empty"/>。
    /// </summary>
    public Guid EMAIL_GROUP_ID { get; set; }

    /// <summary>信件主旨。</summary>
    public string SUBJECT { get; set; } = string.Empty;

    /// <summary>信件內文（HTML）。</summary>
    public string BODY { get; set; } = string.Empty;

    /// <summary>0 = Queued, 1 = Success, 2 = Failed。</summary>
    public SendStatus STATUS { get; set; }

    /// <summary>失敗訊息。</summary>
    public string? ERROR_MESSAGE { get; set; }

    /// <summary>重試次數。</summary>
    public int RETRY_COUNT { get; set; }

    /// <summary>建立時間（UTC）。</summary>
    public DateTime CREATED_AT { get; set; }
}
