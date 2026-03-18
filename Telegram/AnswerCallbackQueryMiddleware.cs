using Telegram.Bot;

namespace mementobot.Telegram;

internal class AnswerCallbackQueryMiddleware(ITelegramBotClient client) : IMiddleware
{
    public async Task Handle(Context context, UpdateDelegate next)
    {
        if (context.Update.CallbackQuery is { } callbackQuery)
        {
            await client.AnswerCallbackQuery(callbackQuery.Id);
        }

        await next(context);
    }
}
