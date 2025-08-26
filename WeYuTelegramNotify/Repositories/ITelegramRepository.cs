using WeYuTelegramNotify.Enum;
using WeYuTelegramNotify.Models;

namespace WeYuTelegramNotify.Repositories;

/// <summary>
/// Data access methods for Telegram related tables.
/// </summary>
public interface ITelegramRepository
{
    Task<TelegramUser?> GetSingleOrGroup(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> InsertLogAsync(TelegramMessageLog log, CancellationToken cancellationToken = default);

    Task UpdateLogStatusAsync(Guid id, byte status, string? errorMessage, DateTime? sentAt, CancellationToken cancellationToken = default);

    Task<TelegramMessageTemplate?> GetTemplateByCodeAsync(string code, CancellationToken cancellationToken = default);
}
