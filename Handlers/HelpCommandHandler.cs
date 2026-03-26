using mementobot.Services.Messages;
using mementobot.Telegram;

namespace mementobot.Handlers;

internal class HelpCommandHandler(
    HelpMessage helpMessage,
    HelpGraphMessage helpGraphMessage
) : IRouteHandler
{
    public Task Handle(Context context)
    {
        var text = context.Update.Message?.Text ?? "";
        var chatId = context.Update.GetChatId();

        if (CommandArgumentParser.TryGetArgument<string>("/help", text, 0, out var arg) && arg == "graph")
            return helpGraphMessage.Apply(chatId);

        return helpMessage.Apply(chatId);
    }
}
