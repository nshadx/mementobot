using mementobot.Telegram.Messages;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace mementobot.Services.Messages;

internal class SettingsMenuMessage(ITelegramBotClient client, IMessageStore store)
    : BotMessage<UserSettings>(client, store)
{
    protected override async Task<int> Send(long chatId, UserSettings settings)
    {
        var msg = await client.SendMessage(
            chatId,
            "⚙️ *Настройки*",
            parseMode: ParseMode.MarkdownV2,
            replyMarkup: BuildKeyboard(settings));
        return msg.Id;
    }

    protected override async Task Edit(long chatId, int telegramMessageId, UserSettings settings)
    {
        await client.EditMessageReplyMarkup(chatId, telegramMessageId, BuildKeyboard(settings));
    }

    private static InlineKeyboardMarkup BuildKeyboard(UserSettings settings)
    {
        var remindersLabel = settings.RemindersEnabled ? "🔔 Напоминания: ВКЛ" : "🔕 Напоминания: ВЫКЛ";
        var adultLabel = settings.AdultContent ? "🔞 Режим +18: ВКЛ" : "🔞 Режим +18: ВЫКЛ";

        return new InlineKeyboardMarkup([
            [new InlineKeyboardButton(remindersLabel, "settings:reminders")],
            [new InlineKeyboardButton($"⏰ Время напоминания: {settings.ReminderHour}:00", "settings:hour")],
            [new InlineKeyboardButton($"🌡 Температура: {settings.Temperature}/100", "settings:temperature")],
            [new InlineKeyboardButton(adultLabel, "settings:adult")]
        ]);
    }
}
