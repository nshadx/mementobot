using mementobot.Telegram.Messages;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace mementobot.Services.Messages;

internal class MyQuizzesMenuMessage(ITelegramBotClient client, IMessageStore store)
    : BotMessage(client, store)
{
    protected override async Task<int> Send(long chatId)
    {
        var msg = await client.SendMessage(chatId, "📖 Мои опросники:", replyMarkup: new InlineKeyboardMarkup(
        [
            [new InlineKeyboardButton("📚 Мои", "myquizzes:mine")],
            [new InlineKeyboardButton("⭐ Избранные", "myquizzes:favorites")],
            [new InlineKeyboardButton("🕐 Недавние", "myquizzes:recent")],
        ]));
        return msg.Id;
    }
}
