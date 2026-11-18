namespace SpellBrigadeDash.Tests;

using SpellBrigadeDash.Core;
using Xunit;

public class BlazeTicksTests
{
    // Agreed with the owner 18 Nov: a tick every 0.5 s on enemies standing in a flame; the trail lasts 1.5 s.
    private static BlazeTicks Ticks() => new(0.5f);

    [Fact]
    public void An_enemy_is_hit_at_once_and_then_every_half_second()
    {
        var t = Ticks();
        Assert.True(t.TryHit(1, 10.0f));
        Assert.False(t.TryHit(1, 10.3f));
        Assert.True(t.TryHit(1, 10.5f));
        Assert.False(t.TryHit(1, 10.9f));
        Assert.True(t.TryHit(1, 11.0f));
    }

    [Fact]
    public void Each_enemy_has_its_own_timer()
    {
        var t = Ticks();
        Assert.True(t.TryHit(1, 5f));
        Assert.True(t.TryHit(2, 5.1f));
        Assert.False(t.TryHit(1, 5.2f));
    }

    [Fact]
    public void An_enemy_standing_in_a_fresh_trail_takes_three_hits()
    {
        // Scanned ten times a second, like the mod does.
        var t = Ticks();
        t.AddFlame(0f, 0f, 0.5f, until: 1.5f);
        var hits = 0;
        for (var i = 0; i < 30; i++)
        {
            var now = 0.05f + 0.1f * i;
            t.Prune(now);
            if (t.InFlame(0.2f, 0f, 0.3f, now) && t.TryHit(7, now)) hits++;
        }

        Assert.Equal(3, hits);
    }

    [Fact]
    public void Overlapping_flames_from_two_dashes_do_not_double_the_hits()
    {
        var t = Ticks();
        t.AddFlame(0f, 0f, 0.5f, until: 1.5f);
        t.AddFlame(0.1f, 0f, 0.5f, until: 1.7f);
        var hits = 0;
        for (var i = 0; i < 10; i++)
        {
            var now = 0.05f + 0.1f * i;
            if (t.InFlame(0f, 0f, 0.3f, now) && t.TryHit(7, now)) hits++;
        }

        Assert.Equal(2, hits); // 0.05 and 0.55
    }

    [Fact]
    public void Reach_is_the_flame_plus_the_enemy_size_on_the_ground()
    {
        var t = Ticks();
        t.AddFlame(0f, 0f, 0.5f, until: 9f);
        Assert.True(t.InFlame(0.79f, 0f, 0.3f, 1f));   // 0.79 < 0.5 + 0.3
        Assert.False(t.InFlame(0.81f, 0f, 0.3f, 1f));
        Assert.True(t.InFlame(0f, 0.7f, 0.3f, 1f));    // ground plane is x/z
    }

    [Fact]
    public void A_burnt_out_flame_no_longer_hurts()
    {
        var t = Ticks();
        t.AddFlame(0f, 0f, 0.5f, until: 2f);
        Assert.True(t.InFlame(0f, 0f, 0.1f, 1.9f));
        Assert.False(t.InFlame(0f, 0f, 0.1f, 2.1f));
        t.Prune(2.1f);
        Assert.Equal(0, t.FlameCount);
    }

    [Fact]
    public void Prune_forgets_old_hits_so_memory_stays_small()
    {
        var t = Ticks();
        for (var i = 0; i < 100; i++) t.TryHit(i, 1f);
        Assert.Equal(100, t.TrackedTargets);
        t.Prune(5f);
        Assert.Equal(0, t.TrackedTargets);
    }

    [Fact]
    public void Clear_drops_flames_and_hits()
    {
        var t = Ticks();
        t.AddFlame(0f, 0f, 1f, 10f);
        t.TryHit(1, 1f);
        t.Clear();
        Assert.Equal(0, t.FlameCount);
        Assert.True(t.TryHit(1, 1.1f));
    }

    [Theory]
    [InlineData(10f, 10f, 1f)]     // the game says: no bonus
    [InlineData(10f, 12.5f, 1.25f)] // +25% damage
    [InlineData(10f, 0f, 1f)]      // nonsense: ignore
    [InlineData(10f, 2000f, 1f)]   // nonsense: ignore
    [InlineData(0f, 5f, 1f)]       // no base: ignore
    public void The_games_value_becomes_a_multiplier_only_when_it_is_sane(float baseValue, float gameValue, float expected)
        => Assert.Equal(expected, BlazeMath.Multiplier(baseValue, gameValue, min: 0.2f, max: 50f), 3);

    // 18 Nov run: PlayerStats.GetSpellValue(Trail, ...) throws for a wizard without a Trail, so the Damage % and Spell
    // Size come from the character's own stat, whatever unit the game keeps it in.
    [Theory]
    [InlineData(false, 1f, 1.25f, 25f, 1.25f)]     // percent stat on a base of 1: value 1.25
    [InlineData(false, 100f, 125f, 25f, 1.25f)]    // percent stat on a base of 100: value 125
    [InlineData(false, 0f, 25f, 25f, 1.25f)]       // percent stat without a base: the modifiers count
    [InlineData(false, 0f, 0f, 0f, 1f)]            // no upgrades
    [InlineData(true, 0f, 25f, 25f, 1.25f)]        // flat stat holding the percent itself
    public void A_character_stat_becomes_a_multiplier(bool flat, float baseValue, float value, float modifiers, float expected)
        => Assert.Equal(expected, BlazeMath.StatMultiplier(flat, baseValue, value, modifiers), 3);

    [Fact]
    public void Per_tick_damage_is_base_times_multiplier_never_negative()
    {
        Assert.Equal(18.75f, BlazeMath.PerTick(15f, 1.25f), 3);
        Assert.Equal(0f, BlazeMath.PerTick(-3f, 1f));
    }
}
