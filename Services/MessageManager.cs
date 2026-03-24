using System.Text;
using mementobot.Services.Quizzing;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace mementobot.Services;

internal class MessageManager(
    ITelegramBotClient client
)
{
    public Task DeleteMessage(long chatId, int messageId)
    {
        return client.DeleteMessage(
            chatId: chatId,
            messageId: messageId
        );
    }

    public async Task<int> EnterQuestionMessage(long chatId)
    {
        var message = await client.SendMessage(
            chatId: chatId,
            text: "✏️ Введи текст вопроса"
        );
        return message.Id;
    }

    public async Task<int> EnterAnswerMessage(long chatId)
    {
        var message = await client.SendMessage(
            chatId: chatId,
            text: "💬 Введи ответ к вопросу"
        );
        return message.Id;
    }

    public async Task<int> CreateNewQuizMessage(long chatId)
    {
        var message = await client.SendMessage(
            chatId: chatId,
            text: "✅ Опросник создан!"
        );
        return message.Id;
    }

    public async Task<int> SelectPollMessage(
        long chatId,
        IReadOnlyCollection<Quiz> quizzes,
        int? editMessageId = null
    )
    {
        Message message;

        if (quizzes.Count == 0)
        {
            message = await client.SendMessage(
                chatId: chatId,
                text: "📭 Сейчас нет доступных опросников."
            );
            return message.Id;
        }

        var keyboard = new InlineKeyboardMarkup(
            inlineKeyboard: quizzes
                .Select(x => new InlineKeyboardButton(x.Name, x.Id.ToString()))
                .Chunk(3)
                .Append([
                    new InlineKeyboardButton("←", "backward"),
                    new InlineKeyboardButton("→", "forward")
                ])
        );

        if (editMessageId is { } i)
        {
            message = await client.EditMessageReplyMarkup(
                chatId: chatId,
                messageId: i,
                replyMarkup: keyboard
            );
        }
        else
        {
            message = await client.SendMessage(
                chatId: chatId,
                replyMarkup: keyboard,
                text: "🔎 Выбери опросник:"
            );
        }

        return message.Id;
    }

    public async Task<int> SendQuizPublishedMessage(long chatId)
    {
        var message = await client.SendMessage(
            chatId: chatId,
            text: "🚀 Опросник опубликован!"
        );
        return message.Id;
    }

    public async Task<int> SendQuestionMessage(long chatId, QuizQuestion question)
    {
        var message = await client.SendMessage(
            chatId: chatId,
            text: $"❓ *{question.Question}*",
            parseMode: ParseMode.Markdown,
            replyMarkup: new InlineKeyboardMarkup(new InlineKeyboardButton("⏭ Пропустить", "skip"))
        );
        return message.Id;
    }

    public async Task<int> SendCompletedAnswering(long chatId, QuizQuestion question, int score, int repeatsAfter)
    {
        var (icon, verdict) = score switch
        {
            >= 80 => ("🎉", "отлично!"),
            >= 50 => ("🤔", "неплохо, но можно лучше."),
            _ => ("😬", "нужно подтянуть!")
        };

        var sb = new StringBuilder();
        sb.AppendLine($"{icon} *{verdict}* ({score}%)");
        sb.AppendLine();
        sb.AppendLine("📝 Правильный ответ:");
        sb.AppendLine($"`{question.Answer}`");

        if (repeatsAfter > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"🔄 Вопрос повторится через {repeatsAfter}");
        }

        var message = await client.SendMessage(
            chatId: chatId,
            text: sb.ToString(),
            parseMode: ParseMode.Markdown
        );
        return message.Id;
    }

    public async Task<int> SendCompletedQuiz(
        long chatId,
        IQuizSessionStatistics statistics,
        QuestionQueue queue,
        IReadOnlyDictionary<int, QuizQuestion> questions)
    {
        var total = statistics.TotalQuestions(queue);
        var answered = statistics.Answered(queue);
        var avgScore = (int)(statistics.AverageScore(queue) * 100);
        var attempts = statistics.AllAttempts(queue);

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
        sb.AppendLine($"🔄 Всего попыток: {attempts.Count}");

        if (total > 0)
        {
            var byQuestion = attempts
                .GroupBy(a => a.QuestionId)
                .OrderBy(g => g.Min(a => a.Timestamp));

            sb.AppendLine();
            sb.AppendLine("─────────────────────");

            foreach (var group in byQuestion)
            {
                var q = questions[group.Key];
                var bestScore = (int)(group.Max(a => a.Score) * 100);
                var attemptCount = group.Count();

                var icon = bestScore >= 80 ? "✅" : bestScore >= 50 ? "⚠️" : "❌";
                var retryNote = attemptCount > 1 ? $" ×{attemptCount}" : "";

                sb.AppendLine($"{icon} {q.Question} — *{bestScore}%*{retryNote}");
            }
        }

        var message = await client.SendMessage(
            chatId: chatId,
            text: sb.ToString(),
            parseMode: ParseMode.Markdown
        );
        return message.Id;
    }
}