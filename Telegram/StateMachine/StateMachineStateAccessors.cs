using System.Linq.Expressions;
using System.Reflection;

namespace mementobot.Telegram.StateMachine;

/// <summary>
/// Абстракция для чтения и записи текущего состояния стейт-машины в инстансе <typeparamref name="TInstance"/>.
/// Сделан интерфейсом, чтобы реализации могли быть декораторами друг над другом
/// (например, <see cref="InitialIfNullStateAccessor{TInstance}"/> над <see cref="IntStateAccessor{TInstance}"/>).
/// </summary>
internal interface IStateAccessor<TInstance> where TInstance : class
{
    /// <summary>Синхронно читает состояние напрямую из инстанса, без контекста. Используется в <see cref="IStateMachine.FindEvent"/>.</summary>
    State<TInstance>? GetState(TInstance instance);

    /// <summary>Асинхронно читает текущее состояние. Может иметь сайд-эффекты (например, первичная инициализация).</summary>
    Task<State<TInstance>?> Get(BehaviorContext<TInstance> context);

    /// <summary>Записывает новое состояние в инстанс.</summary>
    Task Set(BehaviorContext<TInstance> context, State<TInstance> state);
}

/// <summary>
/// Двунаправленный индекс состояний: преобразует <see cref="State{TInstance}"/> ↔ <c>int</c>.
/// Порядок фиксирован: Initial=0, Final=1, затем пользовательские состояния по порядку объявления.
/// </summary>
internal class StateAccessorIndex<TInstance>(State<TInstance> initial, State<TInstance> final, params State<TInstance>[] states) where TInstance : class
{
    private readonly State<TInstance>[] _assignedStates = [initial, final, ..states];

    public State<TInstance> this[int i] => _assignedStates[i];
    public int this[State<TInstance> state] => _assignedStates.IndexOf(state);
}

/// <summary>
/// Заглушка-реализация <see cref="IStateAccessor{TInstance}"/>, всегда возвращающая <c>null</c>.
/// Используется как значение по умолчанию до вызова <c>ConfigureStates</c> в конструкторе машины.
/// </summary>
internal class DefaultStateAccessor<TInstance> : IStateAccessor<TInstance> where TInstance : class
{
    public State<TInstance>? GetState(TInstance instance) => null;
    public Task<State<TInstance>?> Get(BehaviorContext<TInstance> context) => Task.FromResult<State<TInstance>?>(null);
    public Task Set(BehaviorContext<TInstance> context, State<TInstance> state) => Task.CompletedTask;
}

/// <summary>
/// Декоратор над <see cref="IStateAccessor{TInstance}"/>, автоматически устанавливающий начальное состояние
/// при первом обращении к <see cref="Get"/>, если внутренний accessor вернул <c>null</c>.
/// Это происходит ровно один раз — при старте новой сессии стейт-машины.
/// </summary>
internal class InitialIfNullStateAccessor<TInstance> : IStateAccessor<TInstance> where TInstance : class
{
    private readonly IStateAccessor<TInstance> _stateAccessor;
    private readonly IBehavior<TInstance> _initialBehavior;

    public InitialIfNullStateAccessor(State<TInstance> initialState, IStateAccessor<TInstance> stateAccessor)
    {
        var activity = new TransitionStateMachineActivity<TInstance>(initialState, stateAccessor);
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

/// <summary>
/// Реализация <see cref="IStateAccessor{TInstance}"/>, хранящая текущее состояние как <c>int</c>
/// в конкретном свойстве <typeparamref name="TInstance"/>, заданном через Expression.
/// Преобразование State ↔ int происходит через <see cref="StateAccessorIndex{TInstance}"/>.
/// </summary>
internal class IntStateAccessor<TInstance>(Expression<Func<TInstance, int>> expression, StateAccessorIndex<TInstance> index) : IStateAccessor<TInstance> where TInstance : class
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
        return (int)propertyInfo.GetValue(instance)!;
    }

    private void Write(TInstance instance, int state)
    {
        var propertyInfo = GetPropertyInfo();
        propertyInfo.SetValue(instance, state);
    }

    private PropertyInfo GetPropertyInfo() =>
        (PropertyInfo)((MemberExpression)expression.Body).Member;
}
