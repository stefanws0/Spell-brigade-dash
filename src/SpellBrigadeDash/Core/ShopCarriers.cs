namespace SpellBrigadeDash.Core;

using System;
using System.Collections.Generic;

/// <summary>
/// Which game stat each dash card carries in the game's level-up shop (names of the game's <c>StatType</c> values).
///
/// The game decides "same card" (one per shop, bans) by the card's stat, so every dash card has its own:
///  * <see cref="RestStat"/>: carried between shops, i.e. while the shop is generated. A stat with no character card
///    of its own (so a dash card is never "the same" as a real card) and no "Multiplier" in its name. It is never
///    applied to the player, because a card is only applied while carrying its shop stat.
///  * <see cref="ShopStat"/>: carried while a shop is open and when a card is picked. The game's shop analytics parse
///    every card's stat as their <c>ImprovementStat</c> enum (16 values) and throw on anything else. Picking applies
///    this stat with value 0, which changes nothing.
/// The Dash card is only offered while the dash is locked and the upgrades only after it is unlocked, so only the
/// upgrades need different shop stats from each other.
/// </summary>
public static class ShopCarriers
{
    /// <summary>The game's analytics enum <c>ImprovementStat</c> (game 1.2.1).</summary>
    public static readonly IReadOnlyList<string> AnalyticsStats = new[]
    {
        "Damage", "Range", "FireRate", "LifeTime", "DamageTickRate", "Size", "MovementSpeed", "MaxHealth", "Luck", "XPGain",
        "PickupRadius", "CriticalChance", "CriticalDamage", "Armor", "Dodge", "HealthRegeneration",
    };

    /// <summary>Stats of the game's own 13 character cards (logged from its shop, 17 Nov).</summary>
    public static readonly IReadOnlyList<string> RealCardStats = new[]
    {
        "Armor", "CriticalChance", "CriticalDamage", "Damage", "Dodge", "FireRate", "HealthRegeneration", "Luck", "MaxHealth",
        "MovementSpeed", "PickupRadius", "Size", "XPGain",
    };

    public static string RestStat(DashCardId card) => card switch
    {
        DashCardId.Dash => "HealOnDodge",
        DashCardId.ExtraCharge => "HealOnLevelUp",
        DashCardId.QuickRecovery => "XPGainOnDamageTaken",
        DashCardId.LongStride => "DropChance",
        DashCardId.BlazingDash => "GoldGain",
        DashCardId.EtherealStep => "HealthPercentageOnRevive",
        _ => throw new ArgumentOutOfRangeException(nameof(card), card, null),
    };

    public static string ShopStat(DashCardId card) => card switch
    {
        DashCardId.Dash => "Dodge",
        DashCardId.ExtraCharge => "Range",
        DashCardId.QuickRecovery => "LifeTime",
        DashCardId.LongStride => "DamageTickRate",
        DashCardId.BlazingDash => "MovementSpeed",
        DashCardId.EtherealStep => "Dodge",
        _ => throw new ArgumentOutOfRangeException(nameof(card), card, null),
    };
}
