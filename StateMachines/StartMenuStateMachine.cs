using mementobot.Services;
using mementobot.Services.Messages;
using mementobot.Telegram;
using mementobot.Telegram.StateMachine;

namespace mementobot.StateMachines;

internal class StartMenuState
{
    public QuizzingState QuizzingState { get; set; } = null!;
    public FavoritesPickingState FavoritesPickingState { get; set; } = null!;
    public RecentPickingState RecentPickingState { get; set; } = null!;
    public SearchPickingState SearchPickingState { get; set; } = null!;
    public QuizActionMenuState QuizActionMenuState { get; set; } = null!;

    public int CurrentState { get; set; }
}

internal class StartMenuStateMachine : StateMachine<StartMenuState>
{
    public Event DirectStartEvent { get; private set; } = null!;
    public Event MenuStartEvent { get; private set; } = null!;
    public Event FavoritesSelectedEvent { get; private set; } = null!;
    public Event RecentSelectedEvent { get; private set; } = null!;
    public Event SearchSelectedEvent { get; private set; } = null!;

    public State<StartMenuState> Menu { get; private set; } = null!;

    public StartMenuStateMachine(
        QuizzingStateMachine quizzingStateMachine,
        FavoritesPickingStateMachine favoritesPickingStateMachine,
        RecentPickingStateMachine recentPickingStateMachine,
        SearchPickingStateMachine searchPickingStateMachine,
        QuizActionMenuStateMachine quizActionMenuStateMachine)
    {
        ConfigureEvent(() => DirectStartEvent, update =>
        {
            var text = update.Message?.Text;
            return text is not null
                && text.StartsWith("/start ")
                && int.TryParse(text["/start ".Length..].Trim(), out _);
        });
        ConfigureEvent(() => MenuStartEvent, update => update.Message?.Text?.Trim() is "/start");
        ConfigureEvent(() => FavoritesSelectedEvent, update => update.CallbackQuery?.Data is "start:favorites");
        ConfigureEvent(() => RecentSelectedEvent, update => update.CallbackQuery?.Data is "start:recent");
        ConfigureEvent(() => SearchSelectedEvent, update => update.CallbackQuery?.Data is "start:search");

        ConfigureStates(state => state.CurrentState, () => Menu);
        ConfigureStateMachine(quizzingStateMachine, x => x.QuizzingState);
        ConfigureStateMachine(favoritesPickingStateMachine, x => x.FavoritesPickingState);
        ConfigureStateMachine(recentPickingStateMachine, x => x.RecentPickingState);
        ConfigureStateMachine(searchPickingStateMachine, x => x.SearchPickingState);
        ConfigureStateMachine(quizActionMenuStateMachine, x => x.QuizActionMenuState);

        Initially(
            When(DirectStartEvent)
                .Then(context =>
                {
                    var text = context.Update.Message!.Text!;
                    context.Instance.QuizActionMenuState.QuizId = int.Parse(text["/start ".Length..].Trim());
                    return Task.CompletedTask;
                })
                .TransitionTo(quizActionMenuStateMachine, quizActionMenuStateMachine.Initial),
            When(MenuStartEvent)
                .Then(async context =>
                {
                    var startMenu = context.ServiceProvider.GetRequiredService<StartMenuMessage>();
                    await startMenu.Apply(context.Update.GetChatId());
                })
                .TransitionTo(Menu),
            Ignore(FavoritesSelectedEvent),
            Ignore(RecentSelectedEvent),
            Ignore(SearchSelectedEvent)
        );

        During(Menu,
            When(FavoritesSelectedEvent)
                .Then(async context =>
                {
                    var startMenu = context.ServiceProvider.GetRequiredService<StartMenuMessage>();
                    await startMenu.Delete(context.Update.GetChatId());
                })
                .TransitionTo(favoritesPickingStateMachine, favoritesPickingStateMachine.Initial),
            When(RecentSelectedEvent)
                .Then(async context =>
                {
                    var startMenu = context.ServiceProvider.GetRequiredService<StartMenuMessage>();
                    await startMenu.Delete(context.Update.GetChatId());
                })
                .TransitionTo(recentPickingStateMachine, recentPickingStateMachine.Initial),
            When(SearchSelectedEvent)
                .Then(async context =>
                {
                    var startMenu = context.ServiceProvider.GetRequiredService<StartMenuMessage>();
                    await startMenu.Delete(context.Update.GetChatId());
                })
                .TransitionTo(searchPickingStateMachine, searchPickingStateMachine.Initial)
        );

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

        When(searchPickingStateMachine, searchPickingStateMachine.QuizPickedEvent)
            .Then(context =>
            {
                context.Instance.QuizActionMenuState.QuizId = context.Instance.SearchPickingState.QuizId;
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
