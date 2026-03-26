using FluentMigrator;

namespace mementobot.Migrations;

[Migration(version: 20260328)]
public class AddUserSettingsMigration : Migration
{
    public override void Up()
    {
        Alter.Table("users").AddColumn("reminders_enabled").AsBoolean().NotNullable().WithDefaultValue(true);
        Alter.Table("users").AddColumn("reminder_hour").AsInt32().NotNullable().WithDefaultValue(9);
        Alter.Table("users").AddColumn("temperature").AsInt32().NotNullable().WithDefaultValue(50);
        Alter.Table("users").AddColumn("adult_content").AsBoolean().NotNullable().WithDefaultValue(false);
    }

    public override void Down()
    {
        Delete.Column("reminders_enabled").FromTable("users");
        Delete.Column("reminder_hour").FromTable("users");
        Delete.Column("temperature").FromTable("users");
        Delete.Column("adult_content").FromTable("users");
    }
}
