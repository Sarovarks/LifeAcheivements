namespace LifeAchievements.Domain.Rules;

/// <summary>
/// Codes of the hidden achievements, so rule handlers and the seed never drift apart.
/// </summary>
public static class HiddenAchievements
{
    public const string NightOwl = "NIGHT_OWL";
    public const string DejaVu = "DEJA_VU";
    public const string Overachiever = "OVERACHIEVER";
    public const string WellRounded = "WELL_ROUNDED";
    public const string ComebackKid = "COMEBACK_KID";
    public const string FirstSteps = "FIRST_STEPS";

    /// <summary>
    /// Night Owl fires for a claim made between 02:00 and 04:59 *local* time. Timestamps are
    /// stored as UTC, so always convert before asking what time of night it was.
    /// </summary>
    public static bool IsNightOwlHour(DateTimeOffset at) => at.ToLocalTime().Hour is >= 2 and < 5;

    /// <summary>Deja Vu: the 30th approved unlock of one and the same repeatable achievement.</summary>
    public const int DejaVuRepeatCount = 30;

    /// <summary>Overachiever: five approved unlocks inside one calendar day.</summary>
    public const int OverachieverUnlocksPerDay = 5;

    /// <summary>Well Rounded: at least one approved unlock in every category.</summary>
    public const int WellRoundedCategoryCount = 5;

    /// <summary>Comeback Kid: unlocked something again after 14+ quiet days.</summary>
    public const int ComebackKidQuietDays = 14;
}
