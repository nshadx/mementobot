using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using mementobot.Telegram.Messages;

namespace mementobot.Telegram;

internal class UpdateHandler(
    UpdateDelegate pipeline,
    IMessageStore messageStore,
    IContextAccessor contextAccessor,
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

        if (update.Message is { } msg)
            messageStore.TrackMessageId(msg.Chat.Id, msg.Id);

        var context = new Context(update);
        contextAccessor.Current = context;
        return pipeline(context);
    }
}
