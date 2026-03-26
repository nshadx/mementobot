using mementobot.Services;
using mementobot.Services.Messages;
using mementobot.Telegram;
using mementobot.Telegram.StateMachine;

namespace mementobot.StateMachines;

internal class TemperatureState
{
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
                    var prompt = context.ServiceProvider.GetRequiredService<TemperaturePromptMessage>();
                    await prompt.Apply(context.Update.GetChatId(), false);
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
                    var prompt = context.ServiceProvider.GetRequiredService<TemperaturePromptMessage>();

                    context.ServiceProvider.GetRequiredService<IContextAccessor>().Current.DeleteUserMessage = true;

                    if (!int.TryParse(text, out var temperature) || temperature < 0 || temperature > 100)
                    {
                        await prompt.Apply(chatId, true);
                        return;
                    }

                    var userService = context.ServiceProvider.GetRequiredService<UserService>();
                    var userId = userService.GetOrCreateUser(chatId);
                    userService.UpdateTemperature(userId, temperature);

                    await prompt.Delete(chatId);
                    await context.TransitionTo(Final);
                })
        );

        SetCompletedOnFinal();
    }
}
