using FluentMigrator.Runner;
using Microsoft.Data.Sqlite;

namespace mementobot.Services;

internal class DbService(
    SqliteConnection connection,
    IServiceProvider provider
)
{
    public void Migrate()
    {
        using (var scope = provider.CreateScope())
        { 
            scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();
        }
        
        connection.Open();
    }
}