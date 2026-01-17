using mementobot.Services;
using mementobot.Telegram;

namespace mementobot.StateMachines;

public class AddQuizQuestionState
{
    public List<(Quiz Quiz, int Page)> Quizzes { get; set; } = [];
    
    public int MessageId { get; set; }
    public int QuizId { get; set; }
    
    public string Question { get; set; } = null!;
    public string Answer { get; set; } = null!;
    
    public int CurrentState { get; set; }
}

public class AddQuizQuestionStateMachine : StateMachine<AddQuizQuestionState>
{
    public Event AddCommandReceivedEvent { get; private set; } = null!;
    public Event MessageReceivedEvent { get; private set; } = null!;
    public Event PageForwardEvent { get; private set; } = null!;
    public Event PageBackwardEvent { get; private set; } = null!;
    public Event QuizPickedEvent { get; private set; } = null!;

    public State<AddQuizQuestionState> QuizPicking { get; private set; } = null!;
    public State<AddQuizQuestionState> FillingQuestion { get; private set; } = null!;
    public State<AddQuizQuestionState> FillingAnswer { get; private set; } = null!;

    public AddQuizQuestionStateMachine()
    {
        ConfigureEvent(() => AddCommandReceivedEvent, update => update.Message?.Text?.StartsWith("/add") ?? false);
        ConfigureEvent(() => MessageReceivedEvent, update => update.Message?.Text is not null);
        ConfigureEvent(() => PageForwardEvent, update => update.CallbackQuery?.Data is "forward");
        ConfigureEvent(() => PageBackwardEvent, update => update.CallbackQuery?.Data is "backward");
        ConfigureEvent(() => QuizPickedEvent, update => int.TryParse(update.CallbackQuery?.Data, out _));
        
        ConfigureStates(state => state.CurrentState, () => FillingQuestion, () => FillingAnswer, () => QuizPicking);

        Initially(
            When(AddCommandReceivedEvent)
                .Then(BuildSelectingPages)
                .TransitionTo(QuizPicking),
            Ignore(MessageReceivedEvent),
            Ignore(QuizPickedEvent),
            Ignore(PageForwardEvent),
            Ignore(PageBackwardEvent)
        );

        During(QuizPicking,
            When(QuizPickedEvent)
                .Then(PickQuiz)
                .TransitionTo(FillingQuestion)
        );

        During(FillingQuestion, 
            When(MessageReceivedEvent)
                .Then(SetQuestion)
                .TransitionTo(FillingAnswer)
        );

        During(FillingAnswer,
            When(MessageReceivedEvent)
                .Then(SetAnswer)
                .TransitionTo(Final)
        );

        Finally(x => x.Then(AddQuizQuestion));
        
        SetFinishedWhenCompleted();
    }

    private async Task BuildSelectingPages(BehaviorContext<AddQuizQuestionState> context)
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

    private async Task PickQuiz(BehaviorContext<AddQuizQuestionState> context)
    {
        var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
        
        var chatId = context.Update.GetChatId();
        var quizId = int.Parse(context.Update.CallbackQuery?.Data!);
        context.Instance.QuizId = quizId;
        
        var messageId = await messageManager.EnterQuestionMessage(
            chatId: chatId
        );
        context.Instance.MessageId = messageId;
    }

    private async Task SetQuestion(BehaviorContext<AddQuizQuestionState> context)
    {
        var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();

        var chatId = context.Update.GetChatId();
        var question = context.Update.Message?.Text!;
        context.Instance.Question = question;

        var messageId = await messageManager.EnterAnswerMessage(
            chatId: chatId
        );
        context.Instance.MessageId = messageId;
    }

    private async Task SetAnswer(BehaviorContext<AddQuizQuestionState> context)
    {
        var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
        
        var chatId = context.Update.GetChatId();
        var answer = context.Update.Message?.Text!;
        context.Instance.Answer = answer;
        
        await messageManager.DeleteMessage(
            chatId: chatId,
            messageId: context.Instance.MessageId
        );
    }

    private Task AddQuizQuestion(BehaviorContext<AddQuizQuestionState> context)
    {
        var quizService = context.ServiceProvider.GetRequiredService<QuizService>();

        var quizId = context.Instance.QuizId;
        var question = context.Instance.Question;
        var answer = context.Instance.Answer;

        quizService.AddQuizQuestion(
            quizId: quizId,
            question: question,
            answer: answer
        );

        return Task.CompletedTask;
    }
}