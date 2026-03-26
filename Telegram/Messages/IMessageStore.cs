namespace mementobot.Telegram.Messages;

internal interface IMessageStore
{
    int? FindTelegramId(long chatId, string type);
    void Upsert(long chatId, string type, int telegramMessageId);
    void Remove(long chatId, string type);

    int? GetLastMessageId(long chatId);
    void TrackMessageId(long chatId, int messageId);
}
