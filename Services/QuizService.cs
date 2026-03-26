using Dapper;
using Microsoft.Data.Sqlite;

namespace mementobot.Services;

internal class Quiz
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}

internal class QuizHistoryEntry
{
    public int QuizId { get; set; }
    public string QuizName { get; set; } = null!;
    public DateTime LastPlayed { get; set; }
    public double AvgScore { get; set; }
}

internal class QuizQuestion
{
    public int Id { get; set; }
    public string Question { get; set; } = null!;
    public string Answer { get; set; } = null!;
}

internal class QuizService(SqliteConnection connection)
{
    public IEnumerable<QuizQuestion> GetQuizQuestions(int quizId, SqliteTransaction? transaction = null) =>
        connection.Query<QuizQuestion>(
            "SELECT id AS Id, question AS Question, answer AS Answer FROM quiz_questions WHERE quiz_id = @quizId",
            new { quizId }, transaction);

    public bool IsOwnedBy(int userId, int quizId, SqliteTransaction? transaction = null) =>
        connection.ExecuteScalar<int>(
            "SELECT COUNT(1) FROM quizzes WHERE id = @quizId AND user_id = @userId",
            new { userId, quizId }, transaction) > 0;

    public IEnumerable<Quiz> GetQuizzes(bool published, int? userId = null, SqliteTransaction? transaction = null) =>
        connection.Query<Quiz>("""
            SELECT id AS Id, name AS Name FROM quizzes
            WHERE is_published = @published
            AND (@userId IS NULL OR user_id = @userId)
            """, new { published, userId }, transaction);

    public void PublishQuiz(int quizId, SqliteTransaction? transaction = null) =>
        connection.Execute(
            "UPDATE quizzes SET is_published = true WHERE id = @quizId",
            new { quizId }, transaction);

    public void CreateNew(int userId, string name, SqliteTransaction? transaction = null) =>
        connection.Execute(
            "INSERT INTO quizzes(user_id, name, is_published) VALUES (@userId, @name, FALSE)",
            new { userId, name }, transaction);

    public bool IsInFavorites(int userId, int quizId, SqliteTransaction? transaction = null) =>
        connection.ExecuteScalar<int>(
            "SELECT COUNT(1) FROM user_favorites WHERE user_id = @userId AND quiz_id = @quizId",
            new { userId, quizId }, transaction) > 0;

    public void AddToFavorites(int userId, int quizId, SqliteTransaction? transaction = null) =>
        connection.Execute(
            "INSERT OR IGNORE INTO user_favorites (user_id, quiz_id) VALUES (@userId, @quizId)",
            new { userId, quizId }, transaction);

    public void RemoveFromFavorites(int userId, int quizId, SqliteTransaction? transaction = null) =>
        connection.Execute(
            "DELETE FROM user_favorites WHERE user_id = @userId AND quiz_id = @quizId",
            new { userId, quizId }, transaction);

    public IEnumerable<Quiz> GetFavoriteQuizzes(int userId, SqliteTransaction? transaction = null) =>
        connection.Query<Quiz>("""
            SELECT id AS Id, name AS Name FROM quizzes WHERE user_id = @userId
            UNION
            SELECT q.id AS Id, q.name AS Name FROM quizzes q
            JOIN user_favorites f ON f.quiz_id = q.id
            WHERE f.user_id = @userId
            """, new { userId }, transaction);

    public IEnumerable<Quiz> GetRecentQuizzes(int userId, SqliteTransaction? transaction = null) =>
        connection.Query<Quiz>("""
            SELECT q.id AS Id, q.name AS Name FROM quizzes q
            JOIN quiz_history h ON h.quiz_id = q.id
            WHERE h.user_id = @userId
            ORDER BY h.last_played DESC
            """, new { userId }, transaction);

    public IEnumerable<Quiz> SearchPublishedQuizzes(string query, SqliteTransaction? transaction = null) =>
        connection.Query<Quiz>("""
            SELECT id AS Id, name AS Name FROM quizzes
            WHERE is_published = true AND name LIKE @query
            """, new { query = $"%{query}%" }, transaction);

    public void RecordQuizHistory(int userId, int quizId, double avgScore, SqliteTransaction? transaction = null) =>
        connection.Execute("""
            INSERT INTO quiz_history (user_id, quiz_id, last_played, avg_score)
            VALUES (@userId, @quizId, @lastPlayed, @avgScore)
            ON CONFLICT(user_id, quiz_id) DO UPDATE SET
                last_played = excluded.last_played,
                avg_score   = excluded.avg_score
            """, new { userId, quizId, lastPlayed = DateTime.UtcNow, avgScore }, transaction);

    public bool WasQuizCompletedAfter(int userId, int quizId, DateTime since, SqliteTransaction? transaction = null) =>
        connection.ExecuteScalar<int>("""
            SELECT COUNT(1) FROM quiz_history
            WHERE user_id = @userId AND quiz_id = @quizId AND last_played > @since
            """, new { userId, quizId, since }, transaction) > 0;

    public QuizHistoryEntry? GetQuizHistoryEntry(int userId, int quizId, SqliteTransaction? transaction = null) =>
        connection.QueryFirstOrDefault<QuizHistoryEntry>("""
            SELECT q.id AS QuizId, q.name AS QuizName, h.last_played AS LastPlayed, h.avg_score AS AvgScore
            FROM quiz_history h
            JOIN quizzes q ON q.id = h.quiz_id
            WHERE h.user_id = @userId AND h.quiz_id = @quizId
            """, new { userId, quizId }, transaction);

    public IEnumerable<QuizHistoryEntry> GetQuizHistoryForUser(int userId, SqliteTransaction? transaction = null) =>
        connection.Query<QuizHistoryEntry>("""
            SELECT q.id AS QuizId, q.name AS QuizName, h.last_played AS LastPlayed, h.avg_score AS AvgScore
            FROM quiz_history h
            JOIN quizzes q ON q.id = h.quiz_id
            WHERE h.user_id = @userId
            """, new { userId }, transaction);

    public bool IsPublished(int quizId, SqliteTransaction? transaction = null) =>
        connection.ExecuteScalar<int>(
            "SELECT is_published FROM quizzes WHERE id = @quizId",
            new { quizId }, transaction) != 0;

    public IEnumerable<Quiz> GetOwnedQuizzes(int userId, SqliteTransaction? transaction = null) =>
        connection.Query<Quiz>(
            "SELECT id AS Id, name AS Name FROM quizzes WHERE user_id = @userId ORDER BY id DESC",
            new { userId }, transaction);

    public void AddQuizQuestion(int quizId, string question, string answer, SqliteTransaction? transaction = null) =>
        connection.Execute(
            "INSERT INTO quiz_questions(quiz_id, question, answer) VALUES (@quizId, @question, @answer)",
            new { quizId, question, answer }, transaction);
}
