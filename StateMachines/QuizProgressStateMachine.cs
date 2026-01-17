using FuzzySharp;
using mementobot.Services;
using mementobot.Telegram;
using Telegram.Bot;

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
    public int Page { get; set; }
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
            When(PageForwardEvent)
                .Then(ForwardPage)
        );
        
        During(QuizPicking,
            When(PageBackwardEvent)
                .Then(BackwardPage)
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
        
        SetCompletedOnFinal();
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
            userId: userId,
            published: true
        );

        var list = context.Instance.Quizzes;
        var counter = 1;
        var page = 1;
        context.Instance.Page = 1;
        foreach (var quiz in quizzes)
        {
            if (counter <= 6)
            {
                list.Add((quiz, page));
                counter++;
            }
            else
            {
                page++;
                counter = 0;
            }
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
        if (list.Count == 0)
        {
            context.IsCompleted = true;
            return;
        }
    }

    private async Task ForwardPage(BehaviorContext<QuizProgressState> context)
    {
        var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
        var client = context.ServiceProvider.GetRequiredService<ITelegramBotClient>();
        
        await client.AnswerCallbackQuery(
            callbackQueryId: context.Update.CallbackQuery!.Id
        );
        
        var page = context.Instance.Page;
        if (++page > context.Instance.Quizzes.Max(x => x.Page))
        {
            return;
        }
        
        var pageQuizzes = context.Instance.Quizzes
            .Where(x => x.Page == page)
            .Select(x => x.Quiz)
            .ToArray();

        var chatId = context.Update.GetChatId();
        var messageId = await messageManager.SelectPollMessage(
            chatId: chatId,
            quizzes: pageQuizzes,
            editMessageId: context.Instance.MessageId
        );
        context.Instance.MessageId = messageId;
        context.Instance.Page = page;
    }

    private async Task BackwardPage(BehaviorContext<QuizProgressState> context)
    {
        var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
        var client = context.ServiceProvider.GetRequiredService<ITelegramBotClient>();
        
        await client.AnswerCallbackQuery(
            callbackQueryId: context.Update.CallbackQuery!.Id
        );
        
        var page = context.Instance.Page;
        if (--page < context.Instance.Quizzes.Min(x => x.Page))
        {
            return;
        }
        
        var pageQuizzes = context.Instance.Quizzes
            .Where(x => x.Page == page)
            .Select(x => x.Quiz)
            .ToArray();

        var chatId = context.Update.GetChatId();
        var messageId = await messageManager.SelectPollMessage(
            chatId: chatId,
            quizzes: pageQuizzes,
            editMessageId: context.Instance.MessageId
        );
        context.Instance.MessageId = messageId;
        context.Instance.Page = page;
    }

    private async Task PickQuiz(BehaviorContext<QuizProgressState> context)
    {
        var client = context.ServiceProvider.GetRequiredService<ITelegramBotClient>();
        var quizService = context.ServiceProvider.GetRequiredService<QuizService>();
        
        var chatId = context.Update.GetChatId();
        var quizId = int.Parse(context.Update.CallbackQuery?.Data!);
        
        await client.AnswerCallbackQuery(
            callbackQueryId: context.Update.CallbackQuery!.Id
        );
        await client.DeleteMessage(
            chatId: chatId,
            messageId: context.Instance.MessageId
        );
        
        var quizQuestions = quizService.GetQuizQuestions(
            quizId: quizId
        );
        var userQuizQuestions = quizQuestions
            .Select((x, i) => new UserQuizQuestion(x, i))
            .ToList();
        context.Instance.UserQuizQuestions = userQuizQuestions;
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
            await context.TransitionToState(Final);
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