namespace SpellBrigadeDash.Motion;

using System;
using SpellBrigadeDash.Game;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Moves a wizard along the dash path while respecting walls. It works with whatever the game uses
/// for movement: CharacterController, Rigidbody2D, Rigidbody, or a plain Transform.
///
/// 3D collision (The Spell Brigade):
///  * obstacles = the physics layers named in Dash.BlockingLayers ("Obstacles", "TargetableObstacles");
///    the game's collision matrix can't be read (Physics.GetIgnoreLayerCollision returns nonsense in this build),
///  * checked with Physics.SphereCast, falling back to three Physics.Raycasts if SphereCast is stripped
///    (SphereCastAll is: "Method unstripping failed"),
///  * the map edge = the end of the NavMesh, checked with NavMesh.Raycast (Dash.StayOnNavMesh),
///  * things already overlapped at the start are ignored, so a surrounded wizard can still escape.
/// </summary>
internal static class DashMotor
{
    private const float Skin = 0.05f;
    private const float CastHeight = 0.5f;
    private static bool _castBroken;
    private static int _castMode; // 0 = SphereCast, 1 = Raycasts, 2 = none
    private static bool _navBroken;

    /// <summary>Move up to <paramref name="distance"/> along <paramref name="dir"/>. Returns the distance actually travelled.</summary>
    public static float Move(PlayerInfo p, Vector3 dir, float distance)
    {
        if (distance <= 0f) return 0f;
        ModLog.StepOnce("move", $"motor: first dash move ({p.Mode})");
        var t = p.RootT;
        var before = t.position;

        switch (p.Mode)
        {
            case MoveMode.CharacterController when p.Cc != null && p.Cc.enabled:
                p.Cc.Move(dir * distance);
                break;

            case MoveMode.Rigidbody2D:
            case MoveMode.Transform2D:
            {
                var allowed = Allowed2D(p, before, dir, distance);
                var target = before + dir * allowed;
                if (p.Rb2D != null) p.Rb2D.position = new Vector2(target.x, target.y);
                t.position = target;
                break;
            }

            default:
            {
                var allowed = Allowed3D(p, before, dir, distance);
                var target = before + dir * allowed;
                if (p.Rb != null) p.Rb.position = target;
                t.position = target;
                break;
            }
        }

        var moved = t.position - before;
        return Math.Abs(Vector3.Dot(moved, dir));
    }

    public static int BlockingMask(PlayerInfo p)
    {
        var names = ModConfig.SplitList(ModConfig.BlockingLayers.Value);
        if (!p.Is2D && names.Length > 0)
        {
            var named = 0;
            foreach (var name in names)
            {
                try
                {
                    var layer = LayerMask.NameToLayer(name);
                    if (layer >= 0) named |= 1 << layer;
                }
                catch
                {
                    // fall through
                }
            }

            if (named != 0) return named;
            ModLog.WarnOnce("no-blocking-layers", $"None of Dash.BlockingLayers ({ModConfig.BlockingLayers.Value}) exist; falling back to the collision matrix.");
        }

        var own = p.Root.layer;
        var mask = 0;
        try
        {
            if (p.Is2D)
            {
                mask = Physics2D.GetLayerCollisionMask(own);
            }
            else
            {
                for (var i = 0; i < 32; i++)
                    if (!Physics.GetIgnoreLayerCollision(own, i))
                        mask |= 1 << i;
                mask &= ~(1 << own);
            }
        }
        catch
        {
            mask = ~(1 << own);
        }

        if (ModConfig.PassThroughEnemies.Value)
        {
            var hints = ModConfig.SplitList(ModConfig.EnemyLayerHints.Value.ToLowerInvariant());
            for (var i = 0; i < 32; i++)
            {
                var name = (LayerMask.LayerToName(i) ?? "").ToLowerInvariant();
                if (name.Length == 0) continue;
                foreach (var h in hints)
                    if (name.Contains(h))
                    {
                        mask &= ~(1 << i);
                        break;
                    }
            }
        }

        return mask;
    }

    private static float Allowed2D(PlayerInfo p, Vector3 from, Vector3 dir, float distance)
    {
        if (_castBroken) return distance;
        try
        {
            var origin = p.Rb2D != null ? p.Rb2D.position : new Vector2(from.x, from.y);
            var hits = Physics2D.CircleCastAll(origin, p.Radius * 0.9f, new Vector2(dir.x, dir.y), distance + Skin, p.CachedMask ??= BlockingMask(p));
            var allowed = distance;
            foreach (var hit in hits)
            {
                var col = hit.collider;
                if (col == null || col.isTrigger) continue;
                if (hit.distance <= 0.0001f) continue; // overlapping at start
                if (p.GameObjectPtrs.Contains(col.gameObject.Pointer)) continue;
                allowed = Math.Min(allowed, Math.Max(0f, hit.distance - Skin));
            }

            return allowed;
        }
        catch (Exception ex)
        {
            _castBroken = true;
            ModLog.Warn($"Physics2D.CircleCastAll unavailable ({ex.Message}); dashes will ignore walls.");
            return distance;
        }
    }

    private static float Allowed3D(PlayerInfo p, Vector3 from, Vector3 dir, float distance)
    {
        var allowed = distance;
        var mask = p.CachedMask ??= BlockingMask(p);
        if (mask != 0) allowed = Math.Min(allowed, ObstacleDistance(p, from, dir, distance, mask));
        if (ModConfig.StayOnNavMesh.Value) allowed = Math.Min(allowed, NavMeshDistance(from, dir, allowed));
        return Math.Max(0f, allowed);
    }

    /// <summary>Distance until the first obstacle collider, or <paramref name="distance"/> if the way is clear.</summary>
    public static float ObstacleDistance(PlayerInfo p, Vector3 from, Vector3 dir, float distance, int mask)
    {
        var origin = from + Vector3.up * CastHeight;
        var radius = Math.Max(0.1f, p.Radius * 0.9f);

        if (_castMode == 0)
        {
            try
            {
                return Physics.SphereCast(origin, radius, dir, out var hit, distance + Skin, mask, QueryTriggerInteraction.Ignore)
                    ? Math.Max(0f, hit.distance - Skin)
                    : distance;
            }
            catch (Exception ex)
            {
                _castMode = 1;
                ModLog.Warn($"Physics.SphereCast unavailable ({ex.Message}); using raycasts for walls.");
            }
        }

        if (_castMode == 1)
        {
            try
            {
                var side = Vector3.Cross(Vector3.up, dir).normalized * radius;
                var best = distance;
                foreach (var o in new[] { origin, origin + side, origin - side })
                    if (Physics.Raycast(o, dir, out var hit, distance + radius + Skin, mask, QueryTriggerInteraction.Ignore))
                        best = Math.Min(best, Math.Max(0f, hit.distance - radius - Skin));
                return best;
            }
            catch (Exception ex)
            {
                _castMode = 2;
                ModLog.Warn($"Physics.Raycast unavailable ({ex.Message}); dashes only stop at the edge of the map.");
            }
        }

        return distance;
    }

    /// <summary>Distance until the dash would leave the walkable area (the edge of the NavMesh).</summary>
    public static float NavMeshDistance(Vector3 from, Vector3 dir, float distance)
    {
        if (_navBroken || distance <= 0f) return distance;
        try
        {
            // Project the start onto the NavMesh first: the wizard's pivot isn't exactly on it.
            if (!NavMesh.SamplePosition(from, out var start, 2f, -1)) return distance;
            var target = start.position + dir * distance;
            if (!NavMesh.Raycast(start.position, target, out var hit, -1)) return distance;
            var flat = hit.position - start.position;
            flat.y = 0f;
            return Math.Max(0f, flat.magnitude - Skin);
        }
        catch (Exception ex)
        {
            _navBroken = true;
            ModLog.Warn($"NavMesh queries unavailable ({ex.Message}); dashes don't check the map edge.");
            return distance;
        }
    }

    /// <summary>Which wall checks work in this build (for the discovery report).</summary>
    public static string Describe() =>
        $"obstacles: {(_castMode == 0 ? "SphereCast" : _castMode == 1 ? "Raycast x3" : "none")}, map edge: {(_navBroken ? "none" : "NavMesh.Raycast")}";
}
