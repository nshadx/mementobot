using System.Text;
using mementobot.Services.Quizzing;
using mementobot.Telegram.Messages;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace mementobot.Services.Messages;

internal record CompletedAnsweringData(QuizQuestion Question, int Score, int RepeatsAfter);

internal class CompletedAnsweringMessage(ITelegramBotClient client, IMessageStore store)
    : BotMessage<CompletedAnsweringData>(client, store)
{
    protected override async Task<int> Send(long chatId, CompletedAnsweringData data)
    {
        var (icon, verdict) = data.Score switch
        {
            >= 80 => ("🎉", "отлично!"),
            >= 50 => ("🤔", "неплохо, но можно лучше."),
            _ => ("😬", "нужно подтянуть!")
        };

        var sb = new StringBuilder();
        sb.AppendLine($"{icon} *{verdict}* ({data.Score}%)");
        sb.AppendLine();
        sb.AppendLine("📝 Правильный ответ:");
        sb.AppendLine($"`{data.Question.Answer}`");

        if (data.RepeatsAfter > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"🔄 Вопрос повторится через {data.RepeatsAfter}");
        }

        var msg = await client.SendMessage(chatId, sb.ToString(), parseMode: ParseMode.Markdown);
        return msg.Id;
    }
}
