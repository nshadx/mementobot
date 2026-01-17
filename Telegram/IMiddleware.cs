namespace mementobot.Telegram;

public delegate Task UpdateDelegate(Context context);

public interface IMiddleware
{
    Task Handle(Context context, UpdateDelegate next);
}