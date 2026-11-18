namespace SpellBrigadeDash.Tests;

using System;
using System.Linq;
using SpellBrigadeDash.Core;
using Xunit;

/// <summary>
/// The game decides "same card" (duplicates in one shop, bans) by a card's stat, so every dash card needs its own
/// carrier stats. Rules come from the game (17 Nov): its 13 character cards' stats, and its shop analytics, which only
/// accept the 16 stats of the ImprovementStat enum while a shop is open.
/// </summary>
public class ShopCarrierTests
{
    private static readonly DashCardId[] AllCards = (DashCardId[])Enum.GetValues(typeof(DashCardId));

    // The upgrades can all be offered at the same time (after the unlock); the Dash card only while the dash is locked.
    private static readonly DashCardId[] Upgrades = AllCards.Where(c => c != DashCardId.Dash).ToArray();

    [Fact]
    public void Every_card_has_its_own_rest_stat()
    {
        var stats = AllCards.Select(ShopCarriers.RestStat).ToList();
        Assert.Equal(stats.Count, stats.Distinct().Count());
    }

    [Fact]
    public void Rest_stats_are_never_a_real_card_stat_or_a_multiplier()
    {
        foreach (var card in AllCards)
        {
            var stat = ShopCarriers.RestStat(card);
            Assert.DoesNotContain(stat, ShopCarriers.RealCardStats);
            Assert.DoesNotContain("Multiplier", stat);
        }
    }

    [Fact]
    public void Shop_stats_are_ones_the_game_analytics_accept()
    {
        foreach (var card in AllCards)
            Assert.Contains(ShopCarriers.ShopStat(card), ShopCarriers.AnalyticsStats);
    }

    [Fact]
    public void Upgrades_that_can_be_offered_together_have_different_shop_stats()
    {
        var stats = Upgrades.Select(ShopCarriers.ShopStat).ToList();
        Assert.Equal(stats.Count, stats.Distinct().Count());
    }

    [Fact]
    public void Shop_stats_prefer_stats_without_a_real_card()
    {
        // Range, LifeTime and DamageTickRate are the only analytics stats without a character card; all three are used.
        var used = AllCards.Select(ShopCarriers.ShopStat).ToHashSet();
        foreach (var free in ShopCarriers.AnalyticsStats.Except(ShopCarriers.RealCardStats))
            Assert.Contains(free, used);
    }
}
