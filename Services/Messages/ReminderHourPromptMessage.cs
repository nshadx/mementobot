using mementobot.Telegram.Messages;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace mementobot.Services.Messages;

internal class ReminderHourPromptMessage(ITelegramBotClient client, IMessageStore store)
    : BotMessage<bool>(client, store)
{
    protected override async Task<int> Send(long chatId, bool isError)
    {
        var text = isError
            ? "❌ Неверное значение\\. Введи час от 0 до 23:"
            : "⏰ Введи час напоминания \\(0–23\\):";
        var msg = await client.SendMessage(chatId, text, parseMode: ParseMode.MarkdownV2);
        return msg.Id;
    }
}
