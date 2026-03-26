using mementobot.Services;
using mementobot.Services.Messages;
using mementobot.Services.Quizzing;
using mementobot.Telegram;
using mementobot.Telegram.StateMachine;

namespace mementobot.StateMachines;

internal class QuizzingState
{
    public int QuizId { get; set; }

    public QuestionQueue Queue { get; set; } = new();
    public Dictionary<int, QuizQuestion> Questions { get; set; } = [];

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
                .Then(async (BehaviorContext<QuizzingState> context, QuizService quizService) =>
                {
                    foreach (var q in quizService.GetQuizQuestions(quizId: context.Instance.QuizId))
                    {
                        context.Instance.Questions[q.Id] = q;
                        context.Instance.Queue.QuestionIds.Add(q.Id);
                    }
                    await Task.CompletedTask;
                })
                .TransitionTo(QuizQuestion),
            Ignore(MessageReceivedEvent),
            Ignore(OnSkipCallbackEvent)
        );

        During(QuizQuestion,
            When(QuizQuestion.Enter)
                .Then(async (BehaviorContext<QuizzingState> context, QuizQuestionMessage quizQuestionMsg) =>
                {
                    var questionId = engine.GetCurrentQuestionId(context.Instance.Queue);
                    if (questionId is null)
                    {
                        await context.TransitionTo(Final);
                        return;
                    }

                    await quizQuestionMsg.Apply(context.Update.GetChatId(), context.Instance.Questions[questionId.Value]);
                }),
            When(MessageReceivedEvent)
                .Then(async (BehaviorContext<QuizzingState> context, QuizQuestionMessage quizQuestionMsg, CompletedAnsweringMessage completedAnswering, IContextAccessor accessor) =>
                {
                    var currentText = context.Update.Message?.Text ?? context.Update.Message?.Caption;
                    if (currentText is null) return;

                    accessor.Current.DeleteUserMessage = true;

                    var questionId = engine.GetCurrentQuestionId(context.Instance.Queue)!.Value;
                    var question = context.Instance.Questions[questionId];
                    var result = evaluator.Evaluate(question, new TextAnswerResult(currentText));
                    var nextId = engine.Advance(context.Instance.Queue, result);

                    var score = (int)(result.Score * 100);
                    var shift = nextId is not null
                        ? context.Instance.Queue.QuestionIds.IndexOf(questionId)
                        : 0;

                    await completedAnswering.Apply(context.Update.GetChatId(), new(question, score, RepeatsAfter: shift > 0 ? shift : 0));
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

        Finally(x => x.Then(async (BehaviorContext<QuizzingState> context, QuizQuestionMessage quizQuestionMsg, CompletedQuizMessage completedQuiz, QuizService quizService, UserService userService) =>
        {
            var chatId = context.Update.GetChatId();
            await quizQuestionMsg.Delete(chatId);

            var userId = userService.GetOrCreateUser(telegramId: chatId);
            var avgScore = statistics.AverageScore(context.Instance.Queue);
            quizService.RecordQuizHistory(userId: userId, quizId: context.Instance.QuizId, avgScore: avgScore);

            await completedQuiz.Apply(chatId, new(statistics, context.Instance.Queue, context.Instance.Questions));
        }));

        SetCompletedOnFinal();
    }
}
