using Microsoft.Extensions.Caching.Memory;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace mementobot.Telegram;

public class UpdateHandler(
    IServiceProvider serviceProvider,
    ILogger<UpdateHandler> logger,
    IMemoryCache memoryCache,
    IEnumerable<IStateMachine> stateMachines,
    UpdateDelegate pipeline
) : IUpdateHandler
{
    public Task HandleErrorAsync(
        ITelegramBotClient botClient,
        Exception exception,
        HandleErrorSource source,
        CancellationToken cancellationToken
    ) => HandleErrorInternal(exception);

    public Task HandleUpdateAsync(
        ITelegramBotClient botClient,
        Update update,
        CancellationToken cancellationToken
    ) => HandleUpdateInternal(update);

    private Task HandleErrorInternal(Exception exception)
    {
        logger.LogError(exception, "An error occured");
        return Task.CompletedTask;
    }

    private async Task HandleUpdateInternal(Update update)
    {
        logger.LogInformation("Received: {updateType}", update.Type);

        var chatId = update.GetChatId();
        var cacheKey = $"{chatId}-instance";

        var instance = memoryCache.Get<object>(cacheKey);

        // 1. Existing instance
        if (instance is not null)
        {
            if (await TryHandleExistingInstance(update, cacheKey, instance))
                return;
        }

        // 2. Initial event → new instance
        if (await TryHandleInitialEvent(update, cacheKey))
            return;

        // 3. Fallback
        await pipeline(new Context(update));
    }

    private async Task<bool> TryHandleInitialEvent(Update update, string cacheKey)
    {
        foreach (var sm in stateMachines)
        {
            var @event = sm.FindInitialEvent(update);
            if (@event is null)
                continue;

            var instanceType = GetInstanceType(sm);
            if (instanceType is null)
                continue;

            var instance = Activator.CreateInstance(instanceType)!;
            memoryCache.Set(cacheKey, instance);
            await HandleEvent(sm, instance, @event, update, cacheKey);
            return true;
        }
        return false;
    }

    private async Task<bool> TryHandleExistingInstance(Update update, string cacheKey, object instance)
    {
        var sm = stateMachines.FirstOrDefault(sm => GetInstanceType(sm) == instance.GetType());
        if (sm is null)
            return false;

        var @event = sm.FindApplicableEvent(update, instance);
        if (@event is null)
            return false;

        await HandleEvent(sm, instance, @event, update, cacheKey);
        return true;
    }

    private async Task HandleEvent(
        IStateMachine stateMachine,
        object instance,
        Event @event,
        Update update,
        string cacheKey
    )
    {
        var context = CreateContext(stateMachine, instance, @event, update);

        await stateMachine.RaiseEvent(context);

        if (context.IsCompleted)
            memoryCache.Remove(cacheKey);
    }

    private static Type? GetInstanceType(IStateMachine stateMachine)
    {
        var baseType = stateMachine.GetType().BaseType;
        if (baseType is null || !baseType.IsGenericType)
            return null;
        return baseType.GetGenericTypeDefinition() == typeof(StateMachine<>)
            ? baseType.GetGenericArguments()[0]
            : null;
    }

    private BehaviorContext CreateContext(
        IStateMachine stateMachine,
        object instance,
        Event @event,
        Update update
    )
    {
        var contextType = typeof(BehaviorContext<>)
            .MakeGenericType(instance.GetType());

        return (BehaviorContext)Activator.CreateInstance(
            contextType,
            serviceProvider,
            instance,
            @event,
            update
        )!;
    }
}
