using mementobot.Services;
using mementobot.Services.Messages;
using mementobot.Telegram;
using mementobot.Telegram.StateMachine;

namespace mementobot.StateMachines;

internal class OwnedQuizzesPickingState
{
    public List<QuizPage> Quizzes { get; set; } = [];
    public int Page { get; set; }
    public int QuizId { get; set; }

    public int CurrentState { get; set; }
}

internal class OwnedQuizzesPickingStateMachine : StateMachine<OwnedQuizzesPickingState>
{
    public Event PageForwardEvent { get; private set; } = null!;
    public Event PageBackwardEvent { get; private set; } = null!;
    public Event QuizPickedEvent { get; private set; } = null!;

    public State<OwnedQuizzesPickingState> QuizPicking { get; private set; } = null!;

    public OwnedQuizzesPickingStateMachine()
    {
        ConfigureEvent(() => PageForwardEvent, update => update.CallbackQuery?.Data is "forward");
        ConfigureEvent(() => PageBackwardEvent, update => update.CallbackQuery?.Data is "backward");
        ConfigureEvent(() => QuizPickedEvent, update => int.TryParse(update.CallbackQuery?.Data, out _));

        ConfigureStates(state => state.CurrentState, () => QuizPicking);

        Initially(
            When(Initial.Enter)
                .Then(async (BehaviorContext<OwnedQuizzesPickingState> context, QuizService quizService, UserService userService, QuizListMessage quizList) =>
                {
                    var chatId = context.Update.GetChatId();
                    var userId = userService.GetOrCreateUser(telegramId: chatId);
                    var quizzes = quizService.GetOwnedQuizzes(userId: userId);

                    var list = context.Instance.Quizzes;
                    var counter = 1;
                    var page = 1;
                    context.Instance.Page = 1;
                    foreach (var quiz in quizzes)
                    {
                        if (counter <= 6) { list.Add(new QuizPage(quiz, page)); counter++; }
                        else { page++; counter = 1; }
                    }

                    var firstPage = list.Where(x => x.Page == 1).Select(x => x.Quiz).ToArray();
                    await quizList.Apply(chatId, firstPage);

                    if (list.Count == 0)
                        context.IsCompleted = true;
                })
                .TransitionTo(QuizPicking),
            Ignore(QuizPickedEvent),
            Ignore(PageForwardEvent),
            Ignore(PageBackwardEvent)
        );

        During(QuizPicking,
            When(PageForwardEvent)
                .Then(async (BehaviorContext<OwnedQuizzesPickingState> context, QuizListMessage quizList) =>
                {
                    var page = context.Instance.Page;
                    if (++page > context.Instance.Quizzes.Max(x => x.Page)) return;
                    await RenderPage(context, page, quizList);
                }),
            When(PageBackwardEvent)
                .Then(async (BehaviorContext<OwnedQuizzesPickingState> context, QuizListMessage quizList) =>
                {
                    var page = context.Instance.Page;
                    if (--page < context.Instance.Quizzes.Min(x => x.Page)) return;
                    await RenderPage(context, page, quizList);
                }),
            When(QuizPickedEvent)
                .Then(async (BehaviorContext<OwnedQuizzesPickingState> context, QuizListMessage quizList) =>
                {
                    context.Instance.QuizId = int.Parse(context.Update.CallbackQuery!.Data!);
                    await quizList.Delete(context.Update.GetChatId());
                })
                .TransitionTo(Final)
        );

        SetCompletedOnFinal();
    }

    private static async Task RenderPage(BehaviorContext<OwnedQuizzesPickingState> context, int page, QuizListMessage quizList)
    {
        var pageQuizzes = context.Instance.Quizzes
            .Where(x => x.Page == page).Select(x => x.Quiz).ToArray();
        await quizList.Apply(context.Update.GetChatId(), pageQuizzes);
        context.Instance.Page = page;
    }
}
