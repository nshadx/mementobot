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
            });

            return services;
        }

        public IServiceCollection RouteStateMachines()
        {
            services.AddStateMachine<QuizPickingStateMachine, QuizPickingState>();
            services.AddStateMachine<AddQuizQuestionStateMachine, AddQuizQuestionState>();
            services.AddStateMachine<PublishQuizStateMachine, PublishQuizState>();
            services.AddStateMachine<QuizProgressStateMachine, QuizProgressState>();

            return services;
        }

        public IServiceCollection ConfigureAppPipeline()
        {
            services.ConfigurePipeline(x =>
            {
                x.Use<AnswerCallbackQueryMiddleware>();
                x.Use<DeleteCommandMiddleware>();
                x.Use<StateMachineMiddleware>();
                x.Use<RouterMiddleware>();
            });

            return services;
        }
    }
}
