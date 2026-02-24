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
    public QuizPickingState QuizPickingState { get; set; } = null!;
    
    public List<UserQuizQuestion> UserQuizQuestions { get; set; } = [];
    public UserQuizQuestion? CurrentQuestion { get; set; }
    public int MessageId { get; set; }
    
    public int CurrentState { get; set; }
}

public class QuizProgressStateMachine : StateMachine<QuizProgressState>
{
    public Event OnSkipCallbackEvent { get; private set; } = null!;
    public Event StartCommandReceivedEvent { get; private set; } = null!;
    public Event MessageReceivedEvent { get; private set; } = null!;
    
    public State<QuizProgressState> QuizQuestion { get; private set; } = null!;
    
    public QuizProgressStateMachine(QuizPickingStateMachine quizPickingStateMachine)
    {
        ConfigureEvent(() => OnSkipCallbackEvent, update => update.CallbackQuery?.Data is "skip");
        ConfigureEvent(() => StartCommandReceivedEvent, update => update.Message?.Text?.StartsWith("/start") ?? false);
        ConfigureEvent(() => MessageReceivedEvent, update => update.Message?.Text is not null);

        ConfigureStates(state => state.CurrentState, () => QuizQuestion);
        
        ConfigureStateMachine(quizPickingStateMachine, x => x.QuizPickingState);
        
        Initially(
            When(StartCommandReceivedEvent)
                .Then(context =>
                {
                    context.Instance.QuizPickingState.Published = true;
                    context.Instance.QuizPickingState.OnlyCurrentUser = false;
                    return Task.CompletedTask;
                })
                .TransitionTo(quizPickingStateMachine, quizPickingStateMachine.Initial),
            Ignore(MessageReceivedEvent)
        );

        When(quizPickingStateMachine, quizPickingStateMachine.QuizPickedEvent)
            .Then(context =>
            {
                var quizService = context.ServiceProvider.GetRequiredService<QuizService>();

                var quizQuestions = quizService.GetQuizQuestions(
                    quizId: context.Instance.QuizPickingState.QuizId
                );
                var userQuizQuestions = quizQuestions
                    .Select((x, i) => new UserQuizQuestion(x, i))
                    .ToList();
                context.Instance.UserQuizQuestions = userQuizQuestions;
                return Task.CompletedTask;
            })
            .TransitionTo(QuizQuestion);
        
        During(QuizQuestion,
            When(QuizQuestion.Enter)
                .Then(async context =>
                {
                    var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
        
                    var chatId = context.Update.GetChatId();
                    var nextQuestion = context.Instance.UserQuizQuestions.FirstOrDefault(x => x.Order > (context.Instance.CurrentQuestion?.Order ?? -1));
                    if (nextQuestion is null)
                    {
                        await context.TransitionTo(Final);
                        return;
                    }
                    context.Instance.CurrentQuestion = nextQuestion;

                    var messageId = await messageManager.SendQuestionMessage(
                        chatId: chatId,
                        question: nextQuestion.QuizQuestion
                    );
                    context.Instance.MessageId = messageId;
                }),
            When(MessageReceivedEvent)
                .Then(async context =>
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
                })
                .TransitionTo(QuizQuestion),
            When(OnSkipCallbackEvent)
                .TransitionTo(QuizQuestion)
        );
        
        Finally(x => x.Then(async context =>
        {
            var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();

            _ = await messageManager.SendCompletedQuiz(
                chatId: context.Update.GetChatId()
            );
        }));
        
        SetCompletedOnFinal();
    }
}