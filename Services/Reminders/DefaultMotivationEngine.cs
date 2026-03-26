namespace mementobot.Services.Reminders;

internal class DefaultMotivationEngine : IMotivationEngine
{
    public IReadOnlyList<MotivationSpeech> GetSpeeches(QuizHistoryEntry quiz, UserSettings settings)
    {
        var name = Escape(quiz.QuizName);
        var score = (int)(quiz.AvgScore * 100);
        var days = (int)(DateTime.UtcNow - quiz.LastPlayed).TotalDays;

        var tier = settings.Temperature switch
        {
            <= 33 => 0,
            <= 66 => 1,
            _     => 2
        };

        return (settings.AdultContent, tier) switch
        {
            (false, 0) => StandardLow(name, days, score),
            (false, 1) => StandardMedium(name, days, score),
            (false, 2) => StandardHigh(name, days, score),
            (true,  0) => AdultLow(name, days, score),
            (true,  1) => AdultMedium(name, days, score),
            (true,  2) => AdultHigh(name, days, score),
            _          => StandardMedium(name, days, score)
        };
    }

    private static IReadOnlyList<MotivationSpeech> StandardLow(string name, int days, int score) =>
    [
        new($"📚 Напоминание: пора повторить *{name}*\\. Уже {days} дн\\. без повторения\\.", TimeSpan.FromHours(2)),
        new($"📋 Опросник *{name}* всё ещё ждёт\\. Результат прошлого раза: *{score}%*\\.", TimeSpan.FromHours(2)),
        new($"🕐 Сегодня последний шанс пройти *{name}*\\. Открой /start\\.", TimeSpan.Zero)
    ];

    private static IReadOnlyList<MotivationSpeech> StandardMedium(string name, int days, int score) =>
    [
        new($"📚 Время повторить *{name}*\\!\nТы не проходил его уже {days} дн\\.", TimeSpan.FromHours(2)),
        new($"💡 Всё ещё не прошёл *{name}*?\nПоследний результат: *{score}%* — можно лучше 😉", TimeSpan.FromHours(2)),
        new($"⏰ Последний шанс на сегодня — открой бота и нажми /start, чтобы пройти *{name}*\\!", TimeSpan.Zero)
    ];

    private static IReadOnlyList<MotivationSpeech> StandardHigh(string name, int days, int score) =>
    [
        new($"🔥 ПОРА ПОВТОРИТЬ *{name}*\\! Ты уже {days} дн\\. откладываешь — так нельзя\\!", TimeSpan.FromHours(2)),
        new($"⚡ ЭЙ\\! *{name}* всё ещё ждёт тебя\\! {score}% в прошлый раз — ты точно можешь лучше\\! 💪", TimeSpan.FromHours(2)),
        new($"🚨 ПОСЛЕДНИЙ ЗВОНОК\\! /start → пройди *{name}* прямо сейчас\\! Не откладывай\\! 💥", TimeSpan.Zero)
    ];

    private static IReadOnlyList<MotivationSpeech> AdultLow(string name, int days, int score) =>
    [
        new($"*{name}* — {days} дн\\. без прохождения\\. Совсем забил?", TimeSpan.FromHours(2)),
        new($"*{name}* всё ждёт\\. {score}% в прошлый раз — это слабо\\.", TimeSpan.FromHours(2)),
        new($"Последний шанс пройти *{name}* сегодня\\. Открой /start или не жалуйся потом\\.", TimeSpan.Zero)
    ];

    private static IReadOnlyList<MotivationSpeech> AdultMedium(string name, int days, int score) =>
    [
        new($"🤨 Слушай, {days} дн\\. прошло, а *{name}* ты так и не открыл\\. Лень — называй вещи своими именами\\.", TimeSpan.FromHours(2)),
        new($"😒 *{name}* всё ждёт, пока ты прокрастинируешь\\. {score}% в прошлый раз — стыдновато, нет?", TimeSpan.FromHours(2)),
        new($"🖕 Последний шанс: открой /start и пройди *{name}*\\. Или продолжай откладывать — твой выбор, бездарь\\.", TimeSpan.Zero)
    ];

    private static IReadOnlyList<MotivationSpeech> AdultHigh(string name, int days, int score) =>
    [
        new($"😤 {days} дн\\. — и ни разу не открыл *{name}*\\?! Это уже диагноз, дружок\\!", TimeSpan.FromHours(2)),
        new($"🤬 {score}% — это позор\\! *{name}* стоит и ждёт, пока ты гниёшь в прокрастинации\\!", TimeSpan.FromHours(2)),
        new($"💀 Всё\\. Либо открываешь /start и проходишь *{name}* прямо сейчас, либо иди нахуй с этим ботом\\. Твой выбор\\.", TimeSpan.Zero)
    ];

    private static string Escape(string text) =>
        text.Replace("\\", "\\\\")
            .Replace(".", "\\.").Replace("!", "\\!").Replace("-", "\\-")
            .Replace("(", "\\(").Replace(")", "\\)").Replace("_", "\\_")
            .Replace("*", "\\*").Replace("[", "\\[").Replace("]", "\\]");
}
