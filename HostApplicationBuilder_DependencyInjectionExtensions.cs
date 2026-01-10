using mementobot.Handlers;
using mementobot.Middlewares;
using mementobot.Services;

namespace mementobot;

internal static class HostApplicationBuilder_DependencyInjectionExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder RouteCommands()
        {
            builder.Services.AddRouting(builder =>
            {
                builder.Command<StartQuizCommandHandler>("/start");
                builder.Command<CreateNewQuizCommandHandler>("/new");
                builder.Command<PublishQuizCommandHandler>("/publish");
                builder.Command<AddQuizQuestionCommandHandler>("/add");
            });
        
            return builder;
        }

        public IHostApplicationBuilder RouteCallbacks()
        {
            builder.Services.AddRouting(builder =>
            {
                builder.Callback<ForwardPageCallbackHandler>(x => x.Equals("forward", StringComparison.InvariantCultureIgnoreCase));
                builder.Callback<BackPageCallbackHandler>(x => x.Equals("back", StringComparison.InvariantCultureIgnoreCase));
                builder.Callback<SkipQuestionCallbackHandler>(x => x.Equals("skip", StringComparison.InvariantCultureIgnoreCase));
            });
        
            return builder;
        }

        public IHostApplicationBuilder RouteStates()
        {
            builder.Services.AddRouting(builder =>
            {
                builder.When<AnswerMessageHandler>(x => x.CurrentState is StateType.QuizProgressUserState && x.Update.Message?.Text is not null);
                builder.When<StartQuizCallbackHandler>(x => x.CurrentState is StateType.SelectQuizUserState && x.ActionType == ActionType.StartQuiz && x.Update.CallbackQuery?.Data is not null);
                builder.When<PublishQuizCallbackHandler>(x => x.CurrentState is StateType.SelectQuizUserState && x.ActionType == ActionType.Publish && x.Update.CallbackQuery?.Data is not null);
                builder.When<AddQuizQuestionCallbackHandler>(x => x.CurrentState is StateType.SelectQuizUserState && x.ActionType == ActionType.AddQuizQuestion && x.Update.CallbackQuery?.Data is not null);
                builder.When<AddQuizQuestionMessageHandler>(x => x.CurrentState is StateType.AddQuestionUserState && x.Update.Message?.Text is not null);
            });
        
            return builder;
        }
        
        public IHostApplicationBuilder AddAppPipeline()
        {
            builder.Services.AddPipeline(x =>
            {
                x.Use<EnsureUserMiddleware>();
                x.Use<EnsureStateMiddleware>();
                x.Use<RouterMiddleware>();
            });

            return builder;
        }
    }
}