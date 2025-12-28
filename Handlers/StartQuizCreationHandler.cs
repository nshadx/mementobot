using mementobot.Entities.States;
using mementobot.Middlewares;
using mementobot.Telegram;
using Telegram.Bot;

namespace mementobot.Handlers;

internal class StartQuizCreationHandler(ITelegramBotClient client) : IMiddleware
{
    public async Task Invoke(Context context, UpdateDelegate next)
    {
        context.State = new CreateQuizState()
        {
            Quiz = new()
            {
                Published = false,
                UserId = context.User.Id
            }
        };

        await client.SendMessage(
            chatId: context.Update.GetChatId(),
            text: "Введи название опросника"
        );

        await next(context);
    }
}
