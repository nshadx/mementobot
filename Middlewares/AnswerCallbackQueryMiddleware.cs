using Telegram.Bot;

namespace mementobot.Middlewares;

internal class AnswerCallbackQueryMiddleware(ITelegramBotClient client) : IMiddleware
{
    public async Task Invoke(Context context, UpdateDelegate next)
    {
        if (context.Update.CallbackQuery is not { Id: string id })
        {
            return;
        }

        await client.AnswerCallbackQuery(
                callbackQueryId: id
        );
    }
}
