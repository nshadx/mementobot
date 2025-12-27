namespace mementobot.Entities.States;

internal class AddQuestionsState : State
{
    public Guid QuizId { get; set; }
    public Quiz Quiz { get; set; } = null!;
    public AddQuestionsProperty CurrentProperty { get; set; }
    public string? Question { get; set; }
    public string? TextAnswer { get; set; }
    public QuestionType? QuestionType { get; set; }
    public int LastQuestionOrder { get; set; }
    public int LastMessageId { get; set; }
}
internal enum AddQuestionsProperty
{
    Question,
    TextAnswer,
    QuestionType
}
internal enum QuestionType
{
    None,
    Poll,
    Text
}
