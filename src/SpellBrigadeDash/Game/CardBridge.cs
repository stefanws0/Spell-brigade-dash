using System.Collections.Generic;
using SpellBrigadeDash.Core;

namespace SpellBrigadeDash.Game;

/// <summary>
/// Puts the dash cards (<see cref="DashCards"/>) into the game's level-up choices at their configured
/// rarity, and calls <see cref="DashProgress.Apply"/> when one is picked.
///
/// STATUS: PENDING DISCOVERY. The game's upgrade/rarity classes are unknown, so this bridge is
/// inactive. The discovery report (upgrade-type and enum sections, plus the method trace written
/// while you level up) gives what's needed to implement it. Meanwhile, Debug.StartRunWithDash and
/// the debug hotkeys (Left Ctrl + 2..6) stand in for picking cards.
/// </summary>
internal static class CardBridge
{
    public static bool IsActive => false;

    public static string Status => "not wired yet (waiting for discovery report)";

    /// <summary>The cards that could be offered right now; the future injection will draw from this.</summary>
    public static IEnumerable<DashCardDef> Offerable(IReadOnlyList<DashCardDef> catalogue, DashProgress progress)
        => DashCards.Available(catalogue, progress);
}
