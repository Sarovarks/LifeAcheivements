using LifeAchievements.Domain.Events;

namespace LifeAchievements.Application.Abstractions;

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken ct);
}

public interface IDomainEventDispatcher
{
    Task Dispatch<TEvent>(TEvent domainEvent, CancellationToken ct = default) where TEvent : IDomainEvent;
}
