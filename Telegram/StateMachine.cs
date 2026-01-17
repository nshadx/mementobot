using System.Linq.Expressions;
using System.Reflection;
using Telegram.Bot.Types;

namespace mementobot.Telegram;

/*
 * Событие стейт-машины. Является ее синглтоном, просто задает некий триггер.
 */
public class Event(string name, Func<Update, bool> condition)
{
    public string Name { get; } = name;
    public Func<Update, bool> Condition { get; } = condition;
}

/*
 * Основа стейт-машины. К состоянию привязываются события и поведения. Переход стейт-машины к следующему состоянию происходит через state.Raise
 */
public class State<TInstance> where TInstance : class
{
    private readonly Dictionary<Event, ActivityBehaviorBuilder<TInstance>> _behaviors = [];
    private readonly HashSet<Event> _ignoredEvents = [];
    
    public Event Enter { get; }
    public Event Leave { get; }

    public State()
    {
        Enter = new("Enter", _ => true);
        Leave = new("Leave", _ => true);
    }
    
    public void Bind(Event @event, IStateMachineActivity<TInstance> activity)
    {
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
        for (var i = _activities.Count - 1; i >= 0; i--)
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
    public Task<State<TInstance>?> Get(BehaviorContext<TInstance> context)
    {
        return Task.FromResult<State<TInstance>?>(null);
    }

    public Task Set(BehaviorContext<TInstance> context, State<TInstance> state)
    {
        return Task.CompletedTask;
    }
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
    {
        return _stateAccessor.Set(context, state);
    }
}

/*
 * Фактически индексирует состояния по полю из TInstance.
 */
public class IntStateAccessor<TInstance>(Expression<Func<TInstance, int>> expression, StateAccessorIndex<TInstance> index) : IStateAccessor<TInstance> where TInstance : class
{
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
    IReadOnlyCollection<Event> Events { get; }
    Task RaiseEvent(BehaviorContext<object> context);
    bool IsFinished();
}

public class StateMachine<TInstance> : IStateMachine where TInstance : class
{
    private readonly List<Event> _events = [];
    private bool _isFinished;
    
    protected State<TInstance> Final { get; }
    protected State<TInstance> Initial { get; }

    public IStateAccessor<TInstance> StateAccessor { get; private set; }

    protected StateMachine()
    {
        Final = new();
        Initial = new();
        StateAccessor = new DefaultStateAccessor<TInstance>();
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

    public async Task RaiseEvent(BehaviorContext<object> context)
    {
        var objInstance = context.Instance;
        if (objInstance.GetType() == typeof(TInstance))
        {
            var instance = (TInstance)objInstance;
            BehaviorContext<TInstance> newContext = new(context.ServiceProvider, instance, context.Event, context.Update);

            var state = await StateAccessor.Get(newContext);
            if (state is null)
            {
                return;
            }
        
            await state.Raise(newContext);
        }
    }

    public IReadOnlyCollection<Event> Events => _events;
    
    public bool IsFinished() => _isFinished;

    protected void SetFinishedWhenCompleted()
    {
        During(Final, When(Final.Leave).Then(_ =>
        {
            _isFinished = true;
            return Task.CompletedTask;
        }));
    }
    
    protected void ConfigureStates(Expression<Func<TInstance, int>> propertyAccessor, params Expression<Func<State<TInstance>>>[] stateAccessors)
    {
        List<State<TInstance>> stateList = new(stateAccessors.Length);
        foreach (var expression in stateAccessors)
        {
            var propertyInfo = (PropertyInfo)((MemberExpression)expression.Body).Member;
            State<TInstance> state = new(); 
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
        var binder = When(Final.Enter);
        binder = action(binder);
        During(Final, binder);
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

    private void BindActivitiesToState(State<TInstance> state, IEnumerable<IActivityBinder<TInstance>> activityBinders)
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
    public Event Event { get; set; } = @event;
    public Update Update { get; set; } = update;
    public IServiceProvider ServiceProvider { get; } = serviceProvider;
}

public class BehaviorContext<TInstance>(IServiceProvider serviceProvider, TInstance instance, Event @event, Update update) : BehaviorContext(serviceProvider, @event, update) where TInstance : class
{
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
 * Выполняет непосредственно переход к следующему состоянию. Используется в InitialIfNullStateAccessor
 */
public class TransitionStateMachineActivity<TInstance>(State<TInstance> toState, IStateAccessor<TInstance> stateAccessor) : IStateMachineActivity<TInstance> where TInstance : class
{
    public async Task Execute(BehaviorContext<TInstance> context, IBehavior<TInstance> next)
    {
        var currentState = await stateAccessor.Get(context);
        if (currentState == toState || currentState is null)
        {
            return;
        }

        context.Event = currentState.Leave;
        await currentState.Raise(context);
        // в названии этого метода упущено слова CurrentState, что подразумевает следующее: приватные методы обращены в некий скоуп, который в данном случае является непосредственно _toState
        await stateAccessor.Set(context, toState);
        context.Event = toState.Enter;
        await toState.Raise(context);
        
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
    }
}