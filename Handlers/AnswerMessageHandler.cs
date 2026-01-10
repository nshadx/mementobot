using FuzzySharp;
using mementobot.Middlewares;
using mementobot.Services;
using mementobot.Telegram;
using Microsoft.Data.Sqlite;
using Scriban;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace mementobot.Handlers;

internal class AnswerMessageHandler(
    SqliteConnection connection,
    StateService stateService,
    QuizService quizService,
    ITelegramBotClient client
) : IRouteHandler
{
    private readonly Template _responseTemplate = Template.Parse("""
                                                                 Вы ответили: {{
                                                                 bad_answer = false
                                                                 if score > 80
                                                                     "отлично! 🎉"
                                                                 else if score >= 50 && score <= 80
                                                                     $"неплохо, но нужно стараться."
                                                                     bad_answer = true
                                                                 else
                                                                     $"очень плохо!"
                                                                     bad_answer = true
                                                                 end
                                                                 if has_next && bad_answer
                                                                    $" Вопрос будет повторен через {repeat_time}."
                                                                 end
                                                                 }}

                                                                 Правильный ответ:

                                                                 `{{correct_answer}}`
                                                                 """);

    public async Task Handle(Context context)
    {
        using (var transaction = connection.BeginTransaction())
        {
            if (context.Update.Message is not { Text: string text })
            {
                return;
            }
        
            if (stateService.GetStateId(context.UserId, StateType.QuizProgressUserState, transaction) is not int stateId)
            {
                return;
            }
            
            if (stateService.GetMessageId(stateId, StateType.QuizProgressUserState, transaction) is not int messageId)
            {
                return;
            }

            var currentQuestionId = stateService.GetCurrentQuizQuestionId(
                stateId: stateId,
                transaction: transaction
            );
            var correctAnswer = quizService.GetQuizQuestionAnswer(
                questionId: currentQuestionId,
                transaction: transaction
            );
            var userAnswer = text;
            
            var score = Fuzz.TokenSortRatio(correctAnswer, userAnswer);
            var orderNew = score switch
            {
                100 => 0,
                >= 80 => 0,
                >= 50 => 5,
                _ => 3
            };

            stateService.SetQuizQuestionOrder(
                questionId: currentQuestionId,
                order: orderNew,
                transaction: transaction
            );

            var question = stateService.SetNextQuestion(
                stateId: stateId,
                transaction: transaction
            );
            
            var messageText = await _responseTemplate.RenderAsync(new
            {
                Score = score,
                CorrectAnswer = correctAnswer,
                RepeatTime = orderNew,
                HasNext = question is not null
            });

            await client.SendMessage(
                chatId: context.Update.GetChatId(),
                text: messageText,
                ParseMode.Markdown
            );
            
            if (question is null)
            {
                stateService.FinishQuiz(
                    userId: context.UserId,
                    stateId: stateId,
                    transaction: transaction
                );

                await client.SendMessage(
                    chatId: context.Update.GetChatId(),
                    text: "Опросник завершен"
                );
            }
            else
            {
                var newMessage = await client.SendMessage(
                    chatId: context.Update.GetChatId(),
                    text: question,
                    replyMarkup: new InlineKeyboardMarkup(new InlineKeyboardButton("Пропустить", "skip"))
                );
                var newMessageId = newMessage.MessageId;
            
                stateService.SetMessageId(
                    stateId: stateId,
                    type: StateType.QuizProgressUserState,
                    messageId: newMessageId,
                    transaction: transaction
                );
            }
        
            transaction.Commit();
        }
    }
}
