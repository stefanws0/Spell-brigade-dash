namespace SpellBrigadeDash.Game;

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reuses The Spell Brigade's own effects on the wizard, so the dash looks native:
///  * <c>PlayerTeleportEffect.PlayTeleportedEffect()</c>: the game's teleport sparkle burst,
///  * <c>MultiSourceParticleEffect.SetSource(source, on)</c> on the wizard's <c>FastParticleEffect</c>: the game's
///    fast-running speed lines (PS_Run_Fast / PS_RunningLines / PS_Pulse). The game uses it for terrain speed boosts;
///    we add the dash as a source.
/// Each effect switches itself off for the session if it throws.
/// </summary>
internal static class GameFx
{
    private static readonly Dictionary<long, Hooks> Cache = new();
    private static bool _teleportBroken;
    private static bool _speedLinesBroken;

    public static string Status { get; private set; } = "not used yet";

    public static void TeleportBurst(PlayerInfo p)
    {
        if (!ModConfig.GameTeleportBurst.Value || _teleportBroken) return;
        var h = Get(p);
        if (h.Teleport == null) return;
        try
        {
            h.Teleport.PlayTeleportedEffect();
        }
        catch (Exception ex)
        {
            _teleportBroken = true;
            ModLog.Warn($"Game teleport effect failed ({ex.Message}); disabled.");
        }
    }

    /// <summary>Turn the game's speed-lines effect on/off for the dash (other sources, like speed boosts, are unaffected).</summary>
    public static void SpeedLines(PlayerInfo p, Il2CppSystem.Object source, bool on)
    {
        if (!ModConfig.GameSpeedLines.Value || _speedLinesBroken) return;
        var h = Get(p);
        if (h.SpeedLines == null) return;
        try
        {
            h.SpeedLines.SetSource(source, on);
        }
        catch (Exception ex)
        {
            _speedLinesBroken = true;
            ModLog.Warn($"Game speed-lines effect failed ({ex.Message}); disabled.");
        }
    }

    private static Hooks Get(PlayerInfo p)
    {
        if (Cache.TryGetValue(p.Id, out var cached)) return cached;
        var h = new Hooks();
        try
        {
            h.Teleport = p.Root.GetComponentInChildren<PlayerTeleportEffect>(true);
            if (h.Teleport == null) h.Teleport = null; // Unity "fake null" -> real null

            h.SpeedLines = p.Player.FastParticleEffect;
            if (h.SpeedLines == null)
            {
                h.SpeedLines = null;
                foreach (var e in p.Root.GetComponentsInChildren<MultiSourceParticleEffect>(true))
                {
                    if (e == null || !e.gameObject.name.Contains("Fast")) continue;
                    h.SpeedLines = e;
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Looking for the game's effects failed: {ex.Message}");
        }

        Status = $"teleport burst: {(h.Teleport != null ? "found" : "not found")}, speed lines: {(h.SpeedLines != null ? "found" : "not found")}";
        ModLog.Info("Game effects: " + Status);
        Cache[p.Id] = h;
        return h;
    }

    private sealed class Hooks
    {
        public PlayerTeleportEffect? Teleport;
        public MultiSourceParticleEffect? SpeedLines;
    }
}
