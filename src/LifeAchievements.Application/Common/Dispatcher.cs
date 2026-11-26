using FluentValidation;
using LifeAchievements.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace LifeAchievements.Application.Common;

/// <summary>
/// Resolves the handler for a message out of the DI container by closing the open generic
/// handler interface over the runtime message type, then invokes it. Validation runs first,
/// so every command is validated in exactly one place.
/// </summary>
public sealed class Dispatcher(IServiceProvider services) : IDispatcher
{
    public Task<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken ct = default) =>
        Invoke(typeof(ICommandHandler<,>), command, typeof(TResponse), validate: true, ct) is Task<TResponse> t
            ? t
            : throw new InvalidOperationException($"Handler for {command.GetType().Name} returned the wrong type.");

    public Task<TResponse> Query<TResponse>(IQuery<TResponse> query, CancellationToken ct = default) =>
        Invoke(typeof(IQueryHandler<,>), query, typeof(TResponse), validate: false, ct) is Task<TResponse> t
            ? t
            : throw new InvalidOperationException($"Handler for {query.GetType().Name} returned the wrong type.");

    private object Invoke(Type openHandlerType, object message, Type responseType, bool validate, CancellationToken ct)
    {
        var messageType = message.GetType();

        if (validate) ValidateAsync(message, messageType).GetAwaiter().GetResult();

        var handlerType = openHandlerType.MakeGenericType(messageType, responseType);
        var handler = services.GetService(handlerType)
            ?? throw new InvalidOperationException($"No handler registered for {messageType.Name}.");

        var method = handlerType.GetMethod("Handle")!;
        return method.Invoke(handler, [message, ct])!;
    }

    private async Task ValidateAsync(object message, Type messageType)
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(messageType);
        var validators = services.GetServices(validatorType).Cast<IValidator>().ToList();
        if (validators.Count == 0) return;

        var contextType = typeof(ValidationContext<>).MakeGenericType(messageType);
        var context = (IValidationContext)Activator.CreateInstance(contextType, message)!;

        var failures = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(context);
            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0) throw new ValidationException(failures);
    }
}
