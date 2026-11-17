namespace SpellBrigadeDash.Game;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using SpellBrigadeDash.Core;
using UnityEngine;

/// <summary>
/// Reads and lowers the wizard's HP for the dash's health cost.
///
/// STATUS: BEST GUESS until the discovery report confirms the game's health class. It looks for numeric
/// fields/properties named like currentHealth / maxHealth (GameHooks.HealthMembers / MaxHealthMembers) on the
/// wizard's components and writes the new value directly. That way it does NOT go through the game's
/// damage path (no hit flash, and our own i-frames can't block it). In co-op the value is set on this
/// client; whether the host keeps it depends on how the game syncs HP (to be checked with the report).
/// </summary>
internal static class HealthBridge
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

    private static readonly Regex HealthLikeType = new("(health|hurt|damage|life|hp|vital|stat|player|wizard|character)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Dictionary<long, Accessor?> Cache = new();
    private static readonly Dictionary<long, float> RetryAt = new();
    private static bool _candidatesLogged;

    public static string Status { get; private set; } = "not used yet";

    /// <summary>Take the dash's HP cost. Returns the HP taken (0 if nothing was taken or HP couldn't be found).</summary>
    public static float ApplyCost(PlayerInfo p, float fraction)
    {
        if (fraction <= 0f) return 0f;
        try
        {
            var acc = Find(p);
            if (acc == null) return 0f;

            var current = acc.ReadCurrent();
            var max = acc.ReadMax();
            var cost = DashTuning.HealthCost(fraction, current, max);
            if (cost <= 0f) return 0f;

            var target = current - cost;
            if (acc.IsInteger) target = Math.Max(1f, (float)Math.Floor(target));
            acc.WriteCurrent(target);
            return current - target;
        }
        catch (Exception ex)
        {
            ModLog.ErrorThrottled("HealthBridge", ex, Time.unscaledTime, 30f);
            return 0f;
        }
    }

    /// <summary>Every numeric member on the wizard that looks health-related (for the discovery report).</summary>
    public static IEnumerable<string> DescribeCandidates(PlayerInfo p)
    {
        foreach (var (view, member, type) in NumericMembers(p))
        {
            string value;
            try
            {
                value = Convert.ToString(Read(member, view.Wrapped!)) ?? "?";
            }
            catch (Exception ex)
            {
                value = "(" + ex.GetType().Name + ")";
            }

            var lower = member.Name.ToLowerInvariant();
            if (lower.Contains("health") || lower.Contains("hp") || lower.Contains("life") || lower.Contains("shield") || lower.Contains("armor"))
                yield return $"{view.ShortName}.{member.Name} : {type.Name} = {value}";
        }
    }

    // ------------------------------------------------------------------------------------------

    private static Accessor? Find(PlayerInfo p)
    {
        // Found once = keep it. Not found = try again a few seconds later (health initialises after the wizard spawns).
        if (Cache.TryGetValue(p.Id, out var cached) && cached != null) return cached;
        var now = Time.unscaledTime;
        if (RetryAt.TryGetValue(p.Id, out var retry) && now < retry) return null;
        RetryAt[p.Id] = now + 5f;

        var currentNames = ModConfig.SplitList(ModConfig.HealthMembers.Value);
        var maxNames = ModConfig.SplitList(ModConfig.MaxHealthMembers.Value);
        var members = NumericMembers(p).ToList();

        Accessor? found = null;
        foreach (var name in currentNames)
        {
            foreach (var cur in members.Where(m => m.member.Name == name && CanWrite(m.member)))
            {
                // prefer max HP on the same component, else anywhere on the wizard
                var maxCandidates = maxNames.SelectMany(n => members.Where(m => m.view == cur.view && m.member.Name == n))
                    .Concat(maxNames.SelectMany(n => members.Where(m => m.member.Name == n)));
                foreach (var max in maxCandidates)
                {
                    var acc = new Accessor(cur.view.Wrapped!, cur.member, max.view.Wrapped!, max.member, cur.type == typeof(int));
                    if (!LooksLikeHealth(acc)) continue;
                    found = acc;
                    Status = $"current HP = {cur.view.ShortName}.{cur.member.Name} ({acc.ReadCurrent():0.#}), " +
                             $"max HP = {max.view.ShortName}.{max.member.Name} ({acc.ReadMax():0.#})";
                    break;
                }

                if (found != null) break;
            }

            if (found != null) break;
        }

        if (found == null)
        {
            Status = "no health field found on the wizard yet; dash HP cost not applied (retrying; see the discovery report)";
            ModLog.WarnOnce("health-missing", "Dash HP cost: " + Status);
            if (!_candidatesLogged)
            {
                _candidatesLogged = true;
                ModLog.Info("Health-like members on the wizard: " + string.Join("; ", DescribeCandidates(p).Take(30)));
            }
        }
        else
        {
            ModLog.Info("Dash HP cost uses " + Status);
        }

        Cache[p.Id] = found;
        return found;
    }

    private static IEnumerable<(ComponentView view, MemberInfo member, Type type)> NumericMembers(PlayerInfo p)
    {
        var views = new List<ComponentView>(p.Views);
        try
        {
            var seen = new HashSet<IntPtr>(views.Select(v => v.Component.Pointer));
            foreach (var c in p.Root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (c == null || !seen.Add(c.Pointer)) continue;
                if (!HealthLikeType.IsMatch(Il2CppNames.FullName(c))) continue;
                views.Add(new ComponentView(c));
            }
        }
        catch
        {
            // children are optional
        }

        foreach (var v in views)
        {
            if (v.Wrapped == null || v.InteropType == null || !Il2CppNames.IsGameType(v.InteropType)) continue;
            foreach (var prop in v.InteropType.GetProperties(Flags))
            {
                if (prop.GetIndexParameters().Length != 0 || !prop.CanRead || !IsNumeric(prop.PropertyType)) continue;
                yield return (v, prop, prop.PropertyType);
            }

            foreach (var field in v.InteropType.GetFields(Flags))
            {
                if (!IsNumeric(field.FieldType) || field.IsStatic) continue;
                yield return (v, field, field.FieldType);
            }
        }
    }

    /// <summary>Sanity check so an unrelated number (a stat level, a timer...) is never treated as HP.</summary>
    private static bool LooksLikeHealth(Accessor acc)
    {
        try
        {
            var cur = acc.ReadCurrent();
            var max = acc.ReadMax();
            return max >= 1f && cur > 0f && cur <= max * 1.05f && !float.IsNaN(cur) && !float.IsInfinity(max);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsNumeric(Type t) => t == typeof(float) || t == typeof(int) || t == typeof(double);

    private static bool CanWrite(MemberInfo m) => m is FieldInfo || (m is PropertyInfo p && p.CanWrite);

    private static object? Read(MemberInfo m, object target) => m switch
    {
        PropertyInfo p => p.GetValue(target),
        FieldInfo f => f.GetValue(target),
        _ => null,
    };

    private sealed class Accessor
    {
        private readonly object _curTarget;
        private readonly MemberInfo _cur;
        private readonly object _maxTarget;
        private readonly MemberInfo _max;

        public Accessor(object curTarget, MemberInfo cur, object maxTarget, MemberInfo max, bool isInteger)
        {
            _curTarget = curTarget;
            _cur = cur;
            _maxTarget = maxTarget;
            _max = max;
            IsInteger = isInteger;
        }

        public bool IsInteger { get; }

        public float ReadCurrent() => Convert.ToSingle(Read(_cur, _curTarget));

        public float ReadMax() => Convert.ToSingle(Read(_max, _maxTarget));

        public void WriteCurrent(float value)
        {
            var type = _cur is PropertyInfo p ? p.PropertyType : ((FieldInfo)_cur).FieldType;
            object boxed = type == typeof(int) ? (int)value : type == typeof(double) ? (double)value : value;

            // The game keeps a network-synced copy (HealthContainer.SyncedCurrentHealth, a NetworkVariable).
            // Write that first, so the health bar updates and other players see the new HP.
            WriteSynced(boxed);

            if (_cur is PropertyInfo prop) prop.SetValue(_curTarget, boxed);
            else ((FieldInfo)_cur).SetValue(_curTarget, boxed);
        }

        private void WriteSynced(object boxed)
        {
            if (_syncedBroken) return;
            try
            {
                var syncedProp = _curTarget.GetType().GetProperty("Synced" + _cur.Name, Flags);
                var variable = syncedProp?.GetValue(_curTarget);
                var valueProp = variable?.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
                if (valueProp == null || !valueProp.CanWrite) return;
                valueProp.SetValue(variable, boxed);
            }
            catch (Exception ex)
            {
                _syncedBroken = true; // e.g. only the server may write it; the local value is still set
                ModLog.Warn($"Dash HP cost: couldn't write Synced{_cur.Name} ({(ex.InnerException ?? ex).Message}); only the local value is changed.");
            }
        }

        private bool _syncedBroken;
    }
}
