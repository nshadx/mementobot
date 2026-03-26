using mementobot.Telegram.Messages;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace mementobot.Services.Messages;

internal class ReminderSpeechMessage(ITelegramBotClient client, IMessageStore store)
    : BotMessage<string>(client, store)
{
    protected override async Task<int> Send(long chatId, string markdownText)
    {
        var msg = await client.SendMessage(chatId, markdownText, parseMode: ParseMode.MarkdownV2);
        return msg.Id;
    }
}
