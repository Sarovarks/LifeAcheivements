namespace LifeAchievements.Domain.Enums;

/// <summary>
/// How a repeatable achievement limits itself.
///
/// None falls back to <c>CooldownHours</c>, a rolling wait from the moment of the last claim -
/// right for "wait three days before another leg day", wrong for a daily habit, because the
/// boundary drifts later every time you claim.
///
/// Day and Week are calendar-based in the player's own timezone: a fresh midnight (or a fresh
/// Monday) gives you a fresh claim, however late you logged yesterday's.
/// </summary>
public enum CooldownWindow
{
    None = 0,
    Day = 1,
    Week = 2
}
