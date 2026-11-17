namespace SpellBrigadeDash.Core;

using System;

/// <summary>Base dash stats from the config file (before cards are applied).</summary>
public sealed class DashBaseStats
{
    public float Distance { get; set; } = 3f;
    public float Duration { get; set; } = 0.12f;
    public int Charges { get; set; } = 2;
    public float RechargeSeconds { get; set; } = 2f;
    public float IFrameGraceSeconds { get; set; } = 0.08f;
    public float QuickRecoveryPerStack { get; set; } = 0.25f;
    public float LongStridePerStack { get; set; } = 0.30f;

    /// <summary>Share of max HP one dash costs until Ethereal Step is picked (0.05 = 5%).</summary>
    public float HealthCostFraction { get; set; } = 0.05f;
}

/// <summary>Combines base stats with the run's cards. Pure functions.</summary>
public static class DashTuning
{
    public static int MaxCharges(DashBaseStats b, DashProgress p)
        => p.Unlocked ? Math.Max(1, b.Charges + p.Stacks(DashCardId.ExtraCharge)) : 0;

    /// <summary>"25% faster refill" means the refill rate is multiplied by 1.25 per stack (additive).</summary>
    public static float RechargeSeconds(DashBaseStats b, DashProgress p)
    {
        var rate = 1f + Math.Max(0f, b.QuickRecoveryPerStack) * p.Stacks(DashCardId.QuickRecovery);
        return b.RechargeSeconds / rate;
    }

    /// <summary>Long Stride adds distance without adding duration, so the dash also gets faster.</summary>
    public static float Distance(DashBaseStats b, DashProgress p)
        => b.Distance * (1f + Math.Max(0f, b.LongStridePerStack) * p.Stacks(DashCardId.LongStride));

    public static float Duration(DashBaseStats b) => Math.Max(0.01f, b.Duration);

    public static float Speed(DashBaseStats b, DashProgress p) => Distance(b, p) / Duration(b);

    public static float InvulnerableSeconds(DashBaseStats b) => Duration(b) + Math.Max(0f, b.IFrameGraceSeconds);

    public static bool Blazing(DashProgress p) => p.Stacks(DashCardId.BlazingDash) > 0;

    /// <summary>Share of max HP a dash costs right now (0 after Ethereal Step).</summary>
    public static float HealthCostFraction(DashBaseStats b, DashProgress p)
        => p.Stacks(DashCardId.EtherealStep) > 0 ? 0f : Math.Max(0f, b.HealthCostFraction);

    /// <summary>
    /// HP to take for one dash: a share of max HP, but never the last point, so the dash can't kill
    /// (at 1 HP it costs nothing).
    /// </summary>
    public static float HealthCost(float fraction, float currentHp, float maxHp)
    {
        if (fraction <= 0f || maxHp <= 0f || currentHp <= 1f) return 0f;
        return Math.Max(0f, Math.Min(maxHp * fraction, currentHp - 1f));
    }
}
