using mementobot.Constants;
using mementobot.Entities.States;
using mementobot.Extensions;
using mementobot.Services;
using mementobot.Services.Common;
using Telegram.Bot;

namespace mementobot.Handlers;

internal class SelectQuizForStartingHandler(ITelegramBotClient client, AppDbContext dbContext) : IMiddleware
{
    public async Task Invoke(Context context, UpdateDelegate next)
    {
        if (context.Update.CallbackQuery is not { Data: string data })
        {
            return;
        }

        if (!Guid.TryParse(data.AsSpan()[Callback.QuizIdPrefix.Length..], out var quizId))
        {
            return;
        }

        if (context.State is not StartSelectQuizState { MessageId: int messageId, UserId: long userId })
        {
            return;
        }

        context.State = new QuizState()
        {
            QuizId = quizId,
            UserId = userId
        };
        await dbContext.States.AddAsync(context.State);

        await client.DeleteMessage(
            chatId: context.Update.GetChatId(),
            messageId: messageId
        );

        await next(context);
    }
}
