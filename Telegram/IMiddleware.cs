namespace mementobot.Telegram;

internal delegate Task UpdateDelegate(Context context);

internal interface IMiddleware
{
    Task Handle(Context context, UpdateDelegate next);
}