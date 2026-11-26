namespace LifeAchievements.Domain.Enums;

public enum VerificationType
{
    /// <summary>Honour system - approved the moment it is claimed.</summary>
    Self = 0,
    /// <summary>Needs a friend to confirm it. Stays Pending until then (Phase 2).</summary>
    Friend = 1,
    /// <summary>Unlocked by a rule, never claimed by hand (hidden achievements).</summary>
    Auto = 2
}
