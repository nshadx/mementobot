using System.Text.RegularExpressions;
using FuzzySharp;

namespace mementobot.Services.Quizzing;

internal partial class FuzzyAnswerEvaluator : IAnswerEvaluator
{
    public EvaluationResult Evaluate(QuizQuestion question, IAnswerResult answer)
    {
        if (answer is not TextAnswerResult textAnswer)
            return new EvaluationResult(0);

        var score = Fuzz.TokenSetRatio(
            Normalize(question.Answer),
            Normalize(textAnswer.Text)
        );

        return new EvaluationResult(score / 100.0);
    }

    private static string Normalize(string s) =>
        WsRegex().Replace(s.ToLowerInvariant(), " ").Trim();

    [GeneratedRegex(@"[^\w\s]")]
    private static partial Regex WsRegex();
}