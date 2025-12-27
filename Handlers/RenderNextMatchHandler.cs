using mementobot.Constants;
using mementobot.Entities;
using mementobot.Entities.States;
using mementobot.Extensions;
using mementobot.Services;
using mementobot.Services.Common;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace mementobot.Handlers;

internal class RenderNextMatchHandler(
    AppDbContext dbContext,
    ITelegramBotClient client
) : IMiddleware
{
    public async Task Invoke(Context context, UpdateDelegate next)
    {
        if (context.State is not QuizState { CurrentQuestionId: Guid currentQuestionId, LastMessageId: int lastMessageId })
        {
            return;
        }
        
        var currentQuestion = await dbContext.UserQuizQuestions
            .Include(x => x.ChosenMatches)
            .Include(x => x.QuizQuestion)
            .SingleAsync(x => x.Id == currentQuestionId);

        if (currentQuestion.QuizQuestion is not MatchQuizQuestion matchQuizQuestion)
        {
            return;
        }
        
        await dbContext.Entry(matchQuizQuestion).Collection(x => x.Matches).LoadAsync();

        foreach (var match in matchQuizQuestion.Matches)
        {
            await dbContext.Entry(match).Reference(x => x.From).LoadAsync();
            await dbContext.Entry(match).Reference(x => x.To).LoadAsync();
        }
        
        var nextFrom = matchQuizQuestion.Matches
            .Where(x => !currentQuestion.ChosenMatches.Select(x => x.FromId).Contains(x.FromId))
            .Select(x => x.From)
            .FirstOrDefault();

        if (nextFrom is null)
        {
            await next(context);
            return;
        }
        
        var keyboard = matchQuizQuestion.Matches
            .Select(x => x.To)
            .Where(x => !currentQuestion.ChosenMatches.Select(x => x.ToId).Contains(x.Id))
            .Select(x => new InlineKeyboardButton(x.Value, $"{Callback.MatchIdPrefix}{nextFrom.Id};{Callback.MatchIdPrefix}{x.Id}"))
            .Chunk(1)
            .ToArray();

        await client.EditMessageText(
            chatId: context.Update.GetChatId(),
            messageId: lastMessageId,
            text: nextFrom.Value,
            replyMarkup: new InlineKeyboardMarkup(keyboard)
        );
    }
}