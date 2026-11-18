namespace SpellBrigadeDash.Game;

using System;

/// <summary>
/// Which run of the game is in progress, so the dash cards are reset only when a new run starts. A new wizard object
/// is NOT a new run: the game spawns a fresh <c>GameplayPlayer</c> when you're revived (seen 17 Nov: two revives in
/// one run wiped the dash upgrades). Ported from the PC session's GameRun (branch pc-backup).
///
/// Key = the game's <c>SingletonPersistent&lt;RunData&gt;.Instance.RunId</c>, but only while a run's shop exists
/// (<c>NetworkSingleton&lt;StoreManager&gt;</c>), because RunData outlives runs. Without a RunId the shop manager
/// itself (one per run) is the key.
/// </summary>
internal static class GameRun
{
    private static bool _runIdBroken;

    public static string Status { get; private set; } = "no run yet";

    /// <summary>The key of the run in progress, or null outside a run (main menu, loading).</summary>
    public static string? Key()
    {
        StoreManager? manager;
        try
        {
            manager = NetworkSingleton<StoreManager>.Instance;
        }
        catch
        {
            return null;
        }

        if (manager == null) return null;
        var shop = $"shop 0x{manager.Pointer.ToInt64():X}";

        string? id = null;
        if (!_runIdBroken)
        {
            try
            {
                id = SingletonPersistent<RunData>.Instance?.RunId;
            }
            catch (Exception ex)
            {
                _runIdBroken = true;
                ModLog.Warn($"Run detection: RunData.RunId can't be read ({ex.Message}); using the shop manager instead.");
            }
        }

        Status = string.IsNullOrEmpty(id) ? $"{shop} (no RunId)" : $"run {id} ({shop})";
        return string.IsNullOrEmpty(id) ? shop : "run " + id;
    }
}
