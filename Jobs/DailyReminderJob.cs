using Hangfire;
using mementobot.Services;
using mementobot.Services.Reminders;

namespace mementobot.Jobs;

internal class DailyReminderJob(
    UserService userService,
    IQuizSelectionEngine selectionEngine,
    IBackgroundJobClient jobClient
)
{
    public void Execute()
    {
        foreach (var (userId, telegramId) in userService.GetUsersForReminder(DateTime.UtcNow.Hour))
        {
            var quiz = selectionEngine.SelectForUser(userId);
            if (quiz is null) continue;

            jobClient.Enqueue<MotivationSpeechJob>(j =>
                j.Execute(telegramId, userId, quiz.QuizId, 0, DateTime.UtcNow));
        }
    }
}
