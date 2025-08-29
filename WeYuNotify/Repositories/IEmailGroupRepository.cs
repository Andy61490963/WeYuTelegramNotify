

namespace WeYuNotify.Repositories;

/// <summary>
/// 提供與 EMAIL_* 群組相關的存取方法。
/// </summary>
public interface IEmailGroupRepository
{
    /// <summary>
    /// 取得指定群組（含子群組）底下所有啟用的 Email 地址。
    /// </summary>
    /// <param name="groupId">群組識別碼。</param>
    /// <param name="cancellationToken"></param>
    Task<IEnumerable<string>> GetEmailsByGroupAsync(Guid groupId, CancellationToken cancellationToken = default);
}

