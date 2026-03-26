using Telegram.Bot;

namespace mementobot.Telegram;

internal class DeleteUserMessageMiddleware(ITelegramBotClient client) : IMiddleware
{
    public async Task Handle(Context context, UpdateDelegate next)
    {
        await next(context);

        if (context.DeleteUserMessage && context.Update.Message is { } msg)
            await client.DeleteMessage(msg.Chat.Id, msg.Id);
    }
}
