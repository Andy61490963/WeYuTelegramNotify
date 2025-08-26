using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using WeYuTelegramNotify.Enum;
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

    public async Task<TelegramUser?> GetSingleOrGroup(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

        string selectSql = "SELECT TOP 1 * FROM TELEGRAM_GROUP WHERE id = @id";
        var result = await _con.QueryFirstOrDefaultAsync(new CommandDefinition(selectSql, new { id }, cancellationToken: cancellationToken));

        if (result is not null)
        {
            return result;
        }
        return null;
    }

    public async Task<Guid> InsertLogAsync(TelegramMessageLog log, CancellationToken cancellationToken = default)
    {
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
        
        log.ID = Guid.NewGuid();

        const string sql = @"
        INSERT INTO TELEGRAM_MESSAGE_LOG
            (ID, TEMPLATE_CODE, TELEGRAM_USER_ID, TELEGRAM_GROUP_ID, TELEGRAM_MESSAGE_TEMPLATE_ID, SUBJECT, BODY, STATUS, ERROR_MESSAGE, RETRY_COUNT, CREATED_AT, SENT_AT)
        VALUES
            (@Id, @TemplateCode, @TelegramUserId, @TelegramGroupId, @TelegramMessageTemplateId, @Subject, @Body, @Status, @ErrorMessage, @RetryCount, @CreatedAt, @SentAt)";

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

    public async Task<TelegramMessageTemplate?> GetTemplateByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

        const string sql = "SELECT TOP 1 * FROM TELEGRAM_MESSAGE_TEMPLATE WHERE CODE = @Code";
        return await _con.QuerySingleOrDefaultAsync<TelegramMessageTemplate>(new CommandDefinition(sql, new { Code = code }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
