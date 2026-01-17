using mementobot.Services;
using mementobot.Telegram;
using Telegram.Bot;

namespace mementobot.StateMachines;

public class PublishQuizState
{
    public List<(Quiz Quiz, int Page)> Quizzes { get; set; } = [];
    public int MessageId { get; set; }
    public int Page { get; set; }
    public int QuizId { get; set; }
    
    public int CurrentState { get; set; }
}

public class PublishQuizStateMachine : StateMachine<PublishQuizState>
{
    public Event PublishCommandReceivedEvent { get; private set; } = null!;
    public Event PageForwardEvent { get; private set; } = null!;
    public Event PageBackwardEvent { get; private set; } = null!;
    public Event QuizPickedEvent { get; private set; } = null!;

    public State<PublishQuizState> QuizPicking { get; private set; } = null!;
    
    public PublishQuizStateMachine()
    {
        ConfigureEvent(() => PublishCommandReceivedEvent, update => update.Message?.Text?.StartsWith("/publish") ?? false);
        ConfigureEvent(() => PageForwardEvent, update => update.CallbackQuery?.Data is "forward");
        ConfigureEvent(() => PageBackwardEvent, update => update.CallbackQuery?.Data is "backward");
        ConfigureEvent(() => QuizPickedEvent, update => int.TryParse(update.CallbackQuery?.Data, out _));
        
        ConfigureStates(state => state.CurrentState, () => QuizPicking);
        
        Initially(
            When(PublishCommandReceivedEvent)
                .Then(BuildSelectingPages)
                .TransitionTo(QuizPicking),
            Ignore(QuizPickedEvent),
            Ignore(PageForwardEvent),
            Ignore(PageBackwardEvent)
        );

        During(QuizPicking,
            When(PageForwardEvent)
                .Then(ForwardPage)
        );
        
        During(QuizPicking,
            When(PageBackwardEvent)
                .Then(BackwardPage)
        );
        
        During(QuizPicking,
            When(QuizPickedEvent)
                .Then(PickQuiz)
                .TransitionTo(Final)
        );
        
        Finally(x => x.Then(PublishQuiz));
        
        SetCompletedOnFinal();
    }
    
    private async Task BuildSelectingPages(BehaviorContext<PublishQuizState> context)
    {
        var quizService = context.ServiceProvider.GetRequiredService<QuizService>();
        var userService = context.ServiceProvider.GetRequiredService<UserService>();
        var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();

        var chatId = context.Update.GetChatId();
        var userId = userService.GetOrCreateUser(
            telegramId: chatId
        );
        var quizzes = quizService.GetUserQuizzes(
            userId: userId,
            published: false
        );

        var list = context.Instance.Quizzes;
        var counter = 1;
        var page = 1;
        context.Instance.Page = 1;
        foreach (var quiz in quizzes)
        {
            if (counter <= 6)
            {
                list.Add((quiz, page));
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
            return;
        }
    }

    private async Task ForwardPage(BehaviorContext<PublishQuizState> context)
    {
        var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
        var client = context.ServiceProvider.GetRequiredService<ITelegramBotClient>();
        
        await client.AnswerCallbackQuery(
            callbackQueryId: context.Update.CallbackQuery!.Id
        );
        
        var page = context.Instance.Page;
        if (++page > context.Instance.Quizzes.Max(x => x.Page))
        {
            return;
        }
        
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

    private async Task BackwardPage(BehaviorContext<PublishQuizState> context)
    {
        var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
        var client = context.ServiceProvider.GetRequiredService<ITelegramBotClient>();
        
        await client.AnswerCallbackQuery(
            callbackQueryId: context.Update.CallbackQuery!.Id
        );
        
        var page = context.Instance.Page;
        if (--page < context.Instance.Quizzes.Min(x => x.Page))
        {
            return;
        }
        
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

    private async Task PickQuiz(BehaviorContext<PublishQuizState> context)
    {
        var client = context.ServiceProvider.GetRequiredService<ITelegramBotClient>();
        
        await client.AnswerCallbackQuery(
            callbackQueryId: context.Update.CallbackQuery!.Id
        );
        
        var chatId = context.Update.GetChatId();
        var quizId = int.Parse(context.Update.CallbackQuery?.Data!);
        context.Instance.QuizId = quizId;
        
        await client.DeleteMessage(
            chatId: chatId,
            messageId: context.Instance.MessageId
        );
    }

    private async Task PublishQuiz(BehaviorContext<PublishQuizState> context)
    {
        var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
        var quizService = context.ServiceProvider.GetRequiredService<QuizService>();
        
        var chatId = context.Update.GetChatId();
        var quizId = context.Instance.QuizId;
        quizService.PublishQuiz(
            quizId: quizId
        );

        var messageId = await messageManager.SendQuizPublishedMessage(
            chatId: chatId
        );
        context.Instance.MessageId = messageId;
    }
}