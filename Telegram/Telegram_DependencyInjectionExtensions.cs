using Telegram.Bot;
using Telegram.Bot.Polling;

namespace mementobot.Telegram;

public static class Telegram_DependencyInjectionExtensions
{
    public static IHostApplicationBuilder AddTelegram(
        this IHostApplicationBuilder builder
    )
    {
        builder.Services.AddHttpClient("TelegramBotClient")
            .RemoveAllLoggers()
            .AddTypedClient<ITelegramBotClient>((httpClient, _) => new TelegramBotClient(builder.Configuration.GetConnectionString("TelegramBotToken") ?? throw new InvalidOperationException("empty bot token"), httpClient));
        builder.Services.AddHostedService<PollingService>();
        builder.Services.AddScoped<IUpdateHandler, UpdateHandler>();
        builder.Services.AddSingleton<TelegramFileService>();

        return builder;
    }
}