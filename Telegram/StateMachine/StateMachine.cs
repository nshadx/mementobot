using System.Linq.Expressions;
using System.Reflection;
using Telegram.Bot.Types;

namespace mementobot.Telegram.StateMachine;

/// <summary>
/// Базовый класс стейт-машины. Наследуется конкретными машинами, которые определяют своё поведение
/// исключительно через конструктор с помощью fluent-методов: <c>ConfigureEvent</c>, <c>ConfigureStates</c>,
/// <c>Initially</c>, <c>During</c>, <c>Finally</c> и <c>When</c>.
/// <para>
/// Каждый экземпляр машины — синглтон-поведение: он хранит только конфигурацию (события, состояния, привязки),
/// а изменяемые данные живут в отдельном инстансе <typeparamref name="TInstance"/>, хранящемся в сессии.
/// </para>
/// Поддерживает вложенные машины через <see cref="ConfigureStateMachine{TStateMachine,TOtherInstance}"/> —
/// родительская машина делегирует обработку событий активной дочерней.
/// </summary>
internal class StateMachine<TInstance> : IStateMachine where TInstance : class
{
    private readonly List<Event> _events = [];
    private readonly EventObservable<TInstance> _observer = new();
    private readonly List<ISubStateMachineDescriptor<TInstance>> _subStateMachines = [];
    private readonly Dictionary<object, object> _subMachineDescriptors = [];

    public State<TInstance> Final { get; }
    public State<TInstance> Initial { get; }

    public IStateAccessor<TInstance> StateAccessor { get; private set; }

    protected StateMachine()
    {
        Final = new("Final", _observer);
        Initial = new("Initial", _observer);
        StateAccessor = new DefaultStateAccessor<TInstance>();
    }

    internal IStateMachineActivity<TInstance> CreateSubActivateActivity<TSubInstance>(
        StateMachine<TSubInstance> subMachine,
        State<TSubInstance> subState) where TSubInstance : class
    {
        var descriptor = (SubStateMachineDescriptor<TInstance, TSubInstance>)_subMachineDescriptors[subMachine];
        return descriptor.CreateActivateActivity(subState);
    }

    public Event? FindEvent(Update update, object? instance)
    {
        if (instance is null)
        {
            return Initial.Events
                .Where(e => e != Initial.Enter && e != Initial.Leave)
                .FirstOrDefault(e => e.Condition?.Invoke(update) ?? false);
        }

        if (instance is not TInstance typedInstance)
        {
            return null;
        }

        var activeSub = _subStateMachines.FirstOrDefault(d => d.IsActive(typedInstance));
        if (activeSub is not null)
        {
            return activeSub.FindApplicableEvent(update, typedInstance);
        }

        var candidates = StateAccessor.GetState(typedInstance)?.Events ?? _events;
        return candidates.FirstOrDefault(e => e.Condition?.Invoke(update) ?? false);
    }

    public async Task RaiseEvent(BehaviorContext context)
    {
        if (context is BehaviorContext<TInstance> genericContext)
        {
            foreach (var descriptor in _subStateMachines)
            {
                descriptor.EnsureSubInstanceInitialized(genericContext.Instance);
            }

            foreach (var descriptor in _subStateMachines)
            {
                var handled = await descriptor.TryRaiseEvent(genericContext);
                if (handled)
                {
                    return;
                }
            }

            var currentState = await StateAccessor.Get(genericContext);
            if (currentState is not null)
            {
                await currentState.Raise(genericContext);
            }
        }
    }

    /// <summary>
    /// Регистрирует <paramref name="stateMachine"/> как вложенную машину.
    /// Доступ к её инстансу осуществляется через свойство <paramref name="stateAccessor"/> в <typeparamref name="TInstance"/>.
    /// </summary>
    protected void ConfigureStateMachine<TStateMachine, TOtherInstance>(
        TStateMachine stateMachine,
        Expression<Func<TInstance, TOtherInstance?>> stateAccessor
    ) where TOtherInstance : class where TStateMachine : StateMachine<TOtherInstance>
    {
        var descriptor = new SubStateMachineDescriptor<TInstance, TOtherInstance>(stateMachine, stateAccessor);
        _subStateMachines.Add(descriptor);
        _subMachineDescriptors[stateMachine] = descriptor;
    }

    /// <summary>
    /// Начинает fluent-конфигурацию реакции родительской машины на событие <paramref name="subEvent"/> вложенной.
    /// </summary>
    protected SubEventActivityBinder<TInstance, TSubInstance> When<TSubInstance>(
        StateMachine<TSubInstance> subMachine,
        Event subEvent) where TSubInstance : class
    {
        var descriptor = (ISubStateMachineDescriptor<TInstance>)_subMachineDescriptors[subMachine];
        return new SubEventActivityBinder<TInstance, TSubInstance>(this, descriptor, subEvent);
    }

    /// <summary>
    /// Создаёт событие и записывает его в свойство, указанное через Expression.
    /// Expression здесь нужен для получения и имени свойства (имя события), и его setter'а без строк.
    /// </summary>
    protected void ConfigureEvent<TEvent>(Expression<Func<TEvent>> propertyAccessor, Func<Update, bool> condition)
    {
        var propertyInfo = (PropertyInfo)((MemberExpression)propertyAccessor.Body).Member;
        Event @event = new(propertyInfo.Name, condition);
        propertyInfo.SetValue(this, @event);
        _events.Add(@event);
    }

    /// <summary>
    /// Создаёт пользовательские состояния, записывает их в соответствующие свойства машины,
    /// и настраивает <see cref="StateAccessor"/> — цепочку
    /// <see cref="InitialIfNullStateAccessor{TInstance}"/> → <see cref="IntStateAccessor{TInstance}"/>.
    /// </summary>
    protected void ConfigureStates(Expression<Func<TInstance, int>> propertyAccessor, params Expression<Func<State<TInstance>>>[] stateAccessors)
    {
        List<State<TInstance>> stateList = new(stateAccessors.Length);
        foreach (var expression in stateAccessors)
        {
            var propertyInfo = (PropertyInfo)((MemberExpression)expression.Body).Member;
            State<TInstance> state = new(propertyInfo.Name, _observer);
            propertyInfo.SetValue(this, state);
            stateList.Add(state);
        }

        var states = stateList.ToArray();
        StateAccessorIndex<TInstance> index = new(Initial, Final, states);
        IntStateAccessor<TInstance> stateAccessor = new(propertyAccessor, index);

        StateAccessor = new InitialIfNullStateAccessor<TInstance>(Initial, stateAccessor);
    }

    /// <summary>Создаёт конфигуратор, помечающий событие как игнорируемое в данном состоянии.</summary>
    protected IEventActivities<TInstance> Ignore(Event @event) => new EventActivityBinder<TInstance>(this, @event, new IgnoreActivityBinder<TInstance>(@event));

    /// <summary>
    /// Привязывает все активности из <paramref name="eventActivities"/> к состоянию <paramref name="state"/>.
    /// Доступен из extension-методов (<c>internal</c>) для поддержки <see cref="StateMachineExtensions"/>.
    /// </summary>
    internal void During(State<TInstance> state, params IEventActivities<TInstance>[] eventActivities)
    {
        foreach (var eventActivity in eventActivities)
        {
            foreach (var activityBinder in eventActivity.GetStateActivityBinders())
            {
                activityBinder.Bind(state);
            }
        }
    }
    
    /// <summary>
    /// Привязывает активности к состоянию <see cref="StateMachine{TInstance}.Initial"/>.
    /// Используется для настройки реакции на первое событие, запускающее машину.
    /// </summary>
    public void Initially(params IEventActivities<TInstance>[] eventActivities) =>
        During(Initial, eventActivities);

    /// <summary>
    /// Привязывает активности к событию <see cref="State{TInstance}.Enter"/> состояния <see cref="StateMachine{TInstance}.Final"/>.
    /// Удобная обёртка для финальных действий — очистки, отправки сообщений и т.д.
    /// </summary>
    public void Finally(Func<EventActivityBinder<TInstance>, EventActivityBinder<TInstance>> action) =>
        During(Final, action(When(Final.Enter)));

    /// <summary>
    /// Устанавливает <see cref="BehaviorContext.IsCompleted"/> при входе в <see cref="StateMachine{TInstance}.Final"/>,
    /// что сигнализирует <see cref="StateMachineMiddleware"/> об удалении сессии из <see cref="ISessionStore"/>.
    /// Вызывается в конце конструктора машины, если та должна завершаться.
    /// </summary>
    public void SetCompletedOnFinal()
    {
        During(Final, When(Final.Enter).Then(context =>
        {
            context.IsCompleted = true;
            return Task.CompletedTask;
        }));
    }

    /// <summary>
    /// Подключает наблюдателя, который будет вызываться перед каждым выполнением активностей состояния.
    /// </summary>
    public void ConnectEventObserver(IEventObserver<TInstance> observer) => _observer.Connect(observer);

    /// <summary>
    /// Начинает fluent-конфигурацию реакции на событие <paramref name="event"/> в текущей машине.
    /// Доступен из extension-методов (<c>internal</c>) для поддержки <see cref="StateMachineExtensions"/>.
    /// </summary>
    internal EventActivityBinder<TInstance> When(Event @event) => new(this, @event);
}
