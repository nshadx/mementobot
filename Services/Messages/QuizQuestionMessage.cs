using mementobot.Telegram.Messages;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace mementobot.Services.Messages;

internal class QuizQuestionMessage(ITelegramBotClient client, IMessageStore store)
    : BotMessage<QuizQuestion>(client, store)
{
    protected override async Task<int> Send(long chatId, QuizQuestion question)
    {
        var msg = await client.SendMessage(
            chatId,
            $"❓ *{question.Question}*",
            parseMode: ParseMode.Markdown,
            replyMarkup: new InlineKeyboardMarkup(new InlineKeyboardButton("⏭ Пропустить", "skip")));
        return msg.Id;
    }
}
