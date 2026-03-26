using FluentMigrator;

namespace mementobot.Migrations;

[Migration(version: 20260329)]
public class AddBotMessagesMigration : Migration
{
    public override void Up()
    {
        Create.Table("bot_messages")
            .WithColumn("id").AsInt32().Identity().PrimaryKey()
            .WithColumn("chat_id").AsInt64().NotNullable()
            .WithColumn("telegram_message_id").AsInt32().NotNullable()
            .WithColumn("type").AsString().NotNullable();

        Create.Index("ix_bot_messages_chat_type")
            .OnTable("bot_messages")
            .OnColumn("chat_id").Ascending()
            .OnColumn("type").Ascending()
            .WithOptions().Unique();
    }

    public override void Down()
    {
        Delete.Table("bot_messages");
    }
}
