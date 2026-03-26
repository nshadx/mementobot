using mementobot.Services;
using mementobot.Services.Messages;
using mementobot.Telegram;
using mementobot.Telegram.StateMachine;

namespace mementobot.StateMachines;

internal class SettingsMenuState
{
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
                .Then(async (BehaviorContext<SettingsMenuState> context, UserService userService, SettingsMenuMessage settingsMenu) =>
                {
                    var chatId = context.Update.GetChatId();
                    var userId = userService.GetOrCreateUser(chatId);
                    await settingsMenu.Apply(chatId, userService.GetUserSettings(userId));
                })
                .TransitionTo(WaitingAction),
            Ignore(ToggleRemindersEvent),
            Ignore(ToggleAdultContentEvent),
            Ignore(SetReminderHourEvent),
            Ignore(SetTemperatureEvent)
        );

        During(WaitingAction,
            When(ToggleRemindersEvent)
                .Then(async (BehaviorContext<SettingsMenuState> context, UserService userService, SettingsMenuMessage settingsMenu) =>
                {
                    var chatId = context.Update.GetChatId();
                    var userId = userService.GetOrCreateUser(chatId);
                    var settings = userService.GetUserSettings(userId);
                    settings.RemindersEnabled = !settings.RemindersEnabled;
                    userService.UpdateRemindersEnabled(userId, settings.RemindersEnabled);
                    await settingsMenu.Apply(chatId, settings);
                }),
            When(ToggleAdultContentEvent)
                .Then(async (BehaviorContext<SettingsMenuState> context, UserService userService, SettingsMenuMessage settingsMenu) =>
                {
                    var chatId = context.Update.GetChatId();
                    var userId = userService.GetOrCreateUser(chatId);
                    var settings = userService.GetUserSettings(userId);
                    settings.AdultContent = !settings.AdultContent;
                    userService.UpdateAdultContent(userId, settings.AdultContent);
                    await settingsMenu.Apply(chatId, settings);
                }),
            When(SetReminderHourEvent)
                .TransitionTo(reminderTimeStateMachine, reminderTimeStateMachine.Initial),
            When(SetTemperatureEvent)
                .TransitionTo(temperatureStateMachine, temperatureStateMachine.Initial)
        );

        When(reminderTimeStateMachine, reminderTimeStateMachine.Final.Enter)
            .Then(async (BehaviorContext<SettingsMenuState> context, UserService userService, SettingsMenuMessage settingsMenu) =>
            {
                var chatId = context.Update.GetChatId();
                var userId = userService.GetOrCreateUser(chatId);
                await settingsMenu.Apply(chatId, userService.GetUserSettings(userId));
            })
            .TransitionTo(WaitingAction);

        When(temperatureStateMachine, temperatureStateMachine.Final.Enter)
            .Then(async (BehaviorContext<SettingsMenuState> context, UserService userService, SettingsMenuMessage settingsMenu) =>
            {
                var chatId = context.Update.GetChatId();
                var userId = userService.GetOrCreateUser(chatId);
                await settingsMenu.Apply(chatId, userService.GetUserSettings(userId));
            })
            .TransitionTo(WaitingAction);
    }
}
