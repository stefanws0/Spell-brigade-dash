using System.Linq;
using SpellBrigadeDash.Core;
using Xunit;

namespace SpellBrigadeDash.Tests;

public class DashCardsTests
{
    private static readonly DashCardSettings Settings = new();

    [Fact]
    public void Before_unlocking_only_the_Dash_card_is_offered()
    {
        var cards = DashCards.Build(Settings);
        var progress = new DashProgress();

        var offered = DashCards.Available(cards, progress).Select(c => c.Id).ToList();

        Assert.Equal(new[] { DashCardId.Dash }, offered);
    }

    [Fact]
    public void After_unlocking_the_upgrades_are_offered_and_the_unlock_is_not()
    {
        var cards = DashCards.Build(Settings);
        var progress = new DashProgress();
        progress.Apply(DashCardId.Dash);

        var offered = DashCards.Available(cards, progress).Select(c => c.Id).ToHashSet();

        Assert.DoesNotContain(DashCardId.Dash, offered);
        Assert.Contains(DashCardId.ExtraCharge, offered);
        Assert.Contains(DashCardId.QuickRecovery, offered);
        Assert.Contains(DashCardId.LongStride, offered);
        Assert.Contains(DashCardId.BlazingDash, offered);
    }

    [Fact]
    public void Cards_stop_being_offered_at_their_stack_cap()
    {
        var cards = DashCards.Build(new DashCardSettings { ExtraChargeMaxStacks = 2 });
        var progress = new DashProgress();
        progress.Apply(DashCardId.Dash);
        var extra = cards.Single(c => c.Id == DashCardId.ExtraCharge);

        progress.Apply(DashCardId.ExtraCharge);
        Assert.True(DashCards.IsAvailable(extra, progress));
        progress.Apply(DashCardId.ExtraCharge);
        Assert.False(DashCards.IsAvailable(extra, progress));

        var blazing = cards.Single(c => c.Id == DashCardId.BlazingDash);
        progress.Apply(DashCardId.BlazingDash);
        Assert.False(DashCards.IsAvailable(blazing, progress));
    }

    [Fact]
    public void Default_rarities_match_the_design()
    {
        var cards = DashCards.Build(Settings).ToDictionary(c => c.Id);
        Assert.Equal("Epic", cards[DashCardId.Dash].Rarity);
        Assert.Equal("Legendary", cards[DashCardId.ExtraCharge].Rarity);
        Assert.Equal("Epic", cards[DashCardId.QuickRecovery].Rarity);
        Assert.Equal("Epic", cards[DashCardId.LongStride].Rarity);
        Assert.Equal("Legendary", cards[DashCardId.BlazingDash].Rarity);
    }

    [Fact]
    public void Reset_clears_the_run()
    {
        var progress = new DashProgress();
        progress.Apply(DashCardId.Dash);
        progress.Apply(DashCardId.LongStride);
        progress.Reset();
        Assert.False(progress.Unlocked);
        Assert.Equal(0, progress.Stacks(DashCardId.LongStride));
    }
}
