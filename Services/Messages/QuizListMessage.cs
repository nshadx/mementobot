using mementobot.Telegram.Messages;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace mementobot.Services.Messages;

internal class QuizListMessage(ITelegramBotClient client, IMessageStore store)
    : BotMessage<IReadOnlyCollection<Quiz>>(client, store)
{
    protected override async Task<int> Send(long chatId, IReadOnlyCollection<Quiz> quizzes)
    {
        if (quizzes.Count == 0)
        {
            var empty = await client.SendMessage(chatId, "📭 Сейчас нет доступных опросников.");
            return empty.Id;
        }

        var msg = await client.SendMessage(chatId, "🔎 Выбери опросник:", replyMarkup: BuildKeyboard(quizzes));
        return msg.Id;
    }

    protected override async Task Edit(long chatId, int telegramMessageId, IReadOnlyCollection<Quiz> quizzes)
    {
        await client.EditMessageReplyMarkup(chatId, telegramMessageId, BuildKeyboard(quizzes));
    }

    private static InlineKeyboardMarkup BuildKeyboard(IReadOnlyCollection<Quiz> quizzes) =>
        new(quizzes
            .Select(x => new InlineKeyboardButton(x.Name, x.Id.ToString()))
            .Chunk(3)
            .Append([
                new InlineKeyboardButton("←", "backward"),
                new InlineKeyboardButton("→", "forward")
            ]));
}
