using mementobot.Services;
using mementobot.Telegram;

namespace mementobot.Handlers;

internal class HelpCommandHandler(MessageManager messageManager) : IRouteHandler
{
    public async Task Handle(Context context)
    {
        var chatId = context.Update.GetChatId();
        await messageManager.SendHelpMessage(chatId: chatId);
    }
}
