// COMPILE-CHECK STUB ONLY - the game classes SpellBrigadeDash uses with real types (the level-up shop, see
// Game/CardBridge.cs), with the same names, namespaces and member names as the real interop
// (BepInEx/interop/Runtime.dll, Unity.Netcode.Runtime.dll, Unity.Localization.dll). Bodies are dummies.
using System;
using UnityEngine;
using Il2CppList = Il2CppSystem.Collections.Generic.List<StoreItem>;

namespace Unity.Netcode
{
    public class NetworkBehaviour : MonoBehaviour
    {
        public NetworkBehaviour(IntPtr pointer) : base(pointer) { }
    }
}

namespace UnityEngine.Localization
{
    public struct LocaleIdentifier
    {
        public string Code => null;
    }
}

namespace UnityEngine.Localization.Tables
{
    public class StringTableEntry : Il2CppSystem.Object
    {
        public StringTableEntry(IntPtr pointer) : base(pointer) { }
    }

    public class SharedTableData : ScriptableObject
    {
        public SharedTableData(IntPtr pointer) : base(pointer) { }
        public bool Contains(string key) => false;
    }

    public class LocalizationTable : ScriptableObject
    {
        public LocalizationTable(IntPtr pointer) : base(pointer) { }
        public string TableCollectionName => null;
        public SharedTableData SharedData => null;
        public LocaleIdentifier LocaleIdentifier => default;
    }

    public class StringTable : LocalizationTable
    {
        public StringTable(IntPtr pointer) : base(pointer) { }
        public StringTableEntry AddEntry(string key, string localized) => null;
        public StringTableEntry GetEntry(string key) => null;
    }
}

public class NetworkSingleton<T> : Unity.Netcode.NetworkBehaviour
{
    public NetworkSingleton(IntPtr pointer) : base(pointer) { }
    public static T Instance => default;
}

public enum StatType
{
    Damage, Range, FireRate, Speed, LifeTime, DamageTickRate, Size, MovementSpeed, MaxHealth, Luck, XPGain, PickupRadius,
    CriticalChance, CriticalDamage, Armor, Dodge, HealthRegeneration, Rerolls, ReviveSpeed, Revives, Projectiles, HealOnDodge,
    HealOnLevelUp, DropChance, TerrainSpeedMultiplier, HitboxSizeMultiplier, ElementWeaknessMultiplier, ElementStrengthMultiplier,
    HealingMultiplier, XPGainOnDamageTaken, HealthPotionAmountMultiplier, ReviveZoneSize, GoldGain, HealthPercentageOnRevive, Bans, Pins,
}

public enum RarityType { Common, Rare, Epic, Legendary }

public enum StoreGenerationType { Improvement, NewSpell, Infusion, Variant, Artifact }

public class StoreItem : Il2CppSystem.Object
{
    public StoreItem(IntPtr pointer) : base(pointer) { }
    public string TitleEntry => null;
    public string DescriptionEntry => null;
    public bool IsEquivalentIgnoringRarity(StoreItem itemToCompare) => false;
}

public class ImprovementItem : StoreItem
{
    public ImprovementItem(IntPtr pointer) : base(pointer) { }
    public StatType _Stat_k__BackingField { get; set; }
    public StatType Stat => default;
    public float Value => 0f;
    public RarityType Rarity => default;
}

public class CharacterImprovementItem : ImprovementItem
{
    public CharacterImprovementItem(IntPtr pointer) : base(pointer) { }
    public CharacterImprovementItem(string titleEntry, string descriptionEntry, StatType stat, float value, RarityType rarity, Sprite icon, float statLimitForOffer)
        : base(IntPtr.Zero) { }
    public Sprite Icon => null;
    public float statLimitForOffer => 0f;
}

public class CharacterImprovementBanRule : Il2CppSystem.Object
{
    public CharacterImprovementBanRule(IntPtr pointer) : base(pointer) { }
    public static bool IsSame(CharacterImprovementItem item, CharacterImprovementItem bannedItem) => false;
}

public class IGetStoreItems : Il2CppSystem.Object
{
    public IGetStoreItems(IntPtr pointer) : base(pointer) { }
}

public class StoreItemDatabase : Il2CppSystem.Object
{
    public StoreItemDatabase(IntPtr pointer) : base(pointer) { }
    public Il2CppList characterImprovementItems => null;
}

public class IIsStatApplicableChecker : Il2CppSystem.Object
{
    public IIsStatApplicableChecker(IntPtr pointer) : base(pointer) { }
}

public class IsStatApplicableChecker : Il2CppSystem.Object
{
    public IsStatApplicableChecker(IntPtr pointer) : base(pointer) { }
    public Il2CppSystem.Collections.Generic.List<StatType> nonSpellStats => null;
}

public class IStoreInventoryGeneratorFactory : Il2CppSystem.Object
{
    public IStoreInventoryGeneratorFactory(IntPtr pointer) : base(pointer) { }
}

public class StoreInventoryGeneratorFactory : Il2CppSystem.Object
{
    public StoreInventoryGeneratorFactory(IntPtr pointer) : base(pointer) { }
    public IGetStoreItems storeItemDatabase => null;
    public IIsStatApplicableChecker isStatApplicableChecker => null;
}

public class StoreManager : NetworkSingleton<StoreManager>
{
    public StoreManager(IntPtr pointer) : base(pointer) { }
    public IStoreInventoryGeneratorFactory storeInventoryGeneratorFactory => null;
    public bool IsShopPanelActive => false;
    public StoreGenerationType CurrentStoreType => default;
    public Il2CppList itemsCurrentlyInStore => null;
    public void ChooseShopItem(StoreItem item) { }
    public void ChooseCharacterImprovement(CharacterImprovementItem item) { }
}
