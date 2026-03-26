using mementobot.Handlers;
using mementobot.StateMachines;
using mementobot.Telegram;
using mementobot.Telegram.StateMachine;

namespace mementobot;

internal static class ProgramDependencyInjectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection RouteCommands()
        {
            services.AddRouting(builder =>
            {
                builder.Command<CreateNewQuizCommandHandler>("/new");
                builder.Command<HelpCommandHandler>("/help");
            });

            return services;
        }

        public IServiceCollection RouteStateMachines()
        {
            services.AddStateMachine<QuizPickingStateMachine, QuizPickingState>();
            services.AddStateMachine<AddQuizQuestionStateMachine, AddQuizQuestionState>();
            services.AddStateMachine<PublishQuizStateMachine, PublishQuizState>();
            services.AddStateMachine<QuizzingStateMachine, QuizzingState>();
            services.AddStateMachine<FavoritesPickingStateMachine, FavoritesPickingState>();
            services.AddStateMachine<RecentPickingStateMachine, RecentPickingState>();
            services.AddStateMachine<SearchPickingStateMachine, SearchPickingState>();
            services.AddStateMachine<QuizActionMenuStateMachine, QuizActionMenuState>();
            services.AddStateMachine<StartMenuStateMachine, StartMenuState>();
            services.AddStateMachine<ReminderTimeStateMachine, ReminderTimeState>();
            services.AddStateMachine<TemperatureStateMachine, TemperatureState>();
            services.AddStateMachine<SettingsMenuStateMachine, SettingsMenuState>();
            services.AddStateMachine<OwnedQuizzesPickingStateMachine, OwnedQuizzesPickingState>();
            services.AddStateMachine<MyQuizzesStateMachine, MyQuizzesState>();

            return services;
        }

        public IServiceCollection ConfigureAppPipeline()
        {
            services.ConfigurePipeline(x =>
            {
                x.Use<AnswerCallbackQueryMiddleware>();
                x.Use<DeleteCommandMiddleware>();
                x.Use<DeleteUserMessageMiddleware>();
                x.Use<StateMachineMiddleware>();
                x.Use<RouterMiddleware>();
            });

            return services;
        }
    }
}
