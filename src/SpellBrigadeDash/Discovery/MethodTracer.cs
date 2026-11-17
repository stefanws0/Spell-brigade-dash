namespace SpellBrigadeDash.Discovery;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using SpellBrigadeDash.Game;

/// <summary>
/// Discovery helper. It attaches logging-only Harmony postfixes to game methods that look like part
/// of the level-up / upgrade flow. When you level up during a run, the BepInEx log and the discovery
/// report show which methods ran, and that is the hook the card injection needs.
/// </summary>
internal static class MethodTracer
{
    private static readonly Regex TypeRx = new(
        "(level|upgrade|perk|reward|offer|choice|card|shop|xp|experience|rarity|boon|blessing|augment|talent|draft|relic|infusion|tome|book)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex MethodRx = new(
        "(level.?up|upgrade|perk|reward|offer|choice|choose|chosen|card|shop|rarity|roll|pick|select|generate|grant|draft|reroll|unlock|augment|infus|option)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex PriorityRx = new("(level|upgrade|card|reward|choice|offer)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> SkipNames = new(StringComparer.Ordinal)
    {
        "Update", "LateUpdate", "FixedUpdate", "OnGUI", "Awake", "Start", "OnEnable", "OnDisable", "OnDestroy", "OnValidate",
        "ToString", "GetHashCode", "Equals", "Finalize", "MoveNext", "Reset", "Dispose", "OnPointerEnter", "OnPointerExit",
        "OnSelect", "OnDeselect",
    };

    private static bool _installed;

    public static Dictionary<string, int> Calls { get; } = new();
    public static List<string> Traced { get; } = new();
    public static List<string> Failed { get; } = new();

    public static void Install()
    {
        if (_installed || ModConfig.SafeMode.Value || !ModConfig.TraceLevelUpMethods.Value) return;
        _installed = true;

        var candidates = new List<MethodInfo>();
        foreach (var type in Il2CppNames.GameTypes())
        {
            if (type.IsInterface || type.IsEnum || type.ContainsGenericParameters || type.Name.Contains('<') || !TypeRx.IsMatch(type.Name)) continue;
            MethodInfo[] methods;
            try
            {
                methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            }
            catch
            {
                continue;
            }

            foreach (var m in methods)
            {
                if (m.IsSpecialName || m.IsAbstract || m.IsGenericMethodDefinition || m.ContainsGenericParameters) continue;
                if (SkipNames.Contains(m.Name) || m.Name.Contains('<') || m.Name.StartsWith("Native", StringComparison.Ordinal)) continue;
                if (!MethodRx.IsMatch(m.Name)) continue;
                if (m.GetParameters().Any(p => p.ParameterType.IsByRef || p.ParameterType.IsPointer)) continue;
                candidates.Add(m);
            }
        }

        var max = Math.Max(0, ModConfig.TraceMaxMethods.Value);
        var chosen = candidates
            .OrderByDescending(m => PriorityRx.IsMatch(m.DeclaringType?.Name ?? "") ? 1 : 0)
            .ThenByDescending(m => PriorityRx.IsMatch(m.Name) ? 1 : 0)
            .Take(max)
            .ToList();

        var postfix = new HarmonyMethod(typeof(MethodTracer).GetMethod(nameof(Postfix), BindingFlags.Static | BindingFlags.NonPublic));
        foreach (var m in chosen)
        {
            var label = Label(m);
            try
            {
                ModLog.Step($"trace patch: {label}");
                Plugin.Harmony.Patch(m, postfix: postfix);
                ModLog.Step("trace patch ok");
                Traced.Add(label);
            }
            catch (Exception ex)
            {
                Failed.Add($"{label}  [{ex.Message}]");
            }
        }

        ModLog.Info($"Discovery: tracing {Traced.Count} upgrade-related method(s) ({candidates.Count} candidates, {Failed.Count} failed). Level up during a run, then press the report hotkey (Left Ctrl + 1 by default).");
    }

    public static string Label(MethodBase m)
        => $"{m.DeclaringType?.FullName}.{m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{Pretty(p.ParameterType)} {p.Name}"))})";

    public static string Pretty(Type t)
    {
        if (!t.IsGenericType) return t.Name;
        var name = t.Name;
        var tick = name.IndexOf('`');
        if (tick >= 0) name = name[..tick];
        return $"{name}<{string.Join(", ", t.GetGenericArguments().Select(Pretty))}>";
    }

    private static void Postfix(MethodBase __originalMethod)
    {
        try
        {
            var key = $"{__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}";
            Calls.TryGetValue(key, out var n);
            Calls[key] = ++n;
            if (n <= 3) ModLog.Info($"TRACE {key} (call #{n})");
        }
        catch
        {
            // never let tracing break the game
        }
    }
}
