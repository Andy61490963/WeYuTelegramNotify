using System.Globalization;
using WeYuTelegramNotify.Models;

namespace WeYuTelegramNotify.interfaces;

public interface ITemplateRendererService
{
    (string Subject, string Body) Render(
        string? subjectTemplate,
        string bodyTemplate,
        IReadOnlyDictionary<string, object?> data,
        CultureInfo? culture = null,
        bool htmlEncodeValues = true);
}

