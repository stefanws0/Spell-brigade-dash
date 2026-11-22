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

    // TakeDamage, ReceiveDamage, OnHit, RpcTakeDamage... (deliberately not DealDamage/DoDamage: the attacker's side)
    private static readonly Regex DamageLikeName = new(
        @"^(?:UserCode_|RpcLogic___)?(?:Cmd|Rpc|Server|Client|Local|Net|Network|Target)?_?(?:Take|Receive|Apply|Handle|Process|On|Get)?(?:Damage|Damaged|Hit|Hurt|Harm)(?:Taken|Received|Rpc|Server|Client|Local)?(?:_\d+)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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
            var net = NetLibraries.Detect();
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
                if (!DamageLikeName.IsMatch(m.Name) || m.IsSpecialName) continue;
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
            if (MethodTracer.Skipped.Count > 0)
            {
                sb.AppendLine($"Skipped because they crashed the game in an earlier session ({MethodTracer.SkipFilePath}):");
                foreach (var t in MethodTracer.Skipped) sb.AppendLine("  " + t);
            }

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

        Section("15. Game UI bars (to match the dash bar's style)");
        Try("ui bars", () => DescribeUiBars(sb));

        Section("16. Health on your wizard (for the dash's HP cost)");
        Try("health", () =>
        {
            sb.AppendLine("In use: " + HealthBridge.Status);
            var h = local?.Health;
            if (h != null)
                sb.AppendLine($"  CurrentHealth {h.CurrentHealth:0.#}, MaxHealth {h.MaxHealth:0.#}, SyncedCurrentHealth {h.SyncedCurrentHealth.Value:0.#}, " +
                              $"SyncedMaxHealth {h.SyncedMaxHealth.Value:0.#}, invulnerable {h.IsPlayerInvulnerable}");
        });

        Section("17. FMOD sound events (pick one for Sound.FmodEvent)");
        Try("fmod", () =>
        {
            sb.AppendLine("Dash sound: " + sys.Sound.Status);
            var events = FmodSound.ListEvents();
            sb.AppendLine($"{events.Count} event(s) in the loaded banks:");
            foreach (var e in events.Take(3000)) sb.AppendLine("  " + e);
        });

        Section("18. Level-up shop (for wiring the dash cards)");
        Try("store", () => DescribeStore(sb));

        Section("19. The game's damage numbers (to balance Blazing Dash's fire)");
        Try("damage reference", () => DamageReference.Describe(sb));

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
        T("Physics.SphereCast", () => Physics.SphereCast(new Vector3(99999f, 99999f, 99999f), 0.1f, Vector3.right, out _, 0.01f, ~0, QueryTriggerInteraction.Ignore).ToString());
        T("Physics.Raycast", () => Physics.Raycast(new Vector3(99999f, 99999f, 99999f), Vector3.right, out _, 0.01f, ~0, QueryTriggerInteraction.Ignore).ToString());
        T("LayerMask.NameToLayer(Obstacles)", () => LayerMask.NameToLayer("Obstacles").ToString());
        if (p != null && !p.IsDestroyed)
        {
            T("NavMesh.SamplePosition at wizard", () => UnityEngine.AI.NavMesh.SamplePosition(p.RootT.position, out var hit, 2f, -1) ? $"on NavMesh at {hit.position}" : "not on NavMesh");
            T("wall distance ahead (obstacles)", () => DashMotor.ObstacleDistance(p, p.RootT.position, p.RootT.forward, 10f, p.CachedMask ??= DashMotor.BlockingMask(p)).ToString("0.00"));
            T("map-edge distance ahead (NavMesh)", () => DashMotor.NavMeshDistance(p.RootT.position, p.RootT.forward, 50f).ToString("0.00"));
            T("wall checks in use", DashMotor.Describe);
        }

        T("FMOD RuntimeManager", () => FmodSound.Available ? "found" : "not found");
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

        foreach (var v in ComponentView.Of(p.Root))
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

    private static readonly Regex StoreTypeRx = new("(Store|Improvement|PartyLevel|LevelUp|Rarity|StatType|Reroll|UpgradeManager|UpgradeBuyer|Offer|Shop)",
        RegexOptions.CultureInvariant);

    /// <summary>Everything about the game's level-up shop: StoreCard/StoreItem/Improvement classes and their enums.</summary>
    private static void DescribeStore(StringBuilder sb)
    {
        var types = Il2CppNames.GameTypes().Where(t => !t.Name.Contains('<') && StoreTypeRx.IsMatch(t.Name)).OrderBy(t => t.FullName).ToList();
        sb.AppendLine("Enums:");
        foreach (var t in types.Where(t => t.IsEnum))
        {
            try
            {
                sb.AppendLine($"  enum {t.FullName} = {{ {string.Join(", ", Enum.GetNames(t))} }}");
            }
            catch
            {
                // ignore
            }
        }

        var statType = Il2CppNames.FindInteropType("StatType");
        if (statType != null && statType.IsEnum) sb.AppendLine($"  enum StatType = {{ {string.Join(", ", Enum.GetNames(statType))} }}");

        sb.AppendLine();
        sb.AppendLine("Classes:");
        var count = 0;
        foreach (var t in types.Where(t => !t.IsEnum))
        {
            DescribeType(sb, t);
            if (++count >= 150) break;
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

    private static readonly Regex UiBarRx = new("(bar|hp|health|xp|exp|fill|mana|shield|progress|slider|level|stamina|energy)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static void DescribeUiBars(StringBuilder sb)
    {
        var count = 0;
        foreach (var c in UnityFind.All(typeof(UnityEngine.UI.Image)))
        {
            var img = c.TryCast<UnityEngine.UI.Image>();
            if (img == null) continue;
            var sprite = img.sprite;
            var spriteName = sprite != null ? sprite.name : "(none)";
            if (!UiBarRx.IsMatch(img.gameObject.name) && !UiBarRx.IsMatch(spriteName)) continue;

            var rt = img.GetComponent<RectTransform>();
            var size = rt != null ? $"{rt.rect.width:0}x{rt.rect.height:0}" : "?";
            var border = sprite != null ? $"border {sprite.border.x:0}/{sprite.border.y:0}/{sprite.border.z:0}/{sprite.border.w:0}" : "";
            var col = img.color;
            sb.AppendLine($"  {PathOf(img.gameObject)}");
            sb.AppendLine($"      sprite '{spriteName}' {border}  type {img.type}  size {size}  colour #{ToHex(col)}");
            if (++count >= 80) break;
        }

        if (count == 0) sb.AppendLine("  (no bar-like UI images found right now; press the report hotkey during a run)");
        sb.AppendLine("Tip: put one of the sprite names above into Bar.GameSpriteName in the config to draw the dash bar with it.");
    }

    private static string ToHex(Color c)
        => $"{(int)(c.r * 255):X2}{(int)(c.g * 255):X2}{(int)(c.b * 255):X2}{(int)(c.a * 255):X2}";

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
