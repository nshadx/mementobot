using FluentMigrator.Runner;
using Microsoft.Data.Sqlite;

namespace mementobot.Services;

public class DbService(
    SqliteConnection connection,
    IServiceProvider provider
)
{
    public void Migrate()
    {
        using (var scope = provider.CreateScope())
        {
            var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>(); 
            runner.MigrateUp();
        }
        
        connection.Open();
    }
}