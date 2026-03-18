using System.Linq.Expressions;
using System.Reflection;
using Telegram.Bot.Types;

namespace mementobot.Telegram.StateMachine;

/// <summary>
/// Дескриптор вложенной стейт-машины с точки зрения родительской.
/// Инкапсулирует доступ к sub-instance, делегирование событий и выполнение «эпилогов» —
/// реакций родителя на события вложенной машины.
/// </summary>
internal interface ISubStateMachineDescriptor<TInstance> where TInstance : class
{
    /// <summary>Ищет событие, применимое к вложенной машине для данного апдейта и родительского инстанса.</summary>
    Event? FindApplicableEvent(Update update, TInstance parentInstance);

    /// <summary>Возвращает <c>true</c>, если вложенная машина сейчас активна (не в Initial и не в Final).</summary>
    bool IsActive(TInstance parentInstance);

    /// <summary>Пытается поднять событие во вложенной машине. Возвращает <c>true</c>, если событие было обработано ею.</summary>
    Task<bool> TryRaiseEvent(BehaviorContext<TInstance> parentContext);

    /// <summary>Регистрирует «эпилог» — активность родителя, выполняемую синхронно после обработки события вложенной машиной.</summary>
    void AddEpilogue(Event subEvent, IStateMachineActivity<TInstance> activity);

    /// <summary>Создаёт sub-instance через <c>Activator.CreateInstance</c>, если он ещё не существует.</summary>
    void EnsureSubInstanceInitialized(TInstance parentInstance);
}

/// <summary>
/// Конкретная реализация <see cref="ISubStateMachineDescriptor{TInstance}"/>.
/// Обеспечивает полный цикл работы с вложенной машиной: инициализацию sub-instance через рефлексию,
/// делегирование событий, и исполнение эпилогов родителя при завершении вложенной машины.
/// </summary>
internal class SubStateMachineDescriptor<TInstance, TSubInstance> : ISubStateMachineDescriptor<TInstance>
    where TInstance : class
    where TSubInstance : class
{
    private readonly StateMachine<TSubInstance> _subStateMachine;
    private readonly PropertyInfo _subInstanceProperty;
    private readonly List<(Event subEvent, IStateMachineActivity<TInstance> activity)> _epilogues = [];

    public SubStateMachineDescriptor(
        StateMachine<TSubInstance> subStateMachine,
        Expression<Func<TInstance, TSubInstance?>> subInstanceAccessor)
    {
        _subStateMachine = subStateMachine;
        _subInstanceProperty = (PropertyInfo)((MemberExpression)subInstanceAccessor.Body).Member;
    }

    private TSubInstance? GetSubInstance(TInstance parent) =>
        (TSubInstance?)_subInstanceProperty.GetValue(parent);

    private void SetSubInstance(TInstance parent, TSubInstance value) =>
        _subInstanceProperty.SetValue(parent, value);

    public Event? FindApplicableEvent(Update update, TInstance parentInstance)
    {
        var subInstance = GetSubInstance(parentInstance);
        return subInstance is not null ? _subStateMachine.FindEvent(update, subInstance) : null;
    }

    public bool IsActive(TInstance parentInstance)
    {
        var subInstance = GetSubInstance(parentInstance);
        if (subInstance is null)
        {
            return false;
        }

        var state = _subStateMachine.StateAccessor.GetState(subInstance);
        return state != _subStateMachine.Initial && state != _subStateMachine.Final;
    }

    public void AddEpilogue(Event subEvent, IStateMachineActivity<TInstance> activity) =>
        _epilogues.Add((subEvent, activity));

    public void EnsureSubInstanceInitialized(TInstance parentInstance)
    {
        if (GetSubInstance(parentInstance) is not null)
        {
            return;
        }

        SetSubInstance(parentInstance, Activator.CreateInstance<TSubInstance>());
    }

    public IStateMachineActivity<TInstance> CreateActivateActivity(State<TSubInstance> targetState) =>
        new ActivateSubStateMachineActivity<TInstance, TSubInstance>(
            _subStateMachine,
            targetState,
            _subStateMachine.StateAccessor,
            _subInstanceProperty
        );

    public async Task<bool> TryRaiseEvent(BehaviorContext<TInstance> parentContext)
    {
        var subInstance = GetSubInstance(parentContext.Instance);
        if (subInstance is null)
        {
            return false;
        }

        var subContext = new BehaviorContext<TSubInstance>(
            parentContext.ServiceProvider,
            _subStateMachine,
            subInstance,
            parentContext.Event,
            parentContext.Update
        ) { ParentContext = parentContext };

        var state = await _subStateMachine.StateAccessor.Get(subContext);
        if (state is null || state == _subStateMachine.Initial || state == _subStateMachine.Final)
        {
            return false;
        }

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
            {
                await activity.Execute(parentContext, new EmptyBehavior<TInstance>());
            }
        }

        return true;
    }
}

/// <summary>
/// Fluent-builder для реакции родительской машины на события вложенной.
/// Регистрирует активности как «эпилоги» в <see cref="ISubStateMachineDescriptor{TInstance}"/>;
/// они выполняются синхронно в том же call stack сразу после обработки события вложенной машиной.
/// Используется через <c>When(subMachine, subMachine.SomeEvent).Then(...).TransitionTo(...)</c>.
/// </summary>
internal class SubEventActivityBinder<TInstance, TSubInstance>
    where TInstance : class
    where TSubInstance : class
{
    private readonly StateMachine<TInstance> _parentMachine;
    private readonly ISubStateMachineDescriptor<TInstance> _descriptor;
    private readonly Event _subEvent;

    internal SubEventActivityBinder(
        StateMachine<TInstance> parentMachine,
        ISubStateMachineDescriptor<TInstance> descriptor,
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

/// <summary>
/// Активность, переводящая вложенную стейт-машину в заданное состояние.
/// Если sub-instance ещё не создан — создаёт его. Затем устанавливает целевое состояние и поднимает его <c>Enter</c>.
/// Используется, когда родительская машина хочет запустить вложенную с конкретного состояния.
/// </summary>
internal class ActivateSubStateMachineActivity<TInstance, TSubInstance> : IStateMachineActivity<TInstance>
    where TInstance : class
    where TSubInstance : class
{
    private readonly StateMachine<TSubInstance> _subStateMachine;
    private readonly State<TSubInstance> _targetState;
    private readonly IStateAccessor<TSubInstance> _subStateAccessor;
    private readonly PropertyInfo _subInstanceProperty;

    public ActivateSubStateMachineActivity(
        StateMachine<TSubInstance> subStateMachine,
        State<TSubInstance> targetState,
        IStateAccessor<TSubInstance> subStateAccessor,
        PropertyInfo subInstanceProperty)
    {
        _subStateMachine = subStateMachine;
        _targetState = targetState;
        _subStateAccessor = subStateAccessor;
        _subInstanceProperty = subInstanceProperty;
    }

    public async Task Execute(BehaviorContext<TInstance> context, IBehavior<TInstance> next)
    {
        var subInstance = (TSubInstance?)_subInstanceProperty.GetValue(context.Instance);
        if (subInstance is null)
        {
            subInstance = Activator.CreateInstance<TSubInstance>();
            _subInstanceProperty.SetValue(context.Instance, subInstance);
        }

        var subContext = new BehaviorContext<TSubInstance>(
            context.ServiceProvider,
            _subStateMachine,
            subInstance,
            _targetState.Enter,
            context.Update
        ) { ParentContext = context };

        await _subStateAccessor.Set(subContext, _targetState);
        await _targetState.Raise(subContext);

        await next.Execute(context);
    }
}
