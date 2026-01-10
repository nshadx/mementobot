using Microsoft.Data.Sqlite;

namespace mementobot.Services;

public enum StateType
{
    SelectQuizUserState,
    AddQuestionUserState,
    QuizProgressUserState
}

public enum ActionType
{
    Publish,
    AddQuizQuestion,
    StartQuiz
}

public enum PropertyName
{
    Question,
    Answer
}

public class StateService(
    SqliteConnection connection
)
{
    public IEnumerable<(int Id, string Name)> GetQuizNamesWithId(int stateId, int page, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
                                    SELECT quizzes.id, quizzes.name
                                    FROM quizzes
                                    INNER JOIN select_quiz_quizzes on select_quiz_quizzes.quiz_id = quizzes.id
                                    WHERE select_quiz_quizzes.id = @state_id AND select_quiz_quizzes.page = @page 
                                    """, connection, transaction);
        command.Parameters.AddWithValue("@state_id", stateId);
        command.Parameters.AddWithValue("@page", page);
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                yield return (reader.GetInt32(0), reader.GetString(1));
            }
        }
    }
    
    public void AddQuizForSelect(int stateId, int quizId, int page, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""INSERT INTO select_quiz_quizzes(state_id, quiz_id, page) VALUES (@state_id, @quiz_id, @page)""", connection, transaction);
        command.Parameters.AddWithValue("@state_id", stateId);
        command.Parameters.AddWithValue("@quiz_id", quizId);
        command.Parameters.AddWithValue("@page", page);
        command.ExecuteNonQuery();
    }
    
    public int SetSelectQuizUserState(int userId, ActionType actionType, SqliteTransaction? transaction = null)
    {
        SqliteCommand insertCommand = new("""
                                          INSERT INTO select_quiz_user_states(current_page, action_type) VALUES (1, @action_type) RETURNING id
                                          """, connection, transaction);
        insertCommand.Parameters.AddWithValue("@action_type", actionType);
        var id = (int)(long)insertCommand.ExecuteScalar()!;
        
        SqliteCommand updateCommand = new("""
                                    UPDATE users
                                    SET select_quiz_user_state_id = @state_id
                                    WHERE id = @user_id
                                    """, connection, transaction);
        updateCommand.Parameters.AddWithValue("@user_id", userId);
        updateCommand.Parameters.AddWithValue("@state_id", id);

        updateCommand.ExecuteScalar();
        
        return id;
    }
    
    public void DecrementPage(int stateId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""UPDATE select_quiz_user_states SET current_page = current_page - 1 WHERE state_id = @state_id""", connection, transaction);
        command.Parameters.AddWithValue("@state_id", stateId);
        
        command.ExecuteNonQuery();
    }
    
    public void IncrementPage(int stateId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""UPDATE select_quiz_user_states SET current_page = current_page + 1 WHERE state_id = @state_id""", connection, transaction);
        command.Parameters.AddWithValue("@state_id", stateId);
        
        command.ExecuteNonQuery();
    }
    
    public int GetCurrentPage(int stateId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""SELECT current_page FROM select_quiz_user_states WHERE id = @state_id""", connection, transaction);
        command.Parameters.AddWithValue("@state_id", stateId);
        
        var currentPage = (int)(long)command.ExecuteScalar()!;
        return currentPage;
    }

    public int GetPageCount(int stateId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
                                    SELECT MAX(select_quiz_quizzes.page)
                                    FROM select_quiz_quizzes
                                    INNER JOIN select_quiz_user_states on select_quiz_quizzes.state_id = select_quiz_user_states.id  
                                    WHERE select_quiz_user_states.id = @state_id
                                    """, connection, transaction);
        command.Parameters.AddWithValue("@state_id", stateId);
        
        var value = command.ExecuteScalar()!;
        if (value is DBNull)
        {
            return 0;
        }
        return (int)(long)value;
    }
    
    public int? GetMessageId(int stateId, StateType type, SqliteTransaction? transaction = null)
    {
        var tableName = type switch
        {
            StateType.AddQuestionUserState => "add_question_user_states",
            StateType.QuizProgressUserState => "quiz_progress_user_states",
            StateType.SelectQuizUserState => "select_quiz_user_states",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
        
        SqliteCommand command = new($"""SELECT message_id FROM {tableName} WHERE id = @state_id""", connection, transaction);
        command.Parameters.AddWithValue("@state_id", stateId);
        
        var value = command.ExecuteScalar()!;
        if (value is DBNull)
        {
            return null;
        }

        return (int)(long)value;
    }
    
    public int? GetStateId(int userId, StateType type, SqliteTransaction? transaction = null)
    {
        var columnName = type switch
        {
            StateType.AddQuestionUserState => "add_question_user_state_id",
            StateType.QuizProgressUserState => "quiz_progress_user_state_id",
            StateType.SelectQuizUserState => "select_quiz_user_state_id",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        SqliteCommand command = new($"""SELECT {columnName} FROM users WHERE id = @user_id""", connection, transaction);
        command.Parameters.AddWithValue("@user_id", userId);

        var value = command.ExecuteScalar()!;
        if (value is DBNull)
        {
            return null;
        }

        return (int)(long)value;
    }

    public void SetMessageId(int stateId, StateType type, int messageId, SqliteTransaction? transaction = null)
    {
        var tableName = type switch
        {
            StateType.AddQuestionUserState => "add_question_user_states",
            StateType.QuizProgressUserState => "quiz_progress_user_states",
            StateType.SelectQuizUserState => "select_quiz_user_states",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
        
        SqliteCommand command = new($"""UPDATE {tableName} SET message_id = @message_id WHERE id = @state_id""", connection, transaction);
        command.Parameters.AddWithValue("@state_id", stateId);
        command.Parameters.AddWithValue("@message_id", messageId);

        command.ExecuteNonQuery();
    }

    public int SetQuizProgressUserState(int userId, int quizId, SqliteTransaction? transaction = null)
    {
        SqliteCommand insertCommand = new("""
                                          INSERT INTO quiz_progress_user_states(quiz_id) VALUES (@quiz_id)
                                          RETURNING id
                                          """, connection, transaction);
        insertCommand.Parameters.AddWithValue("@quiz_id", quizId);

        var stateId = (int)(long)insertCommand.ExecuteScalar()!;
        
        SqliteCommand command = new("""
                                    UPDATE users
                                    SET quiz_progress_user_state_id = @state_id 
                                    WHERE id = @user_id
                                    """, connection, transaction);
        command.Parameters.AddWithValue("@state_id", stateId);
        command.Parameters.AddWithValue("@user_id", userId);

        command.ExecuteNonQuery();

        return stateId;
    }

    public void CloneQuizQuestions(int stateId, int quizId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
                                    INSERT INTO user_quiz_questions(state_id, quiz_question_id, "order")
                                    SELECT @state_id, id, "order"
                                    FROM quiz_questions
                                    WHERE quiz_id = @quiz_id
                                    """, connection, transaction);
        command.Parameters.AddWithValue("@state_id", stateId);
        command.Parameters.AddWithValue("@quiz_id", quizId);
        
        command.ExecuteNonQuery();
    }
    
    public string? SetNextQuestion(int stateId, SqliteTransaction? transaction = null)
    {
        SqliteCommand selectCommand = new("""
                                          SELECT user_quiz_questions.id
                                          FROM user_quiz_questions
                                          INNER JOIN quiz_progress_user_states ON user_quiz_questions.state_id = quiz_progress_user_states.id 
                                          WHERE user_quiz_questions.state_id = @state_id AND user_quiz_questions.id IS NOT quiz_progress_user_states.current_question_id
                                          ORDER BY "order" ASC
                                          LIMIT 1
                                          """, connection, transaction);
        selectCommand.Parameters.AddWithValue("@state_id", stateId);

        var value = selectCommand.ExecuteScalar();
        if (value is null or DBNull)
        {
            return null;
        }
        var questionId = (int)(long)value;
        
        SqliteCommand command = new("""
                                    UPDATE quiz_progress_user_states
                                    SET current_question_id = @question_id
                                    WHERE id = @state_id
                                    """, connection, transaction);
        command.Parameters.AddWithValue("@state_id", stateId);
        command.Parameters.AddWithValue("@question_id", questionId);

        command.ExecuteNonQuery();

        SqliteCommand questionCommand = new("""
                                            SELECT question
                                            FROM quiz_questions
                                            INNER JOIN user_quiz_questions ON user_quiz_questions.quiz_question_id = quiz_questions.id
                                            WHERE user_quiz_questions.id = @question_id
                                            """, connection, transaction);
        questionCommand.Parameters.AddWithValue("@question_id", questionId);

        var question = (string)questionCommand.ExecuteScalar()!;
        return question;
    }

    public int SetAddQuestionUserState(int userId, int quizId, SqliteTransaction? transaction = null)
    {
        SqliteCommand insertCommand = new("""
                                          INSERT INTO add_question_user_states(quiz_id) VALUES (@quiz_id)
                                          RETURNING id
                                          """, connection, transaction);
        insertCommand.Parameters.AddWithValue("@quiz_id", quizId);

        var stateId = (int)(long)insertCommand.ExecuteScalar()!;
        
        SqliteCommand command = new("""
                                    UPDATE users
                                    SET add_question_user_state_id = @state_id
                                    WHERE id = @user_id RETURNING add_question_user_state_id;
                                    """, connection, transaction);
        command.Parameters.AddWithValue("@user_id", userId);
        command.Parameters.AddWithValue("@state_id", stateId);
        
        var id = (int)(long)command.ExecuteScalar()!;
        return id;
    }

    public string? GetProperty(int stateId, PropertyName propertyName, SqliteTransaction? transaction = null)
    {
        var columnName = propertyName switch
        {
            PropertyName.Answer => "answer",
            PropertyName.Question => "question",
            _ => throw new ArgumentOutOfRangeException(nameof(propertyName), propertyName, null)
        };
        SqliteCommand command = new($"""
                                    SELECT {columnName} 
                                    FROM add_question_user_states
                                    WHERE id = @state_id
                                    """, connection, transaction);
        command.Parameters.AddWithValue("@state_id", stateId);
        
        var value = command.ExecuteScalar()!;
        if (value is DBNull)
        {
            return null;
        }

        return (string)value;
    }

    public void InsertProperty(int stateId, PropertyName propertyName, string text, SqliteTransaction? transaction = null)
    {
        var columnName = propertyName switch
        {
            PropertyName.Answer => "answer",
            PropertyName.Question => "question",
            _ => throw new ArgumentOutOfRangeException(nameof(propertyName), propertyName, null)
        };
        SqliteCommand command = new($"""
                                     UPDATE add_question_user_states
                                     SET {columnName} = @text
                                     WHERE id = @state_id
                                     """, connection, transaction);
        command.Parameters.AddWithValue("@state_id", stateId);
        command.Parameters.AddWithValue("@text", text);

        command.ExecuteNonQuery();
    }

    public void AddQuizQuestion(int stateId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
                                    INSERT INTO quiz_questions(quiz_id, question, answer)
                                    SELECT add_question_user_states.quiz_id, add_question_user_states.question, add_question_user_states.answer 
                                    FROM add_question_user_states
                                    WHERE add_question_user_states.id = @state_id
                                    LIMIT 1
                                    """, connection, transaction);
        command.Parameters.AddWithValue("@state_id", stateId);

        command.ExecuteNonQuery();
    }
    
    public int GetCurrentQuizQuestionId(int stateId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
                                    SELECT current_question_id
                                    FROM quiz_progress_user_states
                                    WHERE id = @state_id
                                    """, connection, transaction);
        command.Parameters.AddWithValue("@state_id", stateId);

        var id = (int)(long)command.ExecuteScalar()!;
        return id;
    }

    public void SetQuizQuestionOrder(int questionId, int order, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
                                    UPDATE user_quiz_questions
                                    SET "order" = @order
                                    WHERE id = @question_id
                                    """, connection, transaction);
        command.Parameters.AddWithValue("@question_id", questionId);
        command.Parameters.AddWithValue("@order", order);
        
        command.ExecuteNonQuery();
    }

    public void FinishQuiz(int userId, int stateId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
                                    UPDATE users
                                    SET quiz_progress_user_state_id = NULL
                                    WHERE id = @user_id;
                                    UPDATE quiz_progress_user_states
                                    SET current_question_id = NULL
                                    WHERE id = @state_id;
                                    DELETE FROM user_quiz_questions
                                    WHERE state_id = @state_id;
                                    DELETE FROM quiz_progress_user_states
                                    WHERE id = @state_id
                                    """, connection, transaction);
        command.Parameters.AddWithValue("@state_id", stateId);
        command.Parameters.AddWithValue("@user_id", userId);
        
        command.ExecuteNonQuery();
    }

    public void CompleteSelect(int userId, int stateId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
                                    UPDATE users
                                    SET select_quiz_user_state_id = NULL
                                    WHERE id = @user_id;
                                    DELETE FROM select_quiz_quizzes
                                    WHERE state_id = @state_id;
                                    DELETE FROM select_quiz_user_states
                                    WHERE id = @state_id;
                                    """, connection, transaction);
        command.Parameters.AddWithValue("@state_id", stateId);
        command.Parameters.AddWithValue("@user_id", userId);
        
        command.ExecuteNonQuery();
    }
    
    public void CompleteAddingQuizQuestion(int userId, int stateId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
                                    UPDATE users
                                    SET add_quiz_question_user_state_id = NULL
                                    WHERE id = @user_id;
                                    DELETE FROM add_quiz_question_user_states
                                    WHERE id = @state_id
                                    """, connection, transaction);
        command.Parameters.AddWithValue("@state_id", stateId);
        command.Parameters.AddWithValue("@user_id", userId);

        command.ExecuteNonQuery();
    }

    public ActionType GetActionType(int stateId, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = new("""
                                    SELECT action_type
                                    FROM select_quiz_user_states
                                    WHERE id = @state_id
                                    """, connection, transaction);
        command.Parameters.AddWithValue("@state_id", stateId);

        var actionType = (ActionType)(long)command.ExecuteScalar()!;
        return actionType;
    }
}