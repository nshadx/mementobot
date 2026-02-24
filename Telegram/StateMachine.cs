using System.Linq.Expressions;
using System.Reflection;
using Telegram.Bot.Types;

namespace mementobot.Telegram;

/*
 * Философия реализации стейт-машины масстранзита в том, что она регистрируется как сервис и задает исключительно поведение (оно определеяется конструктором, импл. стейт-машину)
 * Все непосредственные шаги стейт-машины завязаны на BehaviorContext & TInstance. Потому один из главных классов тут - IntStateAccessor/IStateAccessor.
 */

/*
 * Событие стейт-машины. Является ее синглтоном, просто задает некий триггер.
 */
public class Event
{
    public string Name { get; }
    public Func<Update, bool>? Condition { get; }

    public Event(string name) { Name = name; }
    public Event(string name, Func<Update, bool> condition) { Name = name; Condition = condition; }
}

/*
 * Основа стейт-машины. К состоянию привязываются события и поведения. Переход стейт-машины к следующему состоянию происходит через state.Raise
 */
public class State<TInstance> where TInstance : class
{
    private readonly Dictionary<Event, ActivityBehaviorBuilder<TInstance>> _behaviors = [];
    private readonly HashSet<Event> _ignoredEvents = [];
    private readonly HashSet<Event> _events = [];
    private readonly IEventObserver<TInstance> _observer;
    
    public string Name { get; }
    
    public Event Enter { get; }
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

/*
 * Отвечает за создание цепочки ответственности, он использует промежуточную структуру ActivityBehavior
 */
public class ActivityBehaviorBuilder<TInstance> : IBehavior<TInstance> where TInstance : class
{
    private readonly List<IStateMachineActivity<TInstance>> _activities = [];

    public void Add(IStateMachineActivity<TInstance> activity)
    {
        _activities.Add(activity);
    }
    
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

/*
 * Хвост цепочки ответственностей
 */
public class EmptyBehavior<TInstance> : IBehavior<TInstance> where TInstance : class
{
    public Task Execute(BehaviorContext<TInstance> context)
    {
        return Task.CompletedTask;
    }
}

/*
 * Промежуточная структура, используемая в создании цепочки ответственностей.
 * Цепочка создается в виде IBehavior который можно вызывать. Если бы использовался класс IStateMachineActivity, его было бы невозможно вызвать.
 * Здесь это ограничение обходится путем конструктора и EmptyBehavior в конце.
 */
public class ActivityBehavior<TInstance>(IStateMachineActivity<TInstance> activity, IBehavior<TInstance> next) : IBehavior<TInstance> where TInstance : class
{
    public Task Execute(BehaviorContext<TInstance> context)
    {
        return activity.Execute(context, next);
    }
}

/*
 * Интерфейс получения текущего состояния стейт-машины. Сделан интферфейсом т.к. в качестве реализации активно используются декораторы.
 */
public interface IStateAccessor<TInstance> where TInstance : class
{
    State<TInstance>? GetState(TInstance instance);
    Task<State<TInstance>?> Get(BehaviorContext<TInstance> context);
    Task Set(BehaviorContext<TInstance> context, State<TInstance> state);
}

// public class DefaultStateAccessor<TInstance> : IStateAccessor<TInstance> where TInstance : class
// {
//     private readonly IStateAccessor<TInstance> _accessor;
//     
//     public DefaultStateAccessor()
//     {
//         var states = typeof(TInstance)
//             .GetProperties(BindingFlags.Public | BindingFlags.Instance)
//             .Where(x => x.GetGetMethod() is not null)
//             .Where(x => x.GetSetMethod() is not null)
//             .ToList();
//
//         if (states.Count == 0)
//         {
//             throw new Exception("");
//         }
//
//         var state = states[0];
//         var instance = Expression.Parameter(typeof(TInstance), "instance");
//         var memberExpression = Expression.Property(instance, state);
//         var expression = Expression.Lambda<Func<TInstance, State<TInstance>>>(memberExpression, instance);
//
//          _accessor = new InitialIfNullStateAccessor<TInstance>(new RawStateAccessor<TInstance>(expression));
//     }
//
//     public Task<State<TInstance>> Get(BehaviorContext<TInstance> context)
//     {
//         return _accessor.Get(context);
//     }
//
//     public Task Set(BehaviorContext<TInstance> context, State<TInstance> state)
//     {
//         return _accessor.Set(context, state);
//     }
// }
//
// public class RawStateAccessor<TInstance>(Expression<Func<TInstance, State<TInstance>>> expression) : IStateAccessor<TInstance> where TInstance : class
// {
//     public Task<State<TInstance>> Get(BehaviorContext<TInstance> context)
//     {
//         
//     }
//
//     public Task Set(BehaviorContext<TInstance> context, State<TInstance> state)
//     {
//         throw new NotImplementedException();
//     }
// }

/*
 * Пустая реализация IStateAccessor, ничего примечательного
 */
public class DefaultStateAccessor<TInstance> : IStateAccessor<TInstance> where TInstance : class
{
    public State<TInstance>? GetState(TInstance instance) => null;
    public Task<State<TInstance>?> Get(BehaviorContext<TInstance> context) => Task.FromResult<State<TInstance>?>(null);
    public Task Set(BehaviorContext<TInstance> context, State<TInstance> state) => Task.CompletedTask;
}

/*
 * Переводит стейт-машину в начальное состояние, если никакого другого не задано. Такая ситуация возможна только при старте стейт-машины.
 */
public class InitialIfNullStateAccessor<TInstance> : IStateAccessor<TInstance> where TInstance : class
{
    private readonly IStateAccessor<TInstance> _stateAccessor;
    private readonly IBehavior<TInstance> _initialBehavior;
    
    public InitialIfNullStateAccessor(State<TInstance> initialState, IStateAccessor<TInstance> stateAccessor)
    {
        var activity = new TransitionStateMachineActivity<TInstance>(initialState, stateAccessor);
        //todo: LastBehavior
        _initialBehavior = new ActivityBehavior<TInstance>(activity, new EmptyBehavior<TInstance>());
        _stateAccessor = stateAccessor;
    }
    
    public State<TInstance>? GetState(TInstance instance) => _stateAccessor.GetState(instance);

    public async Task<State<TInstance>?> Get(BehaviorContext<TInstance> context)
    {
        var state = await _stateAccessor.Get(context);
        if (state is null)
        {
            await _initialBehavior.Execute(context);
            state = await _stateAccessor.Get(context);
        }

        return state;
    }

    public Task Set(BehaviorContext<TInstance> context, State<TInstance> state)
        => _stateAccessor.Set(context, state);
}

/*
 * Фактически индексирует состояния по полю из TInstance.
 */
public class IntStateAccessor<TInstance>(Expression<Func<TInstance, int>> expression, StateAccessorIndex<TInstance> index) : IStateAccessor<TInstance> where TInstance : class
{
    public State<TInstance> GetState(TInstance instance) => index[Read(instance)];

    public Task<State<TInstance>?> Get(BehaviorContext<TInstance> context)
    {
        var stateIndex = Read(context.Instance);
        return Task.FromResult<State<TInstance>?>(index[stateIndex]);
    }

    public Task Set(BehaviorContext<TInstance> context, State<TInstance> state)
    {
        var stateIndex = index[state];
        Write(context.Instance, stateIndex);
        return Task.CompletedTask;
    }

    private int Read(TInstance instance)
    {
        var propertyInfo = GetPropertyInfo();
        
        var value = (int)propertyInfo.GetValue(instance)!;
        return value;
    }

    private void Write(TInstance instance, int state)
    {
        var propertyInfo = GetPropertyInfo();
        
        propertyInfo.SetValue(instance, state);
    }

    private PropertyInfo GetPropertyInfo()
    {
        var propertyInfo = (PropertyInfo)((MemberExpression)expression.Body).Member;
        return propertyInfo;
    }
}

/*
 * Класс-обертка над состояниями, инкапсулирующая их в индексируемую структуру.
 */
public class StateAccessorIndex<TInstance>(State<TInstance> initial, State<TInstance> final, params State<TInstance>[] states) where TInstance : class
{
    private readonly State<TInstance>[] _assignedStates = [initial, final, ..states];

    public State<TInstance> this[int i] => _assignedStates[i];
    public int this[State<TInstance> state] => _assignedStates.IndexOf(state);
}

public interface IStateMachine
{
    Event? FindInitialEvent(Update update);
    Event? FindApplicableEvent(Update update, object instance);
    Task RaiseEvent(BehaviorContext context);
}

public interface IEventObserver<TInstance> where TInstance : class
{
    Task Execute(BehaviorContext<TInstance> context);
}

public class EventObservable<TInstance> : IEventObserver<TInstance> where TInstance : class
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

public class StateMachine<TInstance> : IStateMachine where TInstance : class
{
    private readonly List<Event> _events = [];
    private readonly EventObservable<TInstance> _observer = new();
    private readonly List<SubStateMachineDescriptor<TInstance>> _subStateMachines = [];
    private readonly Dictionary<object, SubStateMachineDescriptor<TInstance>> _subMachineDescriptors = [];

    public State<TInstance> Final { get; }
    public State<TInstance> Initial { get; }

    public IStateAccessor<TInstance> StateAccessor { get; private set; }

    protected StateMachine()
    {
        Final = new("Final", _observer);
        Initial = new("Initial", _observer);
        StateAccessor = new DefaultStateAccessor<TInstance>();
    }

    internal void RegisterSubStateMachine<TSubInstance>(
        StateMachine<TSubInstance> subStateMachine,
        Expression<Func<TInstance, TSubInstance?>> subInstanceAccessor)
        where TSubInstance : class
    {
        var descriptor = SubStateMachineDescriptor<TInstance>.Create(subStateMachine, subInstanceAccessor);
        _subStateMachines.Add(descriptor);
        _subMachineDescriptors[subStateMachine] = descriptor;
    }
    
    internal IStateMachineActivity<TInstance> CreateSubActivateActivity<TSubInstance>(
        StateMachine<TSubInstance> subMachine,
        State<TSubInstance> subState) where TSubInstance : class
    {
        var descriptor = (SubStateMachineDescriptorImpl<TInstance, TSubInstance>)_subMachineDescriptors[subMachine];
        return descriptor.CreateActivateActivity(subState);
    }
    
    // public void ScheduleEvent(Event @event)
    // {
    //     When(@event)
    //         .Then(RaiseEventInternal);
    //
    //     async Task RaiseEventInternal(BehaviorContext<TInstance> context)
    //     {
    //         var state = await StateAccessor.Get(context);
    //         if (state is null)
    //         {
    //             return;
    //         }
    //         
    //         context.Event = @event;
    //         await state.Raise(context);
    //     }
    // }

    public Event? FindInitialEvent(Update update) =>
        Initial.Events
            .Where(e => e != Initial.Enter && e != Initial.Leave)
            .FirstOrDefault(e => e.Condition?.Invoke(update) ?? false);

    public Event? FindApplicableEvent(Update update, object instance)
    {
        if (instance is not TInstance typedInstance)
            return null;

        var activeSub = _subStateMachines.FirstOrDefault(d => d.IsActive(typedInstance));
        if (activeSub is not null)
            return activeSub.FindApplicableEvent(update, typedInstance);

        var candidates = StateAccessor.GetState(typedInstance)?.Events ?? _events;
        return candidates.FirstOrDefault(e => e.Condition?.Invoke(update) ?? false);
    }

    public async Task RaiseEvent(BehaviorContext context)
    {
        if (context is BehaviorContext<TInstance> genericContext)
        {
            genericContext.StateMachine = this;

            foreach (var descriptor in _subStateMachines)
                descriptor.EnsureSubInstanceInitialized(genericContext.Instance);

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

    protected void ConfigureStateMachine<TStateMachine, TOtherInstance>(
        TStateMachine stateMachine,
        Expression<Func<TInstance, TOtherInstance?>> stateAccessor
    ) where TOtherInstance : class where TStateMachine : StateMachine<TOtherInstance>
    {
        RegisterSubStateMachine(stateMachine, stateAccessor);
    }

    protected SubEventActivityBinder<TInstance, TSubInstance> When<TSubInstance>(
        StateMachine<TSubInstance> subMachine,
        Event subEvent) where TSubInstance : class
    {
        var descriptor = _subMachineDescriptors[subMachine];
        return new SubEventActivityBinder<TInstance, TSubInstance>(this, descriptor, subEvent);
    }
    
    protected void SetCompletedOnFinal()
    {
        During(Final, When(Final.Enter).Then(context =>
        {
            context.IsCompleted = true;
            return Task.CompletedTask;
        }));
    }
    
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
    
    // нужно запомнить этот мув, Expression позволяет получить property getter & setter
    protected void ConfigureEvent<TEvent>(Expression<Func<TEvent>> propertyAccessor, Func<Update, bool> condition)
    {
        var propertyInfo = (PropertyInfo)((MemberExpression)propertyAccessor.Body).Member;
        Event @event = new(propertyInfo.Name, condition); 
        propertyInfo.SetValue(this, @event);
        _events.Add(@event);
    }

    protected void Initially(params IEventActivities<TInstance>[] eventActivities)
    {
        During(Initial, eventActivities);
    }
    
    protected void Finally(Func<EventActivityBinder<TInstance>, EventActivityBinder<TInstance>> action)
    {
        During(Final, action(When(Final.Enter)));
    }
    
    protected IEventActivities<TInstance> Ignore(Event @event)
    {
        return new EventActivityBinder<TInstance>(this, @event, new IgnoreActivityBinder<TInstance>(@event));
    }
    
    protected void During(State<TInstance> state, params IEventActivities<TInstance>[] eventActivities)
    {
        var stateActivities = eventActivities.SelectMany(x => x.GetStateActivityBinders());
        BindActivitiesToState(state, stateActivities);
    }
    
    protected EventActivityBinder<TInstance> When(Event @event)
    {
        return new EventActivityBinder<TInstance>(this, @event);
    }

    private static void BindActivitiesToState(State<TInstance> state, IEnumerable<IActivityBinder<TInstance>> activityBinders)
    {
        foreach (var binder in activityBinders)
        {
            binder.Bind(state);
        }
    }
}

/*
 * Контекст выполнения стейт-машины. Содержит стриггернутый ивент, само сообщение (в данном случае Update), а также TInstance, необходимый для переключения состояний.
 */

public class BehaviorContext(IServiceProvider serviceProvider, Event @event, Update update)
{
    public bool IsCompleted { get; set; }
    public Event Event { get; set; } = @event;
    public Update Update { get; set; } = update;
    public IServiceProvider ServiceProvider { get; } = serviceProvider;
    public BehaviorContext? ParentContext { get; set; }
}

public class BehaviorContext<TInstance>(IServiceProvider serviceProvider, TInstance instance, Event @event, Update update) : BehaviorContext(serviceProvider, @event, update) where TInstance : class
{
    public StateMachine<TInstance> StateMachine { get; set; } = null!;
    public TInstance Instance { get; } = instance;
}

/*
 * IActivityBinder привязывает к данному Event и State некую активность IStateMachineActivity
 * IActivityBinder в зависимости от реализации может по-разному выполнять привязку (условия, try-catch, etc), потому это интерфейс с __методом__ .Bind
*/
public interface IActivityBinder<TInstance> where TInstance : class
{
    void Bind(State<TInstance> state);
}

/*
 * Поставщик привязок активностей. Обобщенная форма всяких конфигураторов по типу EventActivityBuilder.
 * Какой бы не была реализация будь то билдер или еще что-нибудь - нас интересует только набор привязок.
 */
public interface IEventActivities<TInstance> where TInstance : class
{
    IEnumerable<IActivityBinder<TInstance>> GetStateActivityBinders();
}

/*
 * IBehavior - реализация CoR паттерна, аналогичному ASP.NET. IBehavior - просто некое действие, принимающее только контекст.
 */
public interface IBehavior<TInstance> where TInstance : class
{
    Task Execute(BehaviorContext<TInstance> context);
}

/*
 * IStateMachineActivity непосредственно управляет состоянием автомата
 * IStateMachineActivity принимает и контекст, и следующее поведение в методе .Execute - т.е. является middleware
 */
public interface IStateMachineActivity<TInstance> where TInstance : class
{
    Task Execute(BehaviorContext<TInstance> context, IBehavior<TInstance> next);
}

/*
 * EventActivityBinder - композитор активностей, объединяет в себе множество активностей IActivityBinder[]
 */
public class EventActivityBinder<TInstance>(StateMachine<TInstance> stateMachine, Event @event, params IActivityBinder<TInstance>[] activities) : IEventActivities<TInstance> where TInstance : class
{
    private readonly List<IActivityBinder<TInstance>> _binders = activities.ToList();
    
    public StateMachine<TInstance> StateMachine { get; } = stateMachine;
    
    public EventActivityBinder<TInstance> Add(IStateMachineActivity<TInstance> activity)
    {
        _binders.Add(new ExecuteActivityBinder<TInstance>(@event, activity));
        return this;
    }

    public IEnumerable<IActivityBinder<TInstance>> GetStateActivityBinders()
    {
        return _binders;
    }
}

/*
 * Просто игнорирует событие в данном состоянии
 */
public class IgnoreActivityBinder<TInstance>(Event @event) : IActivityBinder<TInstance> where TInstance : class
{
    public void Bind(State<TInstance> state)
    {
        state.Ignore(@event);
    }
}

/*
 * ExecuteActivityBinder - привязывает активность к состоянию State. ExecuteActivityBinder(ActionStateMachineActivity) - привязывает выполнение кода в момент состояния State,
 * а ExecuteActivityBinder(TransitionStateMachineActivity) - привязывает переход к другому состоянию в момент состояния State
 */
public class ExecuteActivityBinder<TInstance>(Event @event, IStateMachineActivity<TInstance> activity) : IActivityBinder<TInstance> where TInstance : class
{
    public Event Event { get; } = @event;
    
    public void Bind(State<TInstance> state)
    {
        state.Bind(Event, activity);
    }
}

/*
 * ActionStateMachineActivity - просто "выполняет код" по наступлению события Event
 */
public class ActionStateMachineActivity<TInstance>(Func<BehaviorContext<TInstance>, Task> action) : IStateMachineActivity<TInstance> where TInstance : class
{
    public async Task Execute(BehaviorContext<TInstance> context, IBehavior<TInstance> next)
    {
        await action(context);
        await next.Execute(context);
    }
}

/*
 * Выполняет непосредственно переход к следующему состоянию. Также используется в InitialIfNullStateAccessor
 */
public class TransitionStateMachineActivity<TInstance>(State<TInstance> toState, IStateAccessor<TInstance> stateAccessor) : IStateMachineActivity<TInstance> where TInstance : class
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
        // в названии этого метода упущено слово CurrentState, что подразумевает следующее: приватные методы обращены в некий скоуп, который в данном случае является непосредственно _toState
        await stateAccessor.Set(context, toState);
        context.Event = toState.Enter;
        await toState.Raise(context);
        
        await next.Execute(context);
    }
}

/*
 * Дескриптор вложенной стейт-машины, хранит информацию о вложенной стейт-машине
 */
public abstract class SubStateMachineDescriptor<TInstance> where TInstance : class
{
    public abstract Event? FindApplicableEvent(Update update, TInstance parentInstance);
    public abstract bool IsActive(TInstance parentInstance);
    public abstract Task<bool> TryRaiseEvent(BehaviorContext<TInstance> parentContext);
    public abstract void AddEpilogue(Event subEvent, IStateMachineActivity<TInstance> activity);
    public abstract void EnsureSubInstanceInitialized(TInstance parentInstance);

    public static SubStateMachineDescriptor<TInstance> Create<TSubInstance>(
        StateMachine<TSubInstance> subStateMachine,
        Expression<Func<TInstance, TSubInstance?>> subInstanceAccessor)
        where TSubInstance : class
    {
        return new SubStateMachineDescriptorImpl<TInstance, TSubInstance>(
            subStateMachine,
            subInstanceAccessor
        );
    }
}

public class SubStateMachineDescriptorImpl<TInstance, TSubInstance> : SubStateMachineDescriptor<TInstance>
    where TInstance : class
    where TSubInstance : class
{
    private readonly StateMachine<TSubInstance> _subStateMachine;
    private readonly Expression<Func<TInstance, TSubInstance?>> _subInstanceAccessorExpression;
    private readonly Func<TInstance, TSubInstance?> _subInstanceAccessor;
    private readonly List<(Event subEvent, IStateMachineActivity<TInstance> activity)> _epilogues = [];

    public SubStateMachineDescriptorImpl(
        StateMachine<TSubInstance> subStateMachine,
        Expression<Func<TInstance, TSubInstance?>> subInstanceAccessor)
    {
        _subStateMachine = subStateMachine;
        _subInstanceAccessorExpression = subInstanceAccessor;
        _subInstanceAccessor = subInstanceAccessor.Compile();
    }

    public override Event? FindApplicableEvent(Update update, TInstance parentInstance)
    {
        var subInstance = _subInstanceAccessor(parentInstance);
        return subInstance is not null ? _subStateMachine.FindApplicableEvent(update, subInstance) : null;
    }

    public override bool IsActive(TInstance parentInstance)
    {
        var subInstance = _subInstanceAccessor(parentInstance);
        if (subInstance is null)
            return false;
        var state = _subStateMachine.StateAccessor.GetState(subInstance);
        return state != _subStateMachine.Initial && state != _subStateMachine.Final;
    }

    public override void AddEpilogue(Event subEvent, IStateMachineActivity<TInstance> activity)
        => _epilogues.Add((subEvent, activity));

    public override void EnsureSubInstanceInitialized(TInstance parentInstance)
    {
        if (_subInstanceAccessor(parentInstance) is not null)
            return;
        var subInstance = Activator.CreateInstance<TSubInstance>();
        var propertyInfo = (PropertyInfo)((MemberExpression)_subInstanceAccessorExpression.Body).Member;
        propertyInfo.SetValue(parentInstance, subInstance);
    }

    public IStateMachineActivity<TInstance> CreateActivateActivity(State<TSubInstance> targetState)
        => new ActivateSubStateMachineActivity<TInstance, TSubInstance>(
            targetState,
            _subStateMachine.StateAccessor,
            _subInstanceAccessorExpression
        );

    public override async Task<bool> TryRaiseEvent(BehaviorContext<TInstance> parentContext)
    {
        var subInstance = _subInstanceAccessor(parentContext.Instance);
        if (subInstance is null)
            return false;
        var subContext = new BehaviorContext<TSubInstance>(
            parentContext.ServiceProvider,
            subInstance,
            parentContext.Event,
            parentContext.Update
        ) { ParentContext = parentContext };

        var state = await _subStateMachine.StateAccessor.Get(subContext);
        if (state is null || state == _subStateMachine.Initial || state == _subStateMachine.Final)
            return false;

        await _subStateMachine.RaiseEvent(subContext);

        // Эпилог: реакции родителя, синхронно в том же call stack
        var finalEnter = _subStateMachine.Final.Enter;
        var finalLeave = _subStateMachine.Final.Leave;
        foreach (var (subEvent, activity) in _epilogues)
        {
            var shouldFire = subEvent == finalEnter || subEvent == finalLeave
                ? subContext.IsCompleted
                : parentContext.Event == subEvent;

            if (shouldFire)
                await activity.Execute(parentContext, new EmptyBehavior<TInstance>());
        }

        return true;
    }
}

/*
 * SubEventActivityBinder - fluent builder для реакции родительской машины на события вложенной.
 * Активности регистрируются как эпилог в дескрипторе и выполняются синхронно после RaiseEvent вложенной.
 */
public class SubEventActivityBinder<TInstance, TSubInstance>
    where TInstance : class
    where TSubInstance : class
{
    private readonly StateMachine<TInstance> _parentMachine;
    private readonly SubStateMachineDescriptor<TInstance> _descriptor;
    private readonly Event _subEvent;

    internal SubEventActivityBinder(
        StateMachine<TInstance> parentMachine,
        SubStateMachineDescriptor<TInstance> descriptor,
        Event subEvent)
    {
        _parentMachine = parentMachine;
        _descriptor = descriptor;
        _subEvent = subEvent;
    }

    public SubEventActivityBinder<TInstance, TSubInstance> TransitionTo(State<TInstance> state)
        => Add(new TransitionStateMachineActivity<TInstance>(state, _parentMachine.StateAccessor));

    public SubEventActivityBinder<TInstance, TSubInstance> Then(Func<BehaviorContext<TInstance>, Task> action)
        => Add(new ActionStateMachineActivity<TInstance>(action));

    private SubEventActivityBinder<TInstance, TSubInstance> Add(IStateMachineActivity<TInstance> activity)
    {
        _descriptor.AddEpilogue(_subEvent, activity);
        return this;
    }
}

/*
 * Активность для перехода к произвольному состоянию вложенной стейт-машины.
 * Создаёт sub-instance если null, выставляет указанное состояние и поднимает его Enter.
 * TransitionTo(sub.Initial.Enter) и TransitionTo(sub, sub.SomeState) оба создают эту активность.
 */
public class ActivateSubStateMachineActivity<TInstance, TSubInstance> : IStateMachineActivity<TInstance>
    where TInstance : class
    where TSubInstance : class
{
    private readonly State<TSubInstance> _targetState;
    private readonly IStateAccessor<TSubInstance> _subStateAccessor;
    private readonly Expression<Func<TInstance, TSubInstance?>> _subInstanceAccessor;
    private readonly Func<TInstance, TSubInstance?> _subInstanceAccessorCompiled;

    public ActivateSubStateMachineActivity(
        State<TSubInstance> targetState,
        IStateAccessor<TSubInstance> subStateAccessor,
        Expression<Func<TInstance, TSubInstance?>> subInstanceAccessor
    )
    {
        _targetState = targetState;
        _subStateAccessor = subStateAccessor;
        _subInstanceAccessor = subInstanceAccessor;
        _subInstanceAccessorCompiled = subInstanceAccessor.Compile();
    }

    public async Task Execute(BehaviorContext<TInstance> context, IBehavior<TInstance> next)
    {
        var subInstance = _subInstanceAccessorCompiled(context.Instance);
        if (subInstance is null)
        {
            subInstance = Activator.CreateInstance<TSubInstance>();
            var propertyInfo = (PropertyInfo)((MemberExpression)_subInstanceAccessor.Body).Member;
            propertyInfo.SetValue(context.Instance, subInstance);
        }

        var subContext = new BehaviorContext<TSubInstance>(
            context.ServiceProvider,
            subInstance,
            _targetState.Enter,
            context.Update
        ) { ParentContext = context };

        await _subStateAccessor.Set(subContext, _targetState);
        await _targetState.Raise(subContext);

        await next.Execute(context);
    }
}


public static class EventActivityBinderExtensions
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

public static class BehaviorContextExtensions
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