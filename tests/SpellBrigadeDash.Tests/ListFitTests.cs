namespace SpellBrigadeDash.Tests;

using SpellBrigadeDash.Core;
using Xunit;

public class ListFitTests
{
    // The game's stats list: 70-high rows, 30 between them.
    private const float Row = 70f;
    private const float Gap = 30f;

    [Fact]
    public void Keeps_the_games_spacing_when_everything_fits()
        => Assert.Equal(Gap, ListFit.Spacing(room: 2000f, rowsHeight: 13 * Row, count: 13, baseSpacing: Gap, minSpacing: -10f));

    [Fact]
    public void Tightens_the_spacing_just_enough_to_fit()
    {
        // 17 rows: 1190 of rows + 16 gaps of 30 = 1670, in a box of 1590 -> 80 too much -> 5 less per gap.
        var s = ListFit.Spacing(room: 1590f, rowsHeight: 17 * Row, count: 17, baseSpacing: Gap, minSpacing: -10f);
        Assert.Equal(25f, s, 3);
        Assert.Equal(1590f, 17 * Row + 16 * s, 2);
    }

    [Fact]
    public void Never_tighter_than_the_minimum()
        => Assert.Equal(-10f, ListFit.Spacing(room: 1000f, rowsHeight: 18 * Row, count: 18, baseSpacing: Gap, minSpacing: -10f));

    [Fact]
    public void Never_wider_than_the_games_spacing()
        => Assert.Equal(Gap, ListFit.Spacing(room: 5000f, rowsHeight: 5 * Row, count: 5, baseSpacing: Gap, minSpacing: -10f));

    [Fact]
    public void One_row_or_none_keeps_the_spacing()
    {
        Assert.Equal(Gap, ListFit.Spacing(room: 10f, rowsHeight: Row, count: 1, baseSpacing: Gap, minSpacing: -10f));
        Assert.Equal(Gap, ListFit.Spacing(room: 10f, rowsHeight: 0f, count: 0, baseSpacing: Gap, minSpacing: -10f));
    }

    [Fact]
    public void Designed_room_is_rows_plus_the_games_gaps()
    {
        // In-game stats panel, 18 Nov: 15 rows of 70 with gaps of 13.5 end at the frame's inner edge.
        Assert.Equal(1239f, ListFit.DesignedRoom(15, 70f, 13.5f), 3);
        Assert.Equal(0f, ListFit.DesignedRoom(0, 70f, 13.5f));
    }

    [Fact]
    public void Half_a_row_less_room_leaves_half_a_row_of_margin()
    {
        // 18 Nov: with 15 rows of room the last row touched the frame's bottom edge; Stefan wanted a little more space.
        Assert.Equal(1239f - 0.5f * (70f + 13.5f), ListFit.DesignedRoom(14.5f, 70f, 13.5f), 3);
    }

    [Fact]
    public void Eighteen_rows_fit_the_designed_room_with_a_small_overlap()
    {
        var room = ListFit.DesignedRoom(15, 70f, 13.5f);
        var s = ListFit.Spacing(room, 18 * 70f, 18, 13.5f, -14f);
        Assert.InRange(s, -14f, 0f);
        Assert.Equal(room, 18 * 70f + 17 * s, 2);
    }

    [Fact]
    public void A_box_without_a_size_yet_changes_nothing()
        => Assert.Equal(Gap, ListFit.Spacing(room: 0f, rowsHeight: 17 * Row, count: 17, baseSpacing: Gap, minSpacing: -10f));
}
