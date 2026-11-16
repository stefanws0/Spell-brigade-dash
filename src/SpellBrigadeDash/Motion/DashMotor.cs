using System;
using SpellBrigadeDash.Game;
using UnityEngine;

namespace SpellBrigadeDash.Motion;

/// <summary>
/// Moves a wizard along the dash path while respecting walls. It works with whatever the game uses
/// for movement: CharacterController, Rigidbody2D, Rigidbody, or a plain Transform.
///
/// Collision rule: the dash collides with every layer the wizard's own layer collides with
/// (taken from the game's physics collision matrix), so it behaves exactly like walking, except:
///  * colliders that belong to the wizard are ignored,
///  * triggers are ignored,
///  * things we already overlap at the start are ignored (so a surrounded wizard can still escape),
///  * "enemy" layers are ignored when Dash.PassThroughEnemies is on.
/// </summary>
internal static class DashMotor
{
    private const float Skin = 0.02f;
    private static bool _castBroken;

    /// <summary>Move up to <paramref name="distance"/> along <paramref name="dir"/>. Returns the distance actually travelled.</summary>
    public static float Move(PlayerInfo p, Vector3 dir, float distance)
    {
        if (distance <= 0f) return 0f;
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
        var layer = p.Root.layer;
        var mask = 0;
        try
        {
            if (p.Is2D)
            {
                mask = Physics2D.GetLayerCollisionMask(layer);
            }
            else
            {
                for (var i = 0; i < 32; i++)
                    if (!Physics.GetIgnoreLayerCollision(layer, i))
                        mask |= 1 << i;
            }
        }
        catch
        {
            mask = ~0;
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
        if (_castBroken) return distance;
        try
        {
            var origin = from + Vector3.up * Math.Max(p.Radius, 0.1f);
            var hits = Physics.SphereCastAll(origin, p.Radius * 0.9f, dir, distance + Skin, p.CachedMask ??= BlockingMask(p), QueryTriggerInteraction.Ignore);
            var allowed = distance;
            foreach (var hit in hits)
            {
                var col = hit.collider;
                if (col == null) continue;
                if (hit.distance <= 0.0001f) continue;
                if (p.GameObjectPtrs.Contains(col.gameObject.Pointer)) continue;
                allowed = Math.Min(allowed, Math.Max(0f, hit.distance - Skin));
            }

            return allowed;
        }
        catch (Exception ex)
        {
            _castBroken = true;
            ModLog.Warn($"Physics.SphereCastAll unavailable ({ex.Message}); dashes will ignore walls.");
            return distance;
        }
    }
}
