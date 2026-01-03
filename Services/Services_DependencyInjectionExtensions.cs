using Microsoft.EntityFrameworkCore;

namespace mementobot.Services;

internal static class Services_DependencyInjectionExtensions
{
    public static IHostApplicationBuilder AddAppDbContext(
        this IHostApplicationBuilder builder
    )
    {
        builder.Services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlite(builder.Configuration.GetConnectionString("Database") ?? throw new InvalidOperationException("empty database connection string"));
        }, ServiceLifetime.Scoped);

        return builder;
    }
}