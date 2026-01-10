using FluentMigrator;

namespace mementobot.Migrations;

[Migration(version: 20260106)]
public class InitialMigration : Migration
{
    public override void Up()
    {
        Create.Table("users")
            .WithColumn("id").AsInt32().Identity().PrimaryKey()
            .WithColumn("telegram_id").AsInt64().NotNullable().Unique()
            .WithColumn("quiz_progress_user_state_id").AsInt32().ForeignKey("quiz_progress_user_states", "id").Nullable()
            .WithColumn("add_question_user_state_id").AsInt32().ForeignKey("add_question_user_states", "id").Nullable()
            .WithColumn("select_quiz_user_state_id").AsInt32().ForeignKey("select_quiz_user_states", "id").Nullable();
        Create.Table("quizzes")
            .WithColumn("id").AsInt32().Identity().PrimaryKey()
            .WithColumn("user_id").AsInt64().ForeignKey("users", "id").Nullable()
            .WithColumn("name").AsString().NotNullable()
            .WithColumn("is_published").AsBoolean().NotNullable();
        Create.Table("quiz_questions")
            .WithColumn("id").AsInt32().Identity().PrimaryKey()
            .WithColumn("quiz_id").AsInt32().ForeignKey("quizzes", "id").NotNullable()
            .WithColumn("question").AsString().NotNullable()
            .WithColumn("answer").AsString().NotNullable();
        Create.Table("add_question_user_states")
            .WithColumn("id").AsInt32().Identity().PrimaryKey()
            .WithColumn("quiz_id").AsInt32().ForeignKey("quizzes", "id").NotNullable()
            .WithColumn("question").AsString().Nullable()
            .WithColumn("answer").AsString().Nullable()
            .WithColumn("message_id").AsInt32().Nullable();
        Create.Table("quiz_progress_user_states")
            .WithColumn("id").AsInt32().Identity().PrimaryKey()
            .WithColumn("current_question_id").AsInt32().ForeignKey("user_quiz_questions", "id").Nullable()
            .WithColumn("quiz_id").AsInt32().ForeignKey("quizzes", "id").NotNullable()
            .WithColumn("message_id").AsInt32().Nullable();
        Create.Table("user_quiz_questions")
            .WithColumn("id").AsInt32().Identity().PrimaryKey()
            .WithColumn("state_id").AsInt32().ForeignKey("quiz_progress_user_states", "id").NotNullable()
            .WithColumn("quiz_question_id").AsInt32().ForeignKey("quiz_questions", "id").NotNullable()
            .WithColumn("order").AsInt32().NotNullable();
        Create.Table("select_quiz_user_states")
            .WithColumn("id").AsInt32().Identity().PrimaryKey()
            .WithColumn("current_page").AsInt32().NotNullable()
            .WithColumn("action_type").AsInt32().NotNullable()
            .WithColumn("message_id").AsInt32().Nullable();
        Create.Table("select_quiz_quizzes")
            .WithColumn("id").AsInt32().Identity().PrimaryKey()
            .WithColumn("state_id").AsInt32().ForeignKey("select_quiz_user_states", "id").NotNullable()
            .WithColumn("quiz_id").AsInt32().ForeignKey("quizzes", "id").NotNullable()
            .WithColumn("page").AsInt32().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("users");
        Delete.Table("quizzes");
        Delete.Table("add_question_user_states");
        Delete.Table("question_properties");
        Delete.Table("quiz_progress_user_states");
        Delete.Table("user_quiz_questions");
        Delete.Table("select_quiz_user_states");
    }
}