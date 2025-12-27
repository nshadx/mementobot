using mementobot.Entities.States;
using mementobot.Services;
using mementobot.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace mementobot.Handlers;

internal class SelectQuizForQuestionAddingHandler(AppDbContext dbContext) : IMiddleware
{
    private const int ItemsPerPage = 6;
    public async Task Invoke(Context context, UpdateDelegate next)
    {
        var count = await dbContext.Quizes.CountAsync(x => x.UserId == context.User.Id);
        var pages = (int)Math.Ceiling(count / (double)ItemsPerPage);

        context.State = new QuestionsSelectQuizState()
        {
            CurrentPage = 1,
            ItemsPerPage = ItemsPerPage,
            PagesCount = pages,
            OnlyFromUser = true,
            UserId = context.User.Id
        };

        await next(context);
    }
}
