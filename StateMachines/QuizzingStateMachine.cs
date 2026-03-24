using mementobot.Services;
using mementobot.Services.Quizzing;
using mementobot.Telegram;
using mementobot.Telegram.StateMachine;

namespace mementobot.StateMachines;

internal class QuizzingState
{
    public QuizPickingState QuizPickingState { get; set; } = null!;

    public QuestionQueue Queue { get; set; } = new();
    public Dictionary<int, QuizQuestion> Questions { get; set; } = [];
    public int MessageId { get; set; }

    public int CurrentState { get; set; }
}

internal class QuizzingStateMachine : StateMachine<QuizzingState>
{
    public Event OnSkipCallbackEvent { get; private set; } = null!;
    public Event StartCommandReceivedEvent { get; private set; } = null!;
    public Event MessageReceivedEvent { get; private set; } = null!;

    public State<QuizzingState> QuizQuestion { get; private set; } = null!;

    public QuizzingStateMachine(
        QuizPickingStateMachine quizPickingStateMachine,
        IQuestionEngine engine,
        IAnswerEvaluator evaluator,
        IQuizSessionStatistics statistics)
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

                foreach (var q in quizQuestions)
                {
                    context.Instance.Questions[q.Id] = q;
                    context.Instance.Queue.QuestionIds.Add(q.Id);
                }

                return Task.CompletedTask;
            })
            .TransitionTo(QuizQuestion);

        During(QuizQuestion,
            When(QuizQuestion.Enter)
                .Then(async context =>
                {
                    var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
                    var chatId = context.Update.GetChatId();

                    var questionId = engine.GetCurrentQuestionId(context.Instance.Queue);
                    if (questionId is null)
                    {
                        await context.TransitionTo(Final);
                        return;
                    }

                    var question = context.Instance.Questions[questionId.Value];
                    var messageId = await messageManager.SendQuestionMessage(
                        chatId: chatId,
                        question: question
                    );
                    context.Instance.MessageId = messageId;
                }),
            When(MessageReceivedEvent)
                .Then(async context =>
                {
                    var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
                    var chatId = context.Update.GetChatId();

                    var currentText = context.Update.Message?.Text ?? context.Update.Message?.Caption;
                    if (currentText is null)
                        return;

                    var questionId = engine.GetCurrentQuestionId(context.Instance.Queue)!.Value;
                    var question = context.Instance.Questions[questionId];

                    var result = evaluator.Evaluate(question, new TextAnswerResult(currentText));
                    var nextId = engine.Advance(context.Instance.Queue, result);

                    var score = (int)(result.Score * 100);
                    var shift = nextId is not null
                        ? context.Instance.Queue.QuestionIds.IndexOf(questionId)
                        : 0;
                    var repeatsAfter = shift > 0 ? shift : 0;

                    await messageManager.SendCompletedAnswering(
                        chatId: chatId,
                        question: question,
                        repeatsAfter: repeatsAfter,
                        score: score
                    );
                })
                .TransitionTo(QuizQuestion),
            When(OnSkipCallbackEvent)
                .Then(context =>
                {
                    engine.Skip(context.Instance.Queue);
                    return Task.CompletedTask;
                })
                .TransitionTo(QuizQuestion)
        );

        Finally(x => x.Then(async context =>
        {
            var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();

            _ = await messageManager.SendCompletedQuiz(
                chatId: context.Update.GetChatId(),
                statistics: statistics,
                queue: context.Instance.Queue,
                questions: context.Instance.Questions
            );
        }));

        SetCompletedOnFinal();
    }
}