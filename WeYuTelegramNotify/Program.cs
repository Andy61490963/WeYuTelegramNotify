using WeYuTelegramNotify.Options;
using WeYuTelegramNotify.Services;
using Microsoft.Extensions.Options;

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

        builder.Services.AddScoped<ITelegramNotifyService, TelegramNotifyService>();

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

