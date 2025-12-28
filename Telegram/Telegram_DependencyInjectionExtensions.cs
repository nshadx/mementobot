using mementobot.Services;
using Telegram.Bot;
using Telegram.Bot.Polling;

namespace mementobot.Telegram;

public static class Telegram_DependencyInjectionExtensions
{
    public static IServiceCollection AddTelegram(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddHttpClient("TelegramBotClient")
            .RemoveAllLoggers()
            .AddTypedClient<ITelegramBotClient>((httpClient, _) => new TelegramBotClient(configuration.GetConnectionString("TelegramBotToken") ?? throw new InvalidOperationException("empty bot token"), httpClient));
        services.AddHostedService<PollingService>();
        services.AddScoped<IUpdateHandler, UpdateHandler>();
        services.AddSingleton<TelegramFileService>();

        return services;
    }
}