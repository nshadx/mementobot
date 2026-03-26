using Dapper;
using Microsoft.Data.Sqlite;

namespace mementobot.Services;

internal class UserSettings
{
    public bool RemindersEnabled { get; set; }
    public int ReminderHour { get; set; }
    public int Temperature { get; set; }
    public bool AdultContent { get; set; }
}

internal class UserService(SqliteConnection connection)
{
    private class UserRow
    {
        public int Id { get; set; }
        public long TelegramId { get; set; }
    }

    public IEnumerable<(int Id, long TelegramId)> GetAllUsers(SqliteTransaction? transaction = null) =>
        connection.Query<UserRow>(
            "SELECT id AS Id, telegram_id AS TelegramId FROM users",
            transaction: transaction)
        .Select(r => (r.Id, r.TelegramId));

    public int GetOrCreateUser(long telegramId, SqliteTransaction? transaction = null) =>
        connection.QuerySingle<int>("""
            INSERT INTO users (telegram_id)
            VALUES (@telegramId)
            ON CONFLICT(telegram_id) DO UPDATE SET telegram_id = excluded.telegram_id
            RETURNING id
            """, new { telegramId }, transaction);

    public UserSettings GetUserSettings(int userId, SqliteTransaction? transaction = null) =>
        connection.QuerySingle<UserSettings>("""
            SELECT reminders_enabled  AS RemindersEnabled,
                   reminder_hour      AS ReminderHour,
                   temperature        AS Temperature,
                   adult_content      AS AdultContent
            FROM users WHERE id = @userId
            """, new { userId }, transaction);

    public void UpdateRemindersEnabled(int userId, bool enabled, SqliteTransaction? transaction = null) =>
        connection.Execute(
            "UPDATE users SET reminders_enabled = @enabled WHERE id = @userId",
            new { userId, enabled }, transaction);

    public void UpdateReminderHour(int userId, int hour, SqliteTransaction? transaction = null) =>
        connection.Execute(
            "UPDATE users SET reminder_hour = @hour WHERE id = @userId",
            new { userId, hour }, transaction);

    public void UpdateTemperature(int userId, int temperature, SqliteTransaction? transaction = null) =>
        connection.Execute(
            "UPDATE users SET temperature = @temperature WHERE id = @userId",
            new { userId, temperature }, transaction);

    public void UpdateAdultContent(int userId, bool enabled, SqliteTransaction? transaction = null) =>
        connection.Execute(
            "UPDATE users SET adult_content = @enabled WHERE id = @userId",
            new { userId, enabled }, transaction);

    public IEnumerable<(int Id, long TelegramId)> GetUsersForReminder(int hour, SqliteTransaction? transaction = null) =>
        connection.Query<UserRow>("""
            SELECT id AS Id, telegram_id AS TelegramId FROM users
            WHERE reminders_enabled = 1 AND reminder_hour = @hour
            """, new { hour }, transaction)
        .Select(r => (r.Id, r.TelegramId));
}
