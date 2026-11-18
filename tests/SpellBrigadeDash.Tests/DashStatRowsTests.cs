namespace SpellBrigadeDash.Tests;

using System.Globalization;
using System.Linq;
using System.Threading;
using SpellBrigadeDash.Core;
using Xunit;

public class DashStatRowsTests
{
    private static DashBaseStats Base() => new()
    {
        Distance = 3f, Charges = 1, RechargeSeconds = 15f, QuickRecoveryPerStack = 0.25f, LongStridePerStack = 0.30f,
        HealthCostFraction = 0.05f,
    };

    private static DashProgress With(params DashCardId[] cards)
    {
        var p = new DashProgress();
        foreach (var c in cards) p.Apply(c);
        return p;
    }

    private static string Value(DashProgress p, string key) => DashStatRows.Build(Base(), p).Single(r => r.Key == key).Value;

    [Fact]
    public void Always_the_same_rows_in_the_same_order()
    {
        var locked = DashStatRows.Build(Base(), new DashProgress()).Select(r => r.Key);
        var full = DashStatRows.Build(Base(), With(DashCardId.Dash, DashCardId.BlazingDash, DashCardId.EtherealStep)).Select(r => r.Key);
        Assert.Equal(DashStatRows.Keys, locked);
        Assert.Equal(DashStatRows.Keys, full);
    }

    [Fact]
    public void Keys_are_unique()
        => Assert.Equal(DashStatRows.Keys.Count, DashStatRows.Keys.Distinct().Count());

    [Fact]
    public void Nothing_shows_while_the_dash_is_locked()
        => Assert.All(DashStatRows.Build(Base(), new DashProgress()), r => Assert.False(r.Visible));

    [Fact]
    public void Base_dash_shows_charges_recharge_distance_and_cost()
    {
        var rows = DashStatRows.Build(Base(), With(DashCardId.Dash));
        Assert.Equal(new[] { "charges", "recharge", "distance", "cost" }, rows.Where(r => r.Visible).Select(r => r.Key));
        Assert.Equal("1", Value(With(DashCardId.Dash), "charges"));
        Assert.Equal("15s", Value(With(DashCardId.Dash), "recharge"));
        Assert.Equal("3", Value(With(DashCardId.Dash), "distance"));
        Assert.Equal("5%", Value(With(DashCardId.Dash), "cost"));
    }

    [Fact]
    public void Cards_change_the_values()
    {
        var p = With(DashCardId.Dash, DashCardId.ExtraCharge, DashCardId.ExtraCharge, DashCardId.QuickRecovery, DashCardId.LongStride);
        Assert.Equal("3", Value(p, "charges"));
        Assert.Equal("12s", Value(p, "recharge"));
        Assert.Equal("3.9", Value(p, "distance"));
    }

    [Fact]
    public void Ethereal_step_makes_the_dash_free()
        => Assert.Equal("None", Value(With(DashCardId.Dash, DashCardId.EtherealStep), "cost"));

    [Fact]
    public void Blazing_dash_row_shows_only_once_picked()
    {
        Assert.False(DashStatRows.Build(Base(), With(DashCardId.Dash)).Single(r => r.Key == "blazing").Visible);
        Assert.True(DashStatRows.Build(Base(), With(DashCardId.Dash, DashCardId.BlazingDash)).Single(r => r.Key == "blazing").Visible);
    }

    [Fact]
    public void Each_row_has_a_title_and_a_card_icon()
    {
        foreach (var r in DashStatRows.Build(Base(), With(DashCardId.Dash, DashCardId.BlazingDash)))
        {
            Assert.False(string.IsNullOrWhiteSpace(r.Title));
            Assert.True(System.Enum.IsDefined(typeof(DashCardId), r.Icon));
        }
    }

    [Fact]
    public void Numbers_use_a_dot_whatever_the_pc_language()
    {
        var old = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("nl-NL");
            Assert.Equal("8.6s", Value(With(DashCardId.Dash, DashCardId.QuickRecovery, DashCardId.QuickRecovery, DashCardId.QuickRecovery), "recharge"));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = old;
        }
    }
}
