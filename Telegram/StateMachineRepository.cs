using Microsoft.Extensions.Caching.Memory;

namespace mementobot.Telegram;

public class StateMachineRepository(IMemoryCache cache)
{
    public (IStateMachine? StateMachine, object Instance) GetCurrentStateMachine(object key)
    {
        if (cache.TryGetValue<IStateMachine>($"{key}-stateMachine", out var stateMachine) && cache.TryGetValue($"{key}-instance", out var instance) && instance is not null)
        {
            return (stateMachine, instance);
        }
        
        return default;
    }

    public void SetCurrentStateMachine(object key, IStateMachine stateMachine, object state)
    {
        cache.Set($"{key}-stateMachine", stateMachine);
        cache.Set($"{key}-instance", state);
    }

    public void Remove(object key)
    {
        cache.Remove($"{key}-stateMachine");
        cache.Remove($"{key}-instance");
    }
}