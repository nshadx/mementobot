using mementobot.Services;
using mementobot.Telegram;
using mementobot.Telegram.StateMachine;

namespace mementobot.StateMachines;

internal class ReminderTimeState
{
    public int PromptMessageId { get; set; }
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
                    var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
                    var messageId = await messageManager.SendReminderHourPrompt(context.Update.GetChatId());
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

                    if (!int.TryParse(text, out var hour) || hour < 0 || hour > 23)
                    {
                        await messageManager.DeleteMessage(chatId, context.Update.Message!.MessageId);
                        await messageManager.DeleteMessage(chatId, context.Instance.PromptMessageId);
                        var newId = await messageManager.SendReminderHourPrompt(chatId, isError: true);
                        context.Instance.PromptMessageId = newId;
                        return;
                    }

                    var userService = context.ServiceProvider.GetRequiredService<UserService>();
                    var userId = userService.GetOrCreateUser(chatId);
                    userService.UpdateReminderHour(userId, hour);

                    await messageManager.DeleteMessage(chatId, context.Update.Message!.MessageId);
                    await messageManager.DeleteMessage(chatId, context.Instance.PromptMessageId);

                    await context.TransitionTo(Final);
                })
        );

        SetCompletedOnFinal();
    }
}
