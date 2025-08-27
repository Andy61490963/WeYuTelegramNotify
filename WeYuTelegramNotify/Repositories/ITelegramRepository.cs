using WeYuTelegramNotify.Enum;
using WeYuTelegramNotify.Models;

namespace WeYuTelegramNotify.Repositories;

/// <summary>
/// Data access methods for Telegram related tables.
/// </summary>
public interface ITelegramRepository
{
    /// <summary>
    /// Retrieve a telegram target and all of its chat identifiers.
    /// </summary>
    Task<TelegramTarget?> GetTargetWithChatsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TelegramMessageTemplate?> GetTemplateByIdAsync(Guid? id, CancellationToken cancellationToken = default);
    Task<Guid> InsertLogAsync(TelegramMessageLog log, CancellationToken cancellationToken = default);
    Task UpdateLogStatusAsync(Guid id, byte status, string? errorMessage, DateTime? sentAt, CancellationToken cancellationToken = default);
}

