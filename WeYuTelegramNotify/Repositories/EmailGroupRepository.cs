using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;

namespace WeYuTelegramNotify.Repositories;

/// <summary>
/// 使用 Dapper 存取 Email 群組及聯絡人資料。
/// </summary>
public class EmailGroupRepository : IEmailGroupRepository
{
    private readonly SqlConnection _con;

    public EmailGroupRepository(SqlConnection con)
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

    public async Task<IEnumerable<string>> GetEmailsByGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
WITH RecursiveGroup AS (
    SELECT ID FROM EMAIL_GROUP WHERE ID = @GroupId AND IS_ACTIVE = 1
    UNION ALL
    SELECT g.ID FROM EMAIL_GROUP g
    JOIN RecursiveGroup rg ON g.PARENT_ID = rg.ID
    WHERE g.IS_ACTIVE = 1
)
SELECT DISTINCT c.EMAIL
FROM EMAIL_GROUP_CONTACT gc
JOIN EMAIL_CONTACT c ON c.ID = gc.EMAIL_CONTACT
WHERE gc.EMAIL_GROUP IN (SELECT ID FROM RecursiveGroup)
  AND c.IS_ACTIVE = 1;";

        return await _con.QueryAsync<string>(
            new CommandDefinition(sql, new { GroupId = groupId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}

