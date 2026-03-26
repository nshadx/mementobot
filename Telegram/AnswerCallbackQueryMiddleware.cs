using Telegram.Bot;

namespace mementobot.Telegram;

internal class AnswerCallbackQueryMiddleware(ITelegramBotClient client) : IMiddleware
{
    public async Task Handle(Context context, UpdateDelegate next)
    {
        await next(context);

        if (context.Update.CallbackQuery is not { } callbackQuery)
            return;

        if (!context.IsHandled && callbackQuery.Message is { } msg)
            await client.DeleteMessage(msg.Chat.Id, msg.Id);

        await client.AnswerCallbackQuery(callbackQuery.Id);
    }
}
