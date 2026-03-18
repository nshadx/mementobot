using mementobot.Telegram.StateMachine;
using Telegram.Bot.Types;

namespace mementobot.Telegram;

internal class BehaviorContextFactory(
    IServiceProvider serviceProvider,
    IEnumerable<IStateMachine> stateMachines
)
{
    public (BehaviorContext Context, bool IsInitial)? TryCreate(object? session, Update update)
    {
        foreach (var candidate in stateMachines)
        {
            var @event = candidate.FindEvent(update, null);
            if (@event is null)
            {
                continue;
            }

            var instance = Activator.CreateInstance(GetInstanceType(candidate))!;
            return (CreateContext(candidate, instance, @event, update), true);
        }

        if (session is null)
        {
            return null;
        }

        var sm = stateMachines.SingleOrDefault(x => GetInstanceType(x) == session.GetType());
        if (sm?.FindEvent(update, session) is not { } applicableEvent)
        {
            return null;
        }

        return (CreateContext(sm, session, applicableEvent, update), false);
    }

    private BehaviorContext CreateContext(IStateMachine sm, object instance, Event @event, Update update)
    {
        var instanceType = GetInstanceType(sm);
        var contextType = typeof(BehaviorContext<>).MakeGenericType(instanceType);
        return (BehaviorContext)Activator.CreateInstance(contextType, serviceProvider, sm, instance, @event, update)!;
    }

    private static Type GetInstanceType(IStateMachine sm) =>
        sm.GetType().BaseType!.GetGenericArguments()[0];
}
