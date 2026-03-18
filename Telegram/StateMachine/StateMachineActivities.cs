namespace mementobot.Telegram.StateMachine;

/// <summary>
/// Звено в цепочке ответственности (Chain of Responsibility) стейт-машины.
/// Принимает контекст и выполняет некое действие. Аналог <c>RequestDelegate</c> в ASP.NET Core.
/// </summary>
internal interface IBehavior<TInstance> where TInstance : class
{
    Task Execute(BehaviorContext<TInstance> context);
}

/// <summary>
/// Активность стейт-машины — основной строительный блок поведения.
/// В отличие от <see cref="IBehavior{TInstance}"/>, получает также следующее звено <c>next</c>,
/// что делает её middleware: она может выполнить логику до или после вызова <c>next</c>, либо прервать цепочку.
/// </summary>
internal interface IStateMachineActivity<TInstance> where TInstance : class
{
    Task Execute(BehaviorContext<TInstance> context, IBehavior<TInstance> next);
}

/// <summary>
/// Терминальное (пустое) звено цепочки ответственности.
/// Ставится в конец цепочки, чтобы последней активности не нужно было знать, есть ли после неё что-то ещё.
/// </summary>
internal class EmptyBehavior<TInstance> : IBehavior<TInstance> where TInstance : class
{
    public Task Execute(BehaviorContext<TInstance> context) => Task.CompletedTask;
}

/// <summary>
/// Промежуточное звено цепочки: оборачивает одну <see cref="IStateMachineActivity{TInstance}"/> в <see cref="IBehavior{TInstance}"/>,
/// передавая ей следующее звено через конструктор.
/// Именно эта обёртка позволяет строить цепочку без рекурсивных вызовов.
/// </summary>
internal class ActivityBehavior<TInstance>(IStateMachineActivity<TInstance> activity, IBehavior<TInstance> next) : IBehavior<TInstance> where TInstance : class
{
    public Task Execute(BehaviorContext<TInstance> context) => activity.Execute(context, next);
}

/// <summary>
/// Строитель цепочки ответственности из набора <see cref="IStateMachineActivity{TInstance}"/>.
/// При вызове <see cref="Execute"/> собирает цепочку в обратном порядке: последняя активность оборачивается в
/// <see cref="EmptyBehavior{TInstance}"/>, предпоследняя — в неё, и так далее.
/// </summary>
internal class ActivityBehaviorBuilder<TInstance> : IBehavior<TInstance> where TInstance : class
{
    private readonly List<IStateMachineActivity<TInstance>> _activities = [];

    public void Add(IStateMachineActivity<TInstance> activity) => _activities.Add(activity);

    public Task Execute(BehaviorContext<TInstance> context)
    {
        if (_activities.Count == 0)
        {
            return Task.CompletedTask;
        }

        var behavior = new ActivityBehavior<TInstance>(_activities[^1], new EmptyBehavior<TInstance>());
        for (var i = _activities.Count - 2; i >= 0; i--)
        {
            behavior = new ActivityBehavior<TInstance>(_activities[i], behavior);
        }

        return behavior.Execute(context);
    }
}

/// <summary>
/// Активность, выполняющая произвольный пользовательский код через переданный делегат.
/// После выполнения делегата всегда передаёт управление в следующее звено цепочки.
/// </summary>
internal class ActionStateMachineActivity<TInstance>(Func<BehaviorContext<TInstance>, Task> action) : IStateMachineActivity<TInstance> where TInstance : class
{
    public async Task Execute(BehaviorContext<TInstance> context, IBehavior<TInstance> next)
    {
        await action(context);
        await next.Execute(context);
    }
}

/// <summary>
/// Активность, выполняющая переход между состояниями.
/// Последовательность действий: поднять Leave текущего → записать новое состояние → поднять Enter нового.
/// Также используется в <see cref="InitialIfNullStateAccessor{TInstance}"/> для установки начального состояния.
/// </summary>
internal class TransitionStateMachineActivity<TInstance>(State<TInstance> toState, IStateAccessor<TInstance> stateAccessor) : IStateMachineActivity<TInstance> where TInstance : class
{
    public async Task Execute(BehaviorContext<TInstance> context, IBehavior<TInstance> next)
    {
        var currentState = await stateAccessor.Get(context);
        if (currentState is null)
        {
            return;
        }

        context.Event = currentState.Leave;
        await currentState.Raise(context);
        await stateAccessor.Set(context, toState);
        context.Event = toState.Enter;
        await toState.Raise(context);

        await next.Execute(context);
    }
}
