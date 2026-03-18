namespace mementobot.Telegram;

internal interface ISessionStore
{
    object? Get(long chatId);
    void Set(long chatId, object instance);
    void Remove(long chatId);
}
