using FluentMigrator;

namespace mementobot.Migrations;

[Migration(version: 20260325)]
public class FavoritesMigration : Migration
{
    public override void Up()
    {
        Create.Table("user_favorites")
            .WithColumn("id").AsInt32().Identity().PrimaryKey()
            .WithColumn("user_id").AsInt32().NotNullable().ForeignKey("users", "id")
            .WithColumn("quiz_id").AsInt32().NotNullable().ForeignKey("quizzes", "id");

        Create.Index("ix_user_favorites_user_quiz")
            .OnTable("user_favorites")
            .OnColumn("user_id").Ascending()
            .OnColumn("quiz_id").Ascending()
            .WithOptions().Unique();

        Create.Table("quiz_history")
            .WithColumn("id").AsInt32().Identity().PrimaryKey()
            .WithColumn("user_id").AsInt32().NotNullable().ForeignKey("users", "id")
            .WithColumn("quiz_id").AsInt32().NotNullable().ForeignKey("quizzes", "id")
            .WithColumn("last_played").AsString().NotNullable();
        Create.Index("ix_quiz_history_user_quiz")
            .OnTable("quiz_history")
            .OnColumn("user_id").Ascending()
            .OnColumn("quiz_id").Ascending()
            .WithOptions().Unique();
    }

    public override void Down()
    {
        Delete.Table("user_favorites");
        Delete.Table("quiz_history");
    }
}