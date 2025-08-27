using System.ComponentModel.DataAnnotations;

namespace WeYuTelegramNotify.Models;

public class TelegramNotifyRequest : IValidatableObject
{
    [Required]
    public string ChatId { get; set; } = string.Empty;

    public string? Subject { get; set; }

    [Required]
    public string Body { get; set; } = string.Empty;

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
