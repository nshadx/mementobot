using mementobot.Services;
using mementobot.Telegram;
using mementobot.Telegram.StateMachine;

namespace mementobot.StateMachines;

internal class QuizActionMenuState
{
    public int QuizId { get; set; }
    public int MenuMessageId { get; set; }
    public bool IsFavorited { get; set; }

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
                    var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();

                    var chatId = context.Update.GetChatId();
                    var userId = userService.GetOrCreateUser(telegramId: chatId);
                    var isFavorited = quizService.IsInFavorites(userId: userId, quizId: context.Instance.QuizId);

                    var messageId = await messageManager.SendQuizActionMenu(chatId: chatId, isFavorited: isFavorited);
                    context.Instance.MenuMessageId = messageId;
                })
                .TransitionTo(WaitingAction),
            Ignore(PlaySelectedEvent),
            Ignore(ToggleFavoriteEvent)
        );

        During(WaitingAction,
            When(PlaySelectedEvent)
                .Then(async context =>
                {
                    var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
                    await messageManager.DeleteMessage(context.Update.GetChatId(), context.Instance.MenuMessageId);
                })
                .TransitionTo(Final),
            When(ToggleFavoriteEvent)
                .Then(async context =>
                {
                    var quizService = context.ServiceProvider.GetRequiredService<QuizService>();
                    var userService = context.ServiceProvider.GetRequiredService<UserService>();
                    var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();

                    var chatId = context.Update.GetChatId();
                    var userId = userService.GetOrCreateUser(telegramId: chatId);

                    if (context.Instance.IsFavorited)
                        quizService.RemoveFromFavorites(userId: userId, quizId: context.Instance.QuizId);
                    else
                        quizService.AddToFavorites(userId: userId, quizId: context.Instance.QuizId);

                    context.Instance.IsFavorited = !context.Instance.IsFavorited;

                    await messageManager.EditQuizActionMenu(
                        chatId: chatId,
                        messageId: context.Instance.MenuMessageId,
                        isFavorited: context.Instance.IsFavorited
                    );
                })
        );

        SetCompletedOnFinal();
    }
}
