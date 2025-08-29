

namespace WeYuNotify.Models;

/// <summary>
/// 群組發信的結果資訊。
/// </summary>
public class GroupEmailSendResult
{
    /// <summary>
    /// 是否全部寄送成功。
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 實際寄出的信件數量。
    /// </summary>
    public int SentCount { get; init; }

    /// <summary>
    /// 失敗時的錯誤訊息。
    /// </summary>
    public string? Error { get; init; }

    /// <summary>資料庫 Log 主鍵。</summary>
    public Guid? LogId { get; init; }
}

