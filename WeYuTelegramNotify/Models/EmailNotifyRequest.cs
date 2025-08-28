using System.ComponentModel.DataAnnotations;

namespace WeYuTelegramNotify.Models;

public class EmailNotifyRequest
{
    [Required]
    public string SecretKey { get; set; } = string.Empty;
    
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;
    
    /// <summary>
    /// 文字模板中的 {{Token}} 會以此對應表中的值取代；未提供的 token 保留原樣。
    /// </summary>
    public IDictionary<string, string?> Tokens { get; init; }
        = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
}