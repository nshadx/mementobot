using Microsoft.EntityFrameworkCore;

namespace mementobot.Services;

internal static class Services_DependencyInjectionExtensions
{
    public static IServiceCollection AddAppDbContext(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlite(configuration.GetConnectionString("Database") ?? throw new InvalidOperationException("empty database connection string"));
        }, ServiceLifetime.Scoped);
        
        return services;
    }
}