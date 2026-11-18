namespace SpellBrigadeDash.Tests;

using System;
using System.Linq;
using System.Text.RegularExpressions;
using SpellBrigadeDash.Core;
using Xunit;

public class CardArtTests
{
    private static readonly DashCardId[] AllCards = (DashCardId[])Enum.GetValues(typeof(DashCardId));

    [Fact]
    public void Every_card_has_its_own_icon_file()
    {
        var names = AllCards.Select(CardArt.IconFile).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void Icon_files_are_lower_snake_case_pngs()
    {
        foreach (var card in AllCards)
            Assert.Matches(new Regex("^[a-z]+(_[a-z]+)*\\.png$"), CardArt.IconFile(card));
    }

    [Fact]
    public void Known_names_match_the_art_folder()
    {
        Assert.Equal("dash.png", CardArt.IconFile(DashCardId.Dash));
        Assert.Equal("extra_charge.png", CardArt.IconFile(DashCardId.ExtraCharge));
        Assert.Equal("ethereal_step.png", CardArt.IconFile(DashCardId.EtherealStep));
    }
}
