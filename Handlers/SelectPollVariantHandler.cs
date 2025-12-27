using mementobot.Constants;
using mementobot.Entities.States;
using mementobot.Services;
using mementobot.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace mementobot.Handlers;

internal class SelectPollVariantHandler(
    AppDbContext dbContext
) : IMiddleware
{
    public async Task Invoke(Context context, UpdateDelegate next)
    {
        if (context.Update.CallbackQuery is not { Data: string variant })
        {
            return;
        }

        if (!Guid.TryParse(variant.AsSpan()[Callback.PollVariantIdPrefix.Length..], out var variantId))
        {
            return;
        }

        if (context.State is not QuizState { CurrentQuestionId: Guid currentQuestionId })
        {
            return;
        }
        
        var currentQuestion = await dbContext.UserQuizQuestions.SingleAsync(x => x.Id == currentQuestionId);
        
        currentQuestion.ChosenVariants.Add(variantId);
        
        await next(context);
    }
}