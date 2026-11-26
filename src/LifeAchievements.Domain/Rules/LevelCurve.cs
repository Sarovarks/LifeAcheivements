namespace LifeAchievements.Domain.Rules;

/// <summary>
/// Levels are never stored - they are always computed from the XP ledger total.
/// Everything about the curve lives here so the whole game can be re-tuned in one file.
/// </summary>
public static class LevelCurve
{
    /// <summary>Cumulative XP needed to *reach* the level at that index + 1. Index 0 = level 1.</summary>
    private static readonly int[] Thresholds =
    [
        0,      // 1
        150,    // 2
        350,    // 3
        600,    // 4
        900,    // 5
        1300,   // 6
        1800,   // 7
        2400,   // 8
        3100,   // 9
        3900,   // 10
        4800,   // 11
        5800,   // 12
        6900,   // 13
        8100,   // 14
        9400,   // 15
        10800,  // 16
        12300,  // 17
        13900,  // 18
        15600,  // 19
        17400   // 20
    ];

    /// <summary>Past the table the curve just keeps climbing by a flat step per level.</summary>
    private const int XpPerLevelAfterTable = 2000;

    public static int MaxTabulatedLevel => Thresholds.Length;

    /// <summary>Total XP needed to reach the given level. Level 1 costs nothing.</summary>
    public static int XpRequiredFor(int level)
    {
        if (level <= 1) return 0;
        if (level <= Thresholds.Length) return Thresholds[level - 1];
        return Thresholds[^1] + (level - Thresholds.Length) * XpPerLevelAfterTable;
    }

    public static int LevelFor(int totalXp)
    {
        if (totalXp <= 0) return 1;

        if (totalXp < Thresholds[^1])
        {
            // Highest tabulated level whose threshold we have paid for.
            for (var i = Thresholds.Length - 1; i >= 0; i--)
            {
                if (totalXp >= Thresholds[i]) return i + 1;
            }
            return 1;
        }

        var over = totalXp - Thresholds[^1];
        return Thresholds.Length + over / XpPerLevelAfterTable;
    }

    public static string TitleFor(int level) => level switch
    {
        <= 2 => "NPC",
        <= 4 => "Side Quest Enjoyer",
        <= 6 => "Side Character",
        <= 9 => "Supporting Cast",
        <= 12 => "Main Character",
        <= 15 => "Protagonist",
        <= 19 => "Raid Leader",
        _ => "Final Boss"
    };

    /// <summary>Everything the UI needs to draw the level ring, derived from one number.</summary>
    public static LevelProgress ProgressFor(int totalXp)
    {
        var level = LevelFor(totalXp);
        var floor = XpRequiredFor(level);
        var ceiling = XpRequiredFor(level + 1);
        var span = ceiling - floor;
        var into = totalXp - floor;

        return new LevelProgress(
            Level: level,
            Title: TitleFor(level),
            TotalXp: totalXp,
            CurrentLevelFloorXp: floor,
            NextLevelAtXp: ceiling,
            XpToNextLevel: Math.Max(0, ceiling - totalXp),
            ProgressPercent: span <= 0 ? 0 : Math.Clamp((double)into / span * 100d, 0d, 100d));
    }
}

public readonly record struct LevelProgress(
    int Level,
    string Title,
    int TotalXp,
    int CurrentLevelFloorXp,
    int NextLevelAtXp,
    int XpToNextLevel,
    double ProgressPercent);
