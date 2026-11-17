namespace SpellBrigadeDash.Game;

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Reuses The Spell Brigade's own effects on the wizard (found in the discovery report), so the dash looks native:
///  * PlayerTeleportEffect.PlayTeleportedEffect(): the game's teleport sparkle burst,
///  * MultiSourceParticleEffect.SetSource(source, on) on "FastParticleEffect": the game's fast-running speed lines
///    (PS_Run_Fast / PS_RunningLines / PS_Pulse). The game uses it for terrain speed boosts; we add the dash as a source.
/// Everything goes through reflection on the interop types and is skipped quietly if the game changes.
/// </summary>
internal static class GameFx
{
    private static readonly Dictionary<long, Hooks> Cache = new();

    public static string Status { get; private set; } = "not used yet";

    public static void TeleportBurst(PlayerInfo p)
    {
        if (!ModConfig.GameTeleportBurst.Value) return;
        var h = Get(p);
        if (h.Teleport == null || h.TeleportMethod == null) return;
        try
        {
            h.TeleportMethod.Invoke(h.Teleport, null);
        }
        catch (Exception ex)
        {
            h.TeleportMethod = null;
            ModLog.Warn($"Game teleport effect failed ({(ex.InnerException ?? ex).Message}); disabled.");
        }
    }

    /// <summary>Turn the game's speed-lines effect on/off for the dash (other sources, like speed boosts, are unaffected).</summary>
    public static void SpeedLines(PlayerInfo p, Il2CppSystem.Object source, bool on)
    {
        if (!ModConfig.GameSpeedLines.Value) return;
        var h = Get(p);
        if (h.SpeedLines == null || h.SetSourceMethod == null) return;
        try
        {
            h.SetSourceMethod.Invoke(h.SpeedLines, new object[] { source, on });
        }
        catch (Exception ex)
        {
            h.SetSourceMethod = null;
            ModLog.Warn($"Game speed-lines effect failed ({(ex.InnerException ?? ex).Message}); disabled.");
        }
    }

    private static Hooks Get(PlayerInfo p)
    {
        if (Cache.TryGetValue(p.Id, out var cached)) return cached;
        var h = new Hooks();
        try
        {
            foreach (var c in p.Root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (c == null) continue;
                var name = Il2CppNames.ShortName(c);
                if (name == "PlayerTeleportEffect" && h.Teleport == null)
                {
                    h.Teleport = Il2CppNames.WrapAsOwnType(c, out var type);
                    h.TeleportMethod = type?.GetMethod("PlayTeleportedEffect", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                }
                else if (name == "MultiSourceParticleEffect" && h.SpeedLines == null && c.gameObject.name.Contains("Fast"))
                {
                    h.SpeedLines = Il2CppNames.WrapAsOwnType(c, out var type);
                    h.SetSourceMethod = type?.GetMethod("SetSource", BindingFlags.Instance | BindingFlags.Public);
                }
            }
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Looking for the game's effects failed: {ex.Message}");
        }

        Status = $"teleport burst: {(h.TeleportMethod != null ? "found" : "not found")}, speed lines: {(h.SetSourceMethod != null ? "found" : "not found")}";
        ModLog.Info("Game effects: " + Status);
        Cache[p.Id] = h;
        return h;
    }

    private sealed class Hooks
    {
        public object? Teleport;
        public MethodInfo? TeleportMethod;
        public object? SpeedLines;
        public MethodInfo? SetSourceMethod;
    }
}
