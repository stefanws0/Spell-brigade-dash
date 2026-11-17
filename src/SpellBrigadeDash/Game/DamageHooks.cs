namespace SpellBrigadeDash.Game;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

/// <summary>
/// I-frames: Harmony prefixes on the game's "take damage" methods. A prefix that returns false skips
/// the original method, so the hit never happens while the wizard is in <see cref="Invulnerability"/>.
///
/// The game's real damage method is unknown until discovery, so by default we hook every method on
/// the wizard's components whose name looks like TakeDamage / OnHit / ReceiveDamage / RpcTakeDamage...
/// Exact methods can be added with GameHooks.DamageMethods.
/// </summary>
internal static class DamageHooks
{
    // TakeDamage, ReceiveDamage, ApplyDamage, OnDamaged, OnHit, Hurt, CmdTakeDamage, RpcTakeHit, UserCode_RpcTakeDamage, RpcLogic___TakeDamage_123...
    // Deliberately NOT DealDamage/InflictDamage/DoDamage (those are usually the attacker's side).
    private static readonly Regex AutoName = new(
        @"^(?:UserCode_|RpcLogic___)?(?:Cmd|Rpc|Server|Client|Local|Net|Network|Target)?_?(?:Take|Receive|Apply|Handle|Process|On|Get)?(?:Damage|Damaged|Hit|Hurt|Harm)(?:Taken|Received|Rpc|Server|Client|Local)?(?:_\d+)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex HealthLikeType = new("(health|hurt|damage|life|hp|vital|stat|hitbox|defen|armor|armour)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly HashSet<MethodBase> Patched = new();
    private static HashSet<IntPtr> _localPtrs = new();

    public static List<string> PatchedNames { get; } = new();
    public static List<string> FailedNames { get; } = new();
    public static Dictionary<string, int> CallsOnLocal { get; } = new();

    public static bool IsDamageLikeName(string name) => AutoName.IsMatch(name);

    public static void SetLocal(PlayerInfo p) => _localPtrs = p.GameObjectPtrs;

    /// <summary>Hook the damage methods relevant for this wizard (idempotent).</summary>
    public static void HookFor(PlayerInfo p)
    {
        if (ModConfig.AutoDamageHooks.Value)
        {
            var types = new HashSet<Type>();
            foreach (var v in p.Views)
                if (v.InteropType != null)
                    types.Add(v.InteropType);

            // Also health-like components on child objects (hurtboxes, health on a child...). Other
            // children (weapons, spell casters) are skipped: their "Hit" methods are the wizard hitting enemies.
            try
            {
                var count = 0;
                foreach (var c in p.Root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (c == null || ++count > 200) break;
                    var name = Il2CppNames.FullName(c);
                    if (!HealthLikeType.IsMatch(name)) continue;
                    var t = Il2CppNames.FindInteropType(name);
                    if (t != null) types.Add(t);
                }
            }
            catch (Exception ex)
            {
                ModLog.Debug($"Scanning child components failed: {ex.Message}");
            }

            foreach (var type in types)
            foreach (var t in Reflect.SelfAndGameBases(type))
            foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                if (AutoName.IsMatch(m.Name))
                    TryPatch(m);
        }

        HookConfigured();
    }

    public static void HookConfigured()
    {
        foreach (var entry in ModConfig.SplitList(ModConfig.DamageMethods.Value, ';'))
        {
            var parts = entry.Split(':');
            if (parts.Length != 2)
            {
                ModLog.WarnOnce("dmg-cfg-" + entry, $"GameHooks.DamageMethods entry '{entry}' should look like Type:Method.");
                continue;
            }

            var type = Il2CppNames.FindInteropType(parts[0].Trim());
            if (type == null)
            {
                ModLog.WarnOnce("dmg-cfg-" + entry, $"GameHooks.DamageMethods: type '{parts[0]}' not found.");
                continue;
            }

            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(m => m.Name == parts[1].Trim()).ToList();
            if (methods.Count == 0) ModLog.WarnOnce("dmg-cfg-" + entry, $"GameHooks.DamageMethods: method '{entry}' not found.");
            foreach (var m in methods) TryPatch(m, force: true);
        }
    }

    private static void TryPatch(MethodInfo m, bool force = false)
    {
        if (!Patched.Add(m)) return;
        var label = $"{m.DeclaringType?.Name}.{m.Name}({string.Join(", ", m.GetParameters().Select(x => x.ParameterType.Name))}) : {m.ReturnType.Name}";

        if (m.IsAbstract || m.IsGenericMethodDefinition || m.IsSpecialName || (!force && m.GetParameters().Length == 0)
            || m.GetParameters().Any(x => x.ParameterType.IsByRef || x.ParameterType.IsPointer))
        {
            FailedNames.Add(label + "  [skipped: unsupported signature]");
            return;
        }

        string prefix;
        if (m.ReturnType == typeof(void)) prefix = nameof(PrefixVoid);
        else if (m.ReturnType == typeof(bool)) prefix = nameof(PrefixBool);
        else if (m.ReturnType == typeof(int)) prefix = nameof(PrefixInt);
        else if (m.ReturnType == typeof(float)) prefix = nameof(PrefixFloat);
        else
        {
            FailedNames.Add(label + "  [skipped: return type]");
            return;
        }

        try
        {
            Plugin.Harmony.Patch(m, prefix: new HarmonyMethod(typeof(DamageHooks).GetMethod(prefix, BindingFlags.Static | BindingFlags.NonPublic)));
            PatchedNames.Add(label);
            ModLog.Info($"I-frame hook installed on {label}");
        }
        catch (Exception ex)
        {
            FailedNames.Add($"{label}  [patch failed: {ex.Message}]");
            ModLog.Warn($"Could not hook {label}: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------- Harmony prefixes

    private static bool PrefixVoid(object __instance, MethodBase __originalMethod) => !ShouldBlock(__instance, __originalMethod);

    private static bool PrefixBool(object __instance, MethodBase __originalMethod, ref bool __result)
    {
        if (!ShouldBlock(__instance, __originalMethod)) return true;
        __result = false;
        return false;
    }

    private static bool PrefixInt(object __instance, MethodBase __originalMethod, ref int __result)
    {
        if (!ShouldBlock(__instance, __originalMethod)) return true;
        __result = 0;
        return false;
    }

    private static bool PrefixFloat(object __instance, MethodBase __originalMethod, ref float __result)
    {
        if (!ShouldBlock(__instance, __originalMethod)) return true;
        __result = 0f;
        return false;
    }

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
