using Microsoft.Data.SqlClient;
using WeYuTelegramNotify.Options;
using WeYuTelegramNotify.Services;
using Microsoft.Extensions.Options;
using WeYuTelegramNotify.interfaces;
using WeYuTelegramNotify.Repositories;

namespace WeYuTelegramNotify;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

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
        builder.Services.AddScoped<ITemplateRendererService, TemplateRendererService>();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();
        app.Run();
    }
}

