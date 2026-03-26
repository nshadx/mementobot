using mementobot.Telegram.Messages;
using Telegram.Bot;

namespace mementobot.Services.Messages;

internal class SearchPromptMessage(ITelegramBotClient client, IMessageStore store)
    : BotMessage(client, store)
{
    protected override async Task<int> Send(long chatId)
    {
        var msg = await client.SendMessage(chatId, "🔍 Введи название опросника:");
        return msg.Id;
    }
}
