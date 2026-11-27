using System.Reflection;
using LifeAchievements.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace LifeAchievements.Application.Tests;

/// <summary>
/// Handlers are registered by hand, one line each. Every other test in this project builds a
/// handler directly, so a command that nobody registered passes the whole suite and then
/// throws "No handler registered" the first time the API is asked for it - in production,
/// which is exactly where GetNotificationsQuery was found.
///
/// This walks the Application assembly instead, so adding a command without wiring it up
/// fails here rather than live.
/// </summary>
public class DependencyRegistrationTests
{
    [Fact]
    public void Every_command_and_query_has_a_registered_handler()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        var registered = services.Select(d => d.ServiceType).ToHashSet();

        var assembly = typeof(ICommand<>).Assembly;
        var missing = new List<string>();

        foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        {
            foreach (var contract in type.GetInterfaces().Where(i => i.IsGenericType))
            {
                var definition = contract.GetGenericTypeDefinition();

                var handler =
                    definition == typeof(ICommand<>) ? typeof(ICommandHandler<,>) :
                    definition == typeof(IQuery<>) ? typeof(IQueryHandler<,>) :
                    null;

                if (handler is null) continue;

                var expected = handler.MakeGenericType(type, contract.GetGenericArguments()[0]);
                if (!registered.Contains(expected))
                {
                    missing.Add($"{type.Name} -> no {handler.Name[..^2]} registered in AddApplication");
                }
            }
        }

        Assert.True(missing.Count == 0,
            "Unregistered handlers:" + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    [Fact]
    public void There_is_at_least_one_command_and_one_query_to_check()
    {
        // Guards the test above: a reflection bug that matched nothing would otherwise pass
        // silently and prove only that the loop ran.
        var assembly = typeof(ICommand<>).Assembly;

        var contracts = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType)
            .Select(i => i.GetGenericTypeDefinition())
            .ToList();

        Assert.Contains(typeof(ICommand<>), contracts);
        Assert.Contains(typeof(IQuery<>), contracts);
    }
}
