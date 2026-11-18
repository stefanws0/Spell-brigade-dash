namespace SpellBrigadeDash.Core;

using System;

/// <summary>
/// Fits a vertical list into its box by tightening the gap between rows: the game's stats list has a fixed height
/// with room for its own rows plus about two, so the dash rows would run out of the frame. Pure, unit-tested.
/// </summary>
public static class ListFit
{
    /// <summary>
    /// Height of <paramref name="rows"/> rows at the game's own gap: the room a fixed frame was designed for. Measured
    /// 18 Nov on the in-game stats panel: the frame's inner edge sits exactly where a 15th row would end (13 stats +
    /// Revives + one spare), while the list's own box runs 250 px further, off the bottom of the screen. Fractional
    /// rows leave a margin: 14.5 rows of room ends half a row (and gap) above the 15-row edge.
    /// </summary>
    public static float DesignedRoom(float rows, float rowHeight, float baseSpacing)
        => rows <= 0f ? 0f : rows * rowHeight + (rows - 1f) * baseSpacing;

    /// <param name="room">Height available for rows and gaps (the frame's designed room or the box, whichever is smaller).</param>
    /// <param name="rowsHeight">Total height of the shown rows.</param>
    /// <param name="count">Number of shown rows.</param>
    /// <param name="baseSpacing">The game's own gap; never widened beyond it.</param>
    /// <param name="minSpacing">The tightest gap allowed (may be negative: the rows have empty space above and below their text).</param>
    public static float Spacing(float room, float rowsHeight, int count, float baseSpacing, float minSpacing)
    {
        if (count < 2 || room <= 0f) return baseSpacing;
        if (rowsHeight + baseSpacing * (count - 1) <= room) return baseSpacing;
        var fit = (room - rowsHeight) / (count - 1);
        return Math.Max(minSpacing, Math.Min(baseSpacing, fit));
    }
}
