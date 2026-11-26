namespace LifeAchievements.Application.Abstractions;

/// <summary>Injected so the cooldown and Night Owl tests do not have to wait for 2 AM.</summary>
public interface IClock
{
    DateTimeOffset Now { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}
