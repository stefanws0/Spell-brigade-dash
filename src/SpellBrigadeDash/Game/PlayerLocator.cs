namespace SpellBrigadeDash.Game;

using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using SpellBrigadeDash.Core;
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
    public GameplayPlayer Player = null!;
    public PlayerHealthContainer? Health;
    public PlayerMotor? Motor;
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
/// Finds the wizards in the scene (the game's <c>GameplayPlayer</c> components) and decides which one is yours
/// (<c>NetworkBehaviour.IsOwner</c>; with only one wizard in the scene that one).
/// </summary>
internal sealed class PlayerLocator
{
    private const string PlayerClass = nameof(GameplayPlayer);

    private readonly Dictionary<IntPtr, PlayerInfo> _known = new();
    private float _nextSearch;

    public PlayerInfo? Local { get; private set; }
    public List<PlayerInfo> Remotes { get; } = new();

    /// <summary>Raised when your wizard is found for the first time or replaced by a new object (new run).</summary>
    public event Action<PlayerInfo>? LocalChanged;

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

    /// <summary>A downed wizard is a ghost (<c>PlayerMotor.isGhost</c>) until revived: no dashing then.</summary>
    public bool IsDead(PlayerInfo p)
    {
        try
        {
            var motor = p.Motor;
            return motor != null && motor.isGhost;
        }
        catch
        {
            return false;
        }
    }

    // ------------------------------------------------------------------------------------------

    private void Search()
    {
        var players = new List<PlayerInfo>();
        foreach (var c in UnityFind.All(typeof(GameplayPlayer)))
        {
            var gp = c.TryCast<GameplayPlayer>();
            if (gp == null) continue;
            var ptr = gp.gameObject.Pointer;
            if (!_known.TryGetValue(ptr, out var info) || info.IsDestroyed)
            {
                info = Build(gp);
                _known[ptr] = info;
            }

            players.Add(info);
        }

        if (players.Count == 0)
        {
            LastSearchSummary = "no GameplayPlayer in the scene";
            return;
        }

        // Decide which one is ours. The game's owner flag wins; otherwise keep the wizard we already picked (so a
        // guess can't flip mid-run and reset the run's upgrades).
        PlayerInfo? local = null;
        foreach (var p in players)
        {
            bool owner;
            try
            {
                owner = p.Player.IsOwner;
            }
            catch
            {
                continue;
            }

            p.LocalReason = $"GameplayPlayer.IsOwner = {owner}";
            if (owner && local == null) local = p;
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
            ModLog.Info($"Local wizard: '{local.Root.name}' ({local.LocalReason}), mode {local.Mode}, radius {local.Radius:0.00}, " +
                        $"body '{(local.MainRenderer != null ? local.MainRenderer.gameObject.name : "none")}', {Remotes.Count} other player(s).");
            LocalChanged?.Invoke(local);
        }

        LastSearchSummary = $"{players.Count} {PlayerClass}(s), local = {(Local != null ? Local.Root.name : "none")}";

        // forget destroyed entries
        foreach (var dead in _known.Where(kv => kv.Value.IsDestroyed).Select(kv => kv.Key).ToList()) _known.Remove(dead);
    }

    private static PlayerInfo Build(GameplayPlayer gp)
    {
        var anchor = gp.gameObject;
        ModLog.Step($"player: reading '{anchor.name}' ({PlayerClass})");
        var info = new PlayerInfo { Anchor = anchor, ComponentName = PlayerClass, Player = gp };
        try
        {
            info.Health = gp.HealthContainer;
            info.Motor = gp.PlayerMotor;
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Reading the wizard's health/motor failed: {ex.Message}");
        }

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

        // The anchor, the root and every object in between.
        var seen = new HashSet<IntPtr>();
        for (var tr = anchor.transform; tr != null; tr = tr.parent)
        {
            seen.Add(tr.gameObject.Pointer);
            if (tr.gameObject.Pointer == root.Pointer) break;
        }

        seen.Add(root.Pointer);

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

    /// <summary>The wizard's body renderer; see <see cref="BodyPick"/> (never a spell, the skinned model first).</summary>
    private static Renderer? FindMainRenderer(GameObject root)
    {
        var renderers = new List<Renderer>();
        var candidates = new List<BodyPick.Candidate>();
        foreach (var r in root.GetComponentsInChildren<Renderer>(false))
        {
            if (r == null || !r.enabled) continue;
            BodyPick.Kind kind;
            if (r.TryCast<SkinnedMeshRenderer>() != null) kind = BodyPick.Kind.Skinned;
            else if (r.TryCast<SpriteRenderer>() != null) kind = BodyPick.Kind.Sprite;
            else if (r.TryCast<MeshRenderer>() != null) kind = BodyPick.Kind.Mesh;
            else continue;
            var s = r.bounds.size;
            renderers.Add(r);
            candidates.Add(new BodyPick.Candidate(kind, s.x * s.y + s.y * s.z + s.x * s.z, UnderSpells(r.transform, root.transform)));
        }

        var best = BodyPick.Best(candidates);
        return best < 0 ? null : renderers[best];
    }

    /// <summary>True if the renderer belongs to the wizard's spells (<c>PlayerSpells/...</c>), not to its body.</summary>
    private static bool UnderSpells(Transform t, Transform root)
    {
        for (var cur = t; cur != null && cur.Pointer != root.Pointer; cur = cur.parent)
            if (cur.gameObject.name.StartsWith("PlayerSpells", StringComparison.Ordinal)) return true;
        return false;
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
