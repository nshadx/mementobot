using mementobot.StateMachines;
using mementobot.Telegram;

namespace mementobot.Handlers;

internal class PublishQuizCommandHandler(
    StateMachineRepository stateMachineRepository,
    IServiceProvider serviceProvider
) : IRouteHandler
{
    public async Task Handle(Context context)
    {
        var chatId = context.Update.GetChatId();
        PublishQuizStateMachine stateMachine = new();
        PublishQuizState state = new();
        stateMachineRepository.SetCurrentStateMachine(chatId, stateMachine, state);

        var newContext = new BehaviorContext<object>(serviceProvider, state, stateMachine.PublishCommandReceivedEvent, context.Update);
        await stateMachine.RaiseEvent(newContext);
    }
}
