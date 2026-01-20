using Microsoft.Extensions.Caching.Memory;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace mementobot.Telegram
{
    public class UpdateHandler(
        IServiceProvider serviceProvider,
        ILogger<UpdateHandler> logger,
        IMemoryCache memoryCache,
        IEnumerable<IStateMachine> stateMachines,
        UpdateDelegate pipeline
    ) : IUpdateHandler
    {
        public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken) => HandleErrorInternal(exception);

        public Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken) => HandleUpdateInternal(update);

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

            var instance = memoryCache.Get(cacheKey);

            // 1. Initial event
            var newInstance = await TryHandleInitialEvent(update, cacheKey);
            if (newInstance is not null)
                return;

            // 2. Existing state machine
            if (instance is not null && await TryHandleExistingInstance(update, cacheKey, instance))
                return;

            // 3. Fallback
            await pipeline(new Context(update));
        }

        private async Task<object?> TryHandleInitialEvent(
            Update update,
            string cacheKey
        )
        {
            var stateMachine = stateMachines
                .SingleOrDefault(sm => sm.InitialEvents.Any(e => e.Condition(update)));

            if (stateMachine is null)
                return null;

            var @event = stateMachine.InitialEvents.Single(e => e.Condition(update));

            var instanceType = GetInstanceType(stateMachine);
            if (instanceType is null)
                return null;

            var instance = Activator.CreateInstance(instanceType)!;
            memoryCache.Set(cacheKey, instance);

            await HandleEvent(stateMachine, instance, @event, update, cacheKey);

            return instance;
        }

        private async Task<bool> TryHandleExistingInstance(
            Update update,
            string cacheKey,
            object instance
        )
        {
            var stateMachine = stateMachines.SingleOrDefault(sm =>
                GetInstanceType(sm) == instance.GetType() &&
                sm.Events.Any(e => e.Condition(update))
            );

            if (stateMachine is null)
                return false;

            var @event = stateMachine.Events.Single(e => e.Condition(update));

            memoryCache.Set(cacheKey, instance);

            await HandleEvent(stateMachine, instance, @event, update, cacheKey);

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
        ) => (BehaviorContext)Activator.CreateInstance(typeof(BehaviorContext<>).MakeGenericType(instance.GetType()), serviceProvider, instance, @event, update)!;
    }
}
