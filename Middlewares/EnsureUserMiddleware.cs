using mementobot.Services;
using mementobot.Telegram;

namespace mementobot.Middlewares;

internal class EnsureUserMiddleware(
    UserService userService
) : IMiddleware
{
    public async Task Invoke(Context context, UpdateDelegate next)
    {
        var telegramId = context.Update.GetChatId();

        var userId = userService.GetOrCreateUser(telegramId);
        context.UserId = userId;
        
        await next(context);
    }
}
