using mementobot.Services;
using mementobot.Telegram;
using Telegram.Bot;

namespace mementobot.Handlers;

internal class CreateNewQuizCommandHandler(
    UserService userService,
    QuizService quizService,
    MessageManager messageManager,
    ITelegramBotClient client
) : IRouteHandler
{
    public async Task Handle(Context context)
    {
        if (context.Update.Message is not { Text: { } text, Id: var messageId })
        {
            return;
        }
        
        var chatId = context.Update.GetChatId();
        
        await client.DeleteMessage(
            chatId: chatId,
            messageId: messageId
        );

        if (!CommandArgumentParser.TryGetArgument<string>("/new", text, 0, out var value))
        {
            return;
        }

        var userId = userService.GetOrCreateUser(chatId);
        quizService.CreateNew(
            userId: userId,
            name: value
        );

        await messageManager.CreateNewQuizMessage(
            chatId: chatId
        );
    }
}
