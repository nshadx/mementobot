using mementobot.Handlers;
using mementobot.StateMachines;
using mementobot.Telegram;

namespace mementobot;

internal static class ProgramDependencyInjectionExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder RouteCommands()
        {
            builder.Services.AddRouting(builder =>
            {
                builder.Command<CreateNewQuizCommandHandler>("/new");
            });
        
            return builder;
        }

        public IHostApplicationBuilder RouteStateMachines()
        {
            builder.AddStateMachine<AddQuizQuestionStateMachine, AddQuizQuestionState>();
            builder.AddStateMachine<PublishQuizStateMachine, PublishQuizState>();
            builder.AddStateMachine<QuizProgressStateMachine, QuizProgressState>();

            return builder;
        }
        
        public IHostApplicationBuilder ConfigureAppPipeline()
        {
            builder.ConfigurePipeline(x =>
            {
                x.Use<RouterMiddleware>();
            });

            return builder;
        }
    }
}