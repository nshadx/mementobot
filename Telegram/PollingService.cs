using Telegram.Bot;
using Telegram.Bot.Polling;

namespace mementobot.Telegram
{
    internal class PollingService(ITelegramBotClient client, IServiceProvider serviceProvider) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await using (var scope = serviceProvider.CreateAsyncScope())
                {
                    var updateHandler = scope.ServiceProvider.GetRequiredService<IUpdateHandler>();
                
                    await client.ReceiveAsync(updateHandler, receiverOptions: new ReceiverOptions(){ DropPendingUpdates = true }, cancellationToken: stoppingToken);
                }
                
                await Task.Delay(100, stoppingToken);
            }
        }
    }
}
