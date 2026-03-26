using System.Text;
using mementobot.Services.Quizzing;
using mementobot.Telegram.Messages;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace mementobot.Services.Messages;

internal record CompletedQuizData(
    IQuizSessionStatistics Statistics,
    QuestionQueue Queue,
    IReadOnlyDictionary<int, QuizQuestion> Questions);

internal class CompletedQuizMessage(ITelegramBotClient client, IMessageStore store)
    : BotMessage<CompletedQuizData>(client, store)
{
    protected override async Task<int> Send(long chatId, CompletedQuizData data)
    {
        var total = data.Statistics.TotalQuestions(data.Queue);
        var answered = data.Statistics.Answered(data.Queue);
        var skipped = data.Statistics.Skipped(data.Queue);
        var avgScore = (int)(data.Statistics.AverageScore(data.Queue) * 100);
        var attempts = data.Statistics.AllAttempts(data.Queue);
        var wrongAttempts = attempts.Count(a => a.Score < 1.0);

        var medal = avgScore switch
        {
            >= 90 => "🏆",
            >= 70 => "🥈",
            >= 50 => "🥉",
            _ => "📉"
        };

        var sb = new StringBuilder();
        sb.AppendLine("📋 *Опросник завершён!*");
        sb.AppendLine();
        sb.AppendLine($"{medal} Средний балл: *{avgScore}%*");
        sb.AppendLine($"📊 Вопросов: {answered}/{total}");
        if (skipped > 0) sb.AppendLine($"⏭️ Пропущено: {skipped}");
        if (wrongAttempts > 0) sb.AppendLine($"🔄 Неверных попыток: {wrongAttempts}");

        if (total > 0)
        {
            var byQuestion = attempts
                .GroupBy(a => a.QuestionId)
                .OrderBy(g => g.Min(a => a.Timestamp));

            sb.AppendLine();
            sb.AppendLine("─────────────────────");

            foreach (var group in byQuestion)
            {
                var q = data.Questions[group.Key];
                var bestScore = (int)(group.Max(a => a.Score) * 100);
                var wrongCount = group.Count(a => a.Score < 1.0);
                var icon = bestScore >= 80 ? "✅" : bestScore >= 50 ? "⚠️" : "❌";
                var retryNote = wrongCount > 0 ? $" ×{wrongCount}" : "";
                sb.AppendLine($"{icon} {q.Question} — *{bestScore}%*{retryNote}");
            }
        }

        var msg = await client.SendMessage(chatId, sb.ToString(), parseMode: ParseMode.Markdown);
        return msg.Id;
    }
}
