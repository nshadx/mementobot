using mementobot.Entities.States;
using mementobot.Middlewares;
using mementobot.Services;
using Microsoft.EntityFrameworkCore;

namespace mementobot.Handlers;

internal class SelectMatchHandler(
    AppDbContext dbContext
) : IMiddleware
{
    public async Task Invoke(Context context, UpdateDelegate next)
    {
        if (context.Update.CallbackQuery is not { Data: string match })
        {
            return;
        }

        //todo: matchId: starts with
        var parts = match.Split(';');
        if (!int.TryParse(parts[0].Split(':')[1], out var fromId) || !int.TryParse(parts[1].Split(':')[1], out var toId))
        {
            return;
        }

        if (context.State is not QuizState { CurrentQuestionId: Guid currentQuestionId })
        {
            return;
        }
        
        var currentQuestion = await dbContext.UserQuizQuestions.SingleAsync(x => x.Id == currentQuestionId);

        currentQuestion.ChosenMatches.Add(new UserSelectedMatch()
        {
            FromId = fromId,
            ToId = toId
        });
        
        await next(context);
    }
}