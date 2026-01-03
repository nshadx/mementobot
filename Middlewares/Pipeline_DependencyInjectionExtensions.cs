namespace mementobot.Middlewares;

internal static class Pipeline_DependencyInjectionExtensions
{
    public static IServiceCollection AddPipeline(this IServiceCollection services, Action<PipelineBuilder> configure)
    {
        PipelineBuilder instance = new(services);

        configure(instance);

        instance.Build();

        return services;
    }
    
    public static IHostApplicationBuilder AddAppPipeline(this IHostApplicationBuilder builder)
    {
        builder.Services.AddPipeline(x =>
        {
            x.Use<EnsureUserMiddleware>();
            x.Use<SetStateMiddleware>();
            x.Use<RouterMiddleware>();
            x.Use<SaveStateMiddleware>();
            x.Use<AnswerCallbackQueryMiddleware>();
        });

        return builder;
    }
}
