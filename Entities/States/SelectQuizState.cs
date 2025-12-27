namespace mementobot.Entities.States;

internal class SelectQuizState : State
{
    public int? MessageId { get; set; }
    public int ItemsPerPage { get; set; }
    public int PagesCount { get; set; }
    public int CurrentPage { get; set; }
    public bool? WherePublished { get; set; }
    public bool OnlyFromUser { get; set; }
    public QuizType? Type { get; set; }
}
internal class StartSelectQuizState : SelectQuizState
{
    public QuizErrorHandleMode ErrorHandleMode { get; set; }
}
internal class PublishSelectQuizState : SelectQuizState
{
}
internal class QuestionsSelectQuizState : SelectQuizState
{
}
internal enum QuizErrorHandleMode
{
    RepeatAfterN,
    Category
}
