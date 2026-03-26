using mementobot.Services;
using mementobot.Services.Messages;
using mementobot.Telegram;
using mementobot.Telegram.StateMachine;

namespace mementobot.StateMachines;

internal class ReminderTimeState
{
    public int CurrentState { get; set; }
}

internal class ReminderTimeStateMachine : StateMachine<ReminderTimeState>
{
    public Event MessageReceivedEvent { get; private set; } = null!;

    public State<ReminderTimeState> WaitingInput { get; private set; } = null!;

    public ReminderTimeStateMachine()
    {
        ConfigureEvent(() => MessageReceivedEvent, update => update.Message?.Text is not null);
        ConfigureStates(state => state.CurrentState, () => WaitingInput);

        Initially(
            When(Initial.Enter)
                .Then(async context =>
                {
                    var prompt = context.ServiceProvider.GetRequiredService<ReminderHourPromptMessage>();
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
                    var prompt = context.ServiceProvider.GetRequiredService<ReminderHourPromptMessage>();

                    context.ServiceProvider.GetRequiredService<IContextAccessor>().Current.DeleteUserMessage = true;

                    if (!int.TryParse(text, out var hour) || hour < 0 || hour > 23)
                    {
                        await prompt.Apply(chatId, true);
                        return;
                    }

                    var userService = context.ServiceProvider.GetRequiredService<UserService>();
                    var userId = userService.GetOrCreateUser(chatId);
                    userService.UpdateReminderHour(userId, hour);

                    await prompt.Delete(chatId);
                    await context.TransitionTo(Final);
                })
        );

        SetCompletedOnFinal();
    }
}
