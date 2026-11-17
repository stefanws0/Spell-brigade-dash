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
    // 0.3.0: focused on the level-up shop (StoreCard / StoreItem / PartyLevelManager ...) found in the first report.
    private static readonly Regex TypeRx = new("(Store|Improvement|PartyLevel|LevelUp|Upgrade|Reroll|Offer)",
        RegexOptions.CultureInvariant);


    private static readonly Regex MethodRx = new(
        "(setup|generate|roll|open|show|select|purchase|buy|choose|chosen|pick|confirm|offer|apply|level|upgrade|improv|store|card|reroll|spawn|create|add|grant|refresh)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);


    private static readonly Regex PriorityRx = new("(Store|Improvement|PartyLevel)", RegexOptions.CultureInvariant);

    private static readonly HashSet<string> SkipNames = new(StringComparer.Ordinal)
    {
        "Update", "LateUpdate", "FixedUpdate", "OnGUI", "Awake", "Start", "OnEnable", "OnDisable", "OnDestroy", "OnValidate",
        "ToString", "GetHashCode", "Equals", "Finalize", "MoveNext", "Reset", "Dispose", "OnPointerEnter", "OnPointerExit",
        "OnSelect", "OnDeselect",
    };

    private const string PatchStep = "trace patch: ";
    private const string CallStep = "trace call: ";

    private static readonly Dictionary<MethodBase, int> CallBreadcrumbs = new();
    private static readonly Dictionary<MethodBase, int> CallsCompleted = new();
    private static bool _installed;

    /// <summary>Methods that crashed the game while traced in an earlier session; never traced again.</summary>
    public static string SkipFilePath => System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "SpellBrigadeDash.trace-skip.txt");

    public static List<string> Skipped { get; } = new();

    public static Dictionary<string, int> Calls { get; } = new();
    public static List<string> Traced { get; } = new();
    public static List<string> Failed { get; } = new();

    public static void Install()
    {
        if (_installed || ModConfig.SafeMode.Value || !ModConfig.TraceLevelUpMethods.Value) return;
        _installed = true;
        var skip = LoadSkipList();

        var candidates = new List<MethodInfo>();
        foreach (var type in Il2CppNames.GameTypes())
        {
            if (type.IsInterface || type.IsEnum || type.ContainsGenericParameters || type.Name.Contains('<') || !TypeRx.IsMatch(type.Name)) continue;
            if (IsStruct(type)) continue; // patching methods on (non-blittable) game structs is a classic IL2CPP crash
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
                if (m.Name.StartsWith("__", StringComparison.Ordinal)) continue; // Netcode RPC handlers / generated plumbing
                if (m.GetParameters().Any(p => p.ParameterType.IsByRef || p.ParameterType.IsPointer || IsStructByValue(p.ParameterType))) continue;
                if (IsStructByValue(m.ReturnType)) continue;
                if (skip.Contains(Label(m)))
                {
                    Skipped.Add(Label(m));
                    continue;
                }

                candidates.Add(m);
            }
        }

        var max = Math.Max(0, ModConfig.TraceMaxMethods.Value);
        var chosen = candidates
            .OrderByDescending(m => PriorityRx.IsMatch(m.DeclaringType?.Name ?? "") ? 1 : 0)
            .ThenByDescending(m => PriorityRx.IsMatch(m.Name) ? 1 : 0)
            .Take(max)
            .ToList();

        var prefix = new HarmonyMethod(typeof(MethodTracer).GetMethod(nameof(Prefix), BindingFlags.Static | BindingFlags.NonPublic));
        var postfix = new HarmonyMethod(typeof(MethodTracer).GetMethod(nameof(Postfix), BindingFlags.Static | BindingFlags.NonPublic));
        foreach (var m in chosen)
        {
            var label = Label(m);
            try
            {
                ModLog.Step(PatchStep + label);
                Plugin.Harmony.Patch(m, prefix: prefix, postfix: postfix);
                ModLog.Step("trace patch ok");
                Traced.Add(label);
            }
            catch (Exception ex)
            {
                Failed.Add($"{label}  [{ex.Message}]");
            }
        }

        ModLog.Info($"Discovery: tracing {Traced.Count} level-up shop method(s) ({candidates.Count} candidates, {Failed.Count} failed, {Skipped.Count} skipped after earlier crashes). " +
                    "Level up during a run, then press the report hotkey (Left Ctrl + 1 by default).");
    }

    /// <summary>
    /// Crash journal: if the previous session died while patching or calling a traced method (its log ends with that
    /// breadcrumb and no clean exit), that method is added to the skip file for good.
    /// </summary>
    private static HashSet<string> LoadSkipList()
    {
        var skip = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            if (System.IO.File.Exists(SkipFilePath))
                foreach (var line in System.IO.File.ReadAllLines(SkipFilePath))
                    if (line.Trim().Length > 0 && !line.StartsWith("#"))
                        skip.Add(line.Trim());

            var crashed = ModLog.PreviousCrashStep;
            string? culprit = null;
            if (crashed != null && crashed.StartsWith(PatchStep, StringComparison.Ordinal)) culprit = crashed[PatchStep.Length..].Trim();
            if (crashed != null && crashed.StartsWith(CallStep, StringComparison.Ordinal)) culprit = crashed[CallStep.Length..].Trim();
            if (culprit != null && skip.Add(culprit))
            {
                if (!System.IO.File.Exists(SkipFilePath))
                    System.IO.File.WriteAllText(SkipFilePath, "# Methods that crashed the game while SpellBrigadeDash traced them. They are never traced again.\n");
                System.IO.File.AppendAllText(SkipFilePath, culprit + "\n");
                ModLog.Warn($"The game crashed last time while tracing {culprit}; it is skipped from now on (see {SkipFilePath}).");
            }
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Reading the trace skip list failed: {ex.Message}");
        }

        return skip;
    }

    private static bool IsStruct(Type t)
    {
        if (t.IsValueType) return true;
        for (var b = t.BaseType; b != null; b = b.BaseType)
            if (b.FullName == "Il2CppSystem.ValueType")
                return true;
        return false;
    }

    /// <summary>A struct passed or returned by value (enums and primitives are fine).</summary>
    private static bool IsStructByValue(Type t)
        => t != typeof(void) && !t.IsPrimitive && !t.IsEnum && IsStruct(t);

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

    /// <summary>Breadcrumb before the first few calls: if the game dies inside, the next session skips this method.</summary>
    private static void Prefix(MethodBase __originalMethod)
    {
        try
        {
            CallBreadcrumbs.TryGetValue(__originalMethod, out var n);
            if (n >= 3) return;
            CallBreadcrumbs[__originalMethod] = n + 1;
            ModLog.Step(CallStep + Label(__originalMethod));
        }
        catch
        {
            // never let tracing break the game
        }
    }

    private static void Postfix(MethodBase __originalMethod)
    {
        try
        {
            // close the breadcrumb opened by Prefix (only for the first few calls)
            if (CallBreadcrumbs.TryGetValue(__originalMethod, out var opened))
            {
                CallsCompleted.TryGetValue(__originalMethod, out var closed);
                if (closed < opened)
                {
                    CallsCompleted[__originalMethod] = closed + 1;
                    ModLog.Step("trace call ok");
                }
            }

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
