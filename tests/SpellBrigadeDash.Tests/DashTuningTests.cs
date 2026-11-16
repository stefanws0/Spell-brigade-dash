using SpellBrigadeDash.Core;
using Xunit;

namespace SpellBrigadeDash.Tests;

public class DashTuningTests
{
    private static readonly DashBaseStats Base = new()
    {
        Distance = 3f,
        Duration = 0.12f,
        Charges = 2,
        RechargeSeconds = 2f,
        IFrameGraceSeconds = 0.08f,
        QuickRecoveryPerStack = 0.25f,
        LongStridePerStack = 0.30f,
    };

    [Fact]
    public void Locked_dash_has_zero_charges()
    {
        Assert.Equal(0, DashTuning.MaxCharges(Base, new DashProgress()));
    }

    [Fact]
    public void Unlocked_dash_uses_base_charges_plus_extra_charge_cards()
    {
        var p = new DashProgress();
        p.Apply(DashCardId.Dash);
        Assert.Equal(2, DashTuning.MaxCharges(Base, p));
        p.Apply(DashCardId.ExtraCharge);
        Assert.Equal(3, DashTuning.MaxCharges(Base, p));
    }

    [Fact]
    public void Quick_recovery_speeds_up_the_refill_rate()
    {
        var p = new DashProgress();
        p.Apply(DashCardId.Dash);
        Assert.Equal(2f, DashTuning.RechargeSeconds(Base, p), 3);
        p.Apply(DashCardId.QuickRecovery); // 25% faster → 2 / 1.25
        Assert.Equal(1.6f, DashTuning.RechargeSeconds(Base, p), 3);
        p.Apply(DashCardId.QuickRecovery); // 50% faster → 2 / 1.5
        Assert.Equal(1.333f, DashTuning.RechargeSeconds(Base, p), 3);
    }

    [Fact]
    public void Long_stride_adds_distance_in_the_same_time()
    {
        var p = new DashProgress();
        p.Apply(DashCardId.Dash);
        p.Apply(DashCardId.LongStride);
        Assert.Equal(3.9f, DashTuning.Distance(Base, p), 3);
        Assert.Equal(0.12f, DashTuning.Duration(Base), 3);
        Assert.Equal(32.5f, DashTuning.Speed(Base, p), 2);
    }

    [Fact]
    public void Iframes_cover_the_dash_plus_grace()
    {
        Assert.Equal(0.2f, DashTuning.InvulnerableSeconds(Base), 3);
    }
}
