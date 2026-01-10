using mementobot.Middlewares;
using mementobot.Services;
using mementobot.Telegram;
using Microsoft.Data.Sqlite;
using Telegram.Bot;

namespace mementobot.Handlers;

internal class AddQuizQuestionMessageHandler(
    SqliteConnection connection,
    StateService stateService,
    ITelegramBotClient client
) : IRouteHandler
{
    public async Task Handle(Context context)
    {
        using (var transaction = connection.BeginTransaction())
        {
            if (context.Update.Message is not { Text: string text })
            {
                return;
            }

            if (stateService.GetStateId(context.UserId, StateType.AddQuestionUserState, transaction) is not int stateId)
            {
                return;
            }
            
            if (stateService.GetMessageId(stateId, StateType.AddQuestionUserState, transaction) is not int messageId)
            {
                return;
            }
            
            await client.DeleteMessage(
                chatId: context.Update.GetChatId(),
                messageId: messageId
            );
            
            var question = stateService.GetProperty(
                stateId: stateId,
                propertyName: PropertyName.Question,
                transaction: transaction
            );
            if (question is null)
            {
                stateService.InsertProperty(
                    stateId: stateId,
                    propertyName: PropertyName.Question,
                    text: text,
                    transaction: transaction
                );
                
                var newMessage = await client.SendMessage(
                    chatId: context.Update.GetChatId(),
                    text: "Введи ответ к вопросу"
                );
                var newMessageId = newMessage.MessageId;

                stateService.SetMessageId(
                    stateId: stateId,
                    type: StateType.AddQuestionUserState,
                    messageId: newMessageId,
                    transaction: transaction
                );
            }
            else
            {
                stateService.InsertProperty(
                    stateId: stateId,
                    propertyName: PropertyName.Answer,
                    text: text,
                    transaction: transaction
                );
                stateService.AddQuizQuestion(
                    stateId: stateId,
                    transaction: transaction
                );
            }

            transaction.Commit();
        }
    }
}