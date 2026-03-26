using mementobot.Telegram.Messages;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace mementobot.Services.Messages;

internal class QuizPublishedMessage(ITelegramBotClient client, IMessageStore store)
    : BotMessage<int>(client, store)
{
    protected override async Task<int> Send(long chatId, int quizId)
    {
        var link = $"https://t.me/nshadx_mementobot?start={quizId}";
        var msg = await client.SendMessage(
            chatId,
            $"🚀 *Опросник опубликован\\!*\n\n📎 [Ссылка для прохождения]({link})",
            parseMode: ParseMode.MarkdownV2);
        return msg.Id;
    }
}
