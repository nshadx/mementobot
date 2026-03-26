using mementobot.Telegram.Messages;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace mementobot.Services.Messages;

internal record QuizActionMenuData(bool IsFavorited, bool IsOwned, string? ShareLink = null);

internal class QuizActionMenuMessage(ITelegramBotClient client, IMessageStore store)
    : BotMessage<QuizActionMenuData>(client, store)
{
    protected override async Task<int> Send(long chatId, QuizActionMenuData data)
    {
        var msg = await client.SendMessage(chatId, "Выбери действие:", replyMarkup: BuildKeyboard(data));
        return msg.Id;
    }

    protected override async Task Edit(long chatId, int telegramMessageId, QuizActionMenuData data)
    {
        await client.EditMessageReplyMarkup(chatId, telegramMessageId, BuildKeyboard(data));
    }

    private static InlineKeyboardMarkup BuildKeyboard(QuizActionMenuData data)
    {
        List<IEnumerable<InlineKeyboardButton>> rows =
        [
            [new InlineKeyboardButton("▶️ Пройти", "action:play")]
        ];

        if (!data.IsOwned)
        {
            var label = data.IsFavorited ? "❌ Убрать из избранного" : "⭐ В избранное";
            rows.Add([new InlineKeyboardButton(label, "action:favorite")]);
        }

        if (data.ShareLink is { } link)
            rows.Add([InlineKeyboardButton.WithUrl("📎 Поделиться", link)]);

        return new InlineKeyboardMarkup(rows);
    }
}
