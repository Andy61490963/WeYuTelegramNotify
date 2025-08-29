using System.ComponentModel.DataAnnotations;

namespace WeYuNotify.Models;

public class EmailNotifyRequest
{
    [Required]
    public string SecretKey { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;
}
