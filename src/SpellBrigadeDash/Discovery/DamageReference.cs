namespace SpellBrigadeDash.Discovery;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Il2CppInterop.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// The game's own damage numbers, to balance Blazing Dash's fire against (18 Nov, before its damage is built): every
/// spell's base stats (the Trail spell is the closest relative), the fire element's burn, the lava zone that also hurts
/// enemies, the meteor's ground damage, and the health of the enemies alive now (in the report, and once a minute in
/// the log while enemies are around). Read-only: reads the game's data objects and calls no game logic beyond a curve
/// lookup.
/// </summary>
internal static class DamageReference
{
    private const float EnemyLogSeconds = 60f;
    private static float _nextEnemyLog;
    private static bool _enemyLogOff;

    public static void Describe(StringBuilder sb)
    {
        sb.AppendLine("Spells (SpellConfiguration base stats; Damage is per hit or tick, before the wizard's Damage % and crits):");
        Try(sb, "spells", () =>
        {
            foreach (var cfg in Resources.FindObjectsOfTypeAll<SpellConfiguration>().Where(c => c != null).OrderBy(c => c.SpellType.ToString(), StringComparer.Ordinal))
            {
                var stats = new List<string>();
                var list = cfg.Stats;
                if (list != null)
                    for (var i = 0; i < list.Count; i++)
                    {
                        var s = list[i];
                        if (s != null) stats.Add($"{s.Stat} {F(s.Value)}");
                    }

                sb.AppendLine($"  {cfg.SpellType} ({cfg.name}): {string.Join(", ", stats)}");
            }
        });

        sb.AppendLine();
        Try(sb, "fire burn", () =>
        {
            foreach (var e in Resources.FindObjectsOfTypeAll<ElementEffectConfiguration>())
            {
                if (e == null) continue;
                var f = e.FireEffectConfiguration;
                if (f != null)
                    sb.AppendLine($"Fire burn ({e.name}): {f.NumberOfBurns} burns, {F(f.SecondsBetweenBurns)} s apart, each {F(f.BurnDamageFactor)} x the hit's damage");
                var d = e.FireDoubleEffectConfiguration;
                if (d != null)
                    sb.AppendLine($"Double-fire burn: {d.NumberOfBurns} burns, {F(d.SecondsBetweenBurns)} s apart, each {F(d.BurnDamageFactor)} x the hit's damage");
            }
        });

        Try(sb, "lava", () =>
        {
            foreach (var l in Resources.FindObjectsOfTypeAll<LavaZoneConfiguration>())
            {
                if (l == null) continue;
                var ticks = new[] { 1, 5, 10, 20 }.Select(t => F(l.GetEnemyDamageForEnemy(t)));
                sb.AppendLine($"Lava zone ({l.name}): a tick every {F(l.TimeBetweenTicks)} s; base damage to enemies {l.BaseDamageForEnemy}, " +
                              $"to players {l.BaseDamageForPlayer}; enemy damage at tick 1 / 5 / 10 / 20: {string.Join(" / ", ticks)}");
            }
        });

        Try(sb, "meteor", () =>
        {
            foreach (var m in Resources.FindObjectsOfTypeAll<MeteorDamageArea>().Where(m => m != null).Take(5))
                sb.AppendLine($"Meteor damage area ({m.gameObject.name}): {m.numberOfDamageTicks} ticks, {F(m.secondsBetweenDamageTicks)} s apart, " +
                              $"radius {F(m.damageRadius)} (x{F(m.damageRadiusMultiplier)}), damage divided by {F(m.damageDivider)}");
        });

        sb.AppendLine();
        Try(sb, "enemies", () => sb.AppendLine("Enemies alive now: " + DescribeEnemies()));
    }

    /// <summary>Once a minute while enemies are around (and the discovery report is on): their health, to see how it grows over a run.</summary>
    public static void Tick(float now)
    {
        if (_enemyLogOff || !ModConfig.DiscoveryEnabled.Value || now < _nextEnemyLog) return;
        _nextEnemyLog = now + EnemyLogSeconds;
        try
        {
            var text = DescribeEnemies();
            if (text != "none") ModLog.Info("Enemy health now (for balancing Blazing Dash): " + text);
        }
        catch (Exception ex)
        {
            _enemyLogOff = true;
            ModLog.Warn($"Enemy health log switched off: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string DescribeEnemies()
    {
        var found = Object.FindObjectsOfType(Il2CppType.Of<EnemyHealthContainer>(), false);
        if (found == null || found.Length == 0) return "none";
        var groups = new Dictionary<string, List<float>>();
        var bases = new Dictionary<string, float>();
        var count = 0;
        foreach (var obj in found)
        {
            var h = obj?.TryCast<EnemyHealthContainer>();
            if (h == null) continue;
            count++;
            var name = Clean(h.gameObject.name) + (h.IsBoss ? " (boss)" : "");
            if (!groups.TryGetValue(name, out var list)) groups[name] = list = new List<float>();
            list.Add(h.MaxHealth);
            bases[name] = h.maxHealth;
        }

        if (count == 0) return "none";
        return $"{count}: " + string.Join("; ", groups.OrderByDescending(g => g.Value.Count).Take(12)
            .Select(g => $"{g.Key} x{g.Value.Count} max HP {Range(g.Value)} (base {F(bases[g.Key])})"));
    }

    private static string Range(List<float> values)
    {
        var lo = values.Min();
        var hi = values.Max();
        return Math.Abs(hi - lo) < 0.5f ? F(lo) : $"{F(lo)}-{F(hi)}";
    }

    private static string Clean(string name)
    {
        var n = name.Replace("(Clone)", "").Trim();
        var paren = n.IndexOf(" (", StringComparison.Ordinal);
        return paren > 0 ? n.Substring(0, paren) : n;
    }

    private static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private static void Try(StringBuilder sb, string what, Action a)
    {
        try
        {
            a();
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  !! {what} failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
