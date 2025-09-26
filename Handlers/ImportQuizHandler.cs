using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using repetitorbot.Entities;
using repetitorbot.Services.Common;
using Telegram.Bot;

namespace repetitorbot.Handlers;

[JsonDerivedType(typeof(TextQuizQuestionDto), 0)]
[JsonDerivedType(typeof(PollQuizQuestionDto), 1)]
internal class QuizQuestionDto
{
    public string Question { get; set; } = null!;
    public MatchAlgorithm Algorithm { get; set; }
    public List<string> CategoryNames { get; set; } = [];
}
internal class TextQuizQuestionDto : QuizQuestionDto
{
    public string Answer { get; set; } = null!;
}
internal class PollQuizQuestionDto : QuizQuestionDto
{
    public List<PollQuizQuestionVariantDto> Variants { get; set; } = [];
}
internal class PollQuizQuestionVariantDto
{
    public string Value { get; set; } = null!;
    public bool IsCorrect { get; set; }
}
internal class QuizDto
{
    public string Name { get; set; } = null!;
    public List<QuizQuestionDto> Questions { get; set; } = [];
}
internal class ImportQuizHandler(ITelegramBotClient client, TelegramFileService telegramFileService, AppDbContext dbContext) : IMiddleware
{
    public async Task Invoke(Context context, UpdateDelegate next)
    {
        if (context.Update.Message is not { Id: int messageId, Document.FileId: string fileId })
        {
            return;
        }

        QuizDto dto;
        using (MemoryStream ms = new())
        {
            await telegramFileService.Download(fileId, ms);
            dto = JsonSerializer.Deserialize<QuizDto>(ms) ?? throw new InvalidOperationException("invalid json quiz format");
        }

        List<QuizQuestion> questions = new(dto.Questions.Count);
        var order = 1;
        foreach (var question in dto.Questions)
        {
            questions.Add(await Map(question, order));
            order++;
        }

        await dbContext.Quizes.AddAsync(new()
        {
            Name = dto.Name,
            Questions = questions,
            Type = QuizType.Public,
            Published = true
        });
        await dbContext.SaveChangesAsync();

        await client.DeleteMessage(
            chatId: context.Update.GetChatId(),
            messageId: messageId
        );
        await client.SendMessage(
            chatId: context.Update.GetChatId(),
            text: "✅ Квиз успешно импортирован!"
        );

        await next(context);
    }

    private async Task<QuizQuestion> Map(QuizQuestionDto dto, int order)
    {
        var categories = await dbContext.QuizQuestionCategories
            .Where(x => dto.CategoryNames.Contains(x.Name))
            .Select(x => new QuizQuestionCategoryLink() { QuizQuestionCategoryId = x.Id })
            .ToListAsync();
        return dto switch
        {
            TextQuizQuestionDto text => new TextQuizQuestion()
            {
                Question = text.Question,
                Categories = categories,
                Answer = text.Answer,
                Order = order
            },
            PollQuizQuestionDto poll => new PollQuizQuestion()
            {
                Question = poll.Question,
                Categories = categories,
                Order = order,
                Variants = [.. poll.Variants.Select(x => new PollQuestionVariant() { IsCorrect = x.IsCorrect, Value = x.Value })]
            },
            _ => throw new InvalidOperationException("not supported")
        };
    }
}
