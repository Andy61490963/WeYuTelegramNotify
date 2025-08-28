using Microsoft.Data.SqlClient;
using WeYuTelegramNotify.Options;
using WeYuTelegramNotify.Services;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using WeYuTelegramNotify.interfaces;
using WeYuTelegramNotify.Repositories;

namespace WeYuTelegramNotify;

public class Program
{
    public static void Main(string[] args)
    {
        // 先給一個極簡 Console logger，避免早期錯誤沒地方寫
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        var builder = WebApplication.CreateBuilder(args);

        // 用 IIS 的 ContentRoot 來組絕對路徑（不會搞錯目錄）
        var logDir  = Path.Combine(builder.Environment.ContentRootPath, "Logs");
        Directory.CreateDirectory(logDir); // 不存在就建
        var logPath = Path.Combine(logDir, "app-.log");

        // 重新配置 Serilog（含檔案輸出）
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .WriteTo.Console()
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        builder.Host.UseSerilog();
        
        builder.WebHost.UseUrls("http://0.0.0.0:12345");
        
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddAuthorization();

        builder.Services.Configure<TelegramBotOptions>(builder.Configuration.GetSection("TelegramBot"));

        builder.Services.AddHttpClient("Telegram", (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<TelegramBotOptions>>().Value;
            client.BaseAddress = new Uri($"{options.BaseUrl}{options.BotToken}/");
        });
    
        // -------------------- 連線字串 --------------------
        builder.Services.AddScoped<SqlConnection, SqlConnection>(_ =>
        {
            var conn = new SqlConnection();
            conn.ConnectionString = builder.Configuration.GetConnectionString("Connection");
            return conn;
        });
        
        builder.Services.AddScoped<ITelegramRepository, TelegramRepository>();
        builder.Services.AddScoped<ITelegramNotifyService, TelegramNotifyService>();

        var app = builder.Build();
        
        if (app.Environment.IsDevelopment())
        {
        }

        // app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();
        app.Run();
    }
}

