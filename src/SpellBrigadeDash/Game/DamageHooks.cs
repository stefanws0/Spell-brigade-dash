namespace SpellBrigadeDash.Game;

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

/// <summary>
/// I-frames: Harmony prefixes on the game's two "take damage" methods of <c>PlayerHealthContainer</c>
/// (<c>TakeDamage(float, DamageInfo, bool)</c> and <c>RemoveHealthOnOwner(float, DamageInfo)</c>). A prefix that
/// returns false skips the original method, so the hit never happens while that wizard is in
/// <see cref="Invulnerability"/>. The patches sit on the class, so they cover every wizard (yours and others').
/// </summary>
internal static class DamageHooks
{
    private static bool _installed;
    private static HashSet<IntPtr> _localPtrs = new();

    public static List<string> PatchedNames { get; } = new();
    public static List<string> FailedNames { get; } = new();
    public static Dictionary<string, int> CallsOnLocal { get; } = new();

    public static void SetLocal(PlayerInfo p) => _localPtrs = p.GameObjectPtrs;

    /// <summary>Install the two patches (once).</summary>
    public static void Install()
    {
        if (_installed || ModConfig.SafeMode.Value) return;
        _installed = true;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var type = typeof(PlayerHealthContainer);
        TryPatch(type.GetMethod(nameof(PlayerHealthContainer.TakeDamage), flags, null,
            new[] { typeof(float), typeof(DamageInfo), typeof(bool) }, null), "PlayerHealthContainer.TakeDamage(float, DamageInfo, bool)");
        TryPatch(type.GetMethod(nameof(PlayerHealthContainer.RemoveHealthOnOwner), flags, null,
            new[] { typeof(float), typeof(DamageInfo) }, null), "PlayerHealthContainer.RemoveHealthOnOwner(float, DamageInfo)");
    }

    private static void TryPatch(MethodInfo? m, string label)
    {
        if (m == null)
        {
            FailedNames.Add(label + "  [not found in this game version]");
            ModLog.Warn($"I-frames: {label} not found; that damage path isn't blocked.");
            return;
        }

        if (!NativeCode.CanPatch(m, label, out var why))
        {
            FailedNames.Add($"{label}  [skipped: {why}]");
            ModLog.Warn($"I-frames: not patching {label}: {why}");
            return;
        }

        try
        {
            ModLog.Step($"damage hook patch: {label} ({why})");
            Plugin.Harmony.Patch(m, prefix: new HarmonyMethod(AccessTools.Method(typeof(DamageHooks), nameof(Prefix))));
            ModLog.Step("damage hook patch ok");
            PatchedNames.Add(label);
            ModLog.Info($"I-frame hook installed on {label}");
        }
        catch (Exception ex)
        {
            FailedNames.Add($"{label}  [patch failed: {ex.Message}]");
            ModLog.Warn($"Could not hook {label}: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------- Harmony prefix

    private static bool Prefix(object __instance, MethodBase __originalMethod) => !ShouldBlock(__instance, __originalMethod);

    private static bool ShouldBlock(object instance, MethodBase method)
    {
        try
        {
            if (instance is not Component c) return false;
            var ptr = c.gameObject.Pointer;
            var invulnerable = Invulnerability.IsInvulnerable(ptr);

            if (_localPtrs.Contains(ptr))
            {
                var key = $"{method.DeclaringType?.Name}.{method.Name}";
                CallsOnLocal.TryGetValue(key, out var n);
                CallsOnLocal[key] = n + 1;
                if (n < 3) ModLog.Info($"Damage hook {key} fired on your wizard (invulnerable: {invulnerable}).");
            }

            if (!invulnerable) return false;
            Invulnerability.BlockedHits++;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
