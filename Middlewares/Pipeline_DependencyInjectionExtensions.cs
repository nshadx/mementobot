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
}
