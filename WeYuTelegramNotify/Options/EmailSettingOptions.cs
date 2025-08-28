namespace WeYuTelegramNotify.Options;

public class EmailSettingOptions
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Sw { get; set; } = string.Empty;
    public int Port { get; set; }
    public string InternalSMTP { get; set; } = string.Empty;
    public string ExternalSMTP { get; set; } = string.Empty;
    public bool EnableSSL { get; set; }
}
