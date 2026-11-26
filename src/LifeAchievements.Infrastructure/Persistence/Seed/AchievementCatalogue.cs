using System.Security.Cryptography;
using System.Text;
using LifeAchievements.Domain.Entities;
using LifeAchievements.Domain.Enums;
using LifeAchievements.Domain.Rules;

namespace LifeAchievements.Infrastructure.Persistence.Seed;

/// <summary>
/// The whole v1 catalogue. Ids are derived from the Code so they stay identical on every
/// machine and on every re-seed - which is what makes seeding idempotent.
/// </summary>
public static class AchievementCatalogue
{
    public static Guid IdFor(string code)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes("LifeAchievements:" + code));
        return new Guid(hash);
    }

    private static Achievement A(
        string code, string name, string description, AchievementCategory category,
        AchievementTier tier, int xp, string icon,
        bool repeatable = false, int? cooldownHours = null,
        VerificationType verification = VerificationType.Self,
        bool hidden = false, string? hint = null) =>
        new()
        {
            Id = IdFor(code),
            Code = code,
            Name = name,
            Description = description,
            Category = category,
            Tier = tier,
            XpReward = xp,
            Icon = icon,
            IsRepeatable = repeatable,
            CooldownHours = cooldownHours,
            VerificationType = verification,
            IsHidden = hidden,
            Hint = hint
        };

    public static IReadOnlyList<Achievement> All =>
    [
        // ---------------- Health (10) ----------------
        A("EARLY_BIRD", "Early Bird",
            "Woke up before 6 AM and did not immediately go back to sleep. Legendary restraint.",
            AchievementCategory.Health, AchievementTier.Common, 50, "sunrise", repeatable: true, cooldownHours: 24),

        A("IRON_WILL", "Iron Will",
            "Hit the gym five days in one week. Your future self is quietly applauding.",
            AchievementCategory.Health, AchievementTier.Rare, 200, "dumbbell"),

        A("TEN_K_STEPS", "10K Steps",
            "Walked ten thousand steps in a day. Pacing while on a call absolutely counts.",
            AchievementCategory.Health, AchievementTier.Common, 40, "footprints", repeatable: true, cooldownHours: 24),

        A("HYDRATION_HERO", "Hydration Hero",
            "Drank enough water that you actually noticed. Your kidneys send their regards.",
            AchievementCategory.Health, AchievementTier.Common, 30, "droplet", repeatable: true, cooldownHours: 24),

        A("LEG_DAY", "Leg Day Survivor",
            "Did leg day and still managed to walk down the stairs afterwards.",
            AchievementCategory.Health, AchievementTier.Common, 60, "activity", repeatable: true, cooldownHours: 72),

        A("SWEET_NOTHING", "Sweet Nothing",
            "Seven days with no added sugar. The cake understands. The cake forgives.",
            AchievementCategory.Health, AchievementTier.Rare, 150, "candy-off"),

        A("HALF_MARATHON", "Half Marathon",
            "Ran 21.1 km on purpose. Nobody was chasing you. You simply chose this.",
            AchievementCategory.Health, AchievementTier.Legendary, 500, "medal"),

        A("SLEEP_CHAMPION", "Sleep Champion",
            "Eight hours of real sleep. Not screen time in bed. Actual, honest sleep.",
            AchievementCategory.Health, AchievementTier.Common, 35, "moon", repeatable: true, cooldownHours: 24),

        A("ICE_BREAKER", "Ice Breaker",
            "Took a cold shower voluntarily, then told everyone you know about it.",
            AchievementCategory.Health, AchievementTier.Common, 45, "snowflake", repeatable: true, cooldownHours: 24),

        A("PREVENTIVE_MAINTENANCE", "Preventive Maintenance",
            "Went for a health check-up before anything actually hurt. Very responsible.",
            AchievementCategory.Health, AchievementTier.Rare, 120, "stethoscope"),

        // ---------------- Work / learning (10) ----------------
        A("FRIDAY_SURVIVOR", "Friday Survivor",
            "Fixed a production bug on Friday evening and lived to tell the tale.",
            AchievementCategory.Work, AchievementTier.Rare, 150, "flame"),

        A("PAGE_TURNER", "Page Turner",
            "Finished a book. The whole thing. Including the slow bit in the middle.",
            AchievementCategory.Work, AchievementTier.Rare, 120, "book", repeatable: true, cooldownHours: 24),

        A("INBOX_ZERO", "Inbox Zero",
            "Your inbox is empty. It will not last. Savour it while it does.",
            AchievementCategory.Work, AchievementTier.Common, 60, "inbox", repeatable: true, cooldownHours: 168),

        A("NEW_SKILL", "New Skill Unlocked",
            "Learned something genuinely new and can prove it to a sceptical friend.",
            AchievementCategory.Work, AchievementTier.Rare, 180, "sparkles"),

        A("SHIPPED_IT", "Shipped It",
            "Deployed to production and the alerts stayed beautifully quiet.",
            AchievementCategory.Work, AchievementTier.Rare, 200, "rocket"),

        A("REVIEW_CRUSADER", "Review Crusader",
            "Reviewed a teammate pull request properly instead of just clicking approve.",
            AchievementCategory.Work, AchievementTier.Common, 50, "check-square", repeatable: true, cooldownHours: 24),

        A("MEETING_SLAYER", "Meeting Slayer",
            "Declined a meeting that could have been an email. An act of genuine courage.",
            AchievementCategory.Work, AchievementTier.Common, 40, "calendar-off", repeatable: true, cooldownHours: 168),

        A("DEEP_WORK", "Deep Work",
            "Three uninterrupted hours of real focus. Several tabs were harmed.",
            AchievementCategory.Work, AchievementTier.Common, 70, "target", repeatable: true, cooldownHours: 24),

        A("LIBRARY_CARD", "Library Card",
            "Twelve books finished in one year. Audiobooks count. Summaries do not.",
            AchievementCategory.Work, AchievementTier.Legendary, 600, "library"),

        A("SIDE_PROJECT_SHIPPER", "Side Project Shipper",
            "Actually finished a side project instead of starting a shinier one. Rare species.",
            AchievementCategory.Work, AchievementTier.Legendary, 450, "package"),

        // ---------------- Fun / lifestyle (10) ----------------
        A("FIRST_DAY_FIRST_SHOW", "First Day First Show",
            "Watched a film on release day. Phone off, popcorn on, spoilers dodged.",
            AchievementCategory.Fun, AchievementTier.Common, 75, "ticket", verification: VerificationType.Friend),

        A("HOME_CHEF", "Home Chef",
            "Cooked instead of ordering in. The delivery app is visibly confused.",
            AchievementCategory.Fun, AchievementTier.Common, 40, "chef-hat", repeatable: true, cooldownHours: 24),

        A("SOLO_TRIP", "Solo Trip",
            "Travelled alone and came back with stories nobody else can tell.",
            AchievementCategory.Fun, AchievementTier.Legendary, 400, "compass"),

        A("SUNSET_CHASER", "Sunset Chaser",
            "Stopped whatever you were doing to actually watch a sunset. No photo needed.",
            AchievementCategory.Fun, AchievementTier.Common, 35, "sunset", repeatable: true, cooldownHours: 24),

        A("CULINARY_EXPLORER", "Culinary Explorer",
            "Tried a cuisine you could not confidently pronounce. Survived. Thrived.",
            AchievementCategory.Fun, AchievementTier.Common, 50, "utensils", repeatable: true, cooldownHours: 72),

        A("FRONT_ROW", "Front Row",
            "Went to a live show and sang along, badly, with total commitment.",
            AchievementCategory.Fun, AchievementTier.Rare, 150, "music", verification: VerificationType.Friend),

        A("ROAD_TRIP", "Road Trip",
            "Drove somewhere far with a playlist of questionable but confident choices.",
            AchievementCategory.Fun, AchievementTier.Rare, 220, "car"),

        A("DUST_OFF", "Dust Off",
            "Picked up an old hobby that had been sulking in a cupboard for years.",
            AchievementCategory.Fun, AchievementTier.Common, 60, "palette"),

        A("TABLETOP_TACTICIAN", "Tabletop Tactician",
            "Hosted a board game night and only mildly bent the rules in your favour.",
            AchievementCategory.Fun, AchievementTier.Common, 45, "dice", repeatable: true, cooldownHours: 168,
            verification: VerificationType.Friend),

        A("SHUTTERBUG", "Shutterbug",
            "Went out with no plan beyond photographing whatever happened to show up.",
            AchievementCategory.Fun, AchievementTier.Common, 40, "camera", repeatable: true, cooldownHours: 72),

        // ---------------- Social (8) ----------------
        A("TURF_LEGEND", "Turf Legend",
            "Scored fifty runs in Sunday turf cricket. Witnesses strongly required.",
            AchievementCategory.Social, AchievementTier.Rare, 250, "trophy", verification: VerificationType.Friend),

        A("RECONNECTED", "Reconnected",
            "Called an old friend out of nowhere. Better still, they picked up.",
            AchievementCategory.Social, AchievementTier.Common, 60, "phone", repeatable: true, cooldownHours: 168),

        A("PARTY_STARTER", "Party Starter",
            "Made the gathering genuinely fun. Somebody said so out loud, unprompted.",
            AchievementCategory.Social, AchievementTier.Common, 50, "party", repeatable: true, cooldownHours: 168,
            verification: VerificationType.Friend),

        A("FAMILY_TIME", "Family Time",
            "Spent an evening with family with your phone in an entirely different room.",
            AchievementCategory.Social, AchievementTier.Common, 45, "home", repeatable: true, cooldownHours: 24),

        A("SOCIAL_PATCH_NOTES", "Social Patch Notes",
            "Made a real new friend as an adult. Legitimately one of the harder ones.",
            AchievementCategory.Social, AchievementTier.Rare, 120, "user-plus"),

        A("HELPING_HAND", "Helping Hand",
            "Helped someone move house and barely complained about the sofa.",
            AchievementCategory.Social, AchievementTier.Common, 70, "hand-heart", verification: VerificationType.Friend),

        A("DINNER_HOST", "Dinner Host",
            "Cooked for friends and nobody quietly ordered backup pizza afterwards.",
            AchievementCategory.Social, AchievementTier.Rare, 130, "serving", verification: VerificationType.Friend),

        A("MIC_DROP", "Mic Drop",
            "Spoke in front of a real audience and did not faint, flee or combust.",
            AchievementCategory.Social, AchievementTier.Legendary, 400, "mic"),

        // ---------------- Mind / habits (6) ----------------
        A("MONK_MODE", "Monk Mode",
            "Twenty-four hours with no social media. The algorithm misses you terribly.",
            AchievementCategory.Mind, AchievementTier.Common, 100, "lotus", repeatable: true, cooldownHours: 24),

        A("DIGITAL_DETOX", "Digital Detox Weekend",
            "A whole weekend offline. Yes, the world kept turning without your input.",
            AchievementCategory.Mind, AchievementTier.Rare, 200, "wifi-off"),

        A("NO_SPEND_WEEK", "No Spend Week",
            "Seven days without a single impulse purchase. The cart stayed abandoned.",
            AchievementCategory.Mind, AchievementTier.Rare, 180, "wallet"),

        A("STILL_MIND", "Still Mind",
            "Ten minutes of meditation without once checking how much was left.",
            AchievementCategory.Mind, AchievementTier.Common, 35, "brain", repeatable: true, cooldownHours: 24),

        A("DEAR_DIARY", "Dear Diary",
            "Wrote down what actually happened today, honestly, including the awkward part.",
            AchievementCategory.Mind, AchievementTier.Common, 30, "pen", repeatable: true, cooldownHours: 24),

        A("INNER_WORK", "Inner Work",
            "Talked to a professional about what goes on in your head. Strongest move here.",
            AchievementCategory.Mind, AchievementTier.Rare, 160, "heart-pulse"),

        // ---------------- Hidden (6) ----------------
        A(HiddenAchievements.FirstSteps, "First Steps",
            "Your very first unlock. The run has officially begun.",
            AchievementCategory.Mind, AchievementTier.Common, 50, "flag",
            verification: VerificationType.Auto, hidden: true,
            hint: "Everybody starts somewhere."),

        A(HiddenAchievements.NightOwl, "Night Owl",
            "Claimed an achievement between 2 and 5 AM. Genuinely, go to sleep.",
            AchievementCategory.Mind, AchievementTier.Rare, 100, "owl",
            verification: VerificationType.Auto, hidden: true,
            hint: "Some badges only come out in the dark."),

        A(HiddenAchievements.DejaVu, "Deja Vu",
            "Unlocked the same repeatable achievement thirty times. Impressive. Mildly concerning.",
            AchievementCategory.Mind, AchievementTier.Rare, 250, "repeat",
            verification: VerificationType.Auto, hidden: true,
            hint: "Do something often enough and the universe starts taking notes."),

        A(HiddenAchievements.Overachiever, "Overachiever",
            "Five unlocks inside a single day. Please drink some water and sit down.",
            AchievementCategory.Mind, AchievementTier.Rare, 200, "zap",
            verification: VerificationType.Auto, hidden: true,
            hint: "How much can one single day really hold?"),

        A(HiddenAchievements.WellRounded, "Well Rounded",
            "At least one unlock in every category. A suspiciously balanced human being.",
            AchievementCategory.Mind, AchievementTier.Legendary, 400, "circle-dot",
            verification: VerificationType.Auto, hidden: true,
            hint: "Do not let any part of your life gather dust."),

        A(HiddenAchievements.ComebackKid, "Comeback Kid",
            "Returned after two quiet weeks. The streak is dead; long live the streak.",
            AchievementCategory.Mind, AchievementTier.Rare, 150, "undo",
            verification: VerificationType.Auto, hidden: true,
            hint: "Disappear. Then, crucially, do not stay gone.")
    ];
}
