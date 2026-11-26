using LifeAchievements.Application.Abstractions;
using LifeAchievements.Domain.Events;
using Microsoft.Extensions.DependencyInjection;

namespace LifeAchievements.Application.Common;

/// <summary>Calls every handler registered for the event type, in registration order.</summary>
public sealed class DomainEventDispatcher(IServiceProvider services) : IDomainEventDispatcher
{
    public async Task Dispatch<TEvent>(TEvent domainEvent, CancellationToken ct = default) where TEvent : IDomainEvent
    {
        foreach (var handler in services.GetServices<IDomainEventHandler<TEvent>>())
        {
            await handler.Handle(domainEvent, ct);
        }
    }
}
