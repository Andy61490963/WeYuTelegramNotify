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

    public async Task<TelegramGroup> GetOrCreateGroupAsync(long chatId, CancellationToken cancellationToken = default)
    {
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

        const string selectSql = "SELECT TOP 1 * FROM TELEGRAM_GROUP WHERE CHAT_ID = @ChatId";
        var group = await _con.QuerySingleOrDefaultAsync<TelegramGroup>(new CommandDefinition(selectSql, new { ChatId = chatId }, cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (group is not null)
        {
            return group;
        }

        group = new TelegramGroup
        {
            Id = Guid.NewGuid(),
            ChatId = chatId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        const string insertSql = @"INSERT INTO TELEGRAM_GROUP (ID, CHAT_ID, GROUPNAME, IS_ACTIVE, CREATED_AT)
                                   VALUES (@Id, @ChatId, @GroupName, @IsActive, @CreatedAt)";
        try
        {
            await _con.ExecuteAsync(new CommandDefinition(insertSql, group, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch (SqlException ex) when (ex.Number == 2627) // Unique constraint violation
        {
            // In case of race condition, re-select the existing record.
            group = await _con.QuerySingleAsync<TelegramGroup>(new CommandDefinition(selectSql, new { ChatId = chatId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        return group;
    }

    public async Task<Guid> InsertLogAsync(TelegramMessageLog log, CancellationToken cancellationToken = default)
    {
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

        log.Id = log.Id == Guid.Empty ? Guid.NewGuid() : log.Id;
        log.CreatedAt = DateTime.UtcNow;

        const string sql = @"INSERT INTO TELEGRAM_MESSAGE_LOG
                                (ID, TEMPLATE_CODE, TELEGRAM_USER_ID, TELEGRAM_GROUP_ID, TELEGRAM_MESSAGE_TEMPLATE_ID, SUBJECT, BODY, STATUS, ERROR_MESSAGE, RETRY_COUNT, CREATED_AT, SENT_AT)
                                VALUES (@Id, @TemplateCode, @TelegramUserId, @TelegramGroupId, @TelegramMessageTemplateId, @Subject, @Body, @Status, @ErrorMessage, @RetryCount, @CreatedAt, @SentAt)";

        await _con.ExecuteAsync(new CommandDefinition(sql, log, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return log.Id;
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
