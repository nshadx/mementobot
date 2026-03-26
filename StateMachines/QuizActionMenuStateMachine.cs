using mementobot.Services;
using mementobot.Services.Messages;
using mementobot.Telegram;
using mementobot.Telegram.StateMachine;

namespace mementobot.StateMachines;

internal class QuizActionMenuState
{
    public int QuizId { get; set; }
    public string? ShareLink { get; set; }

    public int CurrentState { get; set; }
}

internal class QuizActionMenuStateMachine : StateMachine<QuizActionMenuState>
{
    public Event PlaySelectedEvent { get; private set; } = null!;
    public Event ToggleFavoriteEvent { get; private set; } = null!;

    public State<QuizActionMenuState> WaitingAction { get; private set; } = null!;

    public QuizActionMenuStateMachine()
    {
        ConfigureEvent(() => PlaySelectedEvent, update => update.CallbackQuery?.Data is "action:play");
        ConfigureEvent(() => ToggleFavoriteEvent, update => update.CallbackQuery?.Data is "action:favorite");

        ConfigureStates(state => state.CurrentState, () => WaitingAction);

        Initially(
            When(Initial.Enter)
                .Then(async context =>
                {
                    var quizService = context.ServiceProvider.GetRequiredService<QuizService>();
                    var userService = context.ServiceProvider.GetRequiredService<UserService>();
                    var quizActionMenu = context.ServiceProvider.GetRequiredService<QuizActionMenuMessage>();

                    var chatId = context.Update.GetChatId();
                    var userId = userService.GetOrCreateUser(telegramId: chatId);
                    var isOwned = quizService.IsOwnedBy(userId: userId, quizId: context.Instance.QuizId);
                    var isFavorited = !isOwned && quizService.IsInFavorites(userId: userId, quizId: context.Instance.QuizId);

                    var isPublished = quizService.IsPublished(quizId: context.Instance.QuizId);
                    context.Instance.ShareLink = isPublished
                        ? $"https://t.me/nshadx_mementobot?start={context.Instance.QuizId}"
                        : null;

                    await quizActionMenu.Apply(chatId, new(isFavorited, isOwned, context.Instance.ShareLink));
                })
                .TransitionTo(WaitingAction),
            Ignore(PlaySelectedEvent),
            Ignore(ToggleFavoriteEvent)
        );

        During(WaitingAction,
            When(PlaySelectedEvent)
                .Then(async context =>
                {
                    var quizActionMenu = context.ServiceProvider.GetRequiredService<QuizActionMenuMessage>();
                    await quizActionMenu.Delete(context.Update.GetChatId());
                })
                .TransitionTo(Final),
            When(ToggleFavoriteEvent)
                .Then(async context =>
                {
                    var quizService = context.ServiceProvider.GetRequiredService<QuizService>();
                    var userService = context.ServiceProvider.GetRequiredService<UserService>();
                    var quizActionMenu = context.ServiceProvider.GetRequiredService<QuizActionMenuMessage>();

                    var chatId = context.Update.GetChatId();
                    var userId = userService.GetOrCreateUser(telegramId: chatId);

                    var isFavorited = quizService.IsInFavorites(userId: userId, quizId: context.Instance.QuizId);
                    if (isFavorited)
                        quizService.RemoveFromFavorites(userId: userId, quizId: context.Instance.QuizId);
                    else
                        quizService.AddToFavorites(userId: userId, quizId: context.Instance.QuizId);

                    await quizActionMenu.Apply(chatId, new(!isFavorited, IsOwned: false, context.Instance.ShareLink));
                })
        );

        SetCompletedOnFinal();
    }
}
