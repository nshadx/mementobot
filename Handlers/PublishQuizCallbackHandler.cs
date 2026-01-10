using mementobot.Middlewares;
using mementobot.Services;
using mementobot.Telegram;
using Microsoft.Data.Sqlite;
using Telegram.Bot;

namespace mementobot.Handlers;

internal class PublishQuizCallbackHandler(
    SqliteConnection connection,
    QuizService quizService,
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
            
            quizService.PublishQuiz(
                quizId: quizId,
                transaction: transaction
            );

            await client.SendMessage(
                chatId: context.Update.GetChatId(),
                text: "Опросник опубликован"
            );
            
            transaction.Commit();
        }
    }
}
