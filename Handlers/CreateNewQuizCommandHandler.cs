using mementobot.Services;
using mementobot.Services.Messages;
using mementobot.Telegram;

namespace mementobot.Handlers;

internal class CreateNewQuizCommandHandler(
    UserService userService,
    QuizService quizService,
    NewQuizMessage newQuizMessage
) : IRouteHandler
{
    public async Task Handle(Context context)
    {
        if (context.Update.Message is not { Text: { } text })
            return;

        if (!CommandArgumentParser.TryGetArgument<string>("/new", text, 0, out var value))
            return;

        var chatId = context.Update.GetChatId();
        var userId = userService.GetOrCreateUser(chatId);

        quizService.CreateNew(userId: userId, name: value);

        await newQuizMessage.Apply(chatId);
    }
}
