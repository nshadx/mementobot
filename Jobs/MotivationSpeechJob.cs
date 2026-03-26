using Hangfire;
using mementobot.Services;
using mementobot.Services.Reminders;

namespace mementobot.Jobs;

internal class MotivationSpeechJob(
    QuizService quizService,
    UserService userService,
    IMotivationEngine motivationEngine,
    MessageManager messageManager,
    IBackgroundJobClient jobClient
)
{
    public async Task Execute(
        long telegramId,
        int userId,
        int quizId,
        int speechIndex,
        DateTime reminderStartedAt)
    {
        if (quizService.WasQuizCompletedAfter(userId, quizId, reminderStartedAt))
            return;

        var quiz = quizService.GetQuizHistoryEntry(userId, quizId);
        if (quiz is null) return;

        var settings = userService.GetUserSettings(userId);
        var speeches = motivationEngine.GetSpeeches(quiz, settings);

        if (speechIndex >= speeches.Count)
            return;

        var speech = speeches[speechIndex];
        await messageManager.SendReminderSpeech(telegramId, speech.Text);

        if (speechIndex + 1 < speeches.Count)
        {
            jobClient.Schedule<MotivationSpeechJob>(
                j => j.Execute(telegramId, userId, quizId, speechIndex + 1, reminderStartedAt),
                speech.DelayUntilNext
            );
        }
    }
}
