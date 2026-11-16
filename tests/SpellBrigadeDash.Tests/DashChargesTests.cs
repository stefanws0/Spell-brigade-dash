using SpellBrigadeDash.Core;
using Xunit;

namespace SpellBrigadeDash.Tests;

public class DashChargesTests
{
    [Fact]
    public void Starts_full_and_spends_one_charge_per_dash()
    {
        var c = new DashCharges(2, 2f);
        Assert.True(c.IsFull);
        Assert.True(c.TryConsume());
        Assert.True(c.TryConsume());
        Assert.False(c.TryConsume());
        Assert.Equal(0, c.Full);
    }

    [Fact]
    public void Segments_refill_one_at_a_time()
    {
        var c = new DashCharges(2, 2f);
        c.TryConsume();
        c.TryConsume();

        Assert.Equal(0, c.Tick(1f)); // halfway through the first segment
        Assert.Equal(0.5f, c.Progress, 3);
        Assert.Equal(0.5f, c.SegmentFill(0), 3);
        Assert.Equal(0f, c.SegmentFill(1), 3);

        Assert.Equal(1, c.Tick(1f)); // first segment done
        Assert.Equal(1, c.Full);
        Assert.Equal(0f, c.Progress, 3);

        Assert.Equal(1, c.Tick(2f)); // second segment done
        Assert.True(c.IsFull);
    }

    [Fact]
    public void A_large_tick_can_refill_several_segments_and_never_overfills()
    {
        var c = new DashCharges(3, 1f);
        c.TryConsume();
        c.TryConsume();
        c.TryConsume();
        Assert.Equal(3, c.Tick(10f));
        Assert.Equal(3, c.Full);
        Assert.Equal(0f, c.Progress);
    }

    [Fact]
    public void Dashing_while_refilling_keeps_the_partial_progress()
    {
        var c = new DashCharges(2, 2f);
        c.TryConsume();
        c.Tick(1f); // segment 1 half full
        c.TryConsume(); // spend the remaining full one
        Assert.Equal(0, c.Full);
        Assert.Equal(0.5f, c.Progress, 3);
    }

    [Fact]
    public void Adding_a_segment_adds_an_empty_one_that_recharges()
    {
        var c = new DashCharges(2, 2f);
        c.SetMax(3);
        Assert.Equal(3, c.Max);
        Assert.Equal(2, c.Full);
        Assert.False(c.IsFull);
        Assert.Equal(1, c.Tick(2f));
        Assert.True(c.IsFull);
    }

    [Fact]
    public void Shrinking_drops_extra_charges()
    {
        var c = new DashCharges(3, 2f);
        c.SetMax(1);
        Assert.Equal(1, c.Full);
        Assert.True(c.IsFull);
    }

    [Fact]
    public void Zero_recharge_time_refills_instantly()
    {
        var c = new DashCharges(2, 0f);
        c.TryConsume();
        c.TryConsume();
        Assert.Equal(2, c.Tick(0.016f));
    }

    [Fact]
    public void A_locked_dash_has_no_segments()
    {
        var c = new DashCharges(0, 2f);
        Assert.False(c.CanDash);
        Assert.False(c.TryConsume());
        Assert.Equal(0, c.Tick(1f));
    }
}
