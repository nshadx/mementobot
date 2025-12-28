using FuzzySharp;
using mementobot.Entities;
using mementobot.Entities.States;
using mementobot.Middlewares;
using mementobot.Services;
using mementobot.Telegram;
using Microsoft.EntityFrameworkCore;
using Scriban;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace mementobot.Handlers;

internal class QuizQuestionAnswerHandler(
    AppDbContext dbContext,
    ITelegramBotClient client
) : IMiddleware
{
    private readonly Template _responseTemplate = Template.Parse("""
                                                                 Вы ответили: {{
                                                                 if score > 80
                                                                     "отлично! 🎉"
                                                                 else if score > 50 && score < 80
                                                                     $"неплохо, но нужно стараться, вы набрали {score}%. Вопрос будет повторен через {repeat_time}"
                                                                 else 
                                                                     $"очень плохо! Вопрос будет повторен через {repeat_time}"
                                                                 end
                                                                 }}

                                                                 Правильный ответ:

                                                                 `{{correct_answer}}`
                                                                 """);

    public async Task Invoke(Context context, UpdateDelegate next)
    {
        if (context.State is not QuizState { CurrentQuestionId: Guid currentQuestionId } state)
        {
            return;
        }

        var question = await dbContext.UserQuizQuestions
            .Include(x => x.QuizQuestion).Include(userQuizQuestion => userQuizQuestion.ChosenMatches)
            .SingleAsync(x => x.Id == currentQuestionId);

        var orderNew = 0;
        var score = 0;
        string correctAnswer = null!;
        switch (question.QuizQuestion)
        {
            case TextQuizQuestion textQuizQuestion:
                if (context.Update.Message is not { Text: string text })
                {
                    return;
                }

                var trimmedUserAnswer = text.Trim('.', ';', ' ');

                correctAnswer = textQuizQuestion.Answer;

                var loweredCorrectAnswer = correctAnswer.ToLower();
                var loweredUserAnswer = trimmedUserAnswer.ToLower();

                score = Fuzz.Ratio(loweredUserAnswer, loweredCorrectAnswer);
                orderNew = score switch
                {
                    100 => 0,
                    >= 80 when textQuizQuestion.MatchAlgorithm is MatchAlgorithm.Fuzzy => 0,
                    >= 50 when textQuizQuestion.MatchAlgorithm is MatchAlgorithm.Fuzzy => 5,
                    _ => 3
                };

                break;
            case PollQuizQuestion pollQuizQuestion:
                var correctVariants = pollQuizQuestion.Variants
                    .Where(x => x.IsCorrect)
                    .ToList();

                var correctVariantIds = correctVariants.Select(x => x.Id).ToHashSet();
                
                correctAnswer = string.Join('\n', correctVariants.Select(x => x.Value));
                
                var answerCorrect = question.ChosenVariants.All(x => correctVariantIds.Contains(x));
                
                orderNew = answerCorrect ? 0 : 3;
                score = answerCorrect ? 100 : -1;
                
                break;
            case MatchQuizQuestion matchQuizQuestion:
                var correctMatches = matchQuizQuestion.Matches;

                var currentMatches = question.ChosenMatches;
                
                var answerCorrect2 = correctMatches.All(x => currentMatches.Select(x => (x.FromId, x.ToId)).Contains((x.FromId, x.ToId)));
                
                correctAnswer = string.Join(",\n", correctMatches.Select(x => $"{x.From.Value} -> {x.To.Value}"));
                
                orderNew = answerCorrect2 ? 0 : 3;
                score = answerCorrect2 ? 100 : -1;
                
                break;
        }
        
        state.OrderNew = question.Order + orderNew;

        var messageText = await _responseTemplate.RenderAsync(new
        {
            Score = score,
            CorrectAnswer = correctAnswer,
            RepeatTime = orderNew
        });

        await client.SendMessage(
            chatId: context.Update.GetChatId(),
            text: messageText,
            ParseMode.Markdown
        );

        await next(context);
    }
}
