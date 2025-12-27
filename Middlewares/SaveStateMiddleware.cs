using mementobot.Services;
using mementobot.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace mementobot.Middlewares;

internal class SaveStateMiddleware(AppDbContext dbContext) : IMiddleware
{
    public async Task Invoke(Context context, UpdateDelegate next)
    {
        if (context.State is null)
        {
            await next(context);
            return;
        }

        var user = await dbContext.Users.SingleAsync(x => x.Id == context.User.Id);

        user.State = context.State;

        await dbContext.SaveChangesAsync();

        await next(context);
    }
}
