using mementobot.Telegram.Messages;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace mementobot.Services.Messages;

internal class StartMenuMessage(ITelegramBotClient client, IMessageStore store)
    : BotMessage<bool>(client, store)
{
    public Task Apply(long chatId) => Apply(chatId, false);

    protected override async Task<int> Send(long chatId, bool _)
    {
        var msg = await client.SendMessage(
            chatId,
            "Выбери опросник:",
            replyMarkup: new InlineKeyboardMarkup([
                [new InlineKeyboardButton("⭐ Избранное", "start:favorites")],
                [new InlineKeyboardButton("🕐 Недавно пройденные", "start:recent")],
                [new InlineKeyboardButton("🔍 Поиск", "start:search")]
            ]));
        return msg.Id;
    }
}
