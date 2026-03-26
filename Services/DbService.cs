using System.Data;
using System.Globalization;
using Dapper;
using FluentMigrator.Runner;
using Microsoft.Data.Sqlite;

namespace mementobot.Services;

internal class DbService(SqliteConnection connection, IServiceProvider provider)
{
    public void Migrate()
    {
        SqlMapper.AddTypeHandler(new DateTimeHandler());

        using (var scope = provider.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();
        }

        connection.Open();
    }

    private sealed class DateTimeHandler : SqlMapper.TypeHandler<DateTime>
    {
        public override void SetValue(IDbDataParameter parameter, DateTime value)
            => parameter.Value = value.ToString("O");

        public override DateTime Parse(object value)
            => DateTime.Parse((string)value, null, DateTimeStyles.RoundtripKind);
    }
}
