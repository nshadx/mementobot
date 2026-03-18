namespace mementobot.Telegram;

internal static class RoutingDependencyInjectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddRouting(Action<RouteBuilder> configure)
        {
            RouteBuilder instance = new(services);
            configure(instance);
            instance.Build();
            return services;
        }
    }
}
