using Microsoft.Extensions.Caching.Memory;

namespace mementobot.Telegram;

internal class MemorySessionStore(IMemoryCache cache) : ISessionStore
{
    public object? Get(long chatId) => cache.Get<object?>($"{chatId}-session");

    public void Set(long chatId, object instance) =>
        cache.Set($"{chatId}-session", instance);

    public void Remove(long chatId) =>
        cache.Remove($"{chatId}-session");
}
