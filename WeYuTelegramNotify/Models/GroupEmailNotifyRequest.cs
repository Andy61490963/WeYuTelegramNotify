using System;
using System.ComponentModel.DataAnnotations;

namespace WeYuTelegramNotify.Models;

/// <summary>
/// 代表群組發信的請求內容。
/// </summary>
public class GroupEmailNotifyRequest
{
    /// <summary>
    /// 驗證呼叫端身份的密鑰。
    /// </summary>
    [Required]
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// 目標群組的識別碼。
    /// </summary>
    [Required]
    public Guid GroupId { get; set; }

    /// <summary>
    /// 郵件主旨。
    /// </summary>
    [Required]
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// 郵件內容 (HTML)。
    /// </summary>
    [Required]
    public string Body { get; set; } = string.Empty;
}

