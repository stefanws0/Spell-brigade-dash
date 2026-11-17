namespace SpellBrigadeDash.Game;

using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using UnityEngine;

internal enum MoveMode
{
    CharacterController,
    Rigidbody2D,
    Rigidbody3D,
    Transform2D,
    Transform3D,
}

/// <summary>Everything the mod knows about one wizard (yours or another player's).</summary>
internal sealed class PlayerInfo
{
    public GameObject Anchor = null!;
    public GameObject Root = null!;
    public IntPtr RootPtr;
    public string ComponentName = "";
    public List<ComponentView> Views = new();
    public MoveMode Mode;
    public Rigidbody2D? Rb2D;
    public Rigidbody? Rb;
    public CharacterController? Cc;
    public float Radius = 0.3f;
    public Renderer? MainRenderer;
    public readonly HashSet<IntPtr> GameObjectPtrs = new();

    /// <summary>Physics layers that stop a dash (computed once per wizard).</summary>
    public int? CachedMask;

    public bool IsLocal;
    public string LocalReason = "";

    // movement tracking (direction fallback, calibration)
    public bool HasLastPos;
    public Vector3 LastPos;
    public Vector3 LastMoveDir;
    public float MaxWalkSpeed;

    public long Id => RootPtr.ToInt64();
    public bool Is2D => Mode is MoveMode.Rigidbody2D or MoveMode.Transform2D;
    public Transform RootT => Root.transform;

    public bool IsDestroyed
    {
        get
        {
            try
            {
                return Root == null || Anchor == null;
            }
            catch
            {
                return true;
            }
        }
    }

    /// <summary>World position of the wizard's feet (for the bar and dust).</summary>
    public Vector3 Feet
    {
        get
        {
            if (Is2D && MainRenderer != null)
            {
                var b = MainRenderer.bounds;
                return new Vector3(b.center.x, b.min.y, RootT.position.z);
            }

            return RootT.position;
        }
    }

    /// <summary>Rough visual height of the wizard in world units (scales the effects).</summary>
    public float VisualSize
    {
        get
        {
            if (MainRenderer == null) return 1f;
            var s = MainRenderer.bounds.size;
            var v = Math.Max(s.y, Math.Max(s.x, s.z) * 0.6f);
            return Mathf.Clamp(v, 0.2f, 10f);
        }
    }
}

/// <summary>
/// Finds the wizards in the scene and decides which one is yours. Fully heuristic until the
/// discovery report tells us the game's real player class (then set GameHooks.PlayerComponent).
/// </summary>
internal sealed class PlayerLocator
{
    private static readonly string[] ExactNames =
    {
        "PlayerController", "Player", "PlayerCharacter", "PlayerMovement", "PlayerMotor", "Wizard", "WizardController",
        "PlayerWizard", "LocalPlayer", "Hero", "HeroController", "PlayerBehaviour", "PlayerBehavior", "PlayerEntity", "PlayerUnit",
    };

    private static readonly string[] NegativeTokens =
    {
        "Camera", "UI", "Hud", "HUD", "Menu", "Manager", "Spawner", "Spawn", "Input", "Settings", "Data", "Lobby", "Audio", "Sound",
        "Select", "Preview", "Portrait", "Icon", "Button", "Card", "Panel", "Screen", "Indicator", "Arrow", "Marker", "Spell",
        "Projectile", "Enemy", "Pickup", "Loot", "Name", "Label", "Text", "Bar", "Ping", "Emote", "Chat", "Cursor", "Aim", "Visual",
        "Animation", "Animator", "Shadow", "Effect", "Fx", "FX", "Sfx", "Outline", "Collector", "Magnet", "Range", "Trigger",
    };

    private readonly Dictionary<IntPtr, PlayerInfo> _known = new();
    private float _nextSearch;
    private Type? _knownType;

    public PlayerInfo? Local { get; private set; }
    public List<PlayerInfo> Remotes { get; } = new();

    /// <summary>Raised when your wizard is found for the first time or replaced by a new object (new run).</summary>
    public event Action<PlayerInfo>? LocalChanged;

    public List<string> LastCandidates { get; } = new();
    public string LastSearchSummary { get; private set; } = "not searched yet";

    public void Update(float now)
    {
        if (Local != null && Local.IsDestroyed) Local = null;
        Remotes.RemoveAll(r => r.IsDestroyed);

        if (now < _nextSearch) return;
        _nextSearch = now + (Local == null ? 1f : 3f);

        try
        {
            Search();
        }
        catch (Exception ex)
        {
            ModLog.ErrorThrottled("PlayerLocator.Search", ex, now, 30f);
        }
    }

    public void ForceSearch() => _nextSearch = 0f;

    private string[]? _deadMembers;

    public bool IsDead(PlayerInfo p)
    {
        _deadMembers ??= ModConfig.SplitList(ModConfig.DeadMembers.Value);
        return Reflect.TryReadAnyBool(p.Views, _deadMembers, out var dead, out _) && dead;
    }

    // ------------------------------------------------------------------------------------------

    private void Search()
    {
        var anchors = FindPlayerAnchors(out var className);
        if (anchors.Count == 0)
        {
            LastSearchSummary = "no player-like component found";
            return;
        }

        var players = new List<PlayerInfo>();
        foreach (var go in anchors)
        {
            var ptr = go.Pointer;
            if (!_known.TryGetValue(ptr, out var info) || info.IsDestroyed)
            {
                info = Build(go, className);
                _known[ptr] = info;
            }

            players.Add(info);
        }

        // Decide which one is ours. A "this is the local player" flag always wins; otherwise keep the
        // wizard we already picked (so heuristics can't flip mid-run and reset the run's upgrades).
        var localMembers = ModConfig.SplitList(ModConfig.LocalPlayerMembers.Value);
        PlayerInfo? local = null;
        foreach (var p in players)
        {
            if (Reflect.TryReadAnyBool(p.Views, localMembers, out var isLocal, out var where))
            {
                p.LocalReason = $"{where} = {isLocal}";
                if (isLocal && local == null) local = p;
            }
        }

        if (local == null && Local != null && !Local.IsDestroyed && players.Contains(Local)) local = Local;

        if (local == null && players.Count == 1)
        {
            local = players[0];
            local.LocalReason = "only player in scene";
        }

        if (local == null) local = ClosestToScreenCenter(players);

        foreach (var p in players) p.IsLocal = p == local;

        Remotes.Clear();
        Remotes.AddRange(players.Where(p => p != local));

        if (local != null && !ReferenceEquals(local, Local))
        {
            Local = local;
            _knownType ??= Il2CppNames.FindInteropType(className);
            ModLog.Info($"Local wizard: '{local.Root.name}' via {className} ({local.LocalReason}), mode {local.Mode}, radius {local.Radius:0.00}, {Remotes.Count} other player(s).");
            LocalChanged?.Invoke(local);
        }

        LastSearchSummary = $"class {className}: {players.Count} player(s), local = {(Local != null ? Local.Root.name : "none")}";

        // forget destroyed entries
        foreach (var dead in _known.Where(kv => kv.Value.IsDestroyed).Select(kv => kv.Key).ToList()) _known.Remove(dead);
    }

    private List<GameObject> FindPlayerAnchors(out string className)
    {
        // 1) explicit config
        var configured = ModConfig.PlayerComponent.Value.Trim();
        if (!string.IsNullOrEmpty(configured) && !configured.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            className = configured;
            var t = Il2CppNames.FindInteropType(configured);
            if (t == null)
            {
                ModLog.WarnOnce("cfg-player-type", $"GameHooks.PlayerComponent '{configured}' not found among interop types; falling back to auto.");
            }
            else
            {
                return UnityFind.All(t).Select(c => c.gameObject).ToList();
            }
        }

        // 2) class learned from an earlier successful auto search (cheap)
        if (_knownType != null)
        {
            className = _knownType.Name;
            var found = UnityFind.All(_knownType).Select(c => c.gameObject).ToList();
            if (found.Count > 0) return found;
        }

        // 3) scan every MonoBehaviour and score its class name
        ModLog.StepOnce("scan", "player: first scan of all MonoBehaviours");
        LastCandidates.Clear();
        var bestPerClass = new Dictionary<string, (int score, List<GameObject> gos)>();
        foreach (var c in UnityFind.All(typeof(MonoBehaviour)))
        {
            List<string> chain;
            try
            {
                chain = Il2CppNames.Chain(c);
            }
            catch
            {
                continue;
            }

            var cls = chain.Count > 0 ? chain[0] : "?";
            var score = Score(chain);
            if (score < 6) continue;

            var go = c.gameObject;
            score += PhysicsBonus(go);
            if (!bestPerClass.TryGetValue(cls, out var entry)) entry = (score, new List<GameObject>());
            entry.score = Math.Max(entry.score, score);
            entry.gos.Add(go);
            bestPerClass[cls] = entry;
        }

        foreach (var kv in bestPerClass.OrderByDescending(kv => kv.Value.score))
            LastCandidates.Add($"{kv.Key} (score {kv.Value.score}, {kv.Value.gos.Count} object(s))");

        if (bestPerClass.Count == 0)
        {
            className = "";
            return new List<GameObject>();
        }

        var best = bestPerClass.OrderByDescending(kv => kv.Value.score).First();
        className = best.Key;
        return best.Value.gos.GroupBy(g => g.Pointer).Select(g => g.First()).ToList();
    }

    private static int Score(List<string> chain)
    {
        var score = 0;
        foreach (var full in chain)
        {
            if (full.StartsWith("UnityEngine.", StringComparison.Ordinal)) break;
            var name = full.Contains('.') ? full[(full.LastIndexOf('.') + 1)..] : full;
            var s = 0;
            if (ExactNames.Contains(name)) s += 10;
            if (name.Contains("Player")) s += 6;
            if (name.Contains("Wizard")) s += 5;
            if (name.Contains("Hero")) s += 3;
            if (name.Contains("Character")) s += 2;
            if (name.Contains("Movement") || name.Contains("Motor") || name.Contains("Controller")) s += 2;
            foreach (var neg in NegativeTokens)
                if (name.Contains(neg, StringComparison.Ordinal))
                    s -= 8;
            score = Math.Max(score, s);
        }

        return score;
    }

    private static int PhysicsBonus(GameObject go)
    {
        var bonus = 0;
        var t = go.transform;
        for (var i = 0; i < 3 && t != null; i++, t = t.parent)
        {
            var g = t.gameObject;
            if (UnityFind.Has<Rigidbody2D>(g) || UnityFind.Has<Rigidbody>(g) || UnityFind.Has<CharacterController>(g))
            {
                bonus += 4;
                break;
            }
        }

        return bonus;
    }

    private static PlayerInfo Build(GameObject anchor, string className)
    {
        ModLog.Step($"player: reading '{anchor.name}' ({className})");
        var info = new PlayerInfo { Anchor = anchor, ComponentName = className };

        // Movement root: nearest object (self or parents) with a physics body; else self.
        GameObject? root = null;
        var t = anchor.transform;
        for (var i = 0; i < 4 && t != null; i++, t = t.parent)
        {
            var g = t.gameObject;
            info.Cc = UnityFind.Get<CharacterController>(g);
            info.Rb2D = info.Cc == null ? UnityFind.Get<Rigidbody2D>(g) : null;
            info.Rb = info.Cc == null && info.Rb2D == null ? UnityFind.Get<Rigidbody>(g) : null;
            if (info.Cc != null || info.Rb2D != null || info.Rb != null)
            {
                root = g;
                break;
            }
        }

        root ??= anchor;
        info.Root = root;
        info.RootPtr = root.Pointer;

        if (info.Cc != null) info.Mode = MoveMode.CharacterController;
        else if (info.Rb2D != null) info.Mode = MoveMode.Rigidbody2D;
        else if (info.Rb != null) info.Mode = MoveMode.Rigidbody3D;
        else info.Mode = UnityFind.InChildren<Collider2D>(root) != null || UnityFind.InChildren<SpriteRenderer>(root) != null
            ? MoveMode.Transform2D
            : MoveMode.Transform3D;

        // Components to read members from: anchor, root and everything in between.
        var seen = new HashSet<IntPtr>();
        for (var tr = anchor.transform; tr != null; tr = tr.parent)
        {
            if (seen.Add(tr.gameObject.Pointer)) info.Views.AddRange(ComponentView.Of(tr.gameObject));
            if (tr.gameObject.Pointer == root.Pointer) break;
        }

        if (seen.Add(root.Pointer)) info.Views.AddRange(ComponentView.Of(root));

        // Every GameObject that "is" this wizard (for i-frame checks and cast filtering).
        foreach (var child in root.GetComponentsInChildren<Transform>(true)) info.GameObjectPtrs.Add(child.gameObject.Pointer);
        foreach (var p in seen) info.GameObjectPtrs.Add(p);

        info.Radius = EstimateRadius(info);
        info.MainRenderer = FindMainRenderer(root);
        return info;
    }

    private static float EstimateRadius(PlayerInfo info)
    {
        try
        {
            if (info.Cc != null)
            {
                var s = info.Root.transform.lossyScale;
                return Mathf.Clamp(info.Cc.radius * Math.Max(Math.Abs(s.x), Math.Abs(s.z)), 0.05f, 3f);
            }

            if (info.Is2D)
            {
                var c2 = UnityFind.InChildren<Collider2D>(info.Root);
                if (c2 != null)
                {
                    var e = c2.bounds.extents;
                    return Mathf.Clamp(Math.Min(e.x, e.y), 0.05f, 3f);
                }
            }
            else
            {
                var c3 = UnityFind.InChildren<Collider>(info.Root);
                if (c3 != null)
                {
                    var e = c3.bounds.extents;
                    return Mathf.Clamp(Math.Min(e.x, e.z), 0.05f, 3f);
                }
            }
        }
        catch
        {
            // fall through
        }

        return 0.3f;
    }

    private static Renderer? FindMainRenderer(GameObject root)
    {
        Renderer? best = null;
        var bestSize = 0f;
        foreach (var r in root.GetComponentsInChildren<Renderer>(false))
        {
            if (r == null || !r.enabled) continue;
            var isBody = r.TryCast<SpriteRenderer>() != null || r.TryCast<SkinnedMeshRenderer>() != null || r.TryCast<MeshRenderer>() != null;
            if (!isBody) continue;
            var s = r.bounds.size;
            var size = s.x * s.y + s.y * s.z + s.x * s.z;
            if (size > bestSize)
            {
                bestSize = size;
                best = r;
            }
        }

        return best;
    }

    private static PlayerInfo? ClosestToScreenCenter(List<PlayerInfo> players)
    {
        var cam = Camera.main;
        if (cam == null || players.Count == 0) return null;
        PlayerInfo? best = null;
        var bestDist = float.MaxValue;
        foreach (var p in players)
        {
            var vp = cam.WorldToViewportPoint(p.RootT.position);
            if (vp.z < 0) continue;
            var d = (vp.x - 0.5f) * (vp.x - 0.5f) + (vp.y - 0.5f) * (vp.y - 0.5f);
            if (d < bestDist)
            {
                bestDist = d;
                best = p;
            }
        }

        if (best != null) best.LocalReason = "closest to screen centre (no local-player flag found)";
        return best;
    }
}

/// <summary>Small wrappers around Unity lookups that tolerate stripped/missing engine modules.</summary>
internal static class UnityFind
{
    /// <summary>All active objects of an interop type (game class or Unity class).</summary>
    public static List<Component> All(Type interopType)
    {
        var list = new List<Component>();
        try
        {
            var arr = UnityEngine.Object.FindObjectsOfType(Il2CppType.From(interopType));
            foreach (var o in arr)
            {
                var c = o?.TryCast<Component>();
                if (c != null) list.Add(c);
            }
        }
        catch (Exception ex)
        {
            ModLog.WarnOnce("find-" + interopType.Name, $"FindObjectsOfType({interopType.Name}) failed: {ex.Message}");
        }

        return list;
    }

    public static T? Get<T>(GameObject go) where T : Component
    {
        try
        {
            var c = go.GetComponent<T>();
            return c == null ? null : c;
        }
        catch
        {
            return null;
        }
    }

    public static bool Has<T>(GameObject go) where T : Component => Get<T>(go) != null;

    public static T? InChildren<T>(GameObject go) where T : Component
    {
        try
        {
            var c = go.GetComponentInChildren<T>(true);
            return c == null ? null : c;
        }
        catch
        {
            return null;
        }
    }
}
