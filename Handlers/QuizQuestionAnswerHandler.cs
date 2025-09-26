using FuzzySharp;
using Microsoft.EntityFrameworkCore;
using repetitorbot.Entities;
using repetitorbot.Entities.States;
using Scriban;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace repetitorbot.Handlers;

internal class QuizQuestionAnswerHandler(
    AppDbContext dbContext,
    ITelegramBotClient client
) : IMiddleware
{
    public async Task Invoke(Context context, UpdateDelegate next)
    {
        if (context.State is not QuizState { CurrentQuestionId: Guid currentQuestionId } state)
        {
            return;
        }
        
        var question = await dbContext.UserQuizQuestions
            .Include(x => x.QuizQuestion)
            .SingleAsync(x => x.Id == currentQuestionId);

        var orderNew = 0;
        switch (question.QuizQuestion)
        {
            case TextQuizQuestion textQuizQuestion:
                if (context.Update.Message is not { Text: string text })
                {
                    return;
                }

                var trimmedUserAnswer = text.Trim('.', ';', ' ');
                var correctAnswer = textQuizQuestion.Answer;
                
                var loweredCorrectAnswer = correctAnswer.ToLower();
                var loweredUserAnswer = trimmedUserAnswer.ToLower();
                        
                var score = Fuzz.Ratio(loweredUserAnswer, loweredCorrectAnswer);
                        
                (orderNew, var message) = score switch
                {
                    100 when question.QuizQuestion.MatchAlgorithm is MatchAlgorithm.Exact => (0, "отлично! 🎉"),
                    >= 80 when question.QuizQuestion.MatchAlgorithm is MatchAlgorithm.Fuzzy => (0, "отлично! 🎉"),
                    >= 50 when question.QuizQuestion.MatchAlgorithm is MatchAlgorithm.Fuzzy => (5, "неплохо, результат {{percent}}. Вопрос будет повторен через 5 ⏳"),
                    _ => (3, "❌ очень плохо 😢 Вопрос будет повторен через 3 ⏳")
                };

                var template = Template.Parse(
                    $"Вы ответили: {message}\n\n" +
                    "Правильный ответ:\n\n" +
                    "`{{answer}}`"
                );

                var messageText = await template.RenderAsync(new
                {
                    Score = message,
                    Answer = correctAnswer,
                    Percent = $"{score}%"
                });

                await client.SendMessage(
                    chatId: context.Update.GetChatId(),
                    text: messageText,
                    ParseMode.Markdown
                );
                break;
            case PollQuizQuestion pollQuizQuestion:
                throw new NotImplementedException();
        }

        state.OrderNew = question.Order + orderNew;

        await next(context);
    }
}
