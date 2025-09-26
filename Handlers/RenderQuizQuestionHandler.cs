using Microsoft.EntityFrameworkCore;
using repetitorbot.Entities.States;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace repetitorbot.Handlers;

internal class RenderQuizQuestionHandler(ITelegramBotClient client, AppDbContext dbContext) : IMiddleware
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

        var message = await client.SendMessage(
            chatId: context.Update.GetChatId(),
            text: currentQuestion.QuizQuestion.Question,
            replyMarkup: new InlineKeyboardMarkup(new InlineKeyboardButton("Пропустить", $"quizQuestionId:{currentQuestionId.ToString()}"))
        );
        
        state.LastMessageId = message.Id;
        
        await next(context);
    }
}
