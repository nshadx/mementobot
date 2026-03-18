namespace mementobot.Telegram.StateMachine;

/// <summary>
/// Связывает конкретную активность с конкретным состоянием <see cref="State{TInstance}"/>.
/// Вызывается на этапе конфигурации машины (в конструкторе) через <see cref="StateMachine{TInstance}.During"/>.
/// </summary>
internal interface IActivityBinder<TInstance> where TInstance : class
{
    void Bind(State<TInstance> state);
}

/// <summary>
/// Поставщик набора привязок <see cref="IActivityBinder{TInstance}"/>.
/// Является общей абстракцией для всех конфигураторов (<see cref="EventActivityBinder{TInstance}"/> и других),
/// позволяя передавать их в методы <c>During</c>/<c>Initially</c>/<c>Finally</c> единообразно.
/// </summary>
internal interface IEventActivities<TInstance> where TInstance : class
{
    IEnumerable<IActivityBinder<TInstance>> GetStateActivityBinders();
}

/// <summary>
/// Fluent-builder для привязки цепочки активностей к конкретному событию в конкретном состоянии.
/// Каждый вызов <c>Then</c>/<c>TransitionTo</c> добавляет новую активность в накапливаемый список.
/// Результирующие привязки применяются при вызове <see cref="StateMachine{TInstance}.During"/>.
/// </summary>
internal class EventActivityBinder<TInstance>(StateMachine<TInstance> stateMachine, Event @event, params IActivityBinder<TInstance>[] activities) : IEventActivities<TInstance> where TInstance : class
{
    private readonly List<IActivityBinder<TInstance>> _binders = activities.ToList();

    public StateMachine<TInstance> StateMachine { get; } = stateMachine;

    public EventActivityBinder<TInstance> Add(IStateMachineActivity<TInstance> activity)
    {
        _binders.Add(new ExecuteActivityBinder<TInstance>(@event, activity));
        return this;
    }

    public IEnumerable<IActivityBinder<TInstance>> GetStateActivityBinders() => _binders;
}

/// <summary>
/// Привязывает событие к состоянию как «игнорируемое» — машина не будет реагировать на него в данном состоянии.
/// Используется через <see cref="StateMachine{TInstance}.Ignore"/>.
/// </summary>
internal class IgnoreActivityBinder<TInstance>(Event @event) : IActivityBinder<TInstance> where TInstance : class
{
    public void Bind(State<TInstance> state) => state.Ignore(@event);
}

/// <summary>
/// Привязывает активность <see cref="IStateMachineActivity{TInstance}"/> к событию в рамках состояния.
/// При каждом срабатывании события в данном состоянии активность будет включена в цепочку выполнения.
/// </summary>
internal class ExecuteActivityBinder<TInstance>(Event @event, IStateMachineActivity<TInstance> activity) : IActivityBinder<TInstance> where TInstance : class
{
    public Event Event { get; } = @event;

    public void Bind(State<TInstance> state) => state.Bind(Event, activity);
}

/// <summary>
/// Расширения <see cref="EventActivityBinder{TInstance}"/> для удобного fluent-синтаксиса.
/// <c>Then</c> — добавить произвольный код; <c>TransitionTo</c> — добавить переход к состоянию (текущей или вложенной машины).
/// </summary>
internal static class EventActivityBinderExtensions
{
    extension<TInstance>(EventActivityBinder<TInstance> binder) where TInstance : class
    {
        public EventActivityBinder<TInstance> Then(Func<BehaviorContext<TInstance>, Task> action)
        {
            return binder.Add(new ActionStateMachineActivity<TInstance>(action));
        }

        public EventActivityBinder<TInstance> TransitionTo(State<TInstance> state)
        {
            return binder.Add(new TransitionStateMachineActivity<TInstance>(state, binder.StateMachine.StateAccessor));
        }

        public EventActivityBinder<TInstance> TransitionTo<TSubInstance>(
            StateMachine<TSubInstance> subMachine,
            State<TSubInstance> subState
        ) where TSubInstance : class
        {
            var activity = binder.StateMachine.CreateSubActivateActivity(subMachine, subState);
            return binder.Add(activity);
        }
    }
}

/// <summary>
/// Расширения <see cref="BehaviorContext{TInstance}"/> для использования внутри активностей.
/// <c>TransitionTo</c> позволяет совершить переход прямо из тела <c>Then</c>-делегата,
/// минуя fluent-builder (полезно при условных переходах).
/// </summary>
internal static class BehaviorContextExtensions
{
    extension<TInstance>(BehaviorContext<TInstance> context) where TInstance : class
    {
        public async Task TransitionTo(State<TInstance> toState)
        {
            var stateAccessor = context.StateMachine.StateAccessor;
            TransitionStateMachineActivity<TInstance> activity = new(toState, stateAccessor);
            ActivityBehavior<TInstance> behavior = new(activity, new EmptyBehavior<TInstance>());
            await behavior.Execute(context);
        }
    }
}
