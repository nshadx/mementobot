using mementobot.Telegram.Messages;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace mementobot.Services.Messages;

internal class TemperaturePromptMessage(ITelegramBotClient client, IMessageStore store)
    : BotMessage<bool>(client, store)
{
    protected override async Task<int> Send(long chatId, bool isError)
    {
        var text = isError
            ? "❌ Неверное значение\\. Введи температуру от 0 до 100:"
            : "🌡 Введи температуру \\(0 — формальный, 100 — огненный\\):";
        var msg = await client.SendMessage(chatId, text, parseMode: ParseMode.MarkdownV2);
        return msg.Id;
    }
}
