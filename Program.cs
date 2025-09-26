using Microsoft.EntityFrameworkCore;
using repetitorbot;
using repetitorbot.Constants;
using repetitorbot.Entities.States;
using repetitorbot.Extensions;
using repetitorbot.Handlers;
using repetitorbot.Middlewares;
using repetitorbot.Services;
using repetitorbot.Services.Common;
using Telegram.Bot;
using Telegram.Bot.Polling;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddHttpClient("TelegramBotClient")
                .RemoveAllLoggers()
                .AddTypedClient<ITelegramBotClient>((httpClient, _) => new TelegramBotClient(context.Configuration.GetConnectionString("TelegramBotToken") ?? throw new InvalidOperationException("empty bot token"), httpClient));
        services.AddHostedService<PollingService>();

        services.AddScoped<IUpdateHandler, UpdateHandler>();

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlite(context.Configuration.GetConnectionString("Database") ?? throw new InvalidOperationException("empty database connection string"));
        }, ServiceLifetime.Scoped);

        services.AddSingleton<TelegramFileService>();

        services.AddRouting(x =>
        {
            x.Command("start", x =>
            {
                x.Use<StartQuizHandler>();
                x.Use<RenderQuizPageHandler>();
            });

            x.Command("new", x =>
            {
                x.Use<StartQuizCreationHandler>();
            });

            x.Command("publish", x =>
            {
                x.Use<SelectQuizForPublishingHandler>();
                x.Use<RenderQuizPageHandler>();
            });

            x.Command("questions", x =>
            {
                x.Use<SelectQuizForQuestionAddingHandler>();
                x.Use<RenderQuizPageHandler>();
            });

            x.File("json", x =>
            {
                x.Use<ImportQuizHandler>();
            });

            x.Callback(x => x is Callback.ForwardPage, x =>
            {
                x.Use<ForwardPageHandler>();
                x.Use<RenderQuizPageHandler>();
            });

            x.Callback(x => x is Callback.BackPage, x =>
            {
                x.Use<BackPageHandler>();
                x.Use<RenderQuizPageHandler>();
            });

            x.When<QuizState>(x =>
            {
                x.Use<SkipQuestionMiddleware>();
                x.Use<SelectNextQuizQuestionHandler>();
                x.Use<RenderQuizQuestionHandler>();
            }, (_, context) => context.Update.CallbackQuery?.Data is string s && s.StartsWith(Callback.QuizQuestionIdPrefix));

            x.When<StartSelectQuizState>(x =>
            {
                x.Use<SelectQuizForStartingHandler>();
                x.Use<SetQuizQuestionsHandler>();
                x.Use<SelectNextQuizQuestionHandler>();
                x.Use<RenderQuizQuestionHandler>();
            }, (_, context) => context.Update.CallbackQuery?.Data is string s && s.StartsWith(Callback.QuizIdPrefix));

            x.When<PublishSelectQuizState>(x =>
            {
                x.Use<PublishQuizHandler>();
            }, (x, context) => context.Update.CallbackQuery?.Data is string s && s.StartsWith(Callback.QuizIdPrefix));

            x.When<QuestionsSelectQuizState>(x =>
            {
                x.Use<StartQuizQuestionsAddingHandler>();
                x.Use<RenderNextQuestionPropertyToFillHandler>();
            }, (x, context) => context.Update.CallbackQuery?.Data is string s && s.StartsWith(Callback.QuizIdPrefix));

            x.When<AddQuestionsState>(x =>
            {
                x.Use<SetQuizQuestionTypeHandler>();
                x.Use<RenderNextQuestionPropertyToFillHandler>();
            }, (x, context) => x.CurrentProperty == AddQuestionsProperty.QuestionType);
            x.When<AddQuestionsState>(x =>
            {
                x.Use<SetQuestionTextHandler>();
                x.Use<RenderNextQuestionPropertyToFillHandler>();
            }, (x, context) => x.CurrentProperty == AddQuestionsProperty.Question);
            x.When<AddQuestionsState>(x =>
            {
                x.Use<SetQuestionAnswerHandler>();
                x.Use<RenderNextQuestionPropertyToFillHandler>();
            }, (x, context) => x.CurrentProperty == AddQuestionsProperty.TextAnswer);

            x.When<QuizState>(x =>
            {
                x.Use<QuizQuestionAnswerHandler>();
                x.Use<SelectNextQuizQuestionHandler>();
                x.Use<RenderQuizQuestionHandler>();
            });

            x.When<CreateQuizState>(x =>
            {
                x.Use<SetQuizNameHandler>();
            }, (x, _) => x.Quiz.Name is null);
        });

        services.AddPipeline(x =>
        {
            x.Use<EnsureUserMiddleware>();
            x.Use<SetStateMiddleware>();
            x.Use<RouterMiddleware>();
            x.Use<SaveStateMiddleware>();
            x.Use<AnswerCallbackQueryMiddleware>();
        });
    })
    .Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    //await db.Database.EnsureDeletedAsync();
    await db.Database.EnsureCreatedAsync();
}

await host.RunAsync();