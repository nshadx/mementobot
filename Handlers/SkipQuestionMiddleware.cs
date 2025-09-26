using repetitorbot.Constants;
using repetitorbot.Entities.States;
using Telegram.Bot;

namespace repetitorbot.Handlers;

internal class SkipQuestionMiddleware(
    ITelegramBotClient client
) : IMiddleware
{
    public async Task Invoke(Context context, UpdateDelegate next)
    {
        if (context.Update.CallbackQuery is not { Data: string data, Message.Id: int messageId })
        {
            return;
        }

        if (context.State is not QuizState state)
        {
            return;
        }

        if (!Guid.TryParse(data[Callback.QuizQuestionIdPrefix.Length..], out var questionId))
        {
            return;
        }

        await client.DeleteMessage(
            chatId: context.Update.GetChatId(),
            messageId: messageId
        );

        if (questionId == state.CurrentQuestionId)
        {
            await next(context);
        }
    }
}
