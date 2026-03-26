using FluentMigrator.Runner;
using mementobot.Jobs;
using mementobot.Migrations;
using mementobot.Services.Messages;
using mementobot.Services.Quizzing;
using mementobot.Services.Reminders;
using mementobot.Telegram.Messages;
using Microsoft.Data.Sqlite;

namespace mementobot.Services;

internal static class ServicesDependencyInjectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddServices()
        {
            services.AddSingleton<QuizService>();
            services.AddSingleton<UserService>();

            services.AddSingleton<IQuizSelectionEngine, QuizSelectionEngine>();
            services.AddSingleton<IMotivationEngine, DefaultMotivationEngine>();
            services.AddSingleton<DailyReminderJob>();
            services.AddSingleton<MotivationSpeechJob>();

            services.AddSingleton<IAnswerEvaluator, FuzzyAnswerEvaluator>();
            services.AddSingleton<IRequeuingStrategy, DefaultRequeuingStrategy>();
            services.AddSingleton<QuestionEngineService>();
            services.AddSingleton<IQuestionEngine>(sp => sp.GetRequiredService<QuestionEngineService>());
            services.AddSingleton<IQuizSessionStatistics>(sp => sp.GetRequiredService<QuestionEngineService>());

            services.AddSingleton<IMessageStore, MessageStore>();
            services.AddSingleton<QuizListMessage>();
            services.AddSingleton<QuizActionMenuMessage>();
            services.AddSingleton<StartMenuMessage>();
            services.AddSingleton<SettingsMenuMessage>();
            services.AddSingleton<SearchPromptMessage>();
            services.AddSingleton<AddQuizQuestionMessage>();
            services.AddSingleton<ReminderHourPromptMessage>();
            services.AddSingleton<TemperaturePromptMessage>();
            services.AddSingleton<QuizQuestionMessage>();
            services.AddSingleton<NewQuizMessage>();
            services.AddSingleton<QuestionAddedMessage>();
            services.AddSingleton<HelpMessage>();
            services.AddSingleton<HelpGraphMessage>();
            services.AddSingleton<QuizPublishedMessage>();
            services.AddSingleton<CompletedAnsweringMessage>();
            services.AddSingleton<CompletedQuizMessage>();
            services.AddSingleton<ReminderSpeechMessage>();
            services.AddSingleton<MyQuizzesMenuMessage>();

            return services;
        }

        public IServiceCollection AddDb(string connectionString)
        {
            services.AddSingleton<DbService>();
            services.AddFluentMigratorCore().ConfigureRunner(x =>
            {
                x.AddSQLite();
                x.WithGlobalConnectionString(connectionString);
                x.ScanIn(typeof(InitialMigration).Assembly);
            });
            services.AddSingleton(_ => new SqliteConnection(connectionString));

            return services;
        }
    }
}
