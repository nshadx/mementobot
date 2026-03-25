using FluentMigrator;

namespace mementobot.Migrations;

[Migration(version: 20260106)]
public class InitialMigration : Migration
{
    public override void Up()
    {
        Create.Table("users")
            .WithColumn("id").AsInt32().Identity().PrimaryKey()
            .WithColumn("telegram_id").AsInt64().NotNullable().Unique();
        Create.Table("quizzes")
            .WithColumn("id").AsInt32().Identity().PrimaryKey()
            .WithColumn("user_id").AsInt64().ForeignKey("users", "id").NotNullable()
            .WithColumn("name").AsString().NotNullable()
            .WithColumn("is_published").AsBoolean().NotNullable();
        Create.Table("quiz_questions")
            .WithColumn("id").AsInt32().Identity().PrimaryKey()
            .WithColumn("quiz_id").AsInt32().ForeignKey("quizzes", "id").NotNullable()
            .WithColumn("question").AsString().NotNullable()
            .WithColumn("answer").AsString().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("users");
        Delete.Table("quizzes");
        Delete.Table("quiz_questions");
    }
}