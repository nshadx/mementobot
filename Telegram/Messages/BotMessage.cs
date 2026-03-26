using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace mementobot.Telegram.Messages;

/// <summary>
/// Базовый класс для отслеживаемых сообщений бота.
/// Хранит одно актуальное сообщение на чат; при повторном Apply сам решает — редактировать или заменить.
/// </summary>
internal abstract class BotMessage<TData>(ITelegramBotClient client, IMessageStore store)
{
    public async Task Apply(long chatId, TData data)
    {
        var existing = store.FindTelegramId(chatId, GetType().Name);

        if (existing is not null)
        {
            var last = store.GetLastMessageId(chatId);

            if (last is not null && existing.Value == last.Value)
            {
                // а) Сообщение последнее в чате — редактируем
                try
                {
                    await Edit(chatId, existing.Value, data);
                    return;
                }
                catch (ApiRequestException ex) when (ex.Message.Contains("message is not modified"))
                {
                    return;
                }
                catch
                {
                    // б) Редактирование упало — сообщения нет в истории или не поддерживается
                    await TryDeleteMessage(chatId, existing.Value);
                    store.Remove(chatId, GetType().Name);
                }
            }
            else
            {
                // в) Сообщение не последнее — удаляем и отправляем новое
                await TryDeleteMessage(chatId, existing.Value);
                store.Remove(chatId, GetType().Name);
            }
        }

        var id = await Send(chatId, data);
        store.Upsert(chatId, GetType().Name, id);
        store.TrackMessageId(chatId, id);
    }

    public async Task Delete(long chatId)
    {
        var existing = store.FindTelegramId(chatId, GetType().Name);
        if (existing is null) return;
        await TryDeleteMessage(chatId, existing.Value);
        store.Remove(chatId, GetType().Name);
    }

    protected abstract Task<int> Send(long chatId, TData data);

    protected virtual Task Edit(long chatId, int telegramMessageId, TData data) =>
        throw new NotSupportedException($"{GetType().Name} does not support in-place editing.");

    private async Task TryDeleteMessage(long chatId, int messageId)
    {
        try { await client.DeleteMessage(chatId, messageId); } catch { }
    }
}

/// <summary>Базовый класс для сообщений без параметров данных.</summary>
internal abstract class BotMessage(ITelegramBotClient client, IMessageStore store)
    : BotMessage<object?>(client, store)
{
    public Task Apply(long chatId) => Apply(chatId, null);

    protected sealed override Task<int> Send(long chatId, object? _) => Send(chatId);

    protected abstract Task<int> Send(long chatId);
}
