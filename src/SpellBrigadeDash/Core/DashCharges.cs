using System;

namespace SpellBrigadeDash.Core;

/// <summary>
/// Charge-based dash energy. The bar is split into <see cref="Max"/> segments. A dash spends one
/// full segment, and empty segments refill one at a time, each taking <see cref="RechargeSeconds"/>.
///
/// Pure C# with no Unity types, so it is covered by the unit tests in /tests.
/// </summary>
public sealed class DashCharges
{
    public DashCharges(int max, float rechargeSeconds)
    {
        Max = Math.Max(0, max);
        Full = Max;
        RechargeSeconds = rechargeSeconds;
    }

    /// <summary>Number of segments in the bar.</summary>
    public int Max { get; private set; }

    /// <summary>Number of segments that are currently full (usable dashes).</summary>
    public int Full { get; private set; }

    /// <summary>0..1 progress of the segment that is currently refilling.</summary>
    public float Progress { get; private set; }

    /// <summary>Seconds it takes to refill one segment. 0 or less = instant.</summary>
    public float RechargeSeconds { get; set; }

    public bool IsFull => Full >= Max;

    public bool CanDash => Full > 0;

    /// <summary>
    /// Change the number of segments, for example after a "+1 Charge" card.
    /// New segments start empty and refill normally. When shrinking, extra full charges are dropped.
    /// </summary>
    public void SetMax(int max)
    {
        max = Math.Max(0, max);
        Max = max;
        if (Full > Max) Full = Max;
        if (IsFull) Progress = 0f;
    }

    /// <summary>Spend one charge. Returns false (and changes nothing) when the bar is empty.</summary>
    public bool TryConsume()
    {
        if (Full <= 0) return false;
        Full--;
        return true;
    }

    /// <summary>Advance the refill timer. Returns how many segments became full during this tick.</summary>
    public int Tick(float deltaSeconds)
    {
        if (Max <= 0 || IsFull)
        {
            Progress = 0f;
            return 0;
        }

        if (deltaSeconds <= 0f) return 0;

        if (RechargeSeconds <= 0f)
        {
            var instant = Max - Full;
            Full = Max;
            Progress = 0f;
            return instant;
        }

        Progress += deltaSeconds / RechargeSeconds;
        var refilled = 0;
        while (Progress >= 1f && Full < Max)
        {
            Progress -= 1f;
            Full++;
            refilled++;
        }

        if (IsFull) Progress = 0f;
        return refilled;
    }

    /// <summary>Fill amount (0..1) of a single segment, for drawing the bar.</summary>
    public float SegmentFill(int index)
    {
        if (index < 0 || index >= Max) return 0f;
        if (index < Full) return 1f;
        return index == Full ? Progress : 0f;
    }

    public void RefillAll()
    {
        Full = Max;
        Progress = 0f;
    }
}
