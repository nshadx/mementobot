using mementobot.Telegram.Messages;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace mementobot.Services.Messages;

internal class HelpGraphMessage(ITelegramBotClient client, IMessageStore store)
    : BotMessage(client, store)
{
    protected override async Task<int> Send(long chatId)
    {
        var msg = await client.SendMessage(chatId,
            """
            ```
            /start
            ├─ /start {id} ───────────────────────────────┐
            └─ Главное меню                                │
               ├─ ⭐ Избранные ──┐                        │
               ├─ 🕐 Недавние   ├─ Список ─► Меню ◄───────┘
               └─ 🔎 Поиск ─────┘

            /myquizzes
            ├─ 📚 Мои ──────────┐
            ├─ ⭐ Избранные ────┼─ Список ─► Меню
            └─ 🕐 Недавние ─────┘

            Список
            ├─ [◄] [►]  листать страницы
            └─ [название] ──────────────► Меню

            Меню опросника
            ├─ ▶️ Пройти ──────────────── Прохождение
            ├─ ⭐/❌ Избранное            (только чужие)
            └─ 📎 Поделиться              (только опубликованные)

            Прохождение
            ├─ ❓ Вопрос
            │   └─ [⏭ Пропустить]
            ├─ Ответ текстом ──── оценка + правильный ответ
            └─ Итоговая сводка (по завершении)

            Управление
            ├─ /new <название>  создать опросник
            ├─ /add             выбор ─ вопрос ─ ответ
            └─ /publish         выбор ─ ссылка для прохождения

            /settings
            ├─ 🕐 Час напоминания
            └─ 🌡 Креативность ответов
            ```
            """,
            parseMode: ParseMode.MarkdownV2);
        return msg.Id;
    }
}
