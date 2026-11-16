using System;
using System.Collections.Generic;
using System.Linq;

namespace SpellBrigadeDash.Game;

/// <summary>
/// Explicit "I dashed" message over the game's own networking, so the host knows exactly when a
/// client is invulnerable.
///
/// STATUS: PENDING DISCOVERY. The networking library isn't known yet. Until it is, co-op i-frames come
/// from <see cref="Core.RemoteDashDetector"/>: every client, the host included, spots other wizards'
/// dashes from their synced movement and protects them. This class already detects the library for
/// the discovery report.
/// </summary>
internal static class NetBridge
{
    private static readonly (string assembly, string library)[] Known =
    {
        ("Mirror", "Mirror"),
        ("FishNet.Runtime", "FishNet"),
        ("Unity.Netcode.Runtime", "Unity Netcode for GameObjects"),
        ("PhotonUnityNetworking", "Photon PUN 2"),
        ("PhotonRealtime", "Photon Realtime"),
        ("Fusion.Runtime", "Photon Fusion"),
        ("Fusion.Unity", "Photon Fusion"),
        ("com.rlabrecque.steamworks.net", "Steamworks.NET"),
        ("Facepunch.Steamworks.Win64", "Facepunch.Steamworks"),
        ("Unity.Networking.Transport", "Unity Transport"),
        ("Coherence.Core", "coherence"),
        ("Netick", "Netick"),
        ("Epic.OnlineServices", "Epic Online Services"),
        ("HeathenEngineering.SteamworksIntegration", "Heathen Steamworks"),
        ("Purrnet", "PurrNet"),
        ("PurrNet", "PurrNet"),
    };

    public static List<string> DetectLibraries()
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var found = new List<string>();
        foreach (var (asm, lib) in Known)
        {
            if (loaded.Contains(asm) || loaded.Contains("Il2Cpp" + asm))
                found.Add($"{lib} ({asm})");
        }

        return found.Distinct().ToList();
    }

    /// <summary>Called when the local wizard dashes. No-op until the networking library is wired.</summary>
    public static void SendDash(float invulnerableSeconds)
    {
        ModLog.InfoOnce("netbridge-pending",
            "Co-op: dash sync message not wired yet; other players' i-frames use movement-based dash detection (Multiplayer.RemoteIFrames).");
    }
}
