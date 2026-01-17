using Telegram.Bot.Types;

namespace mementobot.Telegram;

public class Context(Update update)
{
    public Update Update { get; } = update;
}