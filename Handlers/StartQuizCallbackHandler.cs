using mementobot.Middlewares;
using mementobot.Services;
using mementobot.Telegram;
using Microsoft.Data.Sqlite;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace mementobot.Handlers;

internal class StartQuizCallbackHandler(
    SqliteConnection connection,
    StateService stateService,
    ITelegramBotClient client
) : IRouteHandler
{
    public async Task Handle(Context context)
    {
        using (var transaction = connection.BeginTransaction())
        {
            if (context.Update.CallbackQuery is not { Data: string data, Id: string callbackQueryId })
            {
                return;
            }
            
            await client.AnswerCallbackQuery(
                callbackQueryId: callbackQueryId
            );

            if (!int.TryParse(data, out var quizId))
            {
                return;
            }

            if (stateService.GetStateId(context.UserId, StateType.SelectQuizUserState, transaction) is not int stateId)
            {
                return;
            }
           
            if (stateService.GetMessageId(stateId, StateType.SelectQuizUserState, transaction) is not int messageId)
            {
                return;
            }
            
            await client.DeleteMessage(
                chatId: context.Update.GetChatId(),
                messageId: messageId
            );
            
            stateService.CompleteSelect(
                userId: context.UserId,
                stateId: stateId,
                transaction: transaction
            );
            
            var newStateId = stateService.SetQuizProgressUserState(
                userId: context.UserId,
                quizId: quizId,
                transaction: transaction
            );
            
            stateService.CloneQuizQuestions(
                stateId: newStateId,
                quizId: quizId,
                transaction: transaction
            );

            var question = stateService.SetNextQuestion(
                stateId: newStateId,
                transaction: transaction
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
                    stateId: newStateId,
                    type: StateType.QuizProgressUserState,
                    messageId: newMessageId,
                    transaction: transaction
                );
            }
            
            transaction.Commit();
        }
    }
}

