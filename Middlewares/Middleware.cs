using mementobot.Services;
using Telegram.Bot.Types;

namespace mementobot.Middlewares;

internal record Context(Update Update)
{
    public int UserId { get; set; }
    public StateType? CurrentState { get; set; }
    public ActionType? ActionType { get; set; }
}
internal delegate Task UpdateDelegate(Context context);
internal interface IMiddleware
{
    Task Invoke(Context context, UpdateDelegate next);
}
