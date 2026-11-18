namespace SpellBrigadeDash.Game;

using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using SpellBrigadeDash.Core;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Blazing Dash's fire hurts enemies (rules agreed with Stefan, 18 Nov): enemies standing in a flame take a fire hit
/// every 0.5 s (<see cref="BlazeTicks"/>), up to three per 1.5 s trail. Base damage per tick = the game's own Trail
/// spell's base damage (or <c>Upgrades.BlazingDashDamage</c> when set), scaled by your character's Damage % (the stat
/// in the stats list), crits rolled by the game, the game's fire burn on top. Spell Size widens the flames.
/// The hit goes through the game's own path, <c>Hitbox.Hit</c> with a <c>PlayerSpellDamageInfo</c> (popups, crits,
/// resistances, burn), on enemies only (<c>Enemy.AllEnemies</c>), so never on you or teammates. No patches. Only your
/// own dashes deal damage, from your own PC, like the game's spells.
/// </summary>
internal sealed class BlazeDamage
{
    private const float ScanSeconds = 0.1f;
    private const float StatsSeconds = 2f;
    private const float SummarySeconds = 10f;
    private const float FallbackBaseDamage = 15f;
    private const float FlameRadiusFactor = 0.35f; // of the wizard's height, at fire scale 1
    private const float MaxTargetRadius = 3f;
    private const int MaxErrors = 5;
    private const int DetailedHits = 10;

    private readonly BlazeTicks _ticks = new(0.5f);
    private float _nextScan;
    private bool _off;
    private int _errors;

    private GameplayPlayerIdentity? _identity;
    private float? _trailBase;
    private string _baseSource = "";
    private float _nextStats;
    private float _damageMultiplier = 1f;
    private float _sizeMultiplier = 1f;
    private bool _statsLogged;
    private int _statsLogs;
    private SpellType _spell = SpellType.Trail;
    private bool _spellChosen;
    private bool _spellSwitched;
    private bool _crits = true;

    private int _detailed;
    private int _hits;
    private float _dealt;
    private readonly HashSet<long> _hitTargets = new();
    private float _nextSummary;

    public string Status { get; private set; } = "no Blazing Dash yet";

    /// <summary>Spell Size as a multiplier (1 = no bonus), from the game's Trail-spell value at your upgrades.</summary>
    public float SizeMultiplier => _sizeMultiplier;

    /// <summary>Damage reach of one flame around its centre on the ground, before the enemy's own size.</summary>
    public float FlameRadius(PlayerInfo p, float now)
    {
        RefreshStats(now);
        return FlameRadiusFactor * p.VisualSize * Math.Max(0.05f, ModConfig.GameFireScale.Value) * _sizeMultiplier;
    }

    public void AddFlame(Vector3 ground, float radius, float until)
    {
        if (!_off) _ticks.AddFlame(ground.x, ground.z, radius, until);
    }

    public void Clear()
    {
        _ticks.Clear();
        _identity = null;
    }

    /// <summary>Once per frame with game time (pauses with the game).</summary>
    public void Update(float now)
    {
        if (_off || now < _nextScan) return;
        _nextScan = now + ScanSeconds;
        _ticks.Prune(now);
        if (_ticks.FlameCount > 0)
        {
            try
            {
                Scan(now);
            }
            catch (Exception ex)
            {
                Fail("scanning for enemies in the fire", ex);
            }
        }

        if (_hits > 0 && now >= _nextSummary)
        {
            ModLog.Info($"Blazing Dash: {_hits} fire hit(s) on {_hitTargets.Count} enemy(ies) in the last {SummarySeconds:0} s, {_dealt:0} damage " +
                        "(with crits, before the burn that follows).");
            _hits = 0;
            _dealt = 0f;
            _hitTargets.Clear();
        }
    }

    private void Scan(float now)
    {
        var enemies = Enemy.AllEnemies;
        if (enemies == null || enemies.Count == 0) return;
        for (var i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e == null) continue;
            var pos = e.transform.position;
            if (!_ticks.InFlame(pos.x, pos.z, MaxTargetRadius, now)) continue; // nowhere near the fire
            var hb = e.hitbox;
            if (hb == null) continue;
            var health = hb.HealthContainer;
            if (health == null || health.CurrentHealth <= 0f) continue;
            Footprint(hb, pos, out var x, out var z, out var r);
            if (!_ticks.InFlame(x, z, r, now)) continue;
            if (!_ticks.TryHit(e.Pointer.ToInt64(), now)) continue;
            if (!Hit(e, hb, health, now)) return;
        }
    }

    private static void Footprint(Hitbox hb, Vector3 pos, out float x, out float z, out float r)
    {
        x = pos.x;
        z = pos.z;
        r = 0.3f;
        var cols = hb.colliders;
        if (cols == null || cols.Count == 0) return;
        var c = cols[0];
        if (c == null) return;
        var b = c.bounds;
        x = b.center.x;
        z = b.center.z;
        r = Math.Min(MaxTargetRadius, Math.Max(b.size.x, b.size.z) * 0.5f);
    }

    private bool Hit(Enemy e, Hitbox hb, HealthContainer health, float now)
    {
        RefreshStats(now);
        var stats = _identity?.stats;
        if (_identity == null || stats == null)
        {
            Status = "waiting for your wizard's stats";
            return false;
        }

        var perTick = BlazeMath.PerTick(BaseDamage(), _damageMultiplier);
        var before = health.CurrentHealth;
        try
        {
            hb.Hit(MakeInfo(_identity.OwnerClientId, stats, perTick));
        }
        catch (Exception ex)
        {
            if (!_spellSwitched && SwitchSpell(stats, ex)) return true;
            Fail("hitting an enemy", ex);
            return !_off;
        }

        var after = health.CurrentHealth;
        var done = Math.Max(0f, before - after);
        _hits++;
        _dealt += done;
        _hitTargets.Add(e.Pointer.ToInt64());
        if (_hits == 1 && _nextSummary <= now) _nextSummary = now + SummarySeconds;
        Status = $"burning: {perTick:0.#} per tick ({_baseSource} x{_damageMultiplier:0.##}), fire, crits {(_crits ? "on" : "off")}, as {_spell}";
        if (_detailed < DetailedHits)
        {
            _detailed++;
            // More than sent: a crit (your crit damage) or the enemy's own damage multiplier (weak spot, debuffs).
            var ratio = perTick > 0f ? done / perTick : 1f;
            ModLog.Info($"Blazing Dash hit '{Clean(e.gameObject.name)}': {perTick:0.#} fire damage sent, HP {before:0.#} -> {after:0.#}" +
                        (Math.Abs(ratio - 1f) > 0.05f && after > 0f ? $" (x{ratio:0.##})" : "") + ".");
        }

        return true;
    }

    private PlayerSpellDamageInfo MakeInfo(ulong owner, PlayerStats stats, float damage)
    {
        PlayerSpellDamageInfo info;
        if (_crits)
        {
            try
            {
                // The game's own constructor fills in crit chance and crit damage from your stats.
                info = new PlayerSpellDamageInfo(owner, _spell, new IPlayerStats(stats.Pointer));
            }
            catch (Exception ex)
            {
                _crits = false;
                ModLog.Warn($"Blazing Dash: the game's damage info with your stats failed ({Message(ex)}); hits go on without crits.");
                info = new PlayerSpellDamageInfo(owner, _spell);
            }
        }
        else
        {
            info = new PlayerSpellDamageInfo(owner, _spell);
        }

        info.Damage = damage;
        info.Element = ElementType.Fire;
        return info;
    }

    /// <summary>
    /// The hit is credited to the Trail spell. If the game can't take that (you don't own a Trail), credit your first
    /// spell instead; the damage itself doesn't change.
    /// </summary>
    private bool SwitchSpell(PlayerStats stats, Exception ex)
    {
        _spellSwitched = true;
        try
        {
            var equipped = stats.GetEquippedSpells();
            if (equipped == null || equipped.Count == 0) return false;
            var was = _spell;
            _spell = equipped[0];
            _crits = true;
            ModLog.Warn($"Blazing Dash: a fire hit credited to {was} failed ({Message(ex)}); crediting {_spell} from now on.");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private float BaseDamage()
    {
        var configured = ModConfig.BlazingDashDamage.Value;
        if (configured > 0f)
        {
            _baseSource = $"BlazingDashDamage {configured:0.#}";
            return configured;
        }

        _trailBase ??= FindTrailBase();
        return _trailBase.Value;
    }

    private float FindTrailBase()
    {
        try
        {
            foreach (var cfg in Resources.FindObjectsOfTypeAll<SpellConfiguration>())
            {
                if (cfg == null || cfg.SpellType != SpellType.Trail) continue;
                var list = cfg.Stats;
                if (list == null) continue;
                for (var i = 0; i < list.Count; i++)
                {
                    var s = list[i];
                    if (s == null || s.Stat != StatType.Damage || s.Value <= 0f) continue;
                    _baseSource = $"Trail base {s.Value:0.#}";
                    ModLog.Info($"Blazing Dash: base damage per tick = the game's Trail spell ('{cfg.name}'): {s.Value:0.##}.");
                    return s.Value;
                }
            }
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Blazing Dash: reading the Trail spell's damage failed ({Message(ex)}).");
        }

        _baseSource = $"fallback {FallbackBaseDamage:0}";
        ModLog.Warn($"Blazing Dash: the game's Trail spell wasn't found; using {FallbackBaseDamage:0} damage per tick.");
        return FallbackBaseDamage;
    }

    /// <summary>
    /// Your wizard's stats (found once, then read again every few seconds: upgrades change them). Damage % and Spell
    /// Size come from your character stats (the ones the stats list shows): the game's per-spell lookup
    /// (<c>GetSpellValue</c>) only knows the spells you own (18 Nov: "key 'Trail' was not present").
    /// </summary>
    private void RefreshStats(float now)
    {
        if (now < _nextStats && _identity != null) return;
        _nextStats = now + StatsSeconds;
        try
        {
            if (_identity == null || _identity.stats == null) _identity = FindLocalIdentity();
            var stats = _identity?.stats;
            if (stats == null) return;
            ChooseSpell(stats);
            var damage = StatMultiplier(stats, StatType.Damage, 0.2f, 50f, out var damageDetail);
            var size = StatMultiplier(stats, StatType.Size, 0.5f, 4f, out var sizeDetail);
            var changed = !_statsLogged || Math.Abs(damage - _damageMultiplier) > 0.005f || Math.Abs(size - _sizeMultiplier) > 0.005f;
            _damageMultiplier = damage;
            _sizeMultiplier = size;
            if (changed && _statsLogs < 12)
            {
                _statsLogged = true;
                _statsLogs++;
                ModLog.Info($"Blazing Dash scaling: Damage x{damage:0.###} ({damageDetail}), Spell Size x{size:0.###} ({sizeDetail}); " +
                            $"{BlazeMath.PerTick(BaseDamage(), damage):0.#} per tick, credited to {_spell}.");
            }
        }
        catch (Exception ex)
        {
            ModLog.WarnOnce("blaze-stats", $"Blazing Dash: reading your stats failed ({Message(ex)}); no Damage % / Spell Size bonus for now.");
        }
    }

    private static float StatMultiplier(PlayerStats stats, StatType type, float min, float max, out string detail)
    {
        detail = "not in your stats";
        var list = stats.GetPlayerStats();
        if (list == null) return 1f;
        for (var i = 0; i < list.Count; i++)
        {
            var s = list[i];
            if (s == null || s.StatType != type) continue;
            var calc = s.StatCalculationType;
            var b = s.BaseValue;
            var v = s.GetValue();
            var sum = s.GetSumOfModifiers();
            var raw = BlazeMath.StatMultiplier(calc == StatCalculationType.Flat, b, v, sum);
            var m = BlazeMath.Multiplier(1f, raw, min, max);
            detail = $"{calc}, base {b:0.##}, value {v:0.##}, modifiers {sum:0.##}" + (Math.Abs(m - raw) > 0.0001f ? $", {raw:0.##} ignored" : "");
            return m;
        }

        return 1f;
    }

    /// <summary>
    /// The game credits each hit to a spell and reads that spell's crit stats, and it only knows the spells you own:
    /// your Trail if you have one, else your first spell (its damage counter then includes Blazing Dash's).
    /// </summary>
    private void ChooseSpell(PlayerStats stats)
    {
        var equipped = stats.GetEquippedSpells();
        if (equipped == null || equipped.Count == 0) return;
        var pick = equipped.Contains(SpellType.Trail) ? SpellType.Trail : equipped[0];
        if (pick == _spell && _spellChosen) return;
        _spellChosen = true;
        _spell = pick;
        _crits = true;
        ModLog.Info($"Blazing Dash: fire hits are credited to your {pick} spell{(pick == SpellType.Trail ? "" : " (you have no Trail)")}.");
    }

    private static GameplayPlayerIdentity? FindLocalIdentity()
    {
        var found = Object.FindObjectsOfType(Il2CppType.Of<GameplayPlayerIdentity>(), false);
        if (found == null) return null;
        foreach (var obj in found)
        {
            var id = obj?.TryCast<GameplayPlayerIdentity>();
            if (id != null && id.IsOwner) return id;
        }

        return null;
    }

    private void Fail(string what, Exception ex)
    {
        _errors++;
        ModLog.Warn($"Blazing Dash: {what} failed ({_errors}/{MaxErrors}): {Message(ex)}");
        if (_errors < MaxErrors) return;
        _off = true;
        _ticks.Clear();
        Status = "off after errors (see the log); the trail still shows";
        ModLog.Error("Blazing Dash damage switched off for this session after repeated errors; the fire trail still shows.");
    }

    private static string Message(Exception ex) => (ex.InnerException ?? ex).Message;

    private static string Clean(string name)
    {
        var n = name.Replace("(Clone)", "").Trim();
        var paren = n.IndexOf(" (", StringComparison.Ordinal);
        return paren > 0 ? n.Substring(0, paren) : n;
    }
}
