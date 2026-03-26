using Microsoft.Data.Sqlite;
using mementobot.Telegram.Messages;

namespace mementobot.Services;

internal class MessageStore(SqliteConnection connection) : IMessageStore
{
    public int? FindTelegramId(long chatId, string type)
    {
        SqliteCommand command = new("""
            SELECT telegram_message_id FROM bot_messages
            WHERE chat_id = @chatId AND type = @type
            """, connection);
        command.Parameters.AddWithValue("@chatId", chatId);
        command.Parameters.AddWithValue("@type", type);
        var result = command.ExecuteScalar();
        return result is null ? null : (int)(long)result;
    }

    public void Upsert(long chatId, string type, int telegramMessageId)
    {
        SqliteCommand command = new("""
            INSERT INTO bot_messages (chat_id, type, telegram_message_id)
            VALUES (@chatId, @type, @telegramId)
            ON CONFLICT(chat_id, type) DO UPDATE SET telegram_message_id = excluded.telegram_message_id
            """, connection);
        command.Parameters.AddWithValue("@chatId", chatId);
        command.Parameters.AddWithValue("@type", type);
        command.Parameters.AddWithValue("@telegramId", telegramMessageId);
        command.ExecuteNonQuery();
    }

    public void Remove(long chatId, string type)
    {
        SqliteCommand command = new("""
            DELETE FROM bot_messages WHERE chat_id = @chatId AND type = @type
            """, connection);
        command.Parameters.AddWithValue("@chatId", chatId);
        command.Parameters.AddWithValue("@type", type);
        command.ExecuteNonQuery();
    }

    public int? GetLastMessageId(long chatId) =>
        FindTelegramId(chatId, LastMessageKey);

    public void TrackMessageId(long chatId, int messageId)
    {
        var current = GetLastMessageId(chatId);
        if (current is null || messageId > current.Value)
            Upsert(chatId, LastMessageKey, messageId);
    }

    private const string LastMessageKey = "__last";
}
