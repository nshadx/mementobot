using mementobot.Telegram.Messages;
using Telegram.Bot;

namespace mementobot.Services.Messages;

internal class QuestionAddedMessage(ITelegramBotClient client, IMessageStore store)
    : BotMessage(client, store)
{
    protected override async Task<int> Send(long chatId)
    {
        var msg = await client.SendMessage(chatId, "✅ Вопрос добавлен!");
        return msg.Id;
    }
}
