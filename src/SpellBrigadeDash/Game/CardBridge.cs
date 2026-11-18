namespace SpellBrigadeDash.Game;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using SpellBrigadeDash.Core;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

/// <summary>
/// Puts the dash cards (<see cref="DashCards"/>) into the game's own level-up shop.
///
/// How the game's shop works (game 1.2.1, from the interop): every client keeps a <c>StoreItemDatabase</c> whose
/// <c>characterImprovementItems</c> list holds one <c>CharacterImprovementItem</c> per character stat and rarity.
/// On a level-up each client's <c>ImprovementInventoryGenerator</c> rolls a rarity per slot and picks items of that
/// rarity from that list (after filters such as <c>IsStatApplicableChecker</c>). The chosen card goes through
/// <c>StoreManager.ChooseShopItem(StoreItem)</c>, which for a character card adds a stat modifier (stat, value).
///
/// So, with no patches on the shop itself:
///  * our cards are extra <c>CharacterImprovementItem</c>s in that list, kept in sync with which dash cards are
///    available (<see cref="DashCards.Available"/>); a card's weight is the number of copies;
///  * the game decides "same card" (one per shop, bans) by the stat, so each dash card carries its own stats with
///    value 0 (<see cref="ShopCarriers"/>): between shops a "rest" stat with no real card (added to the checker's
///    <c>nonSpellStats</c> so it isn't filtered out), so a dash card is never "the same" as a real card or another
///    dash card;
///  * while a shop is open (and inside ChooseShopItem) they carry their "shop" stat instead: picking any card logs an
///    analytics event that parses EVERY card's stat as the enum <c>ImprovementStat</c> (16 stats); HealOnDodge made the
///    game's ChooseShopItem throw halfway (0.4.0 test, 17 Nov). The shop stat + 0 is a no-op when the game applies our
///    picked card, and the rest stats are never applied;
///  * titles/descriptions are entries added to the game's own string table (the one holding the real cards' keys);
///  * the pick is seen by ONE prefix on <c>StoreManager.ChooseShopItem</c> (through <see cref="NativeCode.CanPatch"/>),
///    installed as soon as the shop exists (the first shop opens ~1.5 s into a run), which applies our card.
/// </summary>
internal static class CardBridge
{
    private const string KeyPrefix = "SBD_Card_";
    private const float SyncInterval = 0.5f;
    private const float TextCheckInterval = 10f;

    private static readonly Dictionary<IntPtr, DashCardId> OurItems = new();
    private static readonly List<CharacterImprovementItem> OurList = new();
    private static bool _carryingSafeStat;
    private static StoreManager? _liveManager;

    private static StoreItemDatabase? _db;
    private static IntPtr _dbList;
    private static string _signature = "";
    private static float _nextSync;
    private static float _nextTextCheck;
    private static bool _realItemsLogged;
    private static bool _shopWasOpen;
    private static bool _hookInstalled;
    private static Sprite? _icon;
    private static string? _textTable;
    private static bool _instanceBroken;
    private static StoreManager? _manager;
    private static float _nextManagerSearch;

    /// <summary>Called when the local player picks one of our cards in the shop.</summary>
    public static Action<DashCardId>? Picked { get; set; }

    public static bool IsActive => _db != null && OurItems.Count > 0;

    public static string Status { get; private set; } = "waiting for a run";

    public static List<string> Log { get; } = new();

    /// <summary>
    /// Every frame: switch our cards' stat when a shop opens/closes. Every 0.5 s: keep the game's item list in step
    /// with the cards that are available right now.
    /// </summary>
    public static void Update(IReadOnlyList<DashCardDef> catalogue, DashProgress progress, float now)
    {
        try
        {
            if (_liveManager != null) WatchShop(_liveManager);
        }
        catch (Exception ex)
        {
            ModLog.ErrorThrottled("CardBridge.WatchShop", ex, now, 30f);
        }

        if (now < _nextSync) return;
        _nextSync = now + SyncInterval;
        try
        {
            var manager = FindStoreManager();
            _liveManager = manager;
            if (manager == null)
            {
                if (_db != null) Forget("left the run");
                return;
            }

            var db = FindDatabase(manager);
            if (db == null) return;
            var list = db.characterImprovementItems;
            if (list == null) return;

            if (_db == null || _db.Pointer != db.Pointer || _dbList != list.Pointer)
            {
                _db = db;
                _dbList = list.Pointer;
                _signature = "";
                InstallPickHook(); // before our cards can be offered: the first shop opens ~1.5 s into a run
                LogRealItems(list);
                MakeStatApplicable(manager);
                ProbeEquivalence(list);
            }

            if (now >= _nextTextCheck)
            {
                _nextTextCheck = now + TextCheckInterval;
                EnsureTexts(catalogue, list);
            }

            var wanted = ModConfig.ShopCards.Value ? DashCards.Available(catalogue, progress).ToList() : new List<DashCardDef>();
            var signature = string.Join(",", wanted.Select(c => $"{c.Id}:{Rarity(c)}x{Copies(c)}"));
            if (signature != _signature)
            {
                _signature = signature;
                Rebuild(list, wanted);
            }
        }
        catch (Exception ex)
        {
            ModLog.ErrorThrottled("CardBridge", ex, now, 30f);
        }
    }

    /// <summary>The one patch: a prefix on StoreManager.ChooseShopItem that notices our cards being picked.</summary>
    public static void InstallPickHook()
    {
        if (_hookInstalled || ModConfig.SafeMode.Value || !ModConfig.ShopCards.Value) return;
        _hookInstalled = true;

        var prefix = new HarmonyMethod(typeof(CardBridge).GetMethod(nameof(ChooseShopItemPrefix), BindingFlags.Static | BindingFlags.NonPublic));
        foreach (var name in new[] { "ChooseShopItem", "ChooseCharacterImprovement" })
        {
            var m = typeof(StoreManager).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .FirstOrDefault(x => x.Name == name && x.GetParameters().Length == 1);
            if (m == null) continue;

            var label = $"StoreManager.{name}({m.GetParameters()[0].ParameterType.Name})";
            if (!NativeCode.CanPatch(m, label, out var why))
            {
                Note($"Pick hook: not patching {label}: {why}");
                continue;
            }

            try
            {
                ModLog.Step($"card pick patch: {label} ({why})");
                Plugin.Harmony.Patch(m, prefix: prefix);
                ModLog.Step("card pick patch ok");
                Note($"Pick hook installed on {label} ({why}).");
                return;
            }
            catch (Exception ex)
            {
                Note($"Pick hook: patching {label} failed: {ex.Message}");
            }
        }

        Status = "no pick hook: dash cards can show up but picking them does nothing";
        ModLog.Warn("Dash cards: " + Status);
    }

    // ------------------------------------------------------------------------------------------ finding the shop

    private static StoreManager? FindStoreManager()
    {
        if (!_instanceBroken)
        {
            try
            {
                return NetworkSingleton<StoreManager>.Instance;
            }
            catch (Exception ex)
            {
                _instanceBroken = true;
                ModLog.Warn($"Dash cards: NetworkSingleton<StoreManager>.Instance failed ({ex.Message}); looking the manager up by type instead.");
            }
        }

        // Fallback: a (slow) scene search, cached while the manager lives and retried at most every few seconds.
        if (_manager != null) return _manager;
        var now = Time.unscaledTime;
        if (now < _nextManagerSearch) return null;
        _nextManagerSearch = now + 3f;
        var found = Object.FindObjectsOfType(Il2CppInterop.Runtime.Il2CppType.Of<StoreManager>());
        _manager = found != null && found.Length > 0 ? found[0].TryCast<StoreManager>() : null;
        return _manager;
    }

    private static StoreItemDatabase? FindDatabase(StoreManager manager)
    {
        var factory = manager.storeInventoryGeneratorFactory?.TryCast<StoreInventoryGeneratorFactory>();
        var db = factory?.storeItemDatabase?.TryCast<StoreItemDatabase>();
        if (db == null) ModLog.InfoOnce("store-db-missing", "Dash cards: the shop's item database isn't there yet.");
        return db;
    }

    /// <summary>One-off log of the game's own character cards: keys, stat, value and rarity (to check value semantics).</summary>
    private static void LogRealItems(Il2CppSystem.Collections.Generic.List<StoreItem> list)
    {
        if (_realItemsLogged) return;
        _realItemsLogged = true;
        if (!ModConfig.DiscoveryEnabled.Value) return; // diagnostics only
        var sb = new StringBuilder($"Dash cards: the game's shop has {list.Count} character card(s):");
        for (var i = 0; i < list.Count; i++)
        {
            var item = list[i]?.TryCast<CharacterImprovementItem>();
            if (item == null || OurItems.ContainsKey(item.Pointer)) continue;
            sb.Append($"\n    {item.TitleEntry} | {item.DescriptionEntry} | {item.Stat} {item.Value:0.###} {item.Rarity} | limit {item.statLimitForOffer:0.##} | icon {(item.Icon != null ? item.Icon.name : "-")}");
        }

        Note(sb.ToString());
    }

    /// <summary>
    /// The generator drops stats no equipped spell uses unless they're in the checker's nonSpellStats list, so every
    /// card's rest stat is added there (they're not spell stats, so spell upgrades are unaffected).
    /// </summary>
    private static void MakeStatApplicable(StoreManager manager)
    {
        try
        {
            var factory = manager.storeInventoryGeneratorFactory?.TryCast<StoreInventoryGeneratorFactory>();
            var checker = factory?.isStatApplicableChecker?.TryCast<IsStatApplicableChecker>();
            var list = checker?.nonSpellStats;
            if (list == null)
            {
                Note("Shop stats: IsStatApplicableChecker.nonSpellStats not found; the dash cards may be filtered out.");
                return;
            }

            var before = new List<string>();
            for (var i = 0; i < list.Count; i++) before.Add(list[i].ToString());
            var added = new List<string>();
            foreach (DashCardId id in Enum.GetValues(typeof(DashCardId)))
            {
                var stat = RestStat(id);
                if (list.Contains(stat)) continue;
                list.Add(stat);
                added.Add(stat.ToString());
            }

            Note($"Shop stats: the game's non-spell stats were [{string.Join(", ", before)}]; added [{string.Join(", ", added)}] " +
                 "so the dash cards aren't filtered out.");
        }
        catch (Exception ex)
        {
            Note($"Shop stats: couldn't update nonSpellStats ({ex.Message}).");
        }
    }

    // ------------------------------------------------------------------------------------------ our items

    private static void Rebuild(Il2CppSystem.Collections.Generic.List<StoreItem> list, List<DashCardDef> wanted)
    {
        foreach (var item in OurList) list.Remove(item);
        OurList.Clear();
        OurItems.Clear();

        _icon ??= PickIcon(list);
        foreach (var card in wanted)
        {
            var copies = Copies(card);
            for (var i = 0; i < copies; i++)
            {
                var stat = _carryingSafeStat ? ShopStat(card.Id) : RestStat(card.Id);
                var icon = CardIcons.For(card.Id) ?? _icon;
                var item = new CharacterImprovementItem(TitleKey(card.Id), DescriptionKey(card.Id), stat, 0f, Rarity(card), icon!, 1e9f);
                list.Add(item);
                OurList.Add(item);
                OurItems[item.Pointer] = card.Id;
            }
        }

        Status = wanted.Count == 0
            ? "active, no dash card available right now"
            : $"active: {string.Join(", ", wanted.Select(c => $"{c.Title} ({Rarity(c)} x{Copies(c)})"))} in the shop pool" +
              (ModConfig.ShopCardTestMode.Value ? " [TEST MODE: Common, many copies]" : "");
        Note("Dash cards: " + Status);
    }

    /// <summary>
    /// Switch our cards between their rest stat (between shops) and their shop stat (while a shop is open or a card is
    /// being chosen), see <see cref="ShopCarriers"/>. Only our own objects are touched.
    /// </summary>
    private static void SetCarrier(bool safe, string why)
    {
        if (_carryingSafeStat == safe) return;
        _carryingSafeStat = safe;
        foreach (var item in OurList)
        {
            try
            {
                if (OurItems.TryGetValue(item.Pointer, out var id)) item._Stat_k__BackingField = safe ? ShopStat(id) : RestStat(id);
            }
            catch
            {
                // the item died with its database; Rebuild replaces it
            }
        }

        ModLog.StepOnce("carrier-" + safe, $"cards: carrier stats -> {(safe ? "shop" : "rest")} stats ({why})");
    }

    /// <summary>
    /// One-off check of how the game compares cards (logged): two dash cards with their own rest stats (should be
    /// different cards), two copies with the same stat (the same card), and a Dodge-carrying card vs the real Dodge card.
    /// </summary>
    private static void ProbeEquivalence(Il2CppSystem.Collections.Generic.List<StoreItem> list)
    {
        try
        {
            _icon ??= PickIcon(list);
            CharacterImprovementItem? realDodge = null;
            for (var i = 0; i < list.Count && realDodge == null; i++)
            {
                var it = list[i]?.TryCast<CharacterImprovementItem>();
                if (it != null && !OurItems.ContainsKey(it.Pointer) && it.Stat == StatType.Dodge && it.Rarity == RarityType.Common) realDodge = it;
            }

            var a = new CharacterImprovementItem("SBD_Probe_A", "SBD_Probe_A", RestStat(DashCardId.ExtraCharge), 0f, RarityType.Common, _icon!, 1e9f);
            var a2 = new CharacterImprovementItem("SBD_Probe_A2", "SBD_Probe_A2", RestStat(DashCardId.ExtraCharge), 0f, RarityType.Epic, _icon!, 1e9f);
            var b = new CharacterImprovementItem("SBD_Probe_B", "SBD_Probe_B", RestStat(DashCardId.QuickRecovery), 0f, RarityType.Common, _icon!, 1e9f);
            var d = new CharacterImprovementItem("SBD_Probe_D", "SBD_Probe_D", StatType.Dodge, 0f, RarityType.Common, _icon!, 1e9f);
            var sb = new StringBuilder("Shop card comparison (game's own rules): ");
            sb.Append($"two different dash cards: equivalent={a.IsEquivalentIgnoringRarity(b)}, same-for-bans={CharacterImprovementBanRule.IsSame(a, b)}");
            sb.Append($"; two copies of one dash card: equivalent={a.IsEquivalentIgnoringRarity(a2)}, same-for-bans={CharacterImprovementBanRule.IsSame(a, a2)}");
            if (realDodge != null)
                sb.Append($"; a Dodge-carrying dash card vs the real Dodge card: equivalent={d.IsEquivalentIgnoringRarity(realDodge)}, " +
                          $"same-for-bans={CharacterImprovementBanRule.IsSame(d, realDodge)}");
            Note(sb.ToString());
        }
        catch (Exception ex)
        {
            Note($"Shop card comparison failed: {ex.Message}");
        }
    }

    private static Sprite? PickIcon(Il2CppSystem.Collections.Generic.List<StoreItem> list)
    {
        Sprite? any = null;
        for (var i = 0; i < list.Count; i++)
        {
            var item = list[i]?.TryCast<CharacterImprovementItem>();
            if (item?.Icon == null) continue;
            if (item.Stat == StatType.Dodge || item.Stat == StatType.MovementSpeed) return item.Icon;
            any ??= item.Icon;
        }

        return any;
    }

    private static void Forget(string why)
    {
        // OurList is kept on purpose: if the game reuses the same item database next run, Rebuild removes our old
        // items from it before adding new ones (otherwise they'd pile up run after run).
        _db = null;
        _dbList = IntPtr.Zero;
        _signature = "";
        _manager = null;
        _liveManager = null;
        _shopWasOpen = false;
        Status = "waiting for a run (" + why + ")";
    }

    // ------------------------------------------------------------------------------------------ texts

    /// <summary>Add our titles/descriptions to every loaded string table that holds the game's own card keys.</summary>
    private static void EnsureTexts(IReadOnlyList<DashCardDef> catalogue, Il2CppSystem.Collections.Generic.List<StoreItem> list)
    {
        string? realKey = null;
        for (var i = 0; i < list.Count && realKey == null; i++)
        {
            var item = list[i];
            if (item != null && !OurItems.ContainsKey(item.Pointer)) realKey = item.TitleEntry;
        }

        if (string.IsNullOrEmpty(realKey)) return;
        var added = 0;
        var tables = 0;
        foreach (var table in Resources.FindObjectsOfTypeAll<StringTable>())
        {
            if (table == null || table.SharedData == null || !table.SharedData.Contains(realKey)) continue;
            tables++;
            _textTable = $"{table.TableCollectionName} ({table.LocaleIdentifier.Code})";
            foreach (var card in catalogue)
            {
                if (table.GetEntry(TitleKey(card.Id)) == null)
                {
                    table.AddEntry(TitleKey(card.Id), card.Title);
                    added++;
                }

                if (table.GetEntry(DescriptionKey(card.Id)) == null)
                {
                    table.AddEntry(DescriptionKey(card.Id), card.Description);
                    added++;
                }
            }
        }

        if (added > 0) Note($"Shop text: added {added} entries to {tables} string table(s), e.g. {_textTable}.");
        else if (tables == 0) ModLog.WarnOnce("shop-text", $"Shop text: no loaded string table holds the game's key '{realKey}'; dash cards will show their raw keys.");
    }

    // ------------------------------------------------------------------------------------------ watching + picking

    private static void WatchShop(StoreManager manager)
    {
        bool open;
        try
        {
            open = manager.IsShopPanelActive;
        }
        catch
        {
            return;
        }

        // Our cards must carry an analytics-safe stat for as long as the shop is open (picks, rerolls and bans log
        // every card in the shop), and their own carrier stat again before the next shop is generated.
        if (open) SetCarrier(true, "shop open");
        else if (_carryingSafeStat) SetCarrier(false, "shop closed");

        if (open && !_shopWasOpen && ModConfig.DiscoveryEnabled.Value) // diagnostics only
        {
            var items = manager.itemsCurrentlyInStore;
            var sb = new StringBuilder($"Shop opened ({manager.CurrentStoreType}):");
            if (items != null)
                for (var i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    if (it == null) continue;
                    var ours = OurItems.TryGetValue(it.Pointer, out var id);
                    var imp = it.TryCast<ImprovementItem>();
                    sb.Append($"\n    {(ours ? "[DASH] " + id : it.TitleEntry)}{(imp != null ? $" | {imp.Stat} {imp.Value:0.###} {imp.Rarity}" : "")}");
                }

            Note(sb.ToString());
        }

        _shopWasOpen = open;
    }

    /// <summary>
    /// Runs before the game's ChooseShopItem (for every pick, ours or not): make our cards analytics-safe (a backstop
    /// for picks without an open panel, e.g. automatic picks), then apply our card if it is one. Applying it here,
    /// before the game's own code, means a later error in the game can't swallow the pick.
    /// </summary>
    private static void ChooseShopItemPrefix(StoreItem __0)
    {
        try
        {
            SetCarrier(true, "card chosen");
            if (__0 == null || !OurItems.TryGetValue(__0.Pointer, out var id)) return;
            Note($"Shop: you picked the dash card {id}.");
            Picked?.Invoke(id);
        }
        catch (Exception ex)
        {
            ModLog.ErrorThrottled("CardBridge.Pick", ex, Time.unscaledTime, 5f);
        }
    }

    // ------------------------------------------------------------------------------------------ helpers

    private static string TitleKey(DashCardId id) => KeyPrefix + id + "_Title";

    private static string DescriptionKey(DashCardId id) => KeyPrefix + id + "_Description";

    /// <summary>The card's stat between shops (see <see cref="ShopCarriers"/>).</summary>
    private static StatType RestStat(DashCardId id) => ParseStat(ShopCarriers.RestStat(id), StatType.HealOnDodge);

    /// <summary>The card's stat while a shop is open / a card is picked: one the game's analytics accept.</summary>
    private static StatType ShopStat(DashCardId id) => ParseStat(ShopCarriers.ShopStat(id), StatType.Dodge);

    private static StatType ParseStat(string name, StatType fallback)
    {
        if (Enum.TryParse<StatType>(name, false, out var stat)) return stat;
        ModLog.WarnOnce("shop-stat-" + name, $"Dash cards: the game has no stat '{name}' (game update?); using {fallback}.");
        return fallback;
    }

    private static RarityType Rarity(DashCardDef card)
    {
        if (ModConfig.ShopCardTestMode.Value) return RarityType.Common;
        return Enum.TryParse<RarityType>(card.Rarity, true, out var r) ? r : RarityType.Epic;
    }

    private static int Copies(DashCardDef card)
        => ModConfig.ShopCardTestMode.Value ? 12 : Math.Max(1, (int)Math.Round(card.Weight));

    private static void Note(string msg)
    {
        Log.Add($"{DateTime.Now:HH:mm:ss} {msg}");
        if (Log.Count > 60) Log.RemoveAt(0);
        ModLog.Info(msg);
    }
}
