using Microsoft.Data.Sqlite;

namespace mementobot.Services;

internal record UserSettings(bool RemindersEnabled, int ReminderHour, int Temperature, bool AdultContent);

internal class UserService(
    SqliteConnection connection
)
{
    public IEnumerable<(int Id, long TelegramId)> GetAllUsers(SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("SELECT id, telegram_id FROM users", connection, transaction);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return (reader.GetInt32(0), reader.GetInt64(1));
        }
    }

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

    public UserSettings GetUserSettings(int userId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
            SELECT reminders_enabled, reminder_hour, temperature, adult_content
            FROM users WHERE id = @id
            """, connection, transaction);
        command.Parameters.AddWithValue("@id", userId);
        using var reader = command.ExecuteReader();
        reader.Read();
        return new UserSettings(
            RemindersEnabled: reader.GetBoolean(0),
            ReminderHour: reader.GetInt32(1),
            Temperature: reader.GetInt32(2),
            AdultContent: reader.GetBoolean(3)
        );
    }

    public void UpdateRemindersEnabled(int userId, bool enabled, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("UPDATE users SET reminders_enabled = @v WHERE id = @id", connection, transaction);
        command.Parameters.AddWithValue("@v", enabled);
        command.Parameters.AddWithValue("@id", userId);
        command.ExecuteNonQuery();
    }

    public void UpdateReminderHour(int userId, int hour, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("UPDATE users SET reminder_hour = @v WHERE id = @id", connection, transaction);
        command.Parameters.AddWithValue("@v", hour);
        command.Parameters.AddWithValue("@id", userId);
        command.ExecuteNonQuery();
    }

    public void UpdateTemperature(int userId, int temperature, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("UPDATE users SET temperature = @v WHERE id = @id", connection, transaction);
        command.Parameters.AddWithValue("@v", temperature);
        command.Parameters.AddWithValue("@id", userId);
        command.ExecuteNonQuery();
    }

    public void UpdateAdultContent(int userId, bool enabled, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("UPDATE users SET adult_content = @v WHERE id = @id", connection, transaction);
        command.Parameters.AddWithValue("@v", enabled);
        command.Parameters.AddWithValue("@id", userId);
        command.ExecuteNonQuery();
    }

    public IEnumerable<(int Id, long TelegramId)> GetUsersForReminder(int hour, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
            SELECT id, telegram_id FROM users
            WHERE reminders_enabled = 1 AND reminder_hour = @hour
            """, connection, transaction);
        command.Parameters.AddWithValue("@hour", hour);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return (reader.GetInt32(0), reader.GetInt64(1));
        }
    }
}