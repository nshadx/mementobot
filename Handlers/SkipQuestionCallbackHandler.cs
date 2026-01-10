using mementobot.Middlewares;
using mementobot.Services;
using mementobot.Telegram;
using Microsoft.Data.Sqlite;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace mementobot.Handlers;

internal class SkipQuestionCallbackHandler(
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

            if (stateService.GetStateId(context.UserId, StateType.QuizProgressUserState, transaction) is not int stateId)
            {
                return;
            }
            
            if (stateService.GetMessageId(stateId, StateType.QuizProgressUserState, transaction) is not int messageId)
            {
                return;
            }
        
            await client.DeleteMessage(
                chatId: context.Update.GetChatId(),
                messageId: messageId
            );
        
            var question = stateService.SetNextQuestion(
                stateId: stateId,
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
                    stateId: stateId,
                    type: StateType.QuizProgressUserState,
                    messageId: newMessageId,
                    transaction: transaction
                );
            }
        }
    }
}
