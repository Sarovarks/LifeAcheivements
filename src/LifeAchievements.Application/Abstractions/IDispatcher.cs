namespace LifeAchievements.Application.Abstractions;

/// <summary>
/// Hand-written CQRS dispatcher - the same shape as MediatR's ISender but with no licence
/// strings attached. Resolves the one handler registered for the message type and calls it.
/// </summary>
public interface IDispatcher
{
    Task<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken ct = default);
    Task<TResponse> Query<TResponse>(IQuery<TResponse> query, CancellationToken ct = default);
}
