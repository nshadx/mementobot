using mementobot.Services;
using mementobot.Telegram.StateMachine;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;

namespace mementobot.Telegram;

internal static class TelegramDependencyInjectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddTelegram(Action<TelegramConfiguration> configure)
        {
            services.AddHttpClient("TelegramBotClient")
                .RemoveAllLoggers()
                .AddTypedClient<ITelegramBotClient>((httpClient, _) =>
                {
                    TelegramConfiguration instance = new();
                    configure(instance);

                    return new TelegramBotClient(instance.Token, httpClient);
                });
            services.AddHostedService<PollingService>();
            services.AddScoped<IUpdateHandler, UpdateHandler>();
            services.AddScoped<BehaviorContextFactory>();
            services.AddScoped<ISessionStore, MemorySessionStore>();
            services.AddMemoryCache();
            services.AddSingleton<TelegramFileService>();
            services.AddSingleton<MessageManager>();

            return services;
        }

        public IServiceCollection AddStateMachine<TStateMachine, TInstance>()
            where TStateMachine : StateMachine<TInstance>
            where TInstance : class
        {
            services.AddSingleton<TStateMachine>();
            services.AddSingleton<IStateMachine>(sp => sp.GetRequiredService<TStateMachine>());
            return services;
        }

        public IServiceCollection ConfigurePipeline(Action<PipelineBuilder> action)
        {
            var builder = new PipelineBuilder(services);
            action(builder);
            builder.Build();
            return services;
        }
    }
}