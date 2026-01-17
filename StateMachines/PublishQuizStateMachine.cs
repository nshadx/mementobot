using mementobot.Services;
using mementobot.Telegram;

namespace mementobot.StateMachines;

public class PublishQuizState
{
    public List<(Quiz Quiz, int Page)> Quizzes { get; set; } = [];
    public int MessageId { get; set; }
    public int QuizId { get; set; }
    
    public int CurrentState { get; set; }
}

public class PublishQuizStateMachine : StateMachine<PublishQuizState>
{
    public Event PublishCommandReceivedEvent { get; private set; } = null!;
    public Event PageForwardEvent { get; private set; } = null!;
    public Event PageBackwardEvent { get; private set; } = null!;
    public Event QuizPickedEvent { get; private set; } = null!;

    public State<PublishQuizState> QuizPicking { get; private set; } = null!;
    
    public PublishQuizStateMachine()
    {
        ConfigureEvent(() => PublishCommandReceivedEvent, update => update.Message?.Text?.StartsWith("/publish") ?? false);
        ConfigureEvent(() => PageForwardEvent, update => update.CallbackQuery?.Data is "forward");
        ConfigureEvent(() => PageBackwardEvent, update => update.CallbackQuery?.Data is "backward");
        ConfigureEvent(() => QuizPickedEvent, update => int.TryParse(update.CallbackQuery?.Data, out _));
        
        ConfigureStates(state => state.CurrentState, () => QuizPicking);
        
        Initially(
            When(PublishCommandReceivedEvent)
                .Then(BuildSelectingPages)
                .TransitionTo(QuizPicking),
            Ignore(QuizPickedEvent),
            Ignore(PageForwardEvent),
            Ignore(PageBackwardEvent)
        );

        During(QuizPicking,
            When(QuizPickedEvent)
                .Then(PickQuiz)
                .TransitionTo(Final)
        );
        
        Finally(x => x.Then(PublishQuiz));
        
        SetFinishedWhenCompleted();
    }
    
    private async Task BuildSelectingPages(BehaviorContext<PublishQuizState> context)
    {
        var quizService = context.ServiceProvider.GetRequiredService<QuizService>();
        var userService = context.ServiceProvider.GetRequiredService<UserService>();
        var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();

        var chatId = context.Update.GetChatId();
        var userId = userService.GetOrCreateUser(
            telegramId: chatId
        );
        var quizzes = quizService.GetUserQuizzes(
            userId: userId
        );

        var list = context.Instance.Quizzes;
        var counter = 1;
        var page = 1;
        foreach (var quiz in quizzes)
        {
            if (counter <= 6)
            {
                list.Add((quiz, page));
                counter++;
            }

            page++;
        }

        var firstPageQuizzes = list
            .Where(x => x.Page == 1)
            .Select(x => x.Quiz)
            .ToArray();

        var messageId = await messageManager.SelectPollMessage(
            chatId: chatId,
            quizzes: firstPageQuizzes
        );
        context.Instance.MessageId = messageId;
    }

    private Task PickQuiz(BehaviorContext<PublishQuizState> context)
    {
        var chatId = context.Update.GetChatId();
        var quizId = int.Parse(context.Update.CallbackQuery?.Data!);
        context.Instance.QuizId = quizId;

        return Task.CompletedTask;
    }

    private async Task PublishQuiz(BehaviorContext<PublishQuizState> context)
    {
        var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
        var quizService = context.ServiceProvider.GetRequiredService<QuizService>();
        
        var chatId = context.Update.GetChatId();
        var quizId = context.Instance.QuizId;
        quizService.PublishQuiz(
            quizId: quizId
        );

        var messageId = await messageManager.SendQuizPublishedMessage(
            chatId: chatId
        );
        context.Instance.MessageId = messageId;
    }
}