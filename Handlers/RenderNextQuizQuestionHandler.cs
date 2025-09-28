using Microsoft.EntityFrameworkCore;
using repetitorbot.Constants;
using repetitorbot.Entities;
using repetitorbot.Entities.States;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace repetitorbot.Handlers;

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
                    replyMarkup: new InlineKeyboardMarkup(new InlineKeyboardButton("Пропустить", $"quizQuestionId:{currentQuestionId.ToString()}"))
                );
                break;
            case PollQuizQuestion pollQuizQuestion:
                await dbContext.Entry(pollQuizQuestion).Collection(x => x.Variants).LoadAsync();

                var keyboard = pollQuizQuestion.Variants
                    .Select(x => new InlineKeyboardButton(x.Value, $"variantId:{x.Id}"))
                    .Chunk(3)
                    .Append([new InlineKeyboardButton("Ответить", Callback.PollSubmit)])
                    .ToArray();
                
                message = await client.SendMessage(
                    chatId: context.Update.GetChatId(),
                    text: pollQuizQuestion.Question,
                    replyMarkup: new InlineKeyboardMarkup(keyboard)
                );
                break;
        }
        
        state.LastMessageId = message.Id;
        
        await next(context);
    }
}
