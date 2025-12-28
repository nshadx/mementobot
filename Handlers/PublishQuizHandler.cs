using mementobot.Constants;
using mementobot.Entities.States;
using mementobot.Middlewares;
using mementobot.Services;
using mementobot.Telegram;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

namespace mementobot.Handlers;

internal class PublishQuizHandler(ITelegramBotClient client, AppDbContext dbContext) : IMiddleware
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

        if (context.State is not PublishSelectQuizState { MessageId: int messageId, UserId: long userId })
        {
            return;
        }

        var quiz = await dbContext.Quizes.SingleAsync(x => x.Id == quizId);
        quiz.Published = true;
        await dbContext.SaveChangesAsync();

        await client.DeleteMessage(
            chatId: context.Update.GetChatId(),
            messageId: messageId
        );

        await client.SendMessage(
            chatId: context.Update.GetChatId(),
            text: "Опросник опубликован"
        );

        await next(context);
    }
}
