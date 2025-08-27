using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using WeYuTelegramNotify.Models;

namespace WeYuTelegramNotify.Repositories;

/// <summary>
/// Dapper based implementation of <see cref="ITelegramRepository"/>.
/// </summary>
public class TelegramRepository : ITelegramRepository
{
    private readonly SqlConnection _con;

    public TelegramRepository(SqlConnection con)
    {
        _con = con;
    }

    private async Task EnsureConnectionAsync(CancellationToken cancellationToken)
    {
        if (_con.State != ConnectionState.Open)
        {
            await _con.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<Guid> InsertLogAsync(TelegramMessageLog log, CancellationToken cancellationToken = default)
    {
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

        log.ID = log.ID == Guid.Empty ? Guid.NewGuid() : log.ID;

        const string sql = @"
INSERT INTO TELEGRAM_MESSAGE_LOG
    (ID, CHAT_ID, SUBJECT, BODY, STATUS, ERROR_MESSAGE, RETRY_COUNT, CREATED_AT, SENT_AT)
VALUES
    (@ID, @CHAT_ID, @SUBJECT, @BODY, @STATUS, @ERROR_MESSAGE, @RETRY_COUNT, @CREATED_AT, @SENT_AT)";

        await _con.ExecuteAsync(new CommandDefinition(sql, log, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return log.ID;
    }

    public async Task UpdateLogStatusAsync(Guid id, byte status, string? errorMessage, DateTime? sentAt, CancellationToken cancellationToken = default)
    {
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"UPDATE TELEGRAM_MESSAGE_LOG
                             SET STATUS = @Status, ERROR_MESSAGE = @ErrorMessage, SENT_AT = @SentAt
                             WHERE ID = @Id";
        await _con.ExecuteAsync(new CommandDefinition(sql, new { Id = id, Status = status, ErrorMessage = errorMessage, SentAt = sentAt }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
