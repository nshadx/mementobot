using mementobot.Services;
using mementobot.Services.Messages;
using mementobot.Telegram;
using mementobot.Telegram.StateMachine;

namespace mementobot.StateMachines;

internal class PublishQuizState
{
    public QuizPickingState QuizPickingState { get; set; } = null!;

    public int CurrentState { get; set; }
}

internal class PublishQuizStateMachine : StateMachine<PublishQuizState>
{
    public Event PublishCommandReceivedEvent { get; private set; } = null!;

    public PublishQuizStateMachine(QuizPickingStateMachine quizPickingStateMachine)
    {
        ConfigureEvent(() => PublishCommandReceivedEvent, update => update.Message?.Text?.StartsWith("/publish") ?? false);

        ConfigureStates(state => state.CurrentState);
        ConfigureStateMachine(quizPickingStateMachine, x => x.QuizPickingState);

        Initially(
            When(PublishCommandReceivedEvent)
                .Then(context =>
                {
                    context.Instance.QuizPickingState.Published = false;
                    context.Instance.QuizPickingState.OnlyCurrentUser = true;
                    return Task.CompletedTask;
                })
                .TransitionTo(quizPickingStateMachine, quizPickingStateMachine.Initial)
        );

        When(quizPickingStateMachine, quizPickingStateMachine.QuizPickedEvent)
            .Then(async context =>
            {
                var quizService = context.ServiceProvider.GetRequiredService<QuizService>();
                var quizPublishedMessage = context.ServiceProvider.GetRequiredService<QuizPublishedMessage>();

                var chatId = context.Update.GetChatId();
                var quizId = context.Instance.QuizPickingState.QuizId;
                quizService.PublishQuiz(quizId: quizId);

                await quizPublishedMessage.Apply(chatId, quizId);
            });

        SetCompletedOnFinal();
    }
}
