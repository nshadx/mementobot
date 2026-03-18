using Telegram.Bot;

namespace mementobot.Telegram;

internal class DeleteCommandMiddleware(ITelegramBotClient client) : IMiddleware
{
    public async Task Handle(Context context, UpdateDelegate next)
    {
        if (context.Update.Message is { Text: { } text, MessageId: var messageId } && text.StartsWith('/'))
        {
            await client.DeleteMessage(
                chatId: context.Update.GetChatId(),
                messageId: messageId
            );
        }

        await next(context);
    }
}
