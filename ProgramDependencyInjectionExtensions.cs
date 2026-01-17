using mementobot.Handlers;
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
                //builder.Command<StartQuizCommandHandler>("/start");
                builder.Command<CreateNewQuizCommandHandler>("/new");
                builder.Command<PublishQuizCommandHandler>("/publish");
                builder.Command<AddQuizQuestionCommandHandler>("/add");
            });
        
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