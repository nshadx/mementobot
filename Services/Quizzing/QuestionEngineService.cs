namespace mementobot.Services.Quizzing;

internal class QuestionEngineService(IRequeuingStrategy requeuingStrategy) : IQuestionEngine, IQuizSessionStatistics
{
    public int? GetCurrentQuestionId(QuestionQueue queue) =>
        queue.QuestionIds.Count > 0 ? queue.QuestionIds[0] : null;

    public int? Advance(QuestionQueue queue, EvaluationResult result)
    {
        if (queue.QuestionIds.Count == 0)
            return null;

        var questionId = queue.QuestionIds[0];
        queue.QuestionIds.RemoveAt(0);

        queue.Attempts.Add(new QuestionAttempt(questionId, result.Score, DateTime.UtcNow));

        var attemptNumber = queue.Attempts.Count(a => a.QuestionId == questionId);
        var shift = requeuingStrategy.ComputeShift(result, attemptNumber);
        if (shift > 0)
        {
            var insertAt = Math.Min(shift, queue.QuestionIds.Count);
            queue.QuestionIds.Insert(insertAt, questionId);
        }

        return GetCurrentQuestionId(queue);
    }

    public int? Skip(QuestionQueue queue)
    {
        if (queue.QuestionIds.Count == 0)
            return null;

        var questionId = queue.QuestionIds[0];
        queue.QuestionIds.RemoveAt(0);

        queue.SkipCounts.TryGetValue(questionId, out var skipCount);
        skipCount++;
        queue.SkipCounts[questionId] = skipCount;

        var shift = requeuingStrategy.ComputeShift(new EvaluationResult(0), skipCount);
        if (shift > 0)
        {
            var insertAt = Math.Min(shift, queue.QuestionIds.Count);
            queue.QuestionIds.Insert(insertAt, questionId);
        }

        return GetCurrentQuestionId(queue);
    }

    public bool IsCompleted(QuestionQueue queue) => queue.QuestionIds.Count == 0;

    // === IQuizSessionStatistics ===

    public int TotalQuestions(QuestionQueue queue)
    {
        var fromAttempts = queue.Attempts.Select(a => a.QuestionId);
        var fromSkipped = queue.SkipCounts.Keys;
        return fromAttempts.Concat(queue.QuestionIds).Concat(fromSkipped).Distinct().Count();
    }

    public int Answered(QuestionQueue queue) =>
        queue.Attempts
            .Select(a => a.QuestionId)
            .Distinct()
            .Count(id => !queue.QuestionIds.Contains(id));

    public int Skipped(QuestionQueue queue) =>
        queue.SkipCounts.Keys
            .Count(id => !queue.Attempts.Any(a => a.QuestionId == id) && !queue.QuestionIds.Contains(id));

    public int Remaining(QuestionQueue queue) =>
        queue.QuestionIds.Distinct().Count();

    public double AverageScore(QuestionQueue queue) =>
        queue.Attempts.Count > 0 ? queue.Attempts.Average(a => a.Score) : 0;

    public IReadOnlyList<QuestionAttempt> GetAttempts(QuestionQueue queue, int questionId) =>
        queue.Attempts.Where(a => a.QuestionId == questionId).ToList();

    public IReadOnlyList<QuestionAttempt> AllAttempts(QuestionQueue queue) => queue.Attempts;
}