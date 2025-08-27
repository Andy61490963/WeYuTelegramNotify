using System.ComponentModel.DataAnnotations;

public class TelegramNotifyRequest : IValidatableObject
{
    public string? Subject { get; set; }
    public string? Body { get; set; }                  // ← 不必 Required，允許用模板
    public Guid? TemplateId { get; set; }             // ← 允許 null
    [Required]
    public Guid Id { get; set; }

    public IDictionary<string, object?> Tokens { get; init; }
        = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<ValidationResult> Validate(ValidationContext _)
    {
        bool hasBody = !string.IsNullOrWhiteSpace(Body);
        bool hasTpl  = TemplateId.HasValue && TemplateId.Value != Guid.Empty;

        if (!hasBody && !hasTpl)
        {
            yield return new ValidationResult(
                "Body 或 TemplateId 需至少提供一個。",
                new[] { nameof(Body), nameof(TemplateId) });
        }
    }
}