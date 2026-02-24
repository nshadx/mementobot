using mementobot.Services;
using mementobot.Telegram;

namespace mementobot.StateMachines;

public class AddQuizQuestionState
{
    public QuizPickingState QuizPickingState { get; set; } = null!;

    public string Question { get; set; } = null!;
    public string Answer { get; set; } = null!;
    public int MessageId { get; set; }

    public int CurrentState { get; set; }
}

public class AddQuizQuestionStateMachine : StateMachine<AddQuizQuestionState>
{
    public Event AddCommandReceivedEvent { get; private set; } = null!;
    public Event MessageReceivedEvent { get; private set; } = null!;

    public State<AddQuizQuestionState> FillingQuestion { get; private set; } = null!;
    public State<AddQuizQuestionState> FillingAnswer { get; private set; } = null!;

    public AddQuizQuestionStateMachine(QuizPickingStateMachine quizPickingStateMachine)
    {
        ConfigureEvent(() => AddCommandReceivedEvent, update => update.Message?.Text?.StartsWith("/add") ?? false);
        ConfigureEvent(() => MessageReceivedEvent, update => update.Message?.Text is not null);

        ConfigureStates(state => state.CurrentState, () => FillingQuestion, () => FillingAnswer);

        ConfigureStateMachine(quizPickingStateMachine, x => x.QuizPickingState);

        Initially(
            When(AddCommandReceivedEvent)
                .Then(context =>
                {
                    context.Instance.QuizPickingState.Published = false;
                    return Task.CompletedTask;
                })
                .TransitionTo(quizPickingStateMachine, quizPickingStateMachine.Initial),
            Ignore(MessageReceivedEvent)
        );

        When(quizPickingStateMachine, quizPickingStateMachine.QuizPickedEvent)
            .Then(async context =>
            {
                var chatId = context.Update.GetChatId();
                var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
                var messageId = await messageManager.EnterQuestionMessage(
                    chatId: chatId
                );
                context.Instance.MessageId = messageId;
            })
            .TransitionTo(FillingQuestion);

        During(FillingQuestion,
            When(MessageReceivedEvent)
                .Then(context =>
                {
                    context.Instance.Question = context.Update.Message?.Text!;
                    return Task.CompletedTask;
                })
                .Then(async context =>
                {
                    var chatId = context.Update.GetChatId();
                    var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
                    await messageManager.DeleteMessage(chatId, context.Instance.MessageId);
                    await messageManager.DeleteMessage(chatId, context.Update.Message!.MessageId);
                    var messageId = await messageManager.EnterAnswerMessage(chatId: chatId);
                    context.Instance.MessageId = messageId;
                })
                .TransitionTo(FillingAnswer)
        );

        During(FillingAnswer,
            When(MessageReceivedEvent)
                .Then(context =>
                {
                    context.Instance.Answer = context.Update.Message?.Text!;
                    return Task.CompletedTask;
                })
                .Then(async context =>
                {
                    var chatId = context.Update.GetChatId();
                    var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
                    await messageManager.DeleteMessage(chatId, context.Instance.MessageId);
                    await messageManager.DeleteMessage(chatId, context.Update.Message!.MessageId);
                })
                .TransitionTo(Final)
        );

        Finally(x => x.Then(AddQuizQuestion));

        SetCompletedOnFinal();
    }

    private static Task AddQuizQuestion(BehaviorContext<AddQuizQuestionState> context)
    {
        var quizService = context.ServiceProvider.GetRequiredService<QuizService>();

        var quizId = context.Instance.QuizPickingState.QuizId;
        var question = context.Instance.Question;
        var answer = context.Instance.Answer;

        quizService.AddQuizQuestion(
            quizId: quizId,
            question: question,
            answer: answer
        );

        return Task.CompletedTask;
    }
}