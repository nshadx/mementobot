using mementobot.Services;
using Microsoft.EntityFrameworkCore;

namespace mementobot.Middlewares;

internal class SetStateMiddleware(AppDbContext dbContext) : IMiddleware
{
    public async Task Invoke(Context context, UpdateDelegate next)
    {
        context.State = await dbContext.States.SingleOrDefaultAsync(x => x.UserId == context.User.Id);
        await next(context);
    }
}
