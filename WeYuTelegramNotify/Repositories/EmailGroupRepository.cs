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
;WITH RecursiveGroup AS (
    -- 起點：自己
    SELECT ID
    FROM dbo.EMAIL_GROUP
    WHERE ID = @RootGroupId AND IS_ACTIVE = 1

    UNION ALL

    -- 遞迴：抓子節點
    SELECT g.ID
    FROM dbo.EMAIL_GROUP g
    JOIN RecursiveGroup rg ON g.PARENT_ID = rg.ID
    WHERE g.IS_ACTIVE = 1
)
SELECT DISTINCT c.EMAIL
FROM RecursiveGroup rg
JOIN dbo.EMAIL_GROUP_CONTACT gc ON gc.EMAIL_GROUP = rg.ID
JOIN dbo.EMAIL_CONTACT c        ON c.ID = gc.EMAIL_CONTACT
WHERE c.IS_ACTIVE = 1
OPTION (MAXRECURSION 0);  -- 子樹很深就開這個；想限 100 層以內就拿掉
;";

        return await _con.QueryAsync<string>(
            new CommandDefinition(sql, new { RootGroupId = groupId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}

