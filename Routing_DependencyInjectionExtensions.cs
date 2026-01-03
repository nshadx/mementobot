using mementobot.Constants;
using mementobot.Entities.States;
using mementobot.Handlers;
using mementobot.Middlewares;

namespace mementobot;

internal static class Routing_DependencyInjectionExtensions
{
    public static IServiceCollection AddRouting(this IServiceCollection services, Action<RouteBuilder> configure)
    {
        RouteBuilder instance = new(services);

        configure(instance);

        instance.Build();

        return services;
    }

    public static IHostApplicationBuilder RouteCommands(this IHostApplicationBuilder builder)
    {
        builder.Services.AddRouting(builder =>
        {
            builder.Command("start", builder =>
            {
                builder.Use<StartQuizHandler>();
                builder.Use<RenderQuizPageHandler>();
            });

            builder.Command("new", builder =>
            {
                builder.Use<StartQuizCreationHandler>();
            });

            builder.Command("publish", builder =>
            {
                builder.Use<SelectQuizForPublishingHandler>();
                builder.Use<RenderQuizPageHandler>();
            });

            builder.Command("questions", builder =>
            {
                builder.Use<SelectQuizForQuestionAddingHandler>();
                builder.Use<RenderQuizPageHandler>();
            });
        });
        
        return builder;
    }
    
    public static IHostApplicationBuilder RouteCallbacks(this IHostApplicationBuilder builder)
    {
        builder.Services.AddRouting(builder =>
        {
            builder.Callback(builder => builder is Callback.ForwardPage, builder =>
            {
                builder.Use<ForwardPageHandler>();
                builder.Use<RenderQuizPageHandler>();
            });

            builder.Callback(builder => builder is Callback.BackPage, builder =>
            {
                builder.Use<BackPageHandler>();
                builder.Use<RenderQuizPageHandler>();
            });
        });
        
        return builder;
    }
    
    public static IHostApplicationBuilder RouteFiles(this IHostApplicationBuilder builder)
    {
        builder.Services.AddRouting(builder =>
        {
            builder.File("json", builder =>
            {
                builder.Use<ImportQuizHandler>();
            });
        });
        
        return builder;
    }
    
    public static IHostApplicationBuilder RouteStates(this IHostApplicationBuilder builder)
    {
        builder.Services.AddRouting(builder =>
        {
            builder.When<QuizState>(builder =>
            {
                builder.Use<SkipQuestionMiddleware>();
                builder.Use<SelectNextQuizQuestionHandler>();
                builder.Use<RenderNextQuizQuestionHandler>();
            }, (_, context) => context.Update.CallbackQuery?.Data is string s && s.StartsWith(Callback.QuizQuestionIdPrefix));

            builder.When<StartSelectQuizState>(builder =>
            {
                builder.Use<SelectQuizForStartingHandler>();
                builder.Use<SetQuizQuestionsHandler>();
                builder.Use<SelectNextQuizQuestionHandler>();
                builder.Use<RenderNextQuizQuestionHandler>();
            }, (_, context) => context.Update.CallbackQuery?.Data is string s && s.StartsWith(Callback.QuizIdPrefix));

            builder.When<PublishSelectQuizState>(builder =>
            {
                builder.Use<PublishQuizHandler>();
            }, (x, context) => context.Update.CallbackQuery?.Data is string s && s.StartsWith(Callback.QuizIdPrefix));

            builder.When<QuestionsSelectQuizState>(builder =>
            {
                builder.Use<StartQuizQuestionsAddingHandler>();
                builder.Use<RenderNextQuestionPropertyToFillHandler>();
            }, (x, context) => context.Update.CallbackQuery?.Data is string s && s.StartsWith(Callback.QuizIdPrefix));

            builder.When<AddQuestionsState>(builder =>
            {
                builder.Use<SetQuizQuestionTypeHandler>();
                builder.Use<RenderNextQuestionPropertyToFillHandler>();
            }, (x, context) => x.CurrentProperty == AddQuestionsProperty.QuestionType);
            builder.When<AddQuestionsState>(builder =>
            {
                builder.Use<SetQuestionTextHandler>();
                builder.Use<RenderNextQuestionPropertyToFillHandler>();
            }, (x, context) => x.CurrentProperty == AddQuestionsProperty.Question);
            builder.When<AddQuestionsState>(builder =>
            {
                builder.Use<SetQuestionAnswerHandler>();
                builder.Use<RenderNextQuestionPropertyToFillHandler>();
            }, (x, context) => x.CurrentProperty == AddQuestionsProperty.TextAnswer);

            builder.When<QuizState>(builder =>
            {
                builder.Use<SelectMatchHandler>();
                builder.Use<RenderNextMatchHandler>();
                builder.Use<QuizQuestionAnswerHandler>();
                builder.Use<SelectNextQuizQuestionHandler>();
                builder.Use<RenderNextQuizQuestionHandler>();
            }, (_, context) => context.Update.CallbackQuery?.Data is string s && s.StartsWith(Callback.MatchIdPrefix));
            builder.When<QuizState>(builder =>
            {
                builder.Use<SelectPollVariantHandler>();
            }, (_, context) => context.Update.CallbackQuery?.Data is string s && s.StartsWith(Callback.PollVariantIdPrefix));
            builder.When<QuizState>(builder =>
            {
                builder.Use<QuizQuestionAnswerHandler>();
                builder.Use<SelectNextQuizQuestionHandler>();
                builder.Use<RenderNextQuizQuestionHandler>();
            }, (_, context) => context.Update.CallbackQuery?.Data == Callback.PollSubmit);
            builder.When<QuizState>(builder =>
            {
                builder.Use<QuizQuestionAnswerHandler>();
                builder.Use<SelectNextQuizQuestionHandler>();
                builder.Use<RenderNextQuizQuestionHandler>();
            });

            builder.When<CreateQuizState>(builder =>
            {
                builder.Use<SetQuizNameHandler>();
            }, (x, _) => x.Quiz.Name is null);
        });
        
        return builder;
    }
}