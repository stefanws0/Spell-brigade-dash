namespace SpellBrigadeDash.Core;

using System;
using System.Collections.Generic;

/// <summary>
/// Where Blazing Dash's fire burns and when each enemy standing in it may be hit again (agreed with the owner 18 Nov: a
/// tick every 0.5 s, so up to three hits in a 1.5 s trail). Flames live on the ground plane (x/z); an enemy counts as
/// in the fire when its footprint touches a flame. One timer per enemy, shared by all flames, so overlapping trails
/// never stack. Pure, unit-tested.
/// </summary>
public sealed class BlazeTicks
{
    private const float Slack = 0.001f;
    private readonly List<Flame> _flames = new();
    private readonly Dictionary<long, float> _lastHit = new();

    public BlazeTicks(float tickSeconds) => TickSeconds = Math.Max(0.05f, tickSeconds);

    public float TickSeconds { get; }
    public int FlameCount => _flames.Count;
    public int TrackedTargets => _lastHit.Count;

    public void AddFlame(float x, float z, float radius, float until) => _flames.Add(new Flame(x, z, Math.Max(0f, radius), until));

    /// <summary>Drop burnt-out flames and hit times too old to matter.</summary>
    public void Prune(float now)
    {
        _flames.RemoveAll(f => f.Until <= now);
        if (_lastHit.Count == 0) return;
        List<long>? old = null;
        foreach (var kv in _lastHit)
            if (now - kv.Value >= TickSeconds - Slack) (old ??= new List<long>()).Add(kv.Key);
        if (old != null) foreach (var k in old) _lastHit.Remove(k);
    }

    /// <summary>True when a target at (x, z) with this footprint radius touches a flame still burning at <paramref name="now"/>.</summary>
    public bool InFlame(float x, float z, float targetRadius, float now)
    {
        foreach (var f in _flames)
        {
            if (f.Until <= now) continue;
            var dx = x - f.X;
            var dz = z - f.Z;
            var reach = f.Radius + Math.Max(0f, targetRadius);
            if (dx * dx + dz * dz <= reach * reach) return true;
        }

        return false;
    }

    /// <summary>True (and remembered) when <paramref name="target"/> wasn't hit in the last tick; false while it's still cooling down.</summary>
    public bool TryHit(long target, float now)
    {
        if (_lastHit.TryGetValue(target, out var last) && now - last < TickSeconds - Slack) return false;
        _lastHit[target] = now;
        return true;
    }

    public void Clear()
    {
        _flames.Clear();
        _lastHit.Clear();
    }

    private readonly struct Flame
    {
        public Flame(float x, float z, float radius, float until)
        {
            X = x;
            Z = z;
            Radius = radius;
            Until = until;
        }

        public float X { get; }
        public float Z { get; }
        public float Radius { get; }
        public float Until { get; }
    }
}

/// <summary>Blazing Dash's damage numbers. Pure, unit-tested.</summary>
public static class BlazeMath
{
    public static float PerTick(float baseDamage, float multiplier) => Math.Max(0f, baseDamage * multiplier);

    /// <summary>
    /// A character stat (Damage, Spell Size) as a multiplier, whatever unit the game keeps it in: a percent stat on a
    /// base (1 or 100) is value / base; a percent stat without a base is 1 + modifiers %; a flat stat holds the percent.
    /// </summary>
    public static float StatMultiplier(bool flat, float baseValue, float value, float modifiers)
    {
        if (flat) return 1f + value / 100f;
        if (Math.Abs(baseValue) > 0.0001f) return value / baseValue;
        return 1f + modifiers / 100f;
    }

    /// <summary>
    /// The game's own value for a stat at the wizard's upgrades (e.g. the Trail spell's damage with your Damage %),
    /// as a multiplier of the base. Anything outside [min, max] is taken as a misread and ignored (1).
    /// </summary>
    public static float Multiplier(float baseValue, float gameValue, float min, float max)
    {
        if (baseValue <= 0f || float.IsNaN(gameValue) || float.IsInfinity(gameValue)) return 1f;
        var ratio = gameValue / baseValue;
        return ratio >= min && ratio <= max ? ratio : 1f;
    }
}
