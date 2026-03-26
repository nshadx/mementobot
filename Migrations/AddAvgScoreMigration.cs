using FluentMigrator;

namespace mementobot.Migrations;

[Migration(version: 20260327)]
public class AddAvgScoreMigration : Migration
{
    public override void Up()
    {
        Alter.Table("quiz_history")
            .AddColumn("avg_score").AsDouble().NotNullable().WithDefaultValue(0);
    }

    public override void Down()
    {
        Delete.Column("avg_score").FromTable("quiz_history");
    }
}
