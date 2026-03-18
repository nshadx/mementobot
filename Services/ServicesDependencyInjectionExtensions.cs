using FluentMigrator.Runner;
using mementobot.Migrations;
using Microsoft.Data.Sqlite;

namespace mementobot.Services;

internal static class ServicesDependencyInjectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddServices()
        {
            services.AddSingleton<QuizService>();
            services.AddSingleton<UserService>();

            return services;
        }

        public IServiceCollection AddDb(string connectionString)
        {
            services.AddSingleton<DbService>();
            services.AddFluentMigratorCore().ConfigureRunner(x =>
            {
                x.AddSQLite();
                x.WithGlobalConnectionString(connectionString);
                x.ScanIn(typeof(InitialMigration).Assembly);
            });
            services.AddSingleton(_ => new SqliteConnection(connectionString));

            return services;
        }
    }
}
