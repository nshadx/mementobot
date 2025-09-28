namespace repetitorbot.Entities.States;

internal class QuizState : State
{
    public Guid QuizId { get; set; }
    public Quiz Quiz { get; set; } = null!;
    public Guid? CurrentQuestionId { get; set; }
    public int OrderNew { get; set; }
    public int LastMessageId { get; set; }
    public List<UserQuizQuestion> Questions { get; set; } = []; 
}
internal class UserQuizQuestion
{
    public Guid Id { get; set; }
    public Guid QuizStateId { get; set; }
    public QuizState QuizState { get; set; } = null!;
    public Guid QuizQuestionId { get; set; }
    public QuizQuestion QuizQuestion { get; set; } = null!;
    public int Order { get; set; }
    public List<Guid> ChosenVariants { get; set; } = [];
    public List<UserSelectedMatch> ChosenMatches { get; set; } = [];
}

internal class UserSelectedMatch
{
    public Guid UserQuizQuestionId { get; set; }
    public UserQuizQuestion UserQuizQuestion { get; set; } = null!;
    public int FromId { get; set; }
    public MatchOption From { get; set; } = null!;
    public int ToId { get; set; }
    public MatchOption To { get; set; } = null!;
}