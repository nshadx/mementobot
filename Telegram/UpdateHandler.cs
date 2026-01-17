using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace mementobot.Telegram
{
    public class UpdateHandler(
        IServiceProvider serviceProvider,
        ILogger<UpdateHandler> logger,
        StateMachineRepository stateMachineRepository,
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
            var (stateMachine, instance) = stateMachineRepository.GetCurrentStateMachine(chatId);
            if (stateMachine is not null)
            {
                if (stateMachine.IsFinished())
                {
                    stateMachineRepository.Remove(chatId);
                }
                else
                {
                    var events = stateMachine.Events;
                    foreach (var @event in events)
                    {
                        if (@event.Condition(update))
                        {
                            BehaviorContext<object> behaviorContext = new(serviceProvider, instance, @event, update);
                            await stateMachine.RaiseEvent(behaviorContext);
                        }
                    }
                }
                
            }
            
            Context context = new(update);
            await pipeline(context);
        }
    }
}
