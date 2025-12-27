namespace mementobot.Entities.States;

internal class State
{
    public Guid Id { get; set; }
    public long UserId { get; set; }
    public User User { get; set; } = null!;
}
