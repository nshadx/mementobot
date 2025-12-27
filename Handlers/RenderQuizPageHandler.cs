using mementobot.Constants;
using mementobot.Entities.States;
using mementobot.Extensions;
using mementobot.Services;
using mementobot.Services.Common;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace mementobot.Handlers;

internal class RenderQuizPageHandler(ITelegramBotClient client, AppDbContext dbContext) : IMiddleware
{
    public async Task Invoke(Context context, UpdateDelegate next)
    {
        if (context.State is not SelectQuizState state)
        {
            return;
        }

        var quizes = dbContext.Quizes
            .Where(x => state.WherePublished == null ? true : x.Published == state.WherePublished)
            .Where(x => state.OnlyFromUser ? x.UserId == context.User.Id : true)
            .Where(x => state.Type == null ? true : x.Type == state.Type)
            .Skip(state.ItemsPerPage * (state.CurrentPage - 1))
            .Take(state.ItemsPerPage)
            .OrderBy(x => x.Name)
            .ToList();

        if (quizes.Count == 0)
        {
            await client.SendMessage(
                chatId: context.Update.GetChatId(),
                text: "📭 Сейчас нет доступных опросников."
            );
            return;
        }

        var keyboard = new InlineKeyboardMarkup(
            inlineKeyboard: quizes
                .Select(x => new InlineKeyboardButton(x.Name!, $"{Callback.QuizIdPrefix}{x.Id}"))
                .Chunk(3)
                .Append([
                    new InlineKeyboardButton("←", Callback.BackPage),
                    new InlineKeyboardButton("→", Callback.ForwardPage)
                ])
        );

        if (state.MessageId is not int messageId)
        {
            var message = await client.SendMessage(
                chatId: context.Update.GetChatId(),
                replyMarkup: keyboard,
                text: "🔎 Найдено несколько опросников. Выбери нужный:"
            );
            state.MessageId = message.Id;
        }
        else
        {
            await client.EditMessageReplyMarkup(
                chatId: context.Update.GetChatId(),
                messageId: messageId,
                replyMarkup: keyboard
            );
        }
        
        await next(context);
    }
}
