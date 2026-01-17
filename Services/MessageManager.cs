using mementobot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace mementobot.Telegram;

internal class MessageManager(
    ITelegramBotClient client
)
{
    public Task DeleteMessage(long chatId, int messageId)
    {
        return client.DeleteMessage(
            chatId: chatId,
            messageId: messageId
        );
    }
    
    public async Task<int> EnterQuestionMessage(long chatId)
    {
        var message = await client.SendMessage(
            chatId: chatId,
            text: "Введи название вопроса"
        );
        return message.Id;
    }
    
    public async Task<int> EnterAnswerMessage(long chatId)
    {
        var message = await client.SendMessage(
            chatId: chatId,
            text: "Введи ответ к вопрсоу"
        );
        return message.Id;
    }
    
    public async Task<int> CreateNewQuizMessage(long chatId)
    {
        var message = await client.SendMessage(
            chatId: chatId,
            text: "Опросник создан"
        );
        return message.Id;
    }
    
    public async Task<int> SelectPollMessage(
        long chatId,
        IReadOnlyCollection<Quiz> quizzes,
        int? editMessageId = null
    )
    {
        Message message;
        
        if (quizzes.Count == 0)
        {
            message = await client.SendMessage(
                chatId: chatId,
                text: "📭 Сейчас нет доступных опросников."
            );
            return message.Id;
        }

        var keyboard = new InlineKeyboardMarkup(
            inlineKeyboard: quizzes
                .Select(x => new InlineKeyboardButton(x.Name, x.Id.ToString()))
                .Chunk(3)
                .Append([
                    new InlineKeyboardButton("←", "back"),
                    new InlineKeyboardButton("→", "forward")
                ])
        );

        if (editMessageId is int i)
        {
            message = await client.EditMessageReplyMarkup(
                chatId: chatId,
                messageId: i,
                replyMarkup: keyboard
            );
        }
        else
        {
            message = await client.SendMessage(
                chatId: chatId,
                replyMarkup: keyboard,
                text: "🔎 Найдено несколько опросников. Выбери нужный:"
            );
        }

        return message.Id;
    }

    public async Task<int> SendQuizPublishedMessage(long chatId)
    {
        var message = await client.SendMessage(
            chatId: chatId,
            text: "Опросник опубликован"
        );
        return message.Id;
    }
}