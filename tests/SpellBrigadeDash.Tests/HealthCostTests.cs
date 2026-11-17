namespace SpellBrigadeDash.Tests;

using System.Linq;
using SpellBrigadeDash.Core;
using Xunit;

public class HealthCostTests
{
    private static readonly DashBaseStats Base = new() { HealthCostFraction = 0.05f };

    [Fact]
    public void A_dash_costs_5_percent_of_max_hp()
    {
        Assert.Equal(10f, DashTuning.HealthCost(0.05f, currentHp: 150f, maxHp: 200f), 3);
    }

    [Fact]
    public void A_dash_never_takes_the_last_hp_point()
    {
        Assert.Equal(2f, DashTuning.HealthCost(0.05f, currentHp: 3f, maxHp: 200f), 3);
        Assert.Equal(0f, DashTuning.HealthCost(0.05f, currentHp: 1f, maxHp: 200f), 3);
        Assert.Equal(0f, DashTuning.HealthCost(0.05f, currentHp: 0.5f, maxHp: 200f), 3);
    }

    [Fact]
    public void Ethereal_step_removes_the_cost()
    {
        var p = new DashProgress();
        p.Apply(DashCardId.Dash);
        Assert.Equal(0.05f, DashTuning.HealthCostFraction(Base, p), 3);
        p.Apply(DashCardId.EtherealStep);
        Assert.Equal(0f, DashTuning.HealthCostFraction(Base, p), 3);
    }

    [Fact]
    public void Ethereal_step_is_a_legendary_upgrade_offered_once_after_unlocking()
    {
        var cards = DashCards.Build(new DashCardSettings());
        var ethereal = cards.Single(c => c.Id == DashCardId.EtherealStep);
        Assert.Equal("Legendary", ethereal.Rarity);

        var p = new DashProgress();
        Assert.False(DashCards.IsAvailable(ethereal, p));
        p.Apply(DashCardId.Dash);
        Assert.True(DashCards.IsAvailable(ethereal, p));
        p.Apply(DashCardId.EtherealStep);
        Assert.False(DashCards.IsAvailable(ethereal, p));
    }

    [Fact]
    public void Without_a_health_cost_the_card_is_never_offered()
    {
        var cards = DashCards.Build(new DashCardSettings { HealthCostFraction = 0f });
        var p = new DashProgress();
        p.Apply(DashCardId.Dash);
        Assert.False(DashCards.IsAvailable(cards.Single(c => c.Id == DashCardId.EtherealStep), p));
    }
}
