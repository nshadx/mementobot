using mementobot.Services;
using mementobot.Telegram;
using mementobot.Telegram.StateMachine;

namespace mementobot.StateMachines;

internal class SearchPickingState
{
    public List<QuizPage> Quizzes { get; set; } = [];
    public int PromptMessageId { get; set; }
    public int ListMessageId { get; set; }
    public int Page { get; set; }
    public int QuizId { get; set; }

    public int CurrentState { get; set; }
}

internal class SearchPickingStateMachine : StateMachine<SearchPickingState>
{
    public Event MessageReceivedEvent { get; private set; } = null!;
    public Event PageForwardEvent { get; private set; } = null!;
    public Event PageBackwardEvent { get; private set; } = null!;
    public Event QuizPickedEvent { get; private set; } = null!;

    public State<SearchPickingState> EnteringQuery { get; private set; } = null!;
    public State<SearchPickingState> ShowingResults { get; private set; } = null!;

    public SearchPickingStateMachine()
    {
        ConfigureEvent(() => MessageReceivedEvent, update => update.Message?.Text is not null);
        ConfigureEvent(() => PageForwardEvent, update => update.CallbackQuery?.Data is "forward");
        ConfigureEvent(() => PageBackwardEvent, update => update.CallbackQuery?.Data is "backward");
        ConfigureEvent(() => QuizPickedEvent, update => int.TryParse(update.CallbackQuery?.Data, out _));

        ConfigureStates(state => state.CurrentState, () => EnteringQuery, () => ShowingResults);

        Initially(
            When(Initial.Enter)
                .Then(async context =>
                {
                    var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
                    var messageId = await messageManager.SendSearchPrompt(context.Update.GetChatId());
                    context.Instance.PromptMessageId = messageId;
                })
                .TransitionTo(EnteringQuery),
            Ignore(MessageReceivedEvent),
            Ignore(PageForwardEvent),
            Ignore(PageBackwardEvent),
            Ignore(QuizPickedEvent)
        );

        During(EnteringQuery,
            When(MessageReceivedEvent)
                .Then(async context =>
                {
                    var query = context.Update.Message!.Text!;
                    var quizService = context.ServiceProvider.GetRequiredService<QuizService>();
                    var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
                    var chatId = context.Update.GetChatId();

                    await messageManager.DeleteMessage(chatId, context.Instance.PromptMessageId);
                    await messageManager.DeleteMessage(chatId, context.Update.Message!.MessageId);

                    var quizzes = quizService.SearchPublishedQuizzes(query: query);

                    var list = context.Instance.Quizzes;
                    var counter = 1;
                    var page = 1;
                    context.Instance.Page = 1;
                    foreach (var quiz in quizzes)
                    {
                        if (counter <= 6)
                        {
                            list.Add(new QuizPage(quiz, page));
                            counter++;
                        }
                        else
                        {
                            page++;
                            counter = 1;
                        }
                    }

                    var firstPage = list.Where(x => x.Page == 1).Select(x => x.Quiz).ToArray();
                    var messageId = await messageManager.SelectPollMessage(chatId: chatId, quizzes: firstPage);
                    context.Instance.ListMessageId = messageId;

                    if (list.Count == 0)
                    {
                        context.IsCompleted = true;
                    }
                })
                .TransitionTo(ShowingResults),
            Ignore(PageForwardEvent),
            Ignore(PageBackwardEvent),
            Ignore(QuizPickedEvent)
        );

        During(ShowingResults,
            When(PageForwardEvent)
                .Then(async context =>
                {
                    var page = context.Instance.Page;
                    if (++page > context.Instance.Quizzes.Max(x => x.Page))
                        return;
                    await RenderPage(context, page);
                }),
            When(PageBackwardEvent)
                .Then(async context =>
                {
                    var page = context.Instance.Page;
                    if (--page < context.Instance.Quizzes.Min(x => x.Page))
                        return;
                    await RenderPage(context, page);
                }),
            When(QuizPickedEvent)
                .Then(async context =>
                {
                    var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
                    var quizId = int.Parse(context.Update.CallbackQuery!.Data!);
                    context.Instance.QuizId = quizId;
                    await messageManager.DeleteMessage(context.Update.GetChatId(), context.Instance.ListMessageId);
                })
                .TransitionTo(Final)
        );

        SetCompletedOnFinal();
    }

    private static async Task RenderPage(BehaviorContext<SearchPickingState> context, int page)
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
            editMessageId: context.Instance.ListMessageId
        );
        context.Instance.ListMessageId = messageId;
        context.Instance.Page = page;
    }
}
