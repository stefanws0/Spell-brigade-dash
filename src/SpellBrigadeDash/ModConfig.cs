namespace SpellBrigadeDash;

using BepInEx.Configuration;
using SpellBrigadeDash.Core;

/// <summary>
/// Every tunable value. BepInEx writes these to BepInEx/config/com.stefanws0.spellbrigadedash.cfg
/// the first time the game starts with the mod. Edit that file (game closed) to tweak.
/// </summary>
internal static class ModConfig
{
    // ---- Dash ----
    public static ConfigEntry<float> Distance = null!;
    public static ConfigEntry<float> Duration = null!;
    public static ConfigEntry<int> Charges = null!;
    public static ConfigEntry<float> RechargeSeconds = null!;
    public static ConfigEntry<float> IFrameGrace = null!;
    public static ConfigEntry<bool> PassThroughEnemies = null!;
    public static ConfigEntry<string> EnemyLayerHints = null!;
    public static ConfigEntry<float> InputBuffer = null!;

    // ---- Controls ----
    public static ConfigEntry<string> KeyboardBinding = null!;
    public static ConfigEntry<string> GamepadBinding = null!;
    public static ConfigEntry<string> LegacyKey = null!;
    public static ConfigEntry<string> LegacyGamepadKey = null!;

    // ---- Bar ----
    public static ConfigEntry<bool> BarAutoHide = null!;
    public static ConfigEntry<float> BarHideDelay = null!;
    public static ConfigEntry<float> BarWidth = null!;
    public static ConfigEntry<float> BarHeight = null!;
    public static ConfigEntry<float> BarOffset = null!;
    public static ConfigEntry<float> BarScale = null!;
    public static ConfigEntry<string> BarFillColor = null!;
    public static ConfigEntry<string> BarChargingColor = null!;
    public static ConfigEntry<string> BarBackColor = null!;
    public static ConfigEntry<string> BarFlashColor = null!;
    public static ConfigEntry<string> BarEmptyColor = null!;
    public static ConfigEntry<int> BarSortingOrder = null!;

    // ---- Effects ----
    public static ConfigEntry<bool> Afterimages = null!;
    public static ConfigEntry<float> AfterimageInterval = null!;
    public static ConfigEntry<float> AfterimageLifetime = null!;
    public static ConfigEntry<string> AfterimageColor = null!;
    public static ConfigEntry<bool> DustPuff = null!;
    public static ConfigEntry<bool> DustPuffOnLanding = null!;
    public static ConfigEntry<string> DustColor = null!;
    public static ConfigEntry<bool> BarFeedback = null!;
    public static ConfigEntry<bool> RemoteAfterimages = null!;

    // ---- Sound ----
    public static ConfigEntry<bool> SoundEnabled = null!;
    public static ConfigEntry<string> SoundClipName = null!;
    public static ConfigEntry<string> SoundClipHints = null!;
    public static ConfigEntry<float> SoundVolume = null!;

    // ---- Upgrades ----
    public static ConfigEntry<string> UnlockRarity = null!;
    public static ConfigEntry<float> UnlockWeight = null!;
    public static ConfigEntry<string> ExtraChargeRarity = null!;
    public static ConfigEntry<int> ExtraChargeMaxStacks = null!;
    public static ConfigEntry<string> QuickRecoveryRarity = null!;
    public static ConfigEntry<int> QuickRecoveryMaxStacks = null!;
    public static ConfigEntry<float> QuickRecoveryPerStack = null!;
    public static ConfigEntry<string> LongStrideRarity = null!;
    public static ConfigEntry<int> LongStrideMaxStacks = null!;
    public static ConfigEntry<float> LongStridePerStack = null!;
    public static ConfigEntry<string> BlazingDashRarity = null!;
    public static ConfigEntry<float> BlazingDashDamage = null!;
    public static ConfigEntry<float> BlazingTrailSeconds = null!;

    // ---- Multiplayer ----
    public static ConfigEntry<bool> RemoteIFrames = null!;
    public static ConfigEntry<float> RemoteDashSpeedFactor = null!;

    // ---- Game hooks (advanced) ----
    public static ConfigEntry<string> PlayerComponent = null!;
    public static ConfigEntry<string> LocalPlayerMembers = null!;
    public static ConfigEntry<string> DeadMembers = null!;
    public static ConfigEntry<string> DamageMethods = null!;
    public static ConfigEntry<bool> AutoDamageHooks = null!;

    // ---- Discovery ----
    public static ConfigEntry<bool> DiscoveryEnabled = null!;
    public static ConfigEntry<bool> TraceLevelUpMethods = null!;
    public static ConfigEntry<int> TraceMaxMethods = null!;
    public static ConfigEntry<bool> SafeMode = null!;
    public static ConfigEntry<float> HookDelaySeconds = null!;

    // ---- Debug ----
    public static ConfigEntry<bool> StartRunWithDash = null!;
    public static ConfigEntry<bool> DebugHotkeys = null!;
    public static ConfigEntry<string> HotkeyModifier = null!;
    public static ConfigEntry<string> KeyReport = null!;
    public static ConfigEntry<string> KeyToggleDash = null!;
    public static ConfigEntry<string> KeyExtraCharge = null!;
    public static ConfigEntry<string> KeyQuickRecovery = null!;
    public static ConfigEntry<string> KeyLongStride = null!;
    public static ConfigEntry<string> KeyBlazingDash = null!;

    public static void Init(ConfigFile cfg)
    {
        const string dash = "Dash";
        Distance = cfg.Bind(dash, "Distance", 3f,
            new ConfigDescription("How far one dash goes, in world units.", new AcceptableValueRange<float>(0.5f, 20f)));
        Duration = cfg.Bind(dash, "Duration", 0.12f,
            new ConfigDescription("How long one dash takes, in seconds. Lower = snappier.", new AcceptableValueRange<float>(0.03f, 1f)));
        Charges = cfg.Bind(dash, "Charges", 2,
            new ConfigDescription("Bar segments once the dash is unlocked (before +1 Charge cards).", new AcceptableValueRange<int>(1, 6)));
        RechargeSeconds = cfg.Bind(dash, "RechargeSeconds", 2f,
            new ConfigDescription("Seconds to refill ONE segment.", new AcceptableValueRange<float>(0f, 30f)));
        IFrameGrace = cfg.Bind(dash, "IFrameGraceSeconds", 0.08f,
            new ConfigDescription("Extra invulnerability after the dash ends (the dash itself is always invulnerable).", new AcceptableValueRange<float>(0f, 1f)));
        PassThroughEnemies = cfg.Bind(dash, "PassThroughEnemies", false,
            "If true, enemies never stop a dash. If false, the dash collides with whatever your wizard collides with while walking.");
        EnemyLayerHints = cfg.Bind(dash, "EnemyLayerHints", "enemy,enemies,monster,mob",
            "Comma-separated words. Physics layers whose name contains one of them count as 'enemy' for PassThroughEnemies.");
        InputBuffer = cfg.Bind(dash, "InputBufferSeconds", 0.1f,
            new ConfigDescription("Pressing dash this long before the current dash ends queues the next one.", new AcceptableValueRange<float>(0f, 0.5f)));

        const string controls = "Controls";
        KeyboardBinding = cfg.Bind(controls, "KeyboardBinding", "<Keyboard>/leftShift",
            "Input System control path for the keyboard dash key, e.g. <Keyboard>/leftShift, <Keyboard>/space, <Mouse>/rightButton.");
        GamepadBinding = cfg.Bind(controls, "GamepadBinding", "<Gamepad>/leftShoulder",
            "Input System control path for the gamepad dash button, e.g. <Gamepad>/leftShoulder, <Gamepad>/leftTrigger, <Gamepad>/buttonEast. Empty = no gamepad dash.");
        LegacyKey = cfg.Bind(controls, "LegacyKey", "LeftShift",
            "Only used if the game doesn't use Unity's new Input System: a UnityEngine.KeyCode name.");
        LegacyGamepadKey = cfg.Bind(controls, "LegacyGamepadKey", "JoystickButton4",
            "Only used if the game doesn't use Unity's new Input System: KeyCode for the gamepad button (JoystickButton4 = LB on Xbox pads).");

        const string bar = "Bar";
        BarAutoHide = cfg.Bind(bar, "AutoHide", true, "Fade the bar out when all charges are full.");
        BarHideDelay = cfg.Bind(bar, "HideDelaySeconds", 1f, "How long the bar stays visible after it is full again.");
        BarWidth = cfg.Bind(bar, "Width", 56f, "Total bar width in pixels at 1080p.");
        BarHeight = cfg.Bind(bar, "Height", 7f, "Bar height in pixels at 1080p.");
        BarOffset = cfg.Bind(bar, "OffsetBelowFeet", 14f, "Pixels (at 1080p) between the wizard's feet and the bar.");
        BarScale = cfg.Bind(bar, "Scale", 1f, new ConfigDescription("Overall bar size multiplier.", new AcceptableValueRange<float>(0.25f, 4f)));
        BarFillColor = cfg.Bind(bar, "FillColor", "#6FE3FFFF", "Colour of full segments (#RRGGBB or #RRGGBBAA).");
        BarChargingColor = cfg.Bind(bar, "ChargingColor", "#3B86A8FF", "Colour of the segment that is refilling.");
        BarBackColor = cfg.Bind(bar, "BackgroundColor", "#0C1220C8", "Background / frame colour.");
        BarFlashColor = cfg.Bind(bar, "FlashColor", "#FFFFFFFF", "Flash colour when a segment refills.");
        BarEmptyColor = cfg.Bind(bar, "EmptyPressColor", "#FF4A4AFF", "Flash colour when you press dash with no charges.");
        BarSortingOrder = cfg.Bind(bar, "SortingOrder", -50,
            "Canvas sorting order. Negative = drawn below the game's own UI (menus cover the bar). Raise it if the bar is hidden behind the HUD.");

        const string fx = "Effects";
        Afterimages = cfg.Bind(fx, "Afterimages", true, "Leave fading copies of the wizard along the dash path.");
        AfterimageInterval = cfg.Bind(fx, "AfterimageInterval", 0.025f, "Seconds between afterimages during a dash.");
        AfterimageLifetime = cfg.Bind(fx, "AfterimageLifetime", 0.25f, "Seconds an afterimage takes to fade.");
        AfterimageColor = cfg.Bind(fx, "AfterimageColor", "#8FD8FF99", "Tint of the afterimages (alpha = starting opacity).");
        DustPuff = cfg.Bind(fx, "DustPuff", true, "Small dust burst where the dash starts.");
        DustPuffOnLanding = cfg.Bind(fx, "DustPuffOnLanding", true, "Smaller dust burst where the dash ends.");
        DustColor = cfg.Bind(fx, "DustColor", "#E6DCC8D0", "Dust colour.");
        BarFeedback = cfg.Bind(fx, "BarFeedback", true, "Segments flash when refilled; the bar shakes red on an empty press.");
        RemoteAfterimages = cfg.Bind(fx, "RemoteAfterimages", true, "Show afterimages when other (modded) players dash.");

        const string sound = "Sound";
        SoundEnabled = cfg.Bind(sound, "Enabled", true, "Play a sound on dash, reusing one of the game's own sounds.");
        SoundClipName = cfg.Bind(sound, "ClipName", "",
            "Exact AudioClip name to use (see the discovery report for the list). Empty = pick automatically using ClipNameHints.");
        SoundClipHints = cfg.Bind(sound, "ClipNameHints", "dash,dodge,whoosh,woosh,swoosh,swish,swing,blink,teleport,roll,wind",
            "Comma-separated words, in priority order, used to pick a game sound automatically.");
        SoundVolume = cfg.Bind(sound, "Volume", 0.7f, new ConfigDescription("Dash sound volume.", new AcceptableValueRange<float>(0f, 1f)));

        const string up = "Upgrades";
        UnlockRarity = cfg.Bind(up, "DashUnlockRarity", "Epic", "Rarity of the card that unlocks the dash (the game's rarity name).");
        UnlockWeight = cfg.Bind(up, "DashUnlockWeight", 1f, "Relative chance of the unlock card within its rarity (1 = like any other card).");
        ExtraChargeRarity = cfg.Bind(up, "ExtraChargeRarity", "Legendary", "Rarity of '+1 Dash Charge'.");
        ExtraChargeMaxStacks = cfg.Bind(up, "ExtraChargeMaxStacks", 2, "How many times '+1 Dash Charge' can be taken per run.");
        QuickRecoveryRarity = cfg.Bind(up, "QuickRecoveryRarity", "Epic", "Rarity of 'Quick Recovery'.");
        QuickRecoveryMaxStacks = cfg.Bind(up, "QuickRecoveryMaxStacks", 3, "How many times 'Quick Recovery' can be taken per run.");
        QuickRecoveryPerStack = cfg.Bind(up, "QuickRecoveryPerStack", 0.25f, "Refill-rate bonus per 'Quick Recovery' (0.25 = 25% faster).");
        LongStrideRarity = cfg.Bind(up, "LongStrideRarity", "Epic", "Rarity of 'Long Stride'.");
        LongStrideMaxStacks = cfg.Bind(up, "LongStrideMaxStacks", 2, "How many times 'Long Stride' can be taken per run.");
        LongStridePerStack = cfg.Bind(up, "LongStridePerStack", 0.30f, "Distance bonus per 'Long Stride' (0.30 = 30% farther).");
        BlazingDashRarity = cfg.Bind(up, "BlazingDashRarity", "Legendary", "Rarity of 'Blazing Dash'.");
        BlazingDashDamage = cfg.Bind(up, "BlazingDashDamage", 15f, "Damage per tick of the Blazing Dash trail (applied once the game's damage hook is wired).");
        BlazingTrailSeconds = cfg.Bind(up, "BlazingTrailSeconds", 1.5f, "How long the Blazing Dash trail stays on the ground.");

        const string mp = "Multiplayer";
        RemoteIFrames = cfg.Bind(mp, "RemoteIFrames", true,
            "Protect OTHER players while they dash. Needed so the host doesn't apply hits to a dashing client. Every player should have the mod.");
        RemoteDashSpeedFactor = cfg.Bind(mp, "RemoteDashSpeedFactor", 0.6f,
            new ConfigDescription("A remote wizard moving faster than this fraction of dash speed counts as dashing.", new AcceptableValueRange<float>(0.2f, 1.5f)));

        const string hooks = "GameHooks";
        PlayerComponent = cfg.Bind(hooks, "PlayerComponent", "auto",
            "Class name of the component on the player wizard (e.g. PlayerController). 'auto' = guess by name. See the discovery report.");
        LocalPlayerMembers = cfg.Bind(hooks, "LocalPlayerMembers",
            "isLocalPlayer,IsLocalPlayer,IsLocal,isLocal,IsOwner,isOwner,IsOwned,isOwned,IsMine,isMine,hasAuthority,HasAuthority,HasInputAuthority,IsLocallyControlled,isLocallyControlled",
            "Bool fields/properties checked (on any component of the wizard) to tell YOUR wizard apart from other players.");
        DeadMembers = cfg.Bind(hooks, "DeadMembers", "IsDead,isDead,Dead,dead,IsDowned,isDowned,Downed,downed,IsKnockedOut,!IsAlive,!isAlive",
            "Bool members that mean 'can't dash right now'. Prefix with ! to invert (e.g. !IsAlive).");
        DamageMethods = cfg.Bind(hooks, "DamageMethods", "",
            "Extra damage methods to block during i-frames, as Type:Method separated by ';' (e.g. PlayerHealth:TakeDamage). Always applied.");
        AutoDamageHooks = cfg.Bind(hooks, "AutoDamageHooks", true,
            "Automatically hook methods named like TakeDamage/OnHit on the wizard's components for i-frames.");

        const string disc = "Discovery";
        DiscoveryEnabled = cfg.Bind(disc, "Enabled", true,
            "Write BepInEx/SpellBrigadeDash-discovery.txt (game classes, damage methods, upgrade types...) when a run starts and when you press the report hotkey (Left Ctrl + 1 by default). Send this file to the mod author.");
        TraceLevelUpMethods = cfg.Bind(disc, "TraceLevelUpMethods", false,
            "EXPERIMENTAL, can crash the game: logs calls to the game's level-up / upgrade methods so the card hook can be found. " +
            "Only turn on when asked to, and only after the mod runs fine without it.");
        TraceMaxMethods = cfg.Bind(disc, "TraceMaxMethods", 25, "Maximum number of game methods traced.");

        const string dbg = "Debug";
        SafeMode = cfg.Bind(dbg, "SafeMode", false,
            "If the game crashes with the mod: turn this on. No Harmony patches at all (no i-frames, no tracing), only dash + bar + effects. " +
            "Tells us whether the crash comes from the patches. BepInEx/SpellBrigadeDash.log names the last step before a crash.");
        HookDelaySeconds = cfg.Bind(dbg, "HookDelaySeconds", 3f,
            "Seconds after your wizard appears before the i-frame damage hooks are installed.");
        StartRunWithDash = cfg.Bind(dbg, "StartRunWithDash", true,
            "Give the dash at the start of every run instead of waiting for the level-up card. ON by default until the card hook is wired.");
        DebugHotkeys = cfg.Bind(dbg, "Hotkeys", true,
            "Debug hotkeys (hold the modifier, then press the key). Defaults: Left Ctrl + 1 = discovery report, + 2 = toggle dash, " +
            "+ 3 = +1 Charge, + 4 = Quick Recovery, + 5 = Long Stride, + 6 = Blazing Dash.");
        const string keyHelp = " Input System control path, e.g. <Keyboard>/1, <Keyboard>/k, <Keyboard>/numpad1, <Keyboard>/f6.";
        HotkeyModifier = cfg.Bind(dbg, "HotkeyModifier", "<Keyboard>/leftCtrl",
            "Key that must be held for the debug hotkeys. Empty = no modifier." + keyHelp);
        KeyReport = cfg.Bind(dbg, "KeyWriteReport", "<Keyboard>/1", "Write the discovery report now." + keyHelp);
        KeyToggleDash = cfg.Bind(dbg, "KeyToggleDash", "<Keyboard>/2", "Lock / unlock the dash." + keyHelp);
        KeyExtraCharge = cfg.Bind(dbg, "KeyExtraCharge", "<Keyboard>/3", "Apply the '+1 Dash Charge' card." + keyHelp);
        KeyQuickRecovery = cfg.Bind(dbg, "KeyQuickRecovery", "<Keyboard>/4", "Apply the 'Quick Recovery' card." + keyHelp);
        KeyLongStride = cfg.Bind(dbg, "KeyLongStride", "<Keyboard>/5", "Apply the 'Long Stride' card." + keyHelp);
        KeyBlazingDash = cfg.Bind(dbg, "KeyBlazingDash", "<Keyboard>/6", "Apply the 'Blazing Dash' card." + keyHelp);
    }

    public static string DebugKeyPath(Controls.DebugKey key) => key switch
    {
        Controls.DebugKey.Report => KeyReport.Value,
        Controls.DebugKey.ToggleDash => KeyToggleDash.Value,
        Controls.DebugKey.ExtraCharge => KeyExtraCharge.Value,
        Controls.DebugKey.QuickRecovery => KeyQuickRecovery.Value,
        Controls.DebugKey.LongStride => KeyLongStride.Value,
        _ => KeyBlazingDash.Value,
    };

    /// <summary>"&lt;Keyboard&gt;/leftCtrl" → "Left Ctrl", for log messages.</summary>
    public static string PrettyKey(string path)
    {
        var name = path.Contains('/') ? path[(path.LastIndexOf('/') + 1)..] : path;
        if (name.Length == 0) return "(none)";
        var spaced = System.Text.RegularExpressions.Regex.Replace(name, "(?<=[a-z])(?=[A-Z0-9])", " ");
        return char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }

    public static DashBaseStats BaseStats() => new()
    {
        Distance = Distance.Value,
        Duration = Duration.Value,
        Charges = Charges.Value,
        RechargeSeconds = RechargeSeconds.Value,
        IFrameGraceSeconds = IFrameGrace.Value,
        QuickRecoveryPerStack = QuickRecoveryPerStack.Value,
        LongStridePerStack = LongStridePerStack.Value,
    };

    public static DashCardSettings CardSettings() => new()
    {
        UnlockRarity = UnlockRarity.Value,
        UnlockWeight = UnlockWeight.Value,
        ExtraChargeRarity = ExtraChargeRarity.Value,
        ExtraChargeMaxStacks = ExtraChargeMaxStacks.Value,
        QuickRecoveryRarity = QuickRecoveryRarity.Value,
        QuickRecoveryMaxStacks = QuickRecoveryMaxStacks.Value,
        QuickRecoveryPerStack = QuickRecoveryPerStack.Value,
        LongStrideRarity = LongStrideRarity.Value,
        LongStrideMaxStacks = LongStrideMaxStacks.Value,
        LongStridePerStack = LongStridePerStack.Value,
        BlazingDashRarity = BlazingDashRarity.Value,
        BlazingDashDamage = BlazingDashDamage.Value,
    };

    public static string[] SplitList(string value, char sep = ',')
    {
        var parts = value.Split(sep, System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
        return parts;
    }
}
