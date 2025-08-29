using WeYuNotify.Models;

namespace WeYuNotify.Repositories;

/// <summary>
/// Data access methods for Telegram related tables.
/// </summary>
public interface ITelegramRepository
{
    Task<Guid> InsertLogAsync(TelegramMessageLog log, CancellationToken cancellationToken = default);
    Task UpdateLogStatusAsync(Guid id, byte status, string? errorMessage, DateTime? sentAt, CancellationToken cancellationToken = default);
}

