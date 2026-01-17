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
            var instance = memoryCache.Get($"{chatId}-instance");
            if (instance is null)
            {
                var stateMachine = stateMachines.SingleOrDefault(x => x.InitialEvents.Any(x => x.Condition(update)));
                if (stateMachine is not null)
                {
                    var @event = stateMachine.InitialEvents.Single(x => x.Condition(update));
                    var stateMachineType = stateMachine.GetType();
                    if (stateMachineType.BaseType!.IsGenericType && stateMachineType.BaseType.GetGenericTypeDefinition() == typeof(StateMachine<>))
                    {
                        var instanceType = stateMachineType.BaseType.GetGenericArguments()[0];
                        instance = Activator.CreateInstance(instanceType);
                    }

                    memoryCache.Set($"{chatId}-instance", instance);

                    var context = (BehaviorContext)Activator.CreateInstance(typeof(BehaviorContext<>).MakeGenericType(instance!.GetType()), serviceProvider, instance, @event, update)!;
                    await stateMachine.RaiseEvent(context);
                    if (context.IsCompleted)
                    {
                        memoryCache.Remove($"{chatId}-instance");
                    }
                }
                else
                {
                    Context context = new(update);
                    await pipeline(context);
                }
            }
            else
            {
                var stateMachine = stateMachines.SingleOrDefault(x => x.GetType().BaseType!.IsGenericType && x.GetType().BaseType!.GetGenericArguments()[0] == instance.GetType() && x.Events.Any(x => x.Condition(update)));
                if (stateMachine is not null)
                {
                    var @event = stateMachine.Events.Single(x => x.Condition(update));
                    var context = (BehaviorContext)Activator.CreateInstance(typeof(BehaviorContext<>).MakeGenericType(instance.GetType()), serviceProvider, instance, @event, update)!;
                    await stateMachine.RaiseEvent(context);
                    if (context.IsCompleted)
                    {
                        memoryCache.Remove($"{chatId}-instance");
                    }
                }
                else
                {
                    Context context = new(update);
                    await pipeline(context);   
                }
            }
        }
    }
}
