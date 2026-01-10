using mementobot.Middlewares;
using mementobot.Services;
using mementobot.Telegram;
using Microsoft.Data.Sqlite;
using Telegram.Bot;

namespace mementobot.Handlers;

internal class BackPageCallbackHandler(
    SqliteConnection connection,
    StateService stateService,
    MessageManager messageManager,
    ITelegramBotClient client
) : IRouteHandler
{
    public async Task Handle(Context context)
    {
        using (var transaction = connection.BeginTransaction())
        {
            if (context.Update.CallbackQuery is not { Id: string callbackQueryId })
            {
                return;
            }
            
            await client.AnswerCallbackQuery(
                callbackQueryId: callbackQueryId
            );
            
            if (stateService.GetStateId(context.UserId, StateType.SelectQuizUserState, transaction) is not int stateId)
            {
                return;
            }

            var currentPage = stateService.GetCurrentPage(
                stateId: stateId,
                transaction: transaction
            ); 
            if (currentPage <= 1)
            {
                return;
            }

            stateService.DecrementPage(stateId);

            var quizNames = stateService.GetQuizNamesWithId(
                stateId: stateId,
                page: currentPage - 1,
                transaction: transaction
            ).ToList();
            var messageId = stateService.GetMessageId(
                stateId: stateId,
                type: StateType.SelectQuizUserState
            );
            var newMessageId = await messageManager.SelectPollMessage(
                chatId: context.Update.GetChatId(),
                quizzes: quizNames,
                editMessageId: messageId
            );
            if (messageId is null)
            {
                stateService.SetMessageId(stateId, StateType.SelectQuizUserState, newMessageId);
            }
            
            transaction.Commit();
        }
    }
}