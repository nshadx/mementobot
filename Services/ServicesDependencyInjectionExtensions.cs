using FluentMigrator.Runner;
using mementobot.Migrations;
using mementobot.Services.Quizzing;
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

            services.AddSingleton<IAnswerEvaluator, FuzzyAnswerEvaluator>();
            services.AddSingleton<IRequeuingStrategy, DefaultRequeuingStrategy>();
            services.AddSingleton<QuestionEngineService>();
            services.AddSingleton<IQuestionEngine>(sp => sp.GetRequiredService<QuestionEngineService>());
            services.AddSingleton<IQuizSessionStatistics>(sp => sp.GetRequiredService<QuestionEngineService>());

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
