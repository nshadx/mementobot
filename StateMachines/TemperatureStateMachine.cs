using mementobot.Services;
using mementobot.Telegram;
using mementobot.Telegram.StateMachine;

namespace mementobot.StateMachines;

internal class TemperatureState
{
    public int PromptMessageId { get; set; }
    public int CurrentState { get; set; }
}

internal class TemperatureStateMachine : StateMachine<TemperatureState>
{
    public Event MessageReceivedEvent { get; private set; } = null!;

    public State<TemperatureState> WaitingInput { get; private set; } = null!;

    public TemperatureStateMachine()
    {
        ConfigureEvent(() => MessageReceivedEvent, update => update.Message?.Text is not null);
        ConfigureStates(state => state.CurrentState, () => WaitingInput);

        Initially(
            When(Initial.Enter)
                .Then(async context =>
                {
                    var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
                    var messageId = await messageManager.SendTemperaturePrompt(context.Update.GetChatId());
                    context.Instance.PromptMessageId = messageId;
                })
                .TransitionTo(WaitingInput),
            Ignore(MessageReceivedEvent)
        );

        During(WaitingInput,
            When(MessageReceivedEvent)
                .Then(async context =>
                {
                    var text = context.Update.Message!.Text!.Trim();
                    var chatId = context.Update.GetChatId();
                    var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();

                    if (!int.TryParse(text, out var temperature) || temperature < 0 || temperature > 100)
                    {
                        await messageManager.DeleteMessage(chatId, context.Update.Message!.MessageId);
                        await messageManager.DeleteMessage(chatId, context.Instance.PromptMessageId);
                        var newId = await messageManager.SendTemperaturePrompt(chatId, isError: true);
                        context.Instance.PromptMessageId = newId;
                        return;
                    }

                    var userService = context.ServiceProvider.GetRequiredService<UserService>();
                    var userId = userService.GetOrCreateUser(chatId);
                    userService.UpdateTemperature(userId, temperature);

                    await messageManager.DeleteMessage(chatId, context.Update.Message!.MessageId);
                    await messageManager.DeleteMessage(chatId, context.Instance.PromptMessageId);

                    await context.TransitionTo(Final);
                })
        );

        SetCompletedOnFinal();
    }
}
