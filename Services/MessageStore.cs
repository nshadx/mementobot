using Dapper;
using Microsoft.Data.Sqlite;
using mementobot.Telegram.Messages;

namespace mementobot.Services;

internal class MessageStore(SqliteConnection connection) : IMessageStore
{
    public int? FindTelegramId(long chatId, string type) =>
        connection.QueryFirstOrDefault<int?>("""
            SELECT telegram_message_id FROM bot_messages
            WHERE chat_id = @chatId AND type = @type
            """, new { chatId, type });

    public void Upsert(long chatId, string type, int telegramMessageId) =>
        connection.Execute("""
            INSERT INTO bot_messages (chat_id, type, telegram_message_id)
            VALUES (@chatId, @type, @telegramMessageId)
            ON CONFLICT(chat_id, type) DO UPDATE SET telegram_message_id = excluded.telegram_message_id
            """, new { chatId, type, telegramMessageId });

    public void Remove(long chatId, string type) =>
        connection.Execute(
            "DELETE FROM bot_messages WHERE chat_id = @chatId AND type = @type",
            new { chatId, type });

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
