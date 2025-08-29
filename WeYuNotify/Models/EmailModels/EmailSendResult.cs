

namespace WeYuNotify.Models;

public class EmailSendResult
{
    /// <summary>是否寄送成功。</summary>
    public bool Success { get; init; }

    /// <summary>失敗訊息。</summary>
    public string? Error { get; init; }

    /// <summary>資料庫 Log 主鍵。</summary>
    public Guid? LogId { get; init; }
}
