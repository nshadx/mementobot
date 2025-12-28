using mementobot.Constants;
using mementobot.Entities.States;
using mementobot.Middlewares;
using mementobot.Services;
using Microsoft.EntityFrameworkCore;

namespace mementobot.Handlers;

internal class StartQuizQuestionsAddingHandler(
    AppDbContext dbContext
) : IMiddleware
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

        var lastQuestionOrder = await dbContext.QuizQuestions
            .Where(x => x.QuizId == quizId)
            .OrderByDescending(x => x.Order)
            .Select(x => x.Order)
            .FirstOrDefaultAsync();

        context.State = new AddQuestionsState()
        {
            UserId = context.User.Id,
            CurrentProperty = AddQuestionsProperty.QuestionType,
            QuestionType = QuestionType.None,
            LastQuestionOrder = lastQuestionOrder,
            QuizId = quizId
        };

        await next(context);
    }
}
