using FuzzySharp;
using mementobot.Services;
using mementobot.Telegram;

namespace mementobot.StateMachines;

public record UserQuizQuestion(QuizQuestion QuizQuestion, int Order)
{
    public int Order { get; set; } = Order;
}

public class QuizProgressState
{
    public List<UserQuizQuestion> UserQuizQuestions { get; set; } = [];
    public UserQuizQuestion? CurrentQuestion { get; set; }
 
    public List<(Quiz Quiz, int Page)> Quizzes { get; set; } = [];
    
    public int MessageId { get; set; }
    public int CurrentState { get; set; }
}

public class QuizProgressStateMachine : StateMachine<QuizProgressState>
{
    public Event OnSkipCallbackEvent { get; private set; } = null!;
    public Event StartCommandReceivedEvent { get; private set; } = null!;
    public Event MessageReceivedEvent { get; private set; } = null!;
    
    public Event PageForwardEvent { get; private set; } = null!;
    public Event PageBackwardEvent { get; private set; } = null!;
    public Event QuizPickedEvent { get; private set; } = null!;

    public State<QuizProgressState> QuizPicking { get; private set; } = null!;
    public State<QuizProgressState> QuizQuestion { get; private set; } = null!;
    
    public QuizProgressStateMachine()
    {
        ConfigureEvent(() => OnSkipCallbackEvent, update => update.CallbackQuery?.Data is "skip");
        ConfigureEvent(() => StartCommandReceivedEvent, update => update.Message?.Text?.StartsWith("/start") ?? false);
        ConfigureEvent(() => MessageReceivedEvent, update => update.Message?.Text is not null);
        
        ConfigureEvent(() => PageForwardEvent, update => update.CallbackQuery?.Data is "forward");
        ConfigureEvent(() => PageBackwardEvent, update => update.CallbackQuery?.Data is "backward");
        ConfigureEvent(() => QuizPickedEvent, update => int.TryParse(update.CallbackQuery?.Data, out _));

        ConfigureStates(state => state.CurrentState, () => QuizPicking, () => QuizQuestion);
        
        Initially(
            When(StartCommandReceivedEvent)
                .Then(BuildSelectingPages)
                .TransitionTo(QuizPicking),
            Ignore(QuizPickedEvent),
            Ignore(PageForwardEvent),
            Ignore(PageBackwardEvent),
            Ignore(MessageReceivedEvent)
        );

        During(QuizPicking,
            When(QuizPickedEvent)
                .Then(PickQuiz)
                .TransitionTo(QuizQuestion)
        );
        
        During(QuizQuestion,
            When(QuizQuestion.Enter)
                .Then(SendQuestion),
            When(MessageReceivedEvent)
                .Then(AnswerQuestion)
                .TransitionTo(QuizQuestion),
            When(OnSkipCallbackEvent)
                .Then(SkipQuestion)
                .TransitionTo(QuizQuestion)
        );
        
        Finally(x => x.Then(CompleteQuiz));
        
        SetFinishedWhenCompleted();
    }
    
    private async Task BuildSelectingPages(BehaviorContext<QuizProgressState> context)
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

    private Task PickQuiz(BehaviorContext<QuizProgressState> context)
    {
        var quizService = context.ServiceProvider.GetRequiredService<QuizService>();
        
        var chatId = context.Update.GetChatId();
        var quizId = int.Parse(context.Update.CallbackQuery?.Data!);
        var quizQuestions = quizService.GetQuizQuestions(
            quizId: quizId
        );
        var userQuizQuestions = quizQuestions
            .Select((x, i) => new UserQuizQuestion(x, i))
            .ToList();
        if (userQuizQuestions.Count == 0)
        {
            context.TransitionToState();
            return Task.CompletedTask;
        }
        context.Instance.UserQuizQuestions = userQuizQuestions;

        return Task.CompletedTask;
    }

    private async Task AnswerQuestion(BehaviorContext<QuizProgressState> context)
    {
        var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
        
        var currentAnswer = context.Update.Message?.Text;
        var correctAnswer = context.Instance.CurrentQuestion!.QuizQuestion.Answer;
        if (currentAnswer is null)
        {
            return;
        }
        
        var score = Fuzz.TokenSortRatio(correctAnswer, currentAnswer);
        var nextQuestionOrder = score switch
        {
            100 => 0,
            >= 80 => 0,
            >= 50 => 5,
            _ => 3
        };
        context.Instance.CurrentQuestion.Order += nextQuestionOrder;

        var messageId = await messageManager.SendCompletedAnswering(
            chatId: context.Update.GetChatId(),
            question: context.Instance.CurrentQuestion.QuizQuestion,
            repeatsAfter: nextQuestionOrder,
            score: score
        );
        context.Instance.MessageId = messageId;
    }

    private Task SkipQuestion(BehaviorContext<QuizProgressState> context)
    {
        return Task.CompletedTask;
    }

    private async Task SendQuestion(BehaviorContext<QuizProgressState> context)
    {
        var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
        var quizService = context.ServiceProvider.GetRequiredService<QuizService>();
        
        var chatId = context.Update.GetChatId();
        var nextQuestion = context.Instance.UserQuizQuestions.FirstOrDefault(x => x.Order > (context.Instance.CurrentQuestion?.Order ?? -1));
        if (nextQuestion is null)
        {
            context.TransitionToState();
            return;
        }
        context.Instance.CurrentQuestion = nextQuestion;

        var messageId = await messageManager.SendQuestionMessage(
            chatId: chatId,
            question: nextQuestion.QuizQuestion
        );
        context.Instance.MessageId = messageId;
    }

    private async Task CompleteQuiz(BehaviorContext<QuizProgressState> context)
    {
        var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();

        var messageId = await messageManager.SendCompletedQuiz(
            chatId: context.Update.GetChatId()
        );
    }
}