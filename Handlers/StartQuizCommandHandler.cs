using mementobot.StateMachines;
using mementobot.Telegram;

namespace mementobot.Handlers;

internal class StartQuizCommandHandler(
    StateMachineRepository stateMachineRepository,
    IServiceProvider serviceProvider
) : IRouteHandler
{
    public async Task Handle(Context context)
    {
        var chatId = context.Update.GetChatId();
        QuizProgressStateMachine stateMachine = new();
        QuizProgressState state = new();
        stateMachineRepository.SetCurrentStateMachine(chatId, stateMachine, state);

        var newContext = new BehaviorContext<object>(serviceProvider, state, stateMachine.StartCommandReceivedEvent, context.Update);
        await stateMachine.RaiseEvent(newContext);
    }
}
