using mementobot.Entities.States;
using mementobot.Middlewares;
using mementobot.Services;
using Microsoft.EntityFrameworkCore;

namespace mementobot.Handlers;

internal class StartQuizHandler(AppDbContext dbContext) : IMiddleware
{
    private const int ItemsPerPage = 6;
    public async Task Invoke(Context context, UpdateDelegate next)
    {
        var count = await dbContext.Quizes.CountAsync(x => x.Published);
        var pages = (int)Math.Ceiling(count / (double)ItemsPerPage);

        context.State = new StartSelectQuizState()
        {
            CurrentPage = 1,
            ItemsPerPage = ItemsPerPage,
            PagesCount = pages,
            WherePublished = true,
            UserId = context.User.Id,
            Type = Entities.QuizType.Public
        };

        await next(context);
    }
}
