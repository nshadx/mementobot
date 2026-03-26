namespace mementobot.Services.Reminders;

internal interface IMotivationEngine
{
    IReadOnlyList<MotivationSpeech> GetSpeeches(QuizHistoryEntry quiz, UserSettings settings);
}
