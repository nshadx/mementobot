using Microsoft.Data.Sqlite;

namespace mementobot.Services;

public class Quiz
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}

internal class QuizService(
    SqliteConnection connection
)
{
    public IEnumerable<int> GetQuizQuestionIds(int quizId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""SELECT id FROM quiz_questions WHERE quiz_id = @quiz_id""", connection, transaction);
        command.Parameters.AddWithValue("@quiz_id", quizId);
        
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                yield return reader.GetInt32(0);
            }
        }
    }
        
    public void PublishQuiz(int quizId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""UPDATE quizzes SET is_published = true WHERE id = @id""", connection, transaction);
        command.Parameters.AddWithValue("@id", quizId);
        
        command.ExecuteNonQuery();
    }
    
    public IEnumerable<int> GetPublishedQuizIds(SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""SELECT id FROM quizzes WHERE is_published = TRUE""", connection, transaction);
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                yield return reader.GetInt32(0);
            }
        }
    }
    
    public IEnumerable<int> GetUserQuizIds(int userId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""SELECT id FROM quizzes WHERE user_id = @user_id""", connection, transaction);
        command.Parameters.AddWithValue("@user_id", userId);
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                yield return reader.GetInt32(0);
            }
        }
    }
    
    public IEnumerable<Quiz> GetUserQuizzes(int userId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""SELECT id, name FROM quizzes WHERE user_id = @user_id""", connection, transaction);
        command.Parameters.AddWithValue("@user_id", userId);
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var id = reader.GetInt32(0);
                var name = reader.GetString(1);
                yield return new Quiz() { Id = id, Name = name };
            }
        }
    }
    
    public IEnumerable<int> GetNotPublishedUserQuizIds(int userId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""SELECT id FROM quizzes WHERE NOT is_published AND user_id = @user_id""", connection, transaction);
        command.Parameters.AddWithValue("@user_id", userId);
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                yield return reader.GetInt32(0);
            }
        }
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

    public string GetQuizQuestionAnswer(int questionId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
                                    SELECT answer
                                    FROM quiz_questions
                                    INNER JOIN user_quiz_questions ON user_quiz_questions.quiz_question_id = quiz_questions.id
                                    WHERE user_quiz_questions.id = @question_id
                                    """, connection, transaction);
        command.Parameters.AddWithValue("@question_id", questionId);

        var answer = (string)command.ExecuteScalar()!;
        return answer;
    }

    public void AddQuizQuestion(int quizId, string question, string answer)
    {
        
    }
}