namespace mementobot.Services.Reminders;

internal interface IQuizSelectionEngine
{
    QuizHistoryEntry? SelectForUser(int userId);
}
