using mementobot.Services.Messages;
using mementobot.Telegram;
using mementobot.Telegram.StateMachine;

namespace mementobot.StateMachines;

internal class MyQuizzesState
{
    public OwnedQuizzesPickingState OwnedPickingState { get; set; } = null!;
    public FavoritesPickingState FavoritesPickingState { get; set; } = null!;
    public RecentPickingState RecentPickingState { get; set; } = null!;
    public QuizActionMenuState QuizActionMenuState { get; set; } = null!;
    public QuizzingState QuizzingState { get; set; } = null!;

    public int CurrentState { get; set; }
}

internal class MyQuizzesStateMachine : StateMachine<MyQuizzesState>
{
    public Event MyQuizzesCommandEvent { get; private set; } = null!;
    public Event MineSelectedEvent { get; private set; } = null!;
    public Event FavoritesSelectedEvent { get; private set; } = null!;
    public Event RecentSelectedEvent { get; private set; } = null!;

    public State<MyQuizzesState> Menu { get; private set; } = null!;

    public MyQuizzesStateMachine(
        OwnedQuizzesPickingStateMachine ownedPickingStateMachine,
        FavoritesPickingStateMachine favoritesPickingStateMachine,
        RecentPickingStateMachine recentPickingStateMachine,
        QuizActionMenuStateMachine quizActionMenuStateMachine,
        QuizzingStateMachine quizzingStateMachine)
    {
        ConfigureEvent(() => MyQuizzesCommandEvent, update => update.Message?.Text?.Trim() is "/myquizzes");
        ConfigureEvent(() => MineSelectedEvent, update => update.CallbackQuery?.Data is "myquizzes:mine");
        ConfigureEvent(() => FavoritesSelectedEvent, update => update.CallbackQuery?.Data is "myquizzes:favorites");
        ConfigureEvent(() => RecentSelectedEvent, update => update.CallbackQuery?.Data is "myquizzes:recent");

        ConfigureStates(state => state.CurrentState, () => Menu);
        ConfigureStateMachine(ownedPickingStateMachine, x => x.OwnedPickingState);
        ConfigureStateMachine(favoritesPickingStateMachine, x => x.FavoritesPickingState);
        ConfigureStateMachine(recentPickingStateMachine, x => x.RecentPickingState);
        ConfigureStateMachine(quizActionMenuStateMachine, x => x.QuizActionMenuState);
        ConfigureStateMachine(quizzingStateMachine, x => x.QuizzingState);

        Initially(
            When(MyQuizzesCommandEvent)
                .Then(async (BehaviorContext<MyQuizzesState> context, MyQuizzesMenuMessage menu) =>
                {
                    await menu.Apply(context.Update.GetChatId());
                })
                .TransitionTo(Menu),
            Ignore(MineSelectedEvent),
            Ignore(FavoritesSelectedEvent),
            Ignore(RecentSelectedEvent)
        );

        During(Menu,
            When(MineSelectedEvent)
                .Then(async (BehaviorContext<MyQuizzesState> context, MyQuizzesMenuMessage menu) =>
                {
                    await menu.Delete(context.Update.GetChatId());
                })
                .TransitionTo(ownedPickingStateMachine, ownedPickingStateMachine.Initial),
            When(FavoritesSelectedEvent)
                .Then(async (BehaviorContext<MyQuizzesState> context, MyQuizzesMenuMessage menu) =>
                {
                    await menu.Delete(context.Update.GetChatId());
                })
                .TransitionTo(favoritesPickingStateMachine, favoritesPickingStateMachine.Initial),
            When(RecentSelectedEvent)
                .Then(async (BehaviorContext<MyQuizzesState> context, MyQuizzesMenuMessage menu) =>
                {
                    await menu.Delete(context.Update.GetChatId());
                })
                .TransitionTo(recentPickingStateMachine, recentPickingStateMachine.Initial)
        );

        When(ownedPickingStateMachine, ownedPickingStateMachine.QuizPickedEvent)
            .Then(context =>
            {
                context.Instance.QuizActionMenuState.QuizId = context.Instance.OwnedPickingState.QuizId;
                return Task.CompletedTask;
            })
            .TransitionTo(quizActionMenuStateMachine, quizActionMenuStateMachine.Initial);

        When(favoritesPickingStateMachine, favoritesPickingStateMachine.QuizPickedEvent)
            .Then(context =>
            {
                context.Instance.QuizActionMenuState.QuizId = context.Instance.FavoritesPickingState.QuizId;
                return Task.CompletedTask;
            })
            .TransitionTo(quizActionMenuStateMachine, quizActionMenuStateMachine.Initial);

        When(recentPickingStateMachine, recentPickingStateMachine.QuizPickedEvent)
            .Then(context =>
            {
                context.Instance.QuizActionMenuState.QuizId = context.Instance.RecentPickingState.QuizId;
                return Task.CompletedTask;
            })
            .TransitionTo(quizActionMenuStateMachine, quizActionMenuStateMachine.Initial);

        When(quizActionMenuStateMachine, quizActionMenuStateMachine.PlaySelectedEvent)
            .Then(context =>
            {
                context.Instance.QuizzingState.QuizId = context.Instance.QuizActionMenuState.QuizId;
                return Task.CompletedTask;
            })
            .TransitionTo(quizzingStateMachine, quizzingStateMachine.Initial);

        When(quizzingStateMachine, quizzingStateMachine.Final.Enter)
            .Then(context =>
            {
                context.IsCompleted = true;
                return Task.CompletedTask;
            });
    }
}
