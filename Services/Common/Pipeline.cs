using Microsoft.Extensions.DependencyInjection.Extensions;

namespace mementobot.Services.Common;

internal class PipelineBuilder(IServiceCollection services, string? serviceKey = null)
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
        if (serviceKey is null)
        {
            services.AddScoped(CreatePipeline);
        }
        else
        {
            services.AddKeyedScoped(serviceKey, (provider, _) => CreatePipeline(provider));
        }
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
            await instance.Invoke(context, next);
        };
    }
}
