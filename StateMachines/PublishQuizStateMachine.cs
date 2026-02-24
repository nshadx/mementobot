using mementobot.Services;
using mementobot.Telegram;

namespace mementobot.StateMachines;

public class PublishQuizState
{
    public QuizPickingState QuizPickingState { get; set; } = null!;
    
    public int CurrentState { get; set; }
}

public class PublishQuizStateMachine : StateMachine<PublishQuizState>
{
    public Event PublishCommandReceivedEvent { get; private set; } = null!;
    
    public PublishQuizStateMachine(QuizPickingStateMachine quizPickingStateMachine)
    {
        ConfigureEvent(() => PublishCommandReceivedEvent, update => update.Message?.Text?.StartsWith("/publish") ?? false);
        
        ConfigureStates(state => state.CurrentState);

        ConfigureStateMachine(quizPickingStateMachine, x => x.QuizPickingState);
        
        Initially(
            When(PublishCommandReceivedEvent)
                .TransitionTo(quizPickingStateMachine, quizPickingStateMachine.Initial)
        );

        When(quizPickingStateMachine, quizPickingStateMachine.QuizPickedEvent)
            .Then(async context =>
            {
                var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
                var quizService = context.ServiceProvider.GetRequiredService<QuizService>();
        
                var chatId = context.Update.GetChatId();
                var quizId = context.Instance.QuizPickingState.QuizId;
                quizService.PublishQuiz(
                    quizId: quizId
                );

                _ = await messageManager.SendQuizPublishedMessage(
                    chatId: chatId
                );
            });
        
        SetCompletedOnFinal();
    }
}