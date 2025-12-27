using Telegram.Bot.Types;

namespace mementobot.Extensions
{
    internal static class TelegramExtensions
    {
        public static long GetChatId(this Update update) => update.Message?.Chat.Id ??
                                                            update.CallbackQuery?.From.Id ??
                                                            update.PollAnswer?.User?.Id ??
                                                            throw new InvalidOperationException("chat was not found");
    }
}
