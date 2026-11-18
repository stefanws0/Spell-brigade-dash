namespace SpellBrigadeDash.Tests;

using SpellBrigadeDash.Core;
using Xunit;

public class BodyPickTests
{
    private static BodyPick.Candidate C(BodyPick.Kind kind, float size, bool inSpells = false) => new(kind, size, inSpells);

    [Fact]
    public void The_animated_model_beats_a_bigger_mesh()
        // 18 Nov: the Sun Mage's Solar Flare circle (a big plain mesh) was taken for the wizard's body.
        => Assert.Equal(1, BodyPick.Best(new[] { C(BodyPick.Kind.Mesh, 40f), C(BodyPick.Kind.Skinned, 2f) }));

    [Fact]
    public void Nothing_under_the_wizards_spells_counts()
        => Assert.Equal(0, BodyPick.Best(new[] { C(BodyPick.Kind.Mesh, 1f), C(BodyPick.Kind.Skinned, 50f, inSpells: true) }));

    [Fact]
    public void The_largest_of_the_best_kind_wins()
        => Assert.Equal(2, BodyPick.Best(new[] { C(BodyPick.Kind.Skinned, 1f), C(BodyPick.Kind.Mesh, 9f), C(BodyPick.Kind.Skinned, 3f) }));

    [Fact]
    public void Sprites_beat_plain_meshes()
        => Assert.Equal(0, BodyPick.Best(new[] { C(BodyPick.Kind.Sprite, 1f), C(BodyPick.Kind.Mesh, 9f) }));

    [Fact]
    public void No_body_at_all_is_minus_one()
    {
        Assert.Equal(-1, BodyPick.Best(new BodyPick.Candidate[0]));
        Assert.Equal(-1, BodyPick.Best(new[] { C(BodyPick.Kind.Skinned, 5f, inSpells: true) }));
        Assert.Equal(-1, BodyPick.Best(new[] { C(BodyPick.Kind.Skinned, 0f) }));
    }
}
