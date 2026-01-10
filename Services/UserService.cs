using Microsoft.Data.Sqlite;

namespace mementobot.Services;

public class UserService(
    SqliteConnection connection
)
{
    public int GetOrCreateUser(long telegramId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
                                    INSERT INTO users (telegram_id)
                                    VALUES (@telegram_id)
                                    ON CONFLICT(telegram_id) DO UPDATE SET
                                        telegram_id = excluded.telegram_id
                                    RETURNING id;
                                    """, connection, transaction);
        command.Parameters.AddWithValue("@telegram_id", telegramId);
        
        return (int)(long)command.ExecuteScalar()!;
    }
}