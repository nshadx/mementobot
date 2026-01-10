using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;

namespace mementobot.Telegram;

public static class Telegram_DependencyInjectionExtensions
{
    public static IHostApplicationBuilder AddTelegram(
        this IHostApplicationBuilder builder
    )
    {
        builder.Services.Configure<TelegramConfiguration>(builder.Configuration.GetRequiredSection(nameof(TelegramConfiguration)));
        builder.Services.AddHttpClient("TelegramBotClient")
            .RemoveAllLoggers()
            .AddTypedClient<ITelegramBotClient>((httpClient, provider) =>
            {
                var configuration = provider.GetRequiredService<IOptions<TelegramConfiguration>>().Value;
                return new TelegramBotClient(configuration.Token, httpClient);
            });
        builder.Services.AddHostedService<PollingService>();
        builder.Services.AddScoped<IUpdateHandler, UpdateHandler>();
        builder.Services.AddSingleton<TelegramFileService>();
        builder.Services.AddSingleton<MessageManager>();
        
        return builder;
    }
}