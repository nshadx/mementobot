namespace mementobot.Telegram.StateMachine;

internal class StateMachineMiddleware(
    ISessionStore sessionStore,
    BehaviorContextFactory factory
) : IMiddleware
{
    public async Task Handle(Context context, UpdateDelegate next)
    {
        var chatId = context.Update.GetChatId();
        if (factory.TryCreate(sessionStore.Get(chatId), context.Update) is not { } result)
        {
            await next(context);
            return;
        }

        var (behaviorContext, isInitial) = result;
        if (isInitial)
        {
            sessionStore.Remove(chatId);
        }

        context.IsHandled = true;

        if (await behaviorContext.Raise())
        {
            sessionStore.Remove(chatId);
        }
        else
        {
            sessionStore.Set(chatId, behaviorContext.InstanceObject);
        }
    }
}
