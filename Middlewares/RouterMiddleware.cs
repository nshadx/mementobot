using mementobot.Services.Common;

namespace mementobot.Middlewares;

internal delegate Task<bool> Route(IServiceProvider serviceProvider, Context context);
internal class RouterMiddleware(IServiceProvider provider, IEnumerable<Route> routes) : IMiddleware
{
    public async Task Invoke(Context context, UpdateDelegate next)
    {
        foreach (var route in routes)
        {
            if (await route(provider, context))
                break;
        }
        await next(context);
    }
}
internal class RouteBuilder(IServiceCollection services)
{
    private List<Route> _routes { get; } = [];

    public RouteBuilder Command(string name, Action<PipelineBuilder> configure)
    {
        PipelineBuilder builder = new(services, name);

        configure(builder);

        builder.Build();

        _routes.Add(async (provider, context) =>
        {
            if (context.Update.Message is not { Text: string text })
            {
                return false;
            }

            if (text[0] == '/' && text[1..].Equals(name))
            {
                await provider.GetRequiredKeyedService<UpdateDelegate>(name)(context);
                return true;
            }

            return false;
        });

        return this;
    }

    public RouteBuilder File(string extension, Action<PipelineBuilder> configure)
    {
        PipelineBuilder builder = new(services, extension);

        configure(builder);

        builder.Build();

        _routes.Add(async (provider, context) =>
        {
            if (context.Update.Message?.Document is not { FileName: string fileName })
            {
                return false;
            }

            if (Path.GetExtension(fileName).EndsWith(extension))
            {
                await provider.GetRequiredKeyedService<UpdateDelegate>(extension)(context);
                return true;
            }

            return false;
        });

        return this;
    }

    public RouteBuilder Callback(Func<string, bool> predicate, Action<PipelineBuilder> configure)
    {
        var id = Guid.NewGuid().ToString();

        PipelineBuilder builder = new(services, id);

        configure(builder);

        builder.Build();

        _routes.Add(async (provider, context) =>
        {
            if (context.Update.CallbackQuery is not { Data: string data })
            {
                return false;
            }

            if (predicate(data))
            {
                await provider.GetRequiredKeyedService<UpdateDelegate>(id)(context);
                return true;
            }

            return false;
        });

        return this;
    }

    public RouteBuilder When<TState>(Action<PipelineBuilder> configure, Func<TState, Context, bool>? predicate = null)
    {
        var id = Guid.NewGuid().ToString();

        PipelineBuilder builder = new(services, id);

        configure(builder);

        builder.Build();

        _routes.Add(async (provider, context) =>
        {
            if (context.State is TState state)
            {
                if (predicate is not null && !predicate(state, context))
                {
                    return false;
                }
                await provider.GetRequiredKeyedService<UpdateDelegate>(id)(context);
                return true;
            }

            return false;
        });

        return this;
    }

    public void Build()
    {
        foreach (var route in _routes)
        {
            services.AddSingleton(route);
        }
    }
}