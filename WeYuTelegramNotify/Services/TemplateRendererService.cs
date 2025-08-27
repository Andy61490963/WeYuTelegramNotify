using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using WeYuTelegramNotify.Enum;
using WeYuTelegramNotify.interfaces;
using WeYuTelegramNotify.Models;
using WeYuTelegramNotify.Repositories;

namespace WeYuTelegramNotify.Services;

public class TemplateRendererService : ITemplateRendererService
{
    private const int MaxMessageLength = 4096;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITelegramRepository _repository;

    public TemplateRendererService(IHttpClientFactory httpClientFactory, ITelegramRepository repository)
    {
        _httpClientFactory = httpClientFactory;
        _repository = repository;
    }

    // {{Key}} or {{Key|format}}
    private static readonly Regex TokenRegex =
        new(@"\{\{(?<key>[A-Za-z0-9_\.\-]+)(\|(?<format>[^}]+))?\}\}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public (string Subject, string Body) Render(
        string? subjectTemplate,
        string bodyTemplate,
        IReadOnlyDictionary<string, string?> data,
        CultureInfo? culture = null,
        bool htmlEncodeValues = true)
    {
        culture ??= CultureInfo.InvariantCulture;

        string RenderOne(string? tpl)
        {
            if (string.IsNullOrEmpty(tpl)) return string.Empty;

            return TokenRegex.Replace(tpl, m =>
            {
                var key = m.Groups["key"].Value;
                var fmt = m.Groups["format"].Success ? m.Groups["format"].Value : null;

                if (!data.TryGetValue(key, out var raw) || raw is null)
                {
                    // 未提供值：保留原樣，方便開發時快速看出漏填
                    return m.Value;
                }

                string str = FormatValue(raw, fmt, culture);

                if (htmlEncodeValues)
                {
                    // 只 Encode 值，不 Encode 模板中的固定標籤（如 <b>），避免破壞格式
                    str = HtmlEncoder.Default.Encode(str);
                }
                return str;
            });
        }

        var subject = RenderOne(subjectTemplate);
        var body    = RenderOne(bodyTemplate);

        return (subject, body);
    }

    private static string FormatValue(object value, string? format, CultureInfo culture)
    {
        if (value is IFormattable fmt && !string.IsNullOrWhiteSpace(format))
            return fmt.ToString(format, culture);

        return value switch
        {
            DateTime dt => dt.ToString(format ?? "yyyy-MM-dd HH:mm:ss", culture),
            DateTimeOffset dto => dto.ToString(format ?? "yyyy-MM-dd HH:mm:ss zzz", culture),
            _ => value.ToString() ?? string.Empty
        };
    }
}

