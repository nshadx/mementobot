using mementobot.Services;
using mementobot.Telegram;
using Telegram.Bot;

namespace mementobot.StateMachines;

public readonly record struct QuizPage(Quiz Quiz, int Page);

public class QuizPickingState
{
    public bool Published { get; set; }
    public bool OnlyCurrentUser { get; set; } = true;

    public List<QuizPage> Quizzes { get; set; } = [];
    public int MessageId { get; set; }
    public int Page { get; set; }

    public int QuizId { get; set; }

    public int CurrentState { get; set; }
}

public class QuizPickingStateMachine : StateMachine<QuizPickingState>
{
    public Event PageForwardEvent { get; private set; } = null!;
    public Event PageBackwardEvent { get; private set; } = null!;
    public Event QuizPickedEvent { get; private set; } = null!;

    public State<QuizPickingState> QuizPicking { get; private set; } = null!;

    public QuizPickingStateMachine()
    {
        ConfigureEvent(() => PageForwardEvent, update => update.CallbackQuery?.Data is "forward");
        ConfigureEvent(() => PageBackwardEvent, update => update.CallbackQuery?.Data is "backward");
        ConfigureEvent(() => QuizPickedEvent, update => int.TryParse(update.CallbackQuery?.Data, out _));

        ConfigureStates(state => state.CurrentState, () => QuizPicking);

        Initially(
            When(Initial.Enter)
                .Then(async context =>
                {
                    var quizService = context.ServiceProvider.GetRequiredService<QuizService>();
                    var userService = context.ServiceProvider.GetRequiredService<UserService>();
                    var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();

                    var chatId = context.Update.GetChatId();
                    var userId = userService.GetOrCreateUser(
                        telegramId: chatId
                    );
                    var quizzes = context.Instance.OnlyCurrentUser
                        ? quizService.GetUserQuizzes(userId: userId, published: context.Instance.Published)
                        : quizService.GetAllPublishedQuizzes();

                    var list = context.Instance.Quizzes;
                    var counter = 1;
                    var page = 1;
                    context.Instance.Page = 1;
                    foreach (var quiz in quizzes)
                    {
                        if (counter <= 6)
                        {
                            QuizPage quizPage = new(quiz, page);
                            list.Add(quizPage);
                            counter++;
                        }
                        else
                        {
                            page++;
                            counter = 0;
                        }
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
                    if (list.Count == 0)
                    {
                        context.IsCompleted = true;
                    }
                })
                .TransitionTo(QuizPicking),
            Ignore(QuizPickedEvent),
            Ignore(PageForwardEvent),
            Ignore(PageBackwardEvent)
        );
        
        During(QuizPicking,
            When(PageForwardEvent)
                .Then(async context =>
                {
                    var client = context.ServiceProvider.GetRequiredService<ITelegramBotClient>();
                    await client.AnswerCallbackQuery(
                        callbackQueryId: context.Update.CallbackQuery!.Id
                    );
        
                    var page = context.Instance.Page;
                    if (++page > context.Instance.Quizzes.Max(x => x.Page))
                    {
                        return;
                    }

                    await RenderPage(context, page);
                })
        );
        
        During(QuizPicking,
            When(PageBackwardEvent)
                .Then(async context =>
                {
                    var client = context.ServiceProvider.GetRequiredService<ITelegramBotClient>();
                    await client.AnswerCallbackQuery(
                        callbackQueryId: context.Update.CallbackQuery!.Id
                    );
        
                    var page = context.Instance.Page;
                    if (--page < context.Instance.Quizzes.Min(x => x.Page))
                    {
                        return;
                    }

                    await RenderPage(context, page);
                })
        );

        During(QuizPicking,
            When(QuizPickedEvent)
                .Then(async context =>
                {
                    var client = context.ServiceProvider.GetRequiredService<ITelegramBotClient>();
        
                    var chatId = context.Update.GetChatId();
                    var quizId = int.Parse(context.Update.CallbackQuery?.Data!);
                    context.Instance.QuizId = quizId;
        
                    await client.AnswerCallbackQuery(
                        callbackQueryId: context.Update.CallbackQuery!.Id
                    );
                    await client.DeleteMessage(
                        chatId: chatId,
                        messageId: context.Instance.MessageId
                    );
                })
                .TransitionTo(Final)
        );
        
        SetCompletedOnFinal();
    }

    private static async Task RenderPage(BehaviorContext<QuizPickingState> context, int page)
    {
        var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
        
        var pageQuizzes = context.Instance.Quizzes
            .Where(x => x.Page == page)
            .Select(x => x.Quiz)
            .ToArray();
        var chatId = context.Update.GetChatId();
        var messageId = await messageManager.SelectPollMessage(
            chatId: chatId,
            quizzes: pageQuizzes,
            editMessageId: context.Instance.MessageId
        );
        context.Instance.MessageId = messageId;
        context.Instance.Page = page;
    }
}