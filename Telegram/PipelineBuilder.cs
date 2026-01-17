using Microsoft.Extensions.DependencyInjection.Extensions;

namespace mementobot.Telegram;

public class PipelineBuilder(IServiceCollection services)
{
    private readonly List<Func<IServiceProvider, UpdateDelegate, UpdateDelegate>> _components = [];

    public PipelineBuilder Use<TMiddleware>() where TMiddleware : class, IMiddleware
    {
        services.TryAddScoped<TMiddleware>();

        _components.Add(CreateMiddleware<TMiddleware>);
        return this;
    }

    public void Build()
    {
        services.AddScoped(CreatePipeline);
    }

    private UpdateDelegate CreatePipeline(IServiceProvider provider)
    {
        UpdateDelegate pipeline = _ => Task.CompletedTask;
        for (var i = _components.Count - 1; i >= 0; i--)
        {
            pipeline = _components[i](provider, pipeline);
        }
        return pipeline;
    }

    private static UpdateDelegate CreateMiddleware<TMiddleware>(IServiceProvider provider, UpdateDelegate next) where TMiddleware : IMiddleware
    {
        return async context =>
        {
            var instance = provider.GetRequiredService<TMiddleware>();
            await instance.Handle(context, next);
        };
    }
}
