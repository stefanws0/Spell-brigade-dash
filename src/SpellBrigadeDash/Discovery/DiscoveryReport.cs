namespace SpellBrigadeDash.Discovery;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using SpellBrigadeDash.Fx;
using SpellBrigadeDash.Game;
using SpellBrigadeDash.Motion;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Writes BepInEx/SpellBrigadeDash-discovery.txt: what the mod found in the game (player classes,
/// damage methods, upgrade/rarity types, networking, audio, layers, API self-test). Send this file
/// back so the exact hooks can be written.
/// </summary>
internal static class DiscoveryReport
{
    private const int MaxChars = 2_500_000;

    private static readonly Regex UpgradeTypeRx = new(
        "(level|upgrade|perk|reward|offer|choice|card|shop|rarity|boon|blessing|augment|talent|draft|relic|infusion|experience|xp)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RarityValueRx = new("^(common|uncommon|rare|epic|legendary|mythic|mythical|unique|exotic)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string Path => System.IO.Path.Combine(Paths.BepInExRootPath, "SpellBrigadeDash-discovery.txt");

    public static void Write(DashSystem sys, string reason)
    {
        var sb = new StringBuilder(256 * 1024);
        void Section(string title)
        {
            sb.AppendLine();
            sb.AppendLine(new string('=', 100));
            sb.AppendLine("== " + title);
            sb.AppendLine(new string('=', 100));
        }

        void Try(string what, Action a)
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

        sb.AppendLine($"SpellBrigadeDash {Plugin.Version} discovery report");
        sb.AppendLine($"Written: {DateTime.Now:yyyy-MM-dd HH:mm:ss}  (reason: {reason})");
        Try("app info", () =>
        {
            sb.AppendLine($"Game: {Application.productName} {Application.version}   Unity {Application.unityVersion}");
            sb.AppendLine($"Scene: {SceneManager.GetActiveScene().name}   Screen: {Screen.width}x{Screen.height}   timeScale: {Time.timeScale}");
        });

        Section("1. Mod status");
        Try("status", () => sys.AppendStatus(sb));

        Section("2. Libraries");
        Try("libraries", () =>
        {
            var names = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name ?? "").OrderBy(n => n).ToList();
            var net = NetBridge.DetectLibraries();
            sb.AppendLine($"Networking: {(net.Count == 0 ? "none recognised" : string.Join(", ", net))}");
            sb.AppendLine($"Audio middleware: {Flag(names, "FMODUnity", "FMOD")} {Flag(names, "AkSoundEngine", "Wwise")} {Flag(names, "MasterAudio", "MasterAudio")}");
            sb.AppendLine($"Input: {Flag(names, "Unity.InputSystem", "Unity.InputSystem")} {Flag(names, "Rewired_Core", "Rewired")} {Flag(names, "InControl", "InControl")}");
            sb.AppendLine($"Rendering: {Flag(names, "Unity.RenderPipelines.Universal.Runtime", "URP")} {Flag(names, "Unity.RenderPipelines.HighDefinition.Runtime", "HDRP")}");
            sb.AppendLine("Loaded assemblies:");
            foreach (var n in names) sb.AppendLine("  " + n);
        });

        Section("3. API self-test");
        Try("self-test", () => SelfTest(sb, sys.Locator.Local));

        Section("4. Player candidates");
        Try("candidates", () =>
        {
            sb.AppendLine("Search: " + sys.Locator.LastSearchSummary);
            foreach (var c in sys.Locator.LastCandidates) sb.AppendLine("  " + c);
        });

        var local = sys.Locator.Local;
        Section("5. Your wizard");
        if (local == null || local.IsDestroyed)
        {
            sb.AppendLine("(no local wizard found - start a run, then press the report hotkey)");
        }
        else
        {
            Try("wizard", () => DescribePlayer(sb, local));
            Try("remote", () =>
            {
                foreach (var r in sys.Locator.Remotes)
                    if (!r.IsDestroyed)
                        sb.AppendLine($"Other player: {PathOf(r.Root)} ({r.LocalReason})");
            });

            Section("6. Your wizard's game components (members)");
            Try("members", () => DescribeMembers(sb, local));
        }

        Section("7. Damage hooks (i-frames)");
        Try("damage", () =>
        {
            sb.AppendLine($"Hits cancelled by i-frames so far: {Invulnerability.BlockedHits}");
            sb.AppendLine("Installed:");
            foreach (var n in DamageHooks.PatchedNames) sb.AppendLine("  + " + n);
            sb.AppendLine("Skipped/failed:");
            foreach (var n in DamageHooks.FailedNames) sb.AppendLine("  - " + n);
            sb.AppendLine("Calls on your wizard:");
            foreach (var kv in DamageHooks.CallsOnLocal) sb.AppendLine($"  {kv.Key}: {kv.Value}");
            sb.AppendLine("All damage-like methods in game code:");
            var count = 0;
            foreach (var t in Il2CppNames.GameTypes().OrderBy(t => t.FullName))
            foreach (var m in SafeMethods(t))
            {
                if (!DamageHooks.IsDamageLikeName(m.Name) || m.IsSpecialName) continue;
                sb.AppendLine("  " + MethodTracer.Label(m) + " : " + MethodTracer.Pretty(m.ReturnType) + (m.IsStatic ? "  [static]" : ""));
                if (++count >= 400) break;
            }
        });

        Section("8. Upgrade / level-up system candidates");
        Try("upgrades", () => DescribeUpgradeTypes(sb));

        Section("9. Method trace (level up during a run, then press the report hotkey)");
        Try("trace", () =>
        {
            sb.AppendLine($"Traced {MethodTracer.Traced.Count} method(s). Calls so far:");
            foreach (var kv in MethodTracer.Calls.OrderByDescending(k => k.Value)) sb.AppendLine($"  {kv.Value,6}x  {kv.Key}");
            sb.AppendLine("Traced methods:");
            foreach (var t in MethodTracer.Traced) sb.AppendLine("  " + t);
            if (MethodTracer.Failed.Count > 0)
            {
                sb.AppendLine("Could not trace:");
                foreach (var t in MethodTracer.Failed) sb.AppendLine("  " + t);
            }
        });

        Section("10. Input bindings (game's InputActionAssets)");
        Try("input", () => sb.AppendLine(InputAssetsJson()));

        Section("11. Audio");
        Try("audio", () =>
        {
            sb.AppendLine("Dash sound: " + sys.Sound.Status);
            sb.AppendLine("Mixer groups: " + string.Join(", ", SoundPlayer.AllMixerGroups()));
            var clips = SoundPlayer.AllClipNames();
            sb.AppendLine($"Loaded AudioClips ({clips.Count}):");
            foreach (var c in clips.Take(800)) sb.AppendLine("  " + c);
        });

        Section("12. Physics layers");
        Try("layers", () =>
        {
            for (var i = 0; i < 32; i++)
            {
                var n = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(n)) sb.AppendLine($"  {i,2}: {n}");
            }

            if (local != null && !local.IsDestroyed)
            {
                var mask = DashMotor.BlockingMask(local);
                var names = Enumerable.Range(0, 32).Where(i => (mask & (1 << i)) != 0).Select(i => $"{i}:{LayerMask.LayerToName(i)}");
                sb.AppendLine($"Dash collides with: {string.Join(", ", names)}");
            }
        });

        Section("13. Camera");
        Try("camera", () =>
        {
            var cam = Camera.main;
            if (cam == null)
            {
                sb.AppendLine("Camera.main = null");
                return;
            }

            var t = cam.transform;
            sb.AppendLine($"{PathOf(cam.gameObject)} ortho={cam.orthographic} size={cam.orthographicSize} fov={cam.fieldOfView}");
            sb.AppendLine($"pos={t.position} euler={t.eulerAngles} forward={t.forward}");
            if (local != null && !local.IsDestroyed) sb.AppendLine($"offset to wizard: {t.position - local.RootT.position}");
        });

        Section("14. ScriptableObjects in memory (by class)");
        Try("scriptables", () =>
        {
            var groups = Resources.FindObjectsOfTypeAll<ScriptableObject>()
                .Where(o => o != null)
                .Select(o => (cls: Il2CppNames.FullName(o), name: o.name))
                .Where(x => !x.cls.StartsWith("UnityEngine", StringComparison.Ordinal) && !x.cls.StartsWith("TMPro", StringComparison.Ordinal))
                .GroupBy(x => x.cls)
                .OrderByDescending(g => g.Count());
            foreach (var g in groups.Take(200))
                sb.AppendLine($"  {g.Key} x{g.Count()}: {string.Join(", ", g.Select(x => x.name).Take(12))}");
        });

        var text = sb.Length > MaxChars ? sb.ToString(0, MaxChars) + "\n...(truncated)" : sb.ToString();
        File.WriteAllText(Path, text);
        ModLog.Info($"Discovery report written to {Path} ({text.Length / 1024} KB).");
    }

    // ------------------------------------------------------------------------------------------

    private static string Flag(List<string> names, string asm, string label)
        => names.Any(n => n.Equals(asm, StringComparison.OrdinalIgnoreCase) || n.Equals("Il2Cpp" + asm, StringComparison.OrdinalIgnoreCase))
            ? $"[{label}: yes]"
            : $"[{label}: no]";

    private static void SelfTest(StringBuilder sb, PlayerInfo? p)
    {
        void T(string what, Func<string> f)
        {
            try
            {
                sb.AppendLine($"  OK   {what}: {f()}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  FAIL {what}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        T("Shader.Find(Sprites/Default)", () => Shader.Find("Sprites/Default")?.name ?? "null");
        T("Shader.Find(UI/Default)", () => Shader.Find("UI/Default")?.name ?? "null");
        T("Shader.Find(GUI/Text Shader)", () => Shader.Find("GUI/Text Shader")?.name ?? "null");
        T("Texture2D + Sprite.Create", () => FxAssets.SoftCircle.name);
        T("Physics2D.GetLayerCollisionMask", () => Physics2D.GetLayerCollisionMask(0).ToString());
        T("Physics2D.CircleCastAll", () => Physics2D.CircleCastAll(new Vector2(99999f, 99999f), 0.1f, Vector2.right, 0.01f, ~0).Length.ToString());
        T("Physics.GetIgnoreLayerCollision", () => Physics.GetIgnoreLayerCollision(0, 0).ToString());
        T("Physics.SphereCastAll", () => Physics.SphereCastAll(new Vector3(99999f, 99999f, 99999f), 0.1f, Vector3.right, 0.01f, ~0, QueryTriggerInteraction.Ignore).Length.ToString());
        T("Camera.main", () => Camera.main?.name ?? "null");
        T("Time.timeScale", () => Time.timeScale.ToString());
        if (p != null && !p.IsDestroyed)
        {
            T("wizard SpriteRenderers", () => p.Root.GetComponentsInChildren<SpriteRenderer>(false).Length.ToString());
            T("wizard SkinnedMeshRenderers", () => p.Root.GetComponentsInChildren<SkinnedMeshRenderer>(false).Length.ToString());
            T("wizard MeshRenderers", () => p.Root.GetComponentsInChildren<MeshRenderer>(false).Length.ToString());
        }
    }

    private static void DescribePlayer(StringBuilder sb, PlayerInfo p)
    {
        sb.AppendLine($"Identified by: {p.ComponentName}   ({p.LocalReason})");
        sb.AppendLine($"Anchor: {PathOf(p.Anchor)}");
        sb.AppendLine($"Movement root: {PathOf(p.Root)}  layer {p.Root.layer} ({LayerMask.LayerToName(p.Root.layer)})");
        sb.AppendLine($"Mode: {p.Mode}  radius {p.Radius:0.000}  visual size {p.VisualSize:0.00}  main renderer: {(p.MainRenderer != null ? Il2CppNames.FullName(p.MainRenderer) + " on " + p.MainRenderer.gameObject.name : "none")}");
        sb.AppendLine($"Position {p.RootT.position}  rotation {p.RootT.eulerAngles}  lossyScale {p.RootT.lossyScale}");
        sb.AppendLine($"Max observed walking speed: {p.MaxWalkSpeed:0.00} units/s  (dash speed now: {ModConfig.Distance.Value / Math.Max(0.01f, ModConfig.Duration.Value):0.0})");
        sb.AppendLine("Hierarchy (components per object):");
        DumpHierarchy(sb, p.Root.transform, 1, new[] { 0 });
        if (p.Root.transform.parent != null)
        {
            sb.AppendLine("Parents:");
            for (var t = p.Root.transform.parent; t != null; t = t.parent)
                sb.AppendLine($"  {t.gameObject.name}: {string.Join(", ", ComponentView.Of(t.gameObject).Select(v => v.ShortName))}");
        }
    }

    private static void DumpHierarchy(StringBuilder sb, Transform t, int depth, int[] budget)
    {
        if (budget[0]++ > 250 || depth > 8) return;
        var comps = ComponentView.Of(t.gameObject).Select(v => v.ShortName);
        sb.AppendLine($"{new string(' ', depth * 2)}{t.gameObject.name} [{(t.gameObject.activeSelf ? "on" : "off")}]: {string.Join(", ", comps)}");
        for (var i = 0; i < t.childCount; i++) DumpHierarchy(sb, t.GetChild(i), depth + 1, budget);
    }

    private static void DescribeMembers(StringBuilder sb, PlayerInfo p)
    {
        var types = new HashSet<Type>();
        foreach (var c in p.Root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (c == null) continue;
            var t = Il2CppNames.FindInteropType(Il2CppNames.FullName(c));
            if (t != null && Il2CppNames.IsGameType(t)) types.Add(t);
            if (types.Count > 60) break;
        }

        foreach (var v in p.Views)
            if (v.InteropType != null && Il2CppNames.IsGameType(v.InteropType))
                types.Add(v.InteropType);

        foreach (var type in types.OrderBy(t => t.FullName))
        foreach (var t in Reflect.SelfAndGameBases(type))
            DescribeType(sb, t);
    }

    private static void DescribeType(StringBuilder sb, Type t)
    {
        sb.AppendLine();
        sb.AppendLine($"class {t.FullName} : {t.BaseType?.FullName}   [{t.Assembly.GetName().Name}]");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        foreach (var prop in t.GetProperties(flags).OrderBy(x => x.Name))
        {
            if (prop.Name.StartsWith("Native", StringComparison.Ordinal)) continue;
            sb.AppendLine($"    {MethodTracer.Pretty(prop.PropertyType)} {prop.Name}");
        }

        foreach (var m in SafeMethods(t).OrderBy(x => x.Name))
        {
            if (m.IsSpecialName || m.Name.StartsWith("Native", StringComparison.Ordinal) || m.Name.Contains('<')) continue;
            sb.AppendLine($"    {(m.IsStatic ? "static " : "")}{MethodTracer.Pretty(m.ReturnType)} {m.Name}({string.Join(", ", m.GetParameters().Select(x => $"{MethodTracer.Pretty(x.ParameterType)} {x.Name}"))})");
        }
    }

    private static void DescribeUpgradeTypes(StringBuilder sb)
    {
        var types = Il2CppNames.GameTypes().Where(t => !t.Name.Contains('<')).OrderBy(t => t.FullName).ToList();

        sb.AppendLine("Enums that look like rarities:");
        foreach (var t in types.Where(t => t.IsEnum))
        {
            string[] values;
            try
            {
                values = Enum.GetNames(t);
            }
            catch
            {
                continue;
            }

            if (values.Any(v => RarityValueRx.IsMatch(v)) || t.Name.Contains("Rarity", StringComparison.OrdinalIgnoreCase) || t.Name.Contains("Tier", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine($"  enum {t.FullName} = {{ {string.Join(", ", values)} }}");
        }

        sb.AppendLine();
        sb.AppendLine("Classes whose name suggests upgrades / level-ups (full member lists):");
        var detailed = 0;
        foreach (var t in types.Where(t => !t.IsEnum && UpgradeTypeRx.IsMatch(t.Name)))
        {
            if (detailed++ < 60) DescribeType(sb, t);
            else sb.AppendLine($"  class {t.FullName} : {t.BaseType?.Name}");
            if (detailed > 400) break;
        }
    }

    private static IEnumerable<MethodInfo> SafeMethods(Type t)
    {
        try
        {
            return t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        }
        catch
        {
            return Array.Empty<MethodInfo>();
        }
    }

    public static string PathOf(GameObject go)
    {
        var parts = new List<string>();
        for (var t = go.transform; t != null; t = t.parent) parts.Add(t.gameObject.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string InputAssetsJson()
    {
        try
        {
            return InputAssetsJsonImpl();
        }
        catch (Exception ex)
        {
            return $"(Input System not available: {ex.Message})";
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string InputAssetsJsonImpl()
    {
        var sb = new StringBuilder();
        foreach (var asset in Resources.FindObjectsOfTypeAll<UnityEngine.InputSystem.InputActionAsset>())
        {
            if (asset == null) continue;
            sb.AppendLine($"--- {asset.name}");
            var json = asset.ToJson();
            sb.AppendLine(json.Length > 60000 ? json[..60000] + "...(truncated)" : json);
        }

        return sb.Length == 0 ? "(no InputActionAssets loaded)" : sb.ToString();
    }
}
