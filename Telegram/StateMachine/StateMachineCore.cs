using Telegram.Bot.Types;

namespace mementobot.Telegram.StateMachine;

/// <summary>
/// Триггер стейт-машины. Является синглтоном — один экземпляр на всё время жизни машины.
/// Содержит опциональное условие <see cref="Condition"/>, по которому определяется, применим ли триггер к входящему апдейту.
/// Если условие не задано, событие не может быть поднято автоматически (используется только для Enter/Leave).
/// </summary>
internal class Event
{
    public string Name { get; }
    public Func<Update, bool>? Condition { get; }

    public Event(string name) { Name = name; }
    public Event(string name, Func<Update, bool> condition) { Name = name; Condition = condition; }
}

/// <summary>
/// Контракт стейт-машины. Позволяет работать с машиной без знания типа её инстанса <c>TInstance</c>.
/// Используется в инфраструктурном коде (<see cref="BehaviorContextFactory"/>, <see cref="StateMachineMiddleware"/>).
/// </summary>
internal interface IStateMachine
{
    /// <summary>Ищет событие, применимое к данному апдейту и инстансу. Возвращает <c>null</c>, если подходящего события нет.</summary>
    Event? FindEvent(Update update, object? instance);

    /// <summary>Поднимает событие, записанное в <see cref="BehaviorContext.Event"/>, и выполняет привязанные активности.</summary>
    Task RaiseEvent(BehaviorContext context);
}

/// <summary>
/// Наблюдатель, вызываемый перед каждым выполнением активностей состояния.
/// Позволяет встраивать сквозную логику (логирование, трассировка) без изменения самих активностей.
/// </summary>
internal interface IEventObserver<TInstance> where TInstance : class
{
    Task Execute(BehaviorContext<TInstance> context);
}

/// <summary>
/// Реализация паттерна «Наблюдатель» для событий стейт-машины.
/// Хранит список подписчиков и вызывает их последовательно при каждом срабатывании события.
/// </summary>
internal class EventObservable<TInstance> : IEventObserver<TInstance> where TInstance : class
{
    private readonly List<IEventObserver<TInstance>> _observers = [];

    public void Connect(IEventObserver<TInstance> observer)
    {
        _observers.Add(observer);
    }

    public async Task Execute(BehaviorContext<TInstance> context)
    {
        foreach (var observer in _observers)
        {
            await observer.Execute(context);
        }
    }
}

/// <summary>
/// Базовый (нетипизированный) контекст выполнения стейт-машины.
/// Передаётся через всю цепочку активностей и несёт в себе текущее событие, входящий апдейт и флаг завершения.
/// </summary>
internal abstract class BehaviorContext(IServiceProvider serviceProvider, Event @event, Update update)
{
    /// <summary>Флаг завершения</summary>
    public bool IsCompleted { get; set; }

    /// <summary>Текущее поднятое событие. Меняется в процессе перехода (Leave → Enter следующего состояния).</summary>
    public Event Event { get; set; } = @event;

    public Update Update { get; set; } = update;
    public IServiceProvider ServiceProvider { get; } = serviceProvider;

    /// <summary>Контекст родительской стейт-машины при работе в режиме вложенной машины.</summary>
    public BehaviorContext? ParentContext { get; set; }

    public abstract object InstanceObject { get; }

    /// <summary>Делегирует вызов в <see cref="IStateMachine.RaiseEvent"/> и возвращает <see cref="IsCompleted"/>.</summary>
    public abstract Task<bool> Raise();
}

/// <summary>
/// Типизированный контекст выполнения стейт-машины.
/// Добавляет доступ к инстансу <typeparamref name="TInstance"/>, в котором хранится текущее состояние и данные сессии.
/// </summary>
internal class BehaviorContext<TInstance>(
    IServiceProvider serviceProvider,
    StateMachine<TInstance> stateMachine,
    TInstance instance,
    Event @event,
    Update update
) : BehaviorContext(serviceProvider, @event, update) where TInstance : class
{
    public StateMachine<TInstance> StateMachine { get; } = stateMachine;
    public TInstance Instance { get; } = instance;

    public override object InstanceObject => Instance;

    public override async Task<bool> Raise()
    {
        await StateMachine.RaiseEvent(this);
        return IsCompleted;
    }
}

/// <summary>
/// Состояние стейт-машины. Хранит карту «событие → цепочка активностей» и список игнорируемых событий.
/// При вызове <see cref="Raise"/> последовательно выполняет наблюдателей и привязанную цепочку активностей.
/// Каждое состояние автоматически получает два служебных события: <see cref="Enter"/> и <see cref="Leave"/>.
/// </summary>
internal class State<TInstance> where TInstance : class
{
    private readonly Dictionary<Event, ActivityBehaviorBuilder<TInstance>> _behaviors = [];
    private readonly HashSet<Event> _ignoredEvents = [];
    private readonly HashSet<Event> _events = [];
    private readonly IEventObserver<TInstance> _observer;

    public string Name { get; }

    /// <summary>Событие, поднимаемое при входе в это состояние.</summary>
    public Event Enter { get; }

    /// <summary>Событие, поднимаемое при выходе из этого состояния.</summary>
    public Event Leave { get; }

    public IReadOnlyCollection<Event> Events => _events;

    public State(string name, IEventObserver<TInstance> observer)
    {
        _observer = observer;
        Name = name;
        Enter = new($"{name}.Enter");
        Leave = new($"{name}.Leave");
    }

    public void Bind(Event @event, IStateMachineActivity<TInstance> activity)
    {
        _events.Add(@event);
        if (!_behaviors.TryGetValue(@event, out var builder))
        {
            builder = new();
            _behaviors.Add(@event, builder);
        }

        builder.Add(activity);
    }

    public void Ignore(Event @event)
    {
        _ignoredEvents.Add(@event);
    }

    public async Task Raise(BehaviorContext<TInstance> context)
    {
        if (_behaviors.TryGetValue(context.Event, out var behavior))
        {
            if (_ignoredEvents.Contains(context.Event))
            {
                return;
            }

            await _observer.Execute(context);
            await behavior.Execute(context);
        }
    }
}
