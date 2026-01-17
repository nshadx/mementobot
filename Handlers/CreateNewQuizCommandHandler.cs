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
        if (context.Update.Message is not { Text: string text, Id: int messageId })
        {
            return;
        }
        
        await client.DeleteMessage(
            chatId: context.Update.GetChatId(),
            messageId: messageId
        );

        if (!CommandArgumentParser.TryGetArgument<string>("/new", text, 0, out var value))
        {
            return;
        }

        var chatId = context.Update.GetChatId();
        var userId = userService.GetOrCreateUser(chatId);
        quizService.CreateNew(
            userId: userId,
            name: value
        );

        await messageManager.CreateNewQuizMessage(
            chatId: context.Update.GetChatId()
        );
    }
}
