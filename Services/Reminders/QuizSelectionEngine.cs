namespace mementobot.Services.Reminders;

internal class QuizSelectionEngine(QuizService quizService) : IQuizSelectionEngine
{
    public QuizHistoryEntry? SelectForUser(int userId)
    {
        var now = DateTime.UtcNow;

        return quizService.GetQuizHistoryForUser(userId)
            .Select(h => new { Entry = h, DaysSince = (now - h.LastPlayed).TotalDays, Interval = GetInterval(h.AvgScore) })
            .Where(x => x.DaysSince >= x.Interval)
            .OrderByDescending(x => x.DaysSince - x.Interval)
            .Select(x => x.Entry)
            .FirstOrDefault();
    }

    private static int GetInterval(double avgScore) => avgScore switch
    {
        >= 0.8 => 7,
        >= 0.5 => 3,
        _      => 1
    };
}
