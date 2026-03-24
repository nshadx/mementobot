namespace mementobot.Services.Quizzing;

internal class DefaultRequeuingStrategy : IRequeuingStrategy
{
    private const int MaxAttempts = 1;

    public int ComputeShift(EvaluationResult result, int attemptNumber)
    {
        if (attemptNumber >= MaxAttempts)
            return 0;

        var score = (int)(result.Score * 100);
        return score switch
        {
            >= 80 => 0,
            >= 50 => 5,
            _ => 3
        };
    }
}