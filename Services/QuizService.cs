using Microsoft.Data.Sqlite;

namespace mementobot.Services;

internal class Quiz
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}

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