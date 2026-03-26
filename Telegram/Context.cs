using Telegram.Bot.Types;

namespace mementobot.Telegram;

public class Context(Update update)
{
    public Update Update { get; } = update;
    public bool IsHandled { get; set; }
    public bool DeleteUserMessage { get; set; }
}