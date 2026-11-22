namespace SpellBrigadeDash.Game;

/// <summary>
/// Explicit "I dashed" message over the game's own networking (Netcode for GameObjects), so the host knows exactly
/// when a client is invulnerable.
///
/// STATUS: NOT NEEDED SO FAR. The Spell Brigade applies damage on each player's own PC, so every player's own mod
/// protects them while dashing. <see cref="Core.RemoteDashDetector"/> can also spot other wizards' dashes from their
/// synced movement (Multiplayer.RemoteIFrames).
/// </summary>
internal static class NetBridge
{
    /// <summary>Called when the local wizard dashes. No-op until a co-op test shows the host needs to know.</summary>
    public static void SendDash(float invulnerableSeconds)
    {
        ModLog.InfoOnce("netbridge-pending",
            "Co-op: The Spell Brigade applies damage on each player's own PC, so every player's own mod protects them while dashing.");
    }
}
