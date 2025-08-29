using WeYuNotify.Models;

namespace WeYuNotify.Repositories;

/// <summary>
/// Data access methods for EMAIL_LOG table.
/// </summary>
public interface IEmailLogRepository
{
    /// <summary>新增一筆寄信紀錄。</summary>
    Task<Guid> InsertLogAsync(EmailLog log, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新寄信狀態與錯誤資訊。
    /// </summary>
    Task UpdateLogStatusAsync(Guid id, byte status, string? errorMessage, DateTime? sentAt, CancellationToken cancellationToken = default);
}
