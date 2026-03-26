namespace mementobot.Telegram;

internal interface IContextAccessor
{
    Context Current { get; set; }
}

internal class ContextAccessor : IContextAccessor
{
    public Context Current { get; set; } = null!;
}
