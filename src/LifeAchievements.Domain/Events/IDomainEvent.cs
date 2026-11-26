namespace LifeAchievements.Domain.Events;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
