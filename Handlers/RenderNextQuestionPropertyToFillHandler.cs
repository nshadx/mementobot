using mementobot.Constants;
using mementobot.Entities.States;
using mementobot.Extensions;
using mementobot.Services.Common;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace mementobot.Handlers;

internal class RenderNextQuestionPropertyToFillHandler(ITelegramBotClient client) : IMiddleware
{
    public async Task Invoke(Context context, UpdateDelegate next)
    {
        if (context.State is not AddQuestionsState state)
        {
            return;
        }

        Message message = null!;
        switch (state.CurrentProperty)
        {
            case AddQuestionsProperty.Question:
                message = await client.SendMessage(
                    chatId: context.Update.GetChatId(),
                    text: "Введи вопрос"
                );
                break;
            case AddQuestionsProperty.TextAnswer:
                message = await client.SendMessage(
                    chatId: context.Update.GetChatId(),
                    text: "Введи текстовый ответ к вопросу"
                );
                break;
            case AddQuestionsProperty.QuestionType:
                message = await client.SendMessage(
                    chatId: context.Update.GetChatId(),
                    text: "Выбери тип вопроса",
                    replyMarkup: new InlineKeyboardMarkup([
                        [new InlineKeyboardButton("Опросник", Callback.PollQuestionType), new InlineKeyboardButton("Текст", Callback.TextQuestionType)]
                    ])
                );
                break;
        }

        state.LastMessageId = message.Id;

        await next(context);
    }
}
