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

    // {{Key}} 
    private static readonly Regex TokenRegex =
        new(@"\{\{(?<raw>[^}]+)\}\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public (string Subject, string Body) Render(
        string? subjectTemplate,
        string bodyTemplate,
        IReadOnlyDictionary<string, string?> data,
        bool htmlEncodeValues = true)
    {
        string RenderOne(string? tpl)
        {
            if (string.IsNullOrEmpty(tpl)) return string.Empty;

            return TokenRegex.Replace(tpl, m =>
            {
                // 只取 '|' 前的字，視為 key；其餘忽略（不支援格式化）
                var raw = m.Groups["raw"].Value;
                var pipe = raw.IndexOf('|');
                var key = (pipe >= 0 ? raw[..pipe] : raw).Trim();

                if (!data.TryGetValue(key, out var val) || val is null)
                    return m.Value; // 沒值 → 保留原樣，方便偵錯

                return htmlEncodeValues ? HtmlEncoder.Default.Encode(val) : val;
            });
        }

        return (RenderOne(subjectTemplate), RenderOne(bodyTemplate));
    }
}

