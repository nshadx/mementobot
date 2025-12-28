using mementobot.Entities.States;
using mementobot.Middlewares;
using mementobot.Services;
using Microsoft.EntityFrameworkCore;

namespace mementobot.Handlers;

internal class SetQuizQuestionsHandler(AppDbContext dbContext) : IMiddleware
{
    public async Task Invoke(Context context, UpdateDelegate next)
    {
        if (context.State is not QuizState state)
        {
            return;
        }

        var quiz = await dbContext.Quizes.Include(x => x.Questions).SingleAsync(x => x.Id == state.QuizId);

        state.Questions = [.. quiz.Questions.Select(x => new UserQuizQuestion() { QuizQuestionId = x.Id, Order = x.Order })];
        await dbContext.SaveChangesAsync();

        await next(context);
    }
}
