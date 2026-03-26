using mementobot.Telegram.Messages;
using Telegram.Bot;

namespace mementobot.Services.Messages;

internal class AddQuizQuestionMessage(ITelegramBotClient client, IMessageStore store)
    : BotMessage<string>(client, store)
{
    public Task ApplyInputQuestion(long chatId) => Apply(chatId, "✏️ Введи текст вопроса");
    public Task ApplyInputAnswer(long chatId) => Apply(chatId, "💬 Введи ответ к вопросу");

    protected override async Task<int> Send(long chatId, string text)
    {
        var msg = await client.SendMessage(chatId, text);
        return msg.Id;
    }
}
