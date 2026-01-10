using FluentMigrator.Runner;
using mementobot.Migrations;
using Microsoft.Data.Sqlite;

namespace mementobot.Services;

internal static class Services_DependencyInjectionExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddServices()
        {
            builder.Services.AddSingleton<QuizService>();
            builder.Services.AddSingleton<StateService>();
            builder.Services.AddSingleton<UserService>();

            return builder;
        }

        public IHostApplicationBuilder AddDb()
        {
            var connectionString = builder.Configuration.GetConnectionString("Db");
            
            builder.Services.AddSingleton<DbService>();
            builder.Services.AddFluentMigratorCore().ConfigureRunner(x =>
            {
                x.AddSQLite();
                x.WithGlobalConnectionString(connectionString);
                x.ScanIn(typeof(InitialMigration).Assembly);
            });
            builder.Services.AddSingleton(_ => new SqliteConnection(connectionString));
            
            return builder;
        }
    }
}