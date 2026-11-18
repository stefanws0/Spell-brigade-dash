namespace SpellBrigadeDash.Core;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>One level-up card that this mod adds to the game's upgrade pool.</summary>
public sealed class DashCardDef
{
    public DashCardDef(DashCardId id, string title, string description, string rarity, int maxStacks, float weight)
    {
        Id = id;
        Title = title;
        Description = description;
        Rarity = rarity;
        MaxStacks = maxStacks;
        Weight = weight;
    }

    public DashCardId Id { get; }
    public string Title { get; }
    public string Description { get; }

    /// <summary>The game's rarity name, e.g. "Epic" or "Legendary" (matched by name to the game's own rarity enum).</summary>
    public string Rarity { get; }

    public int MaxStacks { get; }

    /// <summary>Relative weight within its rarity tier. 1 = the same as a normal card of that rarity.</summary>
    public float Weight { get; }
}

/// <summary>Values that come from the config file. Kept separate so the catalogue can be unit tested.</summary>
public sealed class DashCardSettings
{
    public string UnlockRarity { get; set; } = "Epic";
    public float UnlockWeight { get; set; } = 5f;

    public string ExtraChargeRarity { get; set; } = "Legendary";
    public int ExtraChargeMaxStacks { get; set; } = 2;

    public string QuickRecoveryRarity { get; set; } = "Epic";
    public int QuickRecoveryMaxStacks { get; set; } = 3;
    public float QuickRecoveryPerStack { get; set; } = 0.25f;

    public string LongStrideRarity { get; set; } = "Epic";
    public int LongStrideMaxStacks { get; set; } = 2;
    public float LongStridePerStack { get; set; } = 0.30f;

    public string BlazingDashRarity { get; set; } = "Legendary";
    public float BlazingDashDamage { get; set; }

    public string EtherealStepRarity { get; set; } = "Legendary";

    /// <summary>Share of max HP one dash costs before Ethereal Step (0.05 = 5%).</summary>
    public float HealthCostFraction { get; set; } = 0.05f;
}

public static class DashCards
{
    public static IReadOnlyList<DashCardDef> Build(DashCardSettings s)
    {
        return new List<DashCardDef>
        {
            new(DashCardId.Dash, "Dash",
                s.HealthCostFraction > 0f
                    ? $"Press Space (or LB) to dash a short distance. You can't be hit while dashing, but each dash costs {Percent(s.HealthCostFraction)} of your max HP."
                    : "Press Space (or LB) to dash a short distance. You can't be hit while dashing.",
                s.UnlockRarity, 1, s.UnlockWeight),
            new(DashCardId.ExtraCharge, "+1 Dash Charge",
                "Your dash bar gains another segment.",
                s.ExtraChargeRarity, Math.Max(0, s.ExtraChargeMaxStacks), 1f),
            new(DashCardId.QuickRecovery, "Quick Recovery",
                $"Dash charges refill {Percent(s.QuickRecoveryPerStack)} faster.",
                s.QuickRecoveryRarity, Math.Max(0, s.QuickRecoveryMaxStacks), 1f),
            new(DashCardId.LongStride, "Long Stride",
                $"Your dash travels {Percent(s.LongStridePerStack)} farther.",
                s.LongStrideRarity, Math.Max(0, s.LongStrideMaxStacks), 1f),
            new(DashCardId.BlazingDash, "Blazing Dash",
                "Your dash leaves a trail of fire that burns enemies.",
                s.BlazingDashRarity, 1, 1f),
            new(DashCardId.EtherealStep, "Ethereal Step",
                "Your dash no longer costs health.",
                s.EtherealStepRarity, s.HealthCostFraction > 0f ? 1 : 0, 1f),
        };
    }

    /// <summary>
    /// Whether a card may currently show up as a level-up choice. The unlock card only appears while
    /// the dash is locked, and the upgrades only after it is unlocked and while below their stack cap.
    /// </summary>
    public static bool IsAvailable(DashCardDef card, DashProgress progress)
    {
        if (card.MaxStacks <= 0) return false;
        if (card.Id == DashCardId.Dash) return !progress.Unlocked;
        return progress.Unlocked && progress.Stacks(card.Id) < card.MaxStacks;
    }

    public static IEnumerable<DashCardDef> Available(IEnumerable<DashCardDef> cards, DashProgress progress)
        => cards.Where(c => IsAvailable(c, progress));

    private static string Percent(float v) => $"{Math.Round(v * 100f)}%";
}
