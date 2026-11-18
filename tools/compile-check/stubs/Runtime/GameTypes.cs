// COMPILE-CHECK STUB ONLY - the game classes SpellBrigadeDash uses with real types (the level-up shop, see
// Game/CardBridge.cs), with the same names, namespaces and member names as the real interop
// (BepInEx/interop/Runtime.dll, Unity.Netcode.Runtime.dll, Unity.Localization.dll, Unity.TextMeshPro.dll). Bodies are dummies.
// Also the stats list (Game/StatPanelRows.cs).
using System;
using UnityEngine;
using Il2CppList = Il2CppSystem.Collections.Generic.List<StoreItem>;

namespace Unity.Netcode
{
    public class NetworkBehaviour : MonoBehaviour
    {
        public NetworkBehaviour(IntPtr pointer) : base(pointer) { }
        public bool IsOwner => false;
        public ulong OwnerClientId => 0;
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

namespace UnityEngine.Localization.Components
{
    public class LocalizeStringEvent : MonoBehaviour
    {
        public LocalizeStringEvent(IntPtr pointer) : base(pointer) { }
        public void RefreshString() { }
    }
}

namespace TMPro
{
    public class TMP_Text : UnityEngine.UI.MaskableGraphic
    {
        public TMP_Text(IntPtr pointer) : base(pointer) { }
        public string text { get; set; }
    }
}

public class StatPanel : MonoBehaviour
{
    public StatPanel(IntPtr pointer) : base(pointer) { }
}

public class PlayerStatEntry : MonoBehaviour
{
    public PlayerStatEntry(IntPtr pointer) : base(pointer) { }
    public UnityEngine.UI.Image statIcon => null;
    public UnityEngine.Localization.Components.LocalizeStringEvent statTitle => null;
    public TMPro.TMP_Text valueText => null;
}

// The game's damage numbers (Discovery/DamageReference.cs).
public enum SpellType
{
    Orb, Boomerang, EnergyZone, Laser, Proximity, Shotgun, Rifle, Smg, Trail, BombLauncher, FallingStar, Mine, Fence, Beam,
    Arrow, Plant, Summon, Slash, Meteor, Book, Bell, Clap, Dice, Splinter, Sawblade,
}

public class BaseStat : Il2CppSystem.Object
{
    public BaseStat(IntPtr pointer) : base(pointer) { }
    public StatType Stat => default;
    public float Value => 0f;
}

public class SpellConfiguration : ScriptableObject
{
    public SpellConfiguration(IntPtr pointer) : base(pointer) { }
    public SpellType SpellType => default;
    public Il2CppSystem.Collections.Generic.List<BaseStat> Stats => null;
}

public class FireEffectConfiguration : Il2CppSystem.Object
{
    public FireEffectConfiguration(IntPtr pointer) : base(pointer) { }
    public float BurnDamageFactor => 0f;
    public int NumberOfBurns => 0;
    public float SecondsBetweenBurns => 0f;
}

public class FireDoubleEffectConfiguration : Il2CppSystem.Object
{
    public FireDoubleEffectConfiguration(IntPtr pointer) : base(pointer) { }
    public float BurnDamageFactor => 0f;
    public int NumberOfBurns => 0;
    public float SecondsBetweenBurns => 0f;
}

public class ElementEffectConfiguration : ScriptableObject
{
    public ElementEffectConfiguration(IntPtr pointer) : base(pointer) { }
    public FireEffectConfiguration FireEffectConfiguration => null;
    public FireDoubleEffectConfiguration FireDoubleEffectConfiguration => null;
}

public class LavaZoneConfiguration : ScriptableObject
{
    public LavaZoneConfiguration(IntPtr pointer) : base(pointer) { }
    public float TimeBetweenTicks => 0f;
    public int BaseDamageForPlayer => 0;
    public int BaseDamageForEnemy => 0;
    public float GetEnemyDamageForEnemy(int tick) => 0f;
}

public class MeteorDamageArea : MonoBehaviour
{
    public MeteorDamageArea(IntPtr pointer) : base(pointer) { }
    public int numberOfDamageTicks => 0;
    public float secondsBetweenDamageTicks => 0f;
    public float damageRadiusMultiplier => 0f;
    public float damageDivider => 0f;
    public float damageRadius => 0f;
}

public class HealthContainer : Unity.Netcode.NetworkBehaviour
{
    public HealthContainer(IntPtr pointer) : base(pointer) { }
    public float MaxHealth => 0f;
    public float CurrentHealth => 0f;
}

public class EnemyHealthContainer : HealthContainer
{
    public EnemyHealthContainer(IntPtr pointer) : base(pointer) { }
    public float maxHealth => 0f;
    public bool IsBoss => false;
}

// Blazing Dash's fire damage (Game/BlazeDamage.cs).
public enum ElementType
{
    None, Fire, FireDouble, Lightning, LightningDouble, FireLightning, Acid, AcidDouble, AcidFire, AcidLightning, Ice, IceDouble,
    FireIce, AcidIce, IceLightning, Dark, DarkDouble, DarkFire, DarkLightning, AcidDark, DarkIce,
}

public class DamageInfo : Il2CppSystem.Object
{
    public DamageInfo(IntPtr pointer) : base(pointer) { }
    public float Damage { get; set; }
    public ElementType Element { get; set; }
}

public class PlayerDamageInfo : DamageInfo
{
    public PlayerDamageInfo(IntPtr pointer) : base(pointer) { }
}

public class PlayerSpellDamageInfo : PlayerDamageInfo
{
    public PlayerSpellDamageInfo(IntPtr pointer) : base(pointer) { }
    public PlayerSpellDamageInfo(ulong ownerClientId, SpellType spell) : base(IntPtr.Zero) { }
    public PlayerSpellDamageInfo(ulong ownerClientId, SpellType spell, IPlayerStats stats) : base(IntPtr.Zero) { }
}

public class IPlayerStats : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
{
    public IPlayerStats(IntPtr pointer) : base(pointer) { }
}

public enum StatCalculationType { PercentAdd, Flat, PercentAddLogarithmic }

public class Stat : Il2CppSystem.Object
{
    public Stat(IntPtr pointer) : base(pointer) { }
    public StatType StatType => default;
    public float BaseValue => 0f;
    public StatCalculationType StatCalculationType => default;
    public float GetValue() => 0f;
    public float GetSumOfModifiers() => 0f;
}

public class PlayerStats : Unity.Netcode.NetworkBehaviour
{
    public PlayerStats(IntPtr pointer) : base(pointer) { }
    public Il2CppSystem.Collections.Generic.List<Stat> GetPlayerStats() => null;
    public float GetCharacterValue(StatType stat) => 0f;
    public float GetSpellValue(SpellType spell, StatType stat, float baseValue) => 0f;
    public Il2CppSystem.Collections.Generic.List<SpellType> GetEquippedSpells() => null;
}

public class GameplayPlayerIdentity : Unity.Netcode.NetworkBehaviour
{
    public GameplayPlayerIdentity(IntPtr pointer) : base(pointer) { }
    public PlayerStats stats => null;
}

public class Hitbox : Unity.Netcode.NetworkBehaviour
{
    public Hitbox(IntPtr pointer) : base(pointer) { }
    public HealthContainer HealthContainer => null;
    public Il2CppSystem.Collections.Generic.List<Collider> colliders => null;
    public void Hit(DamageInfo info) { }
}

public class Enemy : Unity.Netcode.NetworkBehaviour
{
    public Enemy(IntPtr pointer) : base(pointer) { }
    public static Il2CppSystem.Collections.Generic.List<Enemy> AllEnemies => null;
    public Hitbox hitbox => null;
}

public class PauseMenuManager : MonoBehaviour
{
    public PauseMenuManager(IntPtr pointer) : base(pointer) { }
    public bool IsOpen => false;
}

public class StatPanel_Player : StatPanel
{
    public StatPanel_Player(IntPtr pointer) : base(pointer) { }
    public Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<PlayerStatEntry> entries => null;
}

public class NetworkSingleton<T> : Unity.Netcode.NetworkBehaviour
{
    public NetworkSingleton(IntPtr pointer) : base(pointer) { }
    public static T Instance => default;
}

public class SingletonPersistent<T> : MonoBehaviour
{
    public SingletonPersistent(IntPtr pointer) : base(pointer) { }
    public static T Instance => default;
}

public class RunData : SingletonPersistent<RunData>
{
    public RunData(IntPtr pointer) : base(pointer) { }
    public string RunId => null;
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
