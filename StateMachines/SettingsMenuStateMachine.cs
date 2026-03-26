using mementobot.Services;
using mementobot.Telegram;
using mementobot.Telegram.StateMachine;

namespace mementobot.StateMachines;

internal class SettingsMenuState
{
    public int MenuMessageId { get; set; }
    public bool RemindersEnabled { get; set; }
    public bool AdultContent { get; set; }
    public ReminderTimeState ReminderTimeState { get; set; } = null!;
    public TemperatureState TemperatureState { get; set; } = null!;
    public int CurrentState { get; set; }
}

internal class SettingsMenuStateMachine : StateMachine<SettingsMenuState>
{
    public Event SettingsCommandEvent { get; private set; } = null!;
    public Event ToggleRemindersEvent { get; private set; } = null!;
    public Event ToggleAdultContentEvent { get; private set; } = null!;
    public Event SetReminderHourEvent { get; private set; } = null!;
    public Event SetTemperatureEvent { get; private set; } = null!;

    public State<SettingsMenuState> WaitingAction { get; private set; } = null!;

    public SettingsMenuStateMachine(
        ReminderTimeStateMachine reminderTimeStateMachine,
        TemperatureStateMachine temperatureStateMachine)
    {
        ConfigureEvent(() => SettingsCommandEvent, update => update.Message?.Text?.Trim() is "/settings");
        ConfigureEvent(() => ToggleRemindersEvent, update => update.CallbackQuery?.Data is "settings:reminders");
        ConfigureEvent(() => ToggleAdultContentEvent, update => update.CallbackQuery?.Data is "settings:adult");
        ConfigureEvent(() => SetReminderHourEvent, update => update.CallbackQuery?.Data is "settings:hour");
        ConfigureEvent(() => SetTemperatureEvent, update => update.CallbackQuery?.Data is "settings:temperature");

        ConfigureStates(state => state.CurrentState, () => WaitingAction);
        ConfigureStateMachine(reminderTimeStateMachine, x => x.ReminderTimeState);
        ConfigureStateMachine(temperatureStateMachine, x => x.TemperatureState);

        Initially(
            When(SettingsCommandEvent)
                .Then(async context =>
                {
                    var userService = context.ServiceProvider.GetRequiredService<UserService>();
                    var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
                    var chatId = context.Update.GetChatId();
                    var userId = userService.GetOrCreateUser(chatId);
                    var settings = userService.GetUserSettings(userId);

                    context.Instance.RemindersEnabled = settings.RemindersEnabled;
                    context.Instance.AdultContent = settings.AdultContent;

                    var messageId = await messageManager.SendSettingsMenu(chatId, settings);
                    context.Instance.MenuMessageId = messageId;
                })
                .TransitionTo(WaitingAction),
            Ignore(ToggleRemindersEvent),
            Ignore(ToggleAdultContentEvent),
            Ignore(SetReminderHourEvent),
            Ignore(SetTemperatureEvent)
        );

        During(WaitingAction,
            When(ToggleRemindersEvent)
                .Then(async context =>
                {
                    var userService = context.ServiceProvider.GetRequiredService<UserService>();
                    var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
                    var chatId = context.Update.GetChatId();
                    var userId = userService.GetOrCreateUser(chatId);

                    context.Instance.RemindersEnabled = !context.Instance.RemindersEnabled;
                    userService.UpdateRemindersEnabled(userId, context.Instance.RemindersEnabled);

                    var settings = userService.GetUserSettings(userId);
                    await messageManager.EditSettingsMenu(chatId, context.Instance.MenuMessageId, settings);
                }),
            When(ToggleAdultContentEvent)
                .Then(async context =>
                {
                    var userService = context.ServiceProvider.GetRequiredService<UserService>();
                    var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
                    var chatId = context.Update.GetChatId();
                    var userId = userService.GetOrCreateUser(chatId);

                    context.Instance.AdultContent = !context.Instance.AdultContent;
                    userService.UpdateAdultContent(userId, context.Instance.AdultContent);

                    var settings = userService.GetUserSettings(userId);
                    await messageManager.EditSettingsMenu(chatId, context.Instance.MenuMessageId, settings);
                }),
            When(SetReminderHourEvent)
                .TransitionTo(reminderTimeStateMachine, reminderTimeStateMachine.Initial),
            When(SetTemperatureEvent)
                .TransitionTo(temperatureStateMachine, temperatureStateMachine.Initial)
        );

        When(reminderTimeStateMachine, reminderTimeStateMachine.Final.Enter)
            .Then(async context =>
            {
                var userService = context.ServiceProvider.GetRequiredService<UserService>();
                var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
                var chatId = context.Update.GetChatId();
                var userId = userService.GetOrCreateUser(chatId);
                var settings = userService.GetUserSettings(userId);

                context.Instance.RemindersEnabled = settings.RemindersEnabled;
                context.Instance.AdultContent = settings.AdultContent;

                var messageId = await messageManager.SendSettingsMenu(chatId, settings);
                context.Instance.MenuMessageId = messageId;
            })
            .TransitionTo(WaitingAction);

        When(temperatureStateMachine, temperatureStateMachine.Final.Enter)
            .Then(async context =>
            {
                var userService = context.ServiceProvider.GetRequiredService<UserService>();
                var messageManager = context.ServiceProvider.GetRequiredService<MessageManager>();
                var chatId = context.Update.GetChatId();
                var userId = userService.GetOrCreateUser(chatId);
                var settings = userService.GetUserSettings(userId);

                context.Instance.RemindersEnabled = settings.RemindersEnabled;
                context.Instance.AdultContent = settings.AdultContent;

                var messageId = await messageManager.SendSettingsMenu(chatId, settings);
                context.Instance.MenuMessageId = messageId;
            })
            .TransitionTo(WaitingAction);
    }
}
