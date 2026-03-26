namespace mementobot.Services.Quizzing;

// === Answer contracts ===

/// <summary>
/// Маркерный интерфейс для ответа пользователя.
/// Конкретные реализации определяются типом вопроса (текст, выбор, сопоставление, составной).
/// </summary>
internal interface IAnswerResult;

internal record TextAnswerResult(string Text) : IAnswerResult;

// === Evaluation ===

/// <summary>
/// Результат оценки ответа. Score нормализован в диапазоне [0..1].
/// </summary>
internal record EvaluationResult(double Score);

/// <summary>
/// Оценивает ответ пользователя на вопрос.
/// Реализация знает, как интерпретировать конкретный тип <see cref="IAnswerResult"/>.
/// Стейт-машина вызывает evaluator напрямую, engine о нём не знает.
/// </summary>
internal interface IAnswerEvaluator
{
    EvaluationResult Evaluate(QuizQuestion question, IAnswerResult answer);
}

// === Requeuing ===

/// <summary>
/// Стратегия реордеринга: по результату оценки определяет,
/// на сколько позиций вперёд вставить вопрос обратно в очередь.
/// 0 — вопрос отвечен верно, из очереди удаляется.
/// </summary>
internal interface IRequeuingStrategy
{
    /// <param name="result">Результат оценки текущей попытки.</param>
    /// <param name="attemptNumber">Номер попытки (1 = первая).</param>
    /// <returns>Сдвиг вперёд в очереди. 0 — вопрос удаляется из очереди.</returns>
    int ComputeShift(EvaluationResult result, int attemptNumber);
}

// === Queue data (serializable, lives in state) ===

internal record QuestionAttempt(int QuestionId, double Score, DateTime Timestamp);

/// <summary>
/// Сериализуемые данные очереди вопросов. Хранится в стейте стейт-машины.
/// Текущий вопрос — всегда <c>QuestionIds[0]</c>.
/// </summary>
internal class QuestionQueue
{
    /// <summary>Оставшиеся вопросы в порядке прохождения. Голова списка — текущий вопрос.</summary>
    public List<int> QuestionIds { get; set; } = [];

    /// <summary>Счётчик пропусков по каждому вопросу.</summary>
    public Dictionary<int, int> SkipCounts { get; set; } = [];

    /// <summary>История всех попыток ответа (для статистики).</summary>
    public List<QuestionAttempt> Attempts { get; set; } = [];
}

// === Engine ===

/// <summary>
/// Сервис управления очередью вопросов. Stateless — работает с <see cref="QuestionQueue"/> как с данными.
/// <para>
/// Получает <see cref="IRequeuingStrategy"/> через DI.
/// Обрабатывает фолбеки: clamp при выходе за границы, ограничение повторов и т.д.
/// </para>
/// </summary>
internal interface IQuestionEngine
{
    /// <summary>ID текущего вопроса, или null если очередь завершена.</summary>
    int? GetCurrentQuestionId(QuestionQueue queue);

    /// <summary>
    /// Фиксирует результат для текущего вопроса.
    /// Удаляет его из головы очереди и вставляет обратно на позицию, определённую стратегией.
    /// Возвращает ID следующего вопроса, или null если очередь пуста.
    /// </summary>
    int? Advance(QuestionQueue queue, EvaluationResult result);

    /// <summary>Пропустить текущий вопрос — переместить в конец очереди без оценки.</summary>
    int? Skip(QuestionQueue queue);

    bool IsCompleted(QuestionQueue queue);
}

// === Statistics ===

/// <summary>
/// Статистика прохождения опросника. Stateless — читает данные из <see cref="QuestionQueue"/>.
/// </summary>
internal interface IQuizSessionStatistics
{
    int TotalQuestions(QuestionQueue queue);
    int Answered(QuestionQueue queue);
    int Skipped(QuestionQueue queue);
    int Remaining(QuestionQueue queue);
    double AverageScore(QuestionQueue queue);
    IReadOnlyList<QuestionAttempt> GetAttempts(QuestionQueue queue, int questionId);
    IReadOnlyList<QuestionAttempt> AllAttempts(QuestionQueue queue);
}