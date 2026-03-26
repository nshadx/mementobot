using mementobot.Telegram.Messages;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace mementobot.Services.Messages;

internal class HelpMessage(ITelegramBotClient client, IMessageStore store)
    : BotMessage(client, store)
{
    protected override async Task<int> Send(long chatId)
    {
        var msg = await client.SendMessage(
            chatId,
            """
            *Доступные команды:*

            /start — выбрать опросник и начать прохождение
            /myquizzes — мои, избранные и недавние опросники
            /new \<название\> — создать новый опросник
            /add — добавить вопрос в опросник
            /publish — опубликовать опросник
            /settings — настройки
            /help — показать это сообщение
            /help graph — схема интерфейса
            """,
            parseMode: ParseMode.MarkdownV2);
        return msg.Id;
    }
}
