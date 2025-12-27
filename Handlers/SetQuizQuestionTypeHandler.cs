using mementobot.Entities;
using mementobot.Entities.States;
using mementobot.Extensions;
using mementobot.Services;
using mementobot.Services.Common;
using Telegram.Bot;

namespace mementobot.Handlers;

internal class SetQuizQuestionTypeHandler(ITelegramBotClient client) : IMiddleware
{
    public async Task Invoke(Context context, UpdateDelegate next)
    {
        if (context.Update.CallbackQuery is not { Data: string data })
        {
            return;
        }

        if (!Enum.TryParse<QuestionType>(data, out var questionType))
        {
            return;
        }

        if (context.State is not AddQuestionsState state)
        {
            return;
        }

        state.QuestionType = questionType;

        await client.DeleteMessage(
            chatId: context.Update.GetChatId(),
            messageId: state.LastMessageId
        );

        state.CurrentProperty = AddQuestionsProperty.Question;

        await next(context);
    }
}
internal class SetQuestionTextHandler(ITelegramBotClient client) : IMiddleware
{
    public async Task Invoke(Context context, UpdateDelegate next)
    {
        if (context.Update.Message is not { Id: int messageId, Text: string text })
        {
            return;
        }

        if (context.State is not AddQuestionsState state)
        {
            return;
        }

        state.Question = text;

        await client.DeleteMessage(
            chatId: context.Update.GetChatId(),
            messageId: state.LastMessageId
        );
        await client.DeleteMessage(
            chatId: context.Update.GetChatId(),
            messageId: messageId
        );

        state.CurrentProperty = AddQuestionsProperty.TextAnswer;

        await next(context);
    }
}
internal class SetQuestionAnswerHandler(
    ITelegramBotClient client,
    AppDbContext dbContext
) : IMiddleware
{
    public async Task Invoke(Context context, UpdateDelegate next)
    {
        if (context.Update.Message is not { Id: int messageId, Text: string answerText })
        {
            return;
        }

        if (context.State is not AddQuestionsState { Question: string questionText, QuestionType: QuestionType questionType } state)
        {
            return;
        }

        state.TextAnswer = answerText;

        var questionOrder = state.LastQuestionOrder + 1;

        TextQuizQuestion question = new()
        {
            QuizId = state.QuizId,
            Order = questionOrder,
            MatchAlgorithm = MatchAlgorithm.Exact,
            Question = questionText,
            Answer = answerText
        };

        await dbContext.QuizQuestions.AddAsync(question);

        state.LastQuestionOrder = questionOrder;

        await client.DeleteMessage(
            chatId: context.Update.GetChatId(),
            messageId: state.LastMessageId
        );
        await client.DeleteMessage(
            chatId: context.Update.GetChatId(),
            messageId: messageId
        );

        state.CurrentProperty = AddQuestionsProperty.QuestionType;

        await next(context);
    }
}