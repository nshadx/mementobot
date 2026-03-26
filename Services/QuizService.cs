using Microsoft.Data.Sqlite;

namespace mementobot.Services;

internal class Quiz
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}

internal record QuizHistoryEntry(int QuizId, string QuizName, DateTime LastPlayed, double AvgScore);

internal class QuizQuestion
{
    public int Id { get; set; }
    public string Question { get; set; } = null!;
    public string Answer { get; set; } = null!;
}

internal class QuizService(
    SqliteConnection connection
)
{
    public IEnumerable<QuizQuestion> GetQuizQuestions(int quizId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""SELECT id, question, answer FROM quiz_questions WHERE quiz_id = @quiz_id""", connection, transaction);
        command.Parameters.AddWithValue("@quiz_id", quizId);
        
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetInt32(0);
            var question = reader.GetString(1);
            var answer = reader.GetString(2);

            yield return new() { Id = id, Question = question, Answer = answer };
        }
    }

    public bool IsOwnedBy(int userId, int quizId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
            SELECT COUNT(1) FROM quizzes WHERE id = @quiz_id AND user_id = @user_id
            """, connection, transaction);
        command.Parameters.AddWithValue("@user_id", userId);
        command.Parameters.AddWithValue("@quiz_id", quizId);
        return (long)command.ExecuteScalar()! > 0;
    }

    public IEnumerable<Quiz> GetQuizzes(bool published, int? userId = null, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
            SELECT id, name FROM quizzes
            WHERE is_published IS @published
            AND (@user_id IS NULL OR user_id = @user_id)
            """, connection, transaction);
        command.Parameters.AddWithValue("@published", published);
        command.Parameters.AddWithValue("@user_id", userId.HasValue ? userId.Value : DBNull.Value);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return new Quiz { Id = reader.GetInt32(0), Name = reader.GetString(1) };
        }
    }

    public void PublishQuiz(int quizId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""UPDATE quizzes SET is_published = true WHERE id = @id""", connection, transaction);
        command.Parameters.AddWithValue("@id", quizId);
        
        command.ExecuteNonQuery();
    }

    public void CreateNew(int userId, string name, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
                                    INSERT INTO quizzes(user_id, name, is_published) VALUES (@user_id, @name, FALSE)
                                    """, connection, transaction);
        command.Parameters.AddWithValue("@user_id", userId);
        command.Parameters.AddWithValue("@name", name);
        
        command.ExecuteNonQuery();
    }

    public bool IsInFavorites(int userId, int quizId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
            SELECT COUNT(1) FROM user_favorites WHERE user_id = @user_id AND quiz_id = @quiz_id
            """, connection, transaction);
        command.Parameters.AddWithValue("@user_id", userId);
        command.Parameters.AddWithValue("@quiz_id", quizId);
        return (long)command.ExecuteScalar()! > 0;
    }

    public void AddToFavorites(int userId, int quizId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
            INSERT OR IGNORE INTO user_favorites (user_id, quiz_id) VALUES (@user_id, @quiz_id)
            """, connection, transaction);
        command.Parameters.AddWithValue("@user_id", userId);
        command.Parameters.AddWithValue("@quiz_id", quizId);
        command.ExecuteNonQuery();
    }

    public void RemoveFromFavorites(int userId, int quizId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
            DELETE FROM user_favorites WHERE user_id = @user_id AND quiz_id = @quiz_id
            """, connection, transaction);
        command.Parameters.AddWithValue("@user_id", userId);
        command.Parameters.AddWithValue("@quiz_id", quizId);
        command.ExecuteNonQuery();
    }

    public IEnumerable<Quiz> GetFavoriteQuizzes(int userId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
            SELECT id, name FROM quizzes WHERE user_id = @user_id
            UNION
            SELECT q.id, q.name FROM quizzes q
            JOIN user_favorites f ON f.quiz_id = q.id
            WHERE f.user_id = @user_id
            """, connection, transaction);
        command.Parameters.AddWithValue("@user_id", userId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return new Quiz { Id = reader.GetInt32(0), Name = reader.GetString(1) };
        }
    }

    public IEnumerable<Quiz> GetRecentQuizzes(int userId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
            SELECT q.id, q.name FROM quizzes q
            JOIN quiz_history h ON h.quiz_id = q.id
            WHERE h.user_id = @user_id
            ORDER BY h.last_played DESC
            """, connection, transaction);
        command.Parameters.AddWithValue("@user_id", userId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return new Quiz { Id = reader.GetInt32(0), Name = reader.GetString(1) };
        }
    }

    public IEnumerable<Quiz> SearchPublishedQuizzes(string query, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
            SELECT id, name FROM quizzes
            WHERE is_published = true AND name LIKE @query
            """, connection, transaction);
        command.Parameters.AddWithValue("@query", $"%{query}%");
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return new Quiz { Id = reader.GetInt32(0), Name = reader.GetString(1) };
        }
    }

    public void RecordQuizHistory(int userId, int quizId, double avgScore, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
            INSERT INTO quiz_history (user_id, quiz_id, last_played, avg_score)
            VALUES (@user_id, @quiz_id, @last_played, @avg_score)
            ON CONFLICT(user_id, quiz_id) DO UPDATE SET
                last_played = excluded.last_played,
                avg_score   = excluded.avg_score
            """, connection, transaction);
        command.Parameters.AddWithValue("@user_id", userId);
        command.Parameters.AddWithValue("@quiz_id", quizId);
        command.Parameters.AddWithValue("@last_played", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("@avg_score", avgScore);
        command.ExecuteNonQuery();
    }

    public bool WasQuizCompletedAfter(int userId, int quizId, DateTime since, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
            SELECT COUNT(1) FROM quiz_history
            WHERE user_id = @user_id AND quiz_id = @quiz_id AND last_played > @since
            """, connection, transaction);
        command.Parameters.AddWithValue("@user_id", userId);
        command.Parameters.AddWithValue("@quiz_id", quizId);
        command.Parameters.AddWithValue("@since", since.ToString("O"));
        return (long)command.ExecuteScalar()! > 0;
    }

    public QuizHistoryEntry? GetQuizHistoryEntry(int userId, int quizId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
            SELECT q.id, q.name, h.last_played, h.avg_score
            FROM quiz_history h
            JOIN quizzes q ON q.id = h.quiz_id
            WHERE h.user_id = @user_id AND h.quiz_id = @quiz_id
            """, connection, transaction);
        command.Parameters.AddWithValue("@user_id", userId);
        command.Parameters.AddWithValue("@quiz_id", quizId);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        return new QuizHistoryEntry(
            QuizId: reader.GetInt32(0),
            QuizName: reader.GetString(1),
            LastPlayed: DateTime.Parse(reader.GetString(2)),
            AvgScore: reader.GetDouble(3)
        );
    }

    public IEnumerable<QuizHistoryEntry> GetQuizHistoryForUser(int userId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
            SELECT q.id, q.name, h.last_played, h.avg_score
            FROM quiz_history h
            JOIN quizzes q ON q.id = h.quiz_id
            WHERE h.user_id = @user_id
            """, connection, transaction);
        command.Parameters.AddWithValue("@user_id", userId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return new QuizHistoryEntry(
                QuizId: reader.GetInt32(0),
                QuizName: reader.GetString(1),
                LastPlayed: DateTime.Parse(reader.GetString(2)),
                AvgScore: reader.GetDouble(3)
            );
        }
    }

    public bool IsPublished(int quizId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
            SELECT is_published FROM quizzes WHERE id = @quiz_id
            """, connection, transaction);
        command.Parameters.AddWithValue("@quiz_id", quizId);
        var result = command.ExecuteScalar();
        return result is not null && (long)result != 0;
    }

    public IEnumerable<Quiz> GetOwnedQuizzes(int userId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
            SELECT id, name FROM quizzes WHERE user_id = @user_id ORDER BY id DESC
            """, connection, transaction);
        command.Parameters.AddWithValue("@user_id", userId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return new Quiz { Id = reader.GetInt32(0), Name = reader.GetString(1) };
        }
    }

    public void AddQuizQuestion(int quizId, string question, string answer)
    {
        SqliteCommand command = new("""
                                    INSERT INTO quiz_questions(quiz_id, question, answer) VALUES (@quiz_id, @question, @answer)
                                    """, connection);
        command.Parameters.AddWithValue("@quiz_id", quizId);
        command.Parameters.AddWithValue("@question", question);
        command.Parameters.AddWithValue("@answer", answer);

        command.ExecuteNonQuery();
    }
}