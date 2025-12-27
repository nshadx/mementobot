using mementobot.Entities.States;

namespace mementobot.Entities
{
    internal class User
    {
        public long Id { get; set; }
        public Guid? StateId { get; set; }
        public State? State { get; set; }
    }
}
