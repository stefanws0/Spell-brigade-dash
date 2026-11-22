namespace SpellBrigadeDash.Game;

using System.Collections.Generic;
using System.Linq;

/// <summary>DEVELOPER BUILD ONLY: which networking library the game loaded (for the discovery report).</summary>
internal static class NetLibraries
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

    public static List<string> Detect()
    {
        var loaded = Il2CppNames.LoadedAssemblyNames();
        var found = new List<string>();
        foreach (var (asm, lib) in Known)
        {
            if (loaded.Contains(asm) || loaded.Contains("Il2Cpp" + asm))
                found.Add($"{lib} ({asm})");
        }

        return found.Distinct().ToList();
    }
}
