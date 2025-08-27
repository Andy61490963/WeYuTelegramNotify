using System.ComponentModel.DataAnnotations;

namespace WeYuTelegramNotify.Models;

public class TelegramNotifyRequest : IValidatableObject
{
    [Required]
    public string ChatId { get; set; } = string.Empty;

    public string? Subject { get; set; }

    [Required]
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// 文字模板中的 {{Token}} 會以此對應表中的值取代；未提供的 token 保留原樣。
    /// </summary>
    public IDictionary<string, string?> Tokens { get; init; }
        = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<ValidationResult> Validate(ValidationContext _)
    {
        if (string.IsNullOrWhiteSpace(ChatId))
        {
            yield return new ValidationResult(
                "ChatId is required.",
                new[] { nameof(ChatId) });
        }

        if (string.IsNullOrWhiteSpace(Body))
        {
            yield return new ValidationResult(
                "Body is required.",
                new[] { nameof(Body) });
        }
    }
}
