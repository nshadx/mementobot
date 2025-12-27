using mementobot.Extensions;
using mementobot.Middlewares;

namespace mementobot.Handlers;

public static class Pipeline_DependencyInjectionExtensions
{
    public static IServiceCollection AddAppPipeline(this IServiceCollection services)
    {
        services.AddPipeline(x =>
        {
            x.Use<EnsureUserMiddleware>();
            x.Use<SetStateMiddleware>();
            x.Use<RouterMiddleware>();
            x.Use<SaveStateMiddleware>();
            x.Use<AnswerCallbackQueryMiddleware>();
        });

        return services;
    }
}