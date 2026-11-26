namespace LifeAchievements.Domain.Exceptions;

/// <summary>A rule of the game was broken - turns into a 400 with a friendly message.</summary>
public class DomainException(string message) : Exception(message);
