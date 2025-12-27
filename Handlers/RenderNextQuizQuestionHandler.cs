using mementobot.Constants;
using mementobot.Entities;
using mementobot.Entities.States;
using mementobot.Extensions;
using mementobot.Services;
using mementobot.Services.Common;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace mementobot.Handlers;

internal class RenderNextQuizQuestionHandler(ITelegramBotClient client, AppDbContext dbContext) : IMiddleware
{
    public async Task Invoke(Context context, UpdateDelegate next)
    {
        if (context.State is not QuizState { CurrentQuestionId: Guid currentQuestionId } state)
        {
            await client.SendMessage(
                chatId: context.Update.GetChatId(),
                text: "Вы завершили опросник"
            );
            return;
        }

        var currentQuestion = await dbContext.UserQuizQuestions
            .Include(x => x.QuizQuestion)
            .SingleAsync(x => x.Id == currentQuestionId);

        Message message = null!;
        switch (currentQuestion.QuizQuestion)
        {
            case TextQuizQuestion textQuizQuestion:
                message = await client.SendMessage(
                    chatId: context.Update.GetChatId(),
                    text: textQuizQuestion.Question,
                    replyMarkup: new InlineKeyboardMarkup(new InlineKeyboardButton("Пропустить", $"{Callback.QuizQuestionIdPrefix}{currentQuestionId.ToString()}"))
                );
                break;
            case PollQuizQuestion pollQuizQuestion:
                await dbContext.Entry(pollQuizQuestion).Collection(x => x.Variants).LoadAsync();

                var keyboard = pollQuizQuestion.Variants
                    .Select(x => new InlineKeyboardButton(x.Value, $"{Callback.PollVariantIdPrefix}{x.Id}"))
                    .Chunk(3)
                    .Append([new InlineKeyboardButton("Ответить", Callback.PollSubmit)])
                    .ToArray();
                
                message = await client.SendMessage(
                    chatId: context.Update.GetChatId(),
                    text: pollQuizQuestion.Question,
                    replyMarkup: new InlineKeyboardMarkup(keyboard)
                );
                break;
            case MatchQuizQuestion matchQuizQuestion:
                await dbContext.Entry(matchQuizQuestion).Collection(x => x.Matches).LoadAsync();

                foreach (var match in matchQuizQuestion.Matches)
                {
                    await dbContext.Entry(match).Reference(x => x.From).LoadAsync();
                    await dbContext.Entry(match).Reference(x => x.To).LoadAsync();
                }

                var first = matchQuizQuestion.Matches[0].From;
                
                var keyboard2 = matchQuizQuestion.Matches
                    .Select(x => x.To)
                    .Select(x => new InlineKeyboardButton(x.Value, $"{Callback.MatchIdPrefix}{first.Id};{Callback.MatchIdPrefix}{x.Id}"))
                    .Chunk(1)
                    .ToArray();

                message = await client.SendMessage(
                    chatId: context.Update.GetChatId(),
                    text: first.Value,
                    replyMarkup: new InlineKeyboardMarkup(keyboard2)
                );
                break;
        }
        
        state.LastMessageId = message.Id;
        
        await next(context);
    }
}
