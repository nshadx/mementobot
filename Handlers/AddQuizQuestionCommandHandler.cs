using mementobot.StateMachines;
using mementobot.Telegram;

namespace mementobot.Handlers;

internal class AddQuizQuestionCommandHandler(
    StateMachineRepository stateMachineRepository,
    IServiceProvider serviceProvider
) : IRouteHandler
{
    public async Task Handle(Context context)
    {
        var chatId = context.Update.GetChatId();
        AddQuizQuestionStateMachine stateMachine = new();
        AddQuizQuestionState state = new();
        stateMachineRepository.SetCurrentStateMachine(chatId, stateMachine, state);

        var newContext = new BehaviorContext<object>(serviceProvider, state, stateMachine.AddCommandReceivedEvent, context.Update);
        await stateMachine.RaiseEvent(newContext);
    }
}
