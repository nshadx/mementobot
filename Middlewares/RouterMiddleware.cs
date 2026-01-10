using System.Text.RegularExpressions;

namespace mementobot.Middlewares;

internal delegate Task<bool> Route(IServiceProvider serviceProvider, Context context);
internal interface IRouteHandler
{
    Task Handle(Context context);
}
internal class RouterMiddleware(IServiceProvider provider, IEnumerable<Route> routes) : IMiddleware
{
    public async Task Invoke(Context context, UpdateDelegate next)
    {
        var handled = false;
        
        foreach (var route in routes)
        {
            if (await route(provider, context))
            {
                handled = true;
                break;
            }
        }

        if (handled)
        {
            await next(context);
        }
    }
}
internal class RouteBuilder(IServiceCollection services)
{
    private List<Route> _routes { get; } = [];

    public RouteBuilder Command<TRouteHandler>(string name) where TRouteHandler : class, IRouteHandler => When<TRouteHandler>(context => context.Update.Message is { Text: string text } && text.Length >= name.Length && text[..name.Length].Equals(name, StringComparison.InvariantCultureIgnoreCase));

    public RouteBuilder File<TRouteHandler>(string pattern) where TRouteHandler : class, IRouteHandler => When<TRouteHandler>(context => context.Update.Message?.Document is { FileName: string fileName } && Regex.IsMatch(fileName, pattern));

    public RouteBuilder Callback<TRouteHandler>(Func<string, bool> predicate) where TRouteHandler : class, IRouteHandler => When<TRouteHandler>(context => context.Update.CallbackQuery is { Data: string data } && predicate(data));

    public RouteBuilder When<TRouteHandler>(Func<Context, bool> predicate) where TRouteHandler : class, IRouteHandler
    {
        services.AddSingleton<TRouteHandler>();
        
        _routes.Add(async (provider, context) =>
        {
            if (predicate(context))
            {
                await provider.GetRequiredService<TRouteHandler>().Handle(context);
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

internal static class Routing_DependencyInjectionExtensions
{
    public static IServiceCollection AddRouting(this IServiceCollection services, Action<RouteBuilder> configure)
    {
        RouteBuilder instance = new(services);

        configure(instance);
 
        instance.Build();

        return services;
    }
}