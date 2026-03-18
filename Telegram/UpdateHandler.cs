using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace mementobot.Telegram;

internal class UpdateHandler(
    UpdateDelegate pipeline,
    ILogger<UpdateHandler> logger
) : IUpdateHandler
{
    public Task HandleErrorAsync(
        ITelegramBotClient botClient,
        Exception exception,
        HandleErrorSource source,
        CancellationToken cancellationToken
    )
    {
        logger.LogError(exception, "An error occured");
        return Task.CompletedTask;
    }

    public Task HandleUpdateAsync(
        ITelegramBotClient botClient,
        Update update,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("Received: {updateType}", update.Type);
        return pipeline(new Context(update));
    }
}
