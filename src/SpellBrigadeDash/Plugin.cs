namespace SpellBrigadeDash;

using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

/// <summary>
/// BepInEx entry point. Loaded once when the game starts. It only wires things up;
/// the actual per-frame work happens in <see cref="DashRunner"/> → <see cref="DashSystem"/>.
/// </summary>
[BepInPlugin(Guid, Name, Version)]
public class Plugin : BasePlugin
{
    public const string Guid = "com.stefanws0.spellbrigadedash";
    public const string Name = "SpellBrigadeDash";
    public const string Version = "0.3.0";

    internal static Harmony Harmony { get; private set; } = null!;

    public override void Load()
    {
        ModLog.Init(Log);
        ModLog.Step("Load: config");
        ModConfig.Init(Config);
        Harmony = new Harmony(Guid);

        // BasePlugin.AddComponent registers our MonoBehaviour with the IL2CPP runtime and attaches it
        // to BepInEx's persistent (DontDestroyOnLoad) manager object, so it survives scene changes.
        ModLog.Step("Load: registering DashRunner");
        AddComponent<DashRunner>();
        ModLog.Step("Load: done");
        if (ModConfig.SafeMode.Value) ModLog.Warn("Debug.SafeMode is ON: no Harmony patches (no i-frames, no tracing).");

        ModLog.Info($"{Name} {Version} loaded. Discovery report: {(ModConfig.DiscoveryEnabled.Value ? "ON" : "off")}, " +
                    $"debug hotkeys: {(ModConfig.DebugHotkeys.Value ? $"ON (hold {ModConfig.PrettyKey(ModConfig.HotkeyModifier.Value)})" : "off")}.");
    }
}
