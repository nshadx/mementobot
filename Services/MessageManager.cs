using System.Text;
using mementobot.Services.Quizzing;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
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

    public async Task<int> SendQuestionAddedMessage(long chatId)
    {
        var message = await client.SendMessage(
            chatId: chatId,
            text: "✅ Вопрос добавлен!"
        );
        return message.Id;
    }

    public async Task<int> SendHelpMessage(long chatId)
    {
        var message = await client.SendMessage(
            chatId: chatId,
            text: """
                  *Доступные команды:*

                  /start — выбрать опросник и начать прохождение
                  /new \<название\> — создать новый опросник
                  /add — добавить вопрос в опросник
                  /publish — опубликовать опросник
                  /help — показать это сообщение
                  """,
            parseMode: ParseMode.MarkdownV2
        );
        return message.Id;
    }

    public async Task<int> SendQuizActionMenu(long chatId, bool isFavorited)
    {
        var message = await client.SendMessage(
            chatId: chatId,
            text: "Выбери действие:",
            replyMarkup: BuildQuizActionKeyboard(isFavorited)
        );
        return message.Id;
    }

    public async Task EditQuizActionMenu(long chatId, int messageId, bool isFavorited)
    {
        await client.EditMessageReplyMarkup(
            chatId: chatId,
            messageId: messageId,
            replyMarkup: BuildQuizActionKeyboard(isFavorited)
        );
    }

    private static InlineKeyboardMarkup BuildQuizActionKeyboard(bool isFavorited)
    {
        var favoriteButton = isFavorited
            ? new InlineKeyboardButton("❌ Убрать из избранного", "action:favorite")
            : new InlineKeyboardButton("⭐ В избранное", "action:favorite");

        return new InlineKeyboardMarkup([
            [new InlineKeyboardButton("▶️ Пройти", "action:play")],
            [favoriteButton]
        ]);
    }

    public async Task<int> SendSettingsMenu(long chatId, UserSettings settings)
    {
        var message = await client.SendMessage(
            chatId: chatId,
            text: "⚙️ *Настройки*",
            parseMode: ParseMode.MarkdownV2,
            replyMarkup: BuildSettingsKeyboard(settings)
        );
        return message.Id;
    }

    public async Task EditSettingsMenu(long chatId, int messageId, UserSettings settings)
    {
        try
        {
            await client.EditMessageReplyMarkup(
                chatId: chatId,
                messageId: messageId,
                replyMarkup: BuildSettingsKeyboard(settings)
            );
        }
        catch (ApiRequestException ex)
            when (ex.Message.Contains("message is not modified")) { }
    }

    private static InlineKeyboardMarkup BuildSettingsKeyboard(UserSettings settings)
    {
        var remindersLabel = settings.RemindersEnabled ? "🔔 Напоминания: ВКЛ" : "🔕 Напоминания: ВЫКЛ";
        var adultLabel = settings.AdultContent ? "🔞 Режим +18: ВКЛ" : "🔞 Режим +18: ВЫКЛ";

        return new InlineKeyboardMarkup([
            [new InlineKeyboardButton(remindersLabel, "settings:reminders")],
            [new InlineKeyboardButton($"⏰ Время напоминания: {settings.ReminderHour}:00", "settings:hour")],
            [new InlineKeyboardButton($"🌡 Температура: {settings.Temperature}/100", "settings:temperature")],
            [new InlineKeyboardButton(adultLabel, "settings:adult")]
        ]);
    }

    public async Task<int> SendReminderHourPrompt(long chatId, bool isError = false)
    {
        var text = isError
            ? "❌ Неверное значение\\. Введи час от 0 до 23:"
            : "⏰ Введи час напоминания \\(0–23\\):";
        var message = await client.SendMessage(
            chatId: chatId,
            text: text,
            parseMode: ParseMode.MarkdownV2
        );
        return message.Id;
    }

    public async Task<int> SendTemperaturePrompt(long chatId, bool isError = false)
    {
        var text = isError
            ? "❌ Неверное значение\\. Введи температуру от 0 до 100:"
            : "🌡 Введи температуру \\(0 — формальный, 100 — огненный\\):";
        var message = await client.SendMessage(
            chatId: chatId,
            text: text,
            parseMode: ParseMode.MarkdownV2
        );
        return message.Id;
    }

    public async Task SendReminderSpeech(long chatId, string markdownText)
    {
        await client.SendMessage(
            chatId: chatId,
            text: markdownText,
            parseMode: ParseMode.MarkdownV2
        );
    }

    public async Task<int> SendSearchPrompt(long chatId)
    {
        var message = await client.SendMessage(
            chatId: chatId,
            text: "🔍 Введи название опросника:"
        );
        return message.Id;
    }

    public async Task<int> SendStartMenu(long chatId)
    {
        var keyboard = new InlineKeyboardMarkup([
            [new InlineKeyboardButton("⭐ Избранное", "start:favorites")],
            [new InlineKeyboardButton("🕐 Недавно пройденные", "start:recent")],
            [new InlineKeyboardButton("🔍 Поиск", "start:search")]
        ]);
        var message = await client.SendMessage(
            chatId: chatId,
            text: "Выбери опросник:",
            replyMarkup: keyboard
        );
        return message.Id;
    }

    public async Task<int> SendQuizPublishedMessage(long chatId, int quizId)
    {
        var link = $"https://t.me/nshadx_mementobot?start={quizId}";
        var message = await client.SendMessage(
            chatId: chatId,
            text: $"🚀 *Опросник опубликован\\!*\n\n📎 [Ссылка для прохождения]({link})",
            parseMode: ParseMode.MarkdownV2
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