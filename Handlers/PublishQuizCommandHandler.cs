using mementobot.Middlewares;
using mementobot.Services;
using mementobot.Telegram;
using Microsoft.Data.Sqlite;
using Telegram.Bot;

namespace mementobot.Handlers;

internal class PublishQuizCommandHandler(
    SqliteConnection connection,
    StateService stateService,
    QuizService quizService,
    MessageManager messageManager,
    ITelegramBotClient client
) : IRouteHandler
{
    public async Task Handle(Context context)
    {
        using (var transaction = connection.BeginTransaction())
        {
            if (context.Update.Message is not { Id: int messageId })
            {
                return;
            }
            
            await client.DeleteMessage(
                chatId: context.Update.GetChatId(),
                messageId: messageId
            );
            
            var stateId = stateService.SetSelectQuizUserState(
                userId: context.UserId,
                actionType: ActionType.Publish,
                transaction: transaction
            );
            var quizIds = quizService.GetNotPublishedUserQuizIds(
                userId: context.UserId,
                transaction: transaction
            );
        
            var counter = 1;
            var page = 1;
            foreach (var quizId in quizIds)
            {
                if (counter <= 6)
                {
                    stateService.AddQuizForSelect(
                        stateId: stateId,
                        quizId: quizId,
                        page: page,
                        transaction: transaction
                    );
                
                    counter++;
                }

                page++;
            }

            var firstPageQuizzes = stateService.GetQuizNamesWithId(
                stateId: stateId,
                page: 1,
                transaction: transaction
            ).ToList();
        
            var newMessageId = await messageManager.SelectPollMessage(
                chatId: context.Update.GetChatId(),
                quizzes: firstPageQuizzes
            );

            stateService.SetMessageId(
                stateId: stateId,
                type: StateType.SelectQuizUserState,
                messageId: newMessageId,
                transaction: transaction
            );
            
            transaction.Commit();
        }
    }
}
