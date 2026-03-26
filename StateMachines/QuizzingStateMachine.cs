using mementobot.Services;
using mementobot.Services.Quizzing;
using mementobot.Telegram;
using mementobot.Telegram.StateMachine;

namespace mementobot.StateMachines;

internal class QuizzingState
{
    public int QuizId { get; set; }

    public QuestionQueue Queue { get; set; } = new();
    public Dictionary<int, QuizQuestion> Questions { get; set; } = [];
    public int MessageId { get; set; }

    public int CurrentState { get; set; }
}

internal class QuizzingStateMachine : StateMachine<QuizzingState>
{
    public Event OnSkipCallbackEvent { get; private set; } = null!;
    public Event MessageReceivedEvent { get; private set; } = null!;

    public State<QuizzingState> QuizQuestion { get; private set; } = null!;

    public QuizzingStateMachine(
        IQuestionEngine engine,
        IAnswerEvaluator evaluator,
        IQuizSessionStatistics statistics)
    {
        ConfigureEvent(() => OnSkipCallbackEvent, update => update.CallbackQuery?.Data is "skip");
        ConfigureEvent(() => MessageReceivedEvent, update => update.Message?.Text is not null);

        ConfigureStates(state => state.CurrentState, () => QuizQuestion);

        Initially(
            When(Initial.Enter)
                .Then(context =>
                {
                    var quizService = context.ServiceProvider.GetRequiredService<QuizService>();
                    var quizQuestions = quizService.GetQuizQuestions(quizId: context.Instance.QuizId);
                    foreach (var q in quizQuestions)
                    {
                        context.Instance.Questions[q.Id] = q;
                        context.Instance.Queue.QuestionIds.Add(q.Id);
                    }
                    return Task.CompletedTask;
                })
                .TransitionTo(QuizQuestion),
            Ignore(MessageReceivedEvent),
            Ignore(OnSkipCallbackEvent)
        );

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
            var quizService = context.ServiceProvider.GetRequiredService<QuizService>();
            var userService = context.ServiceProvider.GetRequiredService<UserService>();

            var chatId = context.Update.GetChatId();
            var userId = userService.GetOrCreateUser(telegramId: chatId);
            var avgScore = statistics.AverageScore(context.Instance.Queue);
            quizService.RecordQuizHistory(userId: userId, quizId: context.Instance.QuizId, avgScore: avgScore);

            _ = await messageManager.SendCompletedQuiz(
                chatId: chatId,
                statistics: statistics,
                queue: context.Instance.Queue,
                questions: context.Instance.Questions
            );
        }));

        SetCompletedOnFinal();
    }
}