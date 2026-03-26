using mementobot.Telegram.Messages;
using Telegram.Bot;

namespace mementobot.Services.Messages;

internal class SearchPromptMessage(ITelegramBotClient client, IMessageStore store)
    : BotMessage<bool>(client, store)
{
    public Task Apply(long chatId) => Apply(chatId, false);

    protected override async Task<int> Send(long chatId, bool _)
    {
        var msg = await client.SendMessage(chatId, "🔍 Введи название опросника:");
        return msg.Id;
    }
}
