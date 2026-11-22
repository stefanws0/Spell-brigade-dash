namespace SpellBrigadeDash.Game;

using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

/// <summary>
/// Blazing Dash's trail, made of the game's own fire. Every wizard carries a <c>FireVisualizer</c> (under its
/// "Visual" child) with the VFX Graph effect <c>Effect_Fire</c> that the game shows when a wizard burns. We clone the
/// local wizard's FireVisualizer, stand the clones along the dash path, and drive them with the game's own
/// <c>FireVisualizer.PlayEffect(FireTheme)</c> / <c>StopEffect()</c> (scale in, burn, scale out).
///
/// These are plain calls on our own copies: no Harmony patches. Clones are pooled, because creating a VisualEffect
/// allocates its particle buffers. If anything is missing or throws, <see cref="TrySpawn"/> returns false and the
/// caller falls back to the mod's own sprite flames.
/// </summary>
internal sealed class GameFireTrail
{
    private const int MaxFlames = 24;
    private const float ReuseDelay = 1.25f; // StopEffect scales the fire out first; reuse a clone only after that

    private readonly List<Flame> _flames = new();
    private readonly Random _rng = new();
    private Template? _template;
    private long _templateFor;
    private bool _broken;
    private bool _activationWarned;

    public string Status { get; private set; } = "not used yet";

    /// <summary>False once the game's fire turned out to be unusable (then the sprite flames are used).</summary>
    public bool Usable => !_broken && ModConfig.GameFireTrail.Value;

    /// <summary>
    /// Light one of the game's fires at <paramref name="ground"/> for <paramref name="life"/> seconds (game time),
    /// <paramref name="sizeScale"/> times its normal size (Spell Size).
    /// </summary>
    public bool TrySpawn(PlayerInfo p, Vector3 ground, Transform parent, float life, float now, float sizeScale = 1f)
    {
        if (!Usable) return false;
        try
        {
            var t = GetTemplate(p);
            if (t == null) return false;

            var flame = FreeFlame(now) ?? (_flames.Count < MaxFlames ? Create(t, parent) : null);
            if (flame == null) return true; // pool exhausted: skip this one rather than mixing in sprite flames

            var tr = flame.Go.transform;
            tr.position = ground + t.Offset;
            tr.rotation = t.Rotation * Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f);
            var scale = Math.Max(0.05f, ModConfig.GameFireScale.Value) * Mathf.Clamp(sizeScale, 0.5f, 4f) * (0.9f + 0.2f * (float)_rng.NextDouble());
            tr.localScale = t.Scale * scale;

            ModLog.StepOnce("game-fire-play", "fx: first game fire (FireVisualizer.PlayEffect)");
            flame.Visualizer.PlayEffect(t.Theme);
            EnsureEffectActive(flame);
            flame.Burning = true;
            flame.StopAt = now + Math.Max(0.1f, life);
            return true;
        }
        catch (Exception ex)
        {
            Disable("lighting a fire failed: " + ex.Message);
            return false;
        }
    }

    /// <summary>Put out fires whose time is up (the game scales them out). Call once per frame with game time.</summary>
    public void Update(float now)
    {
        if (_flames.Count == 0) return;
        for (var i = _flames.Count - 1; i >= 0; i--)
        {
            var f = _flames[i];
            if (f.Go == null)
            {
                _flames.RemoveAt(i); // destroyed by a scene change
                continue;
            }

            if (!f.Burning || now < f.StopAt) continue;
            f.Burning = false;
            f.FreeAt = now + ReuseDelay;
            try
            {
                f.Visualizer.StopEffect();
            }
            catch (Exception ex)
            {
                Object.Destroy(f.Go);
                _flames.RemoveAt(i);
                ModLog.WarnOnce("game-fire-stop", "Game fire trail: StopEffect failed (" + ex.Message + "); that fire was removed.");
            }
        }
    }

    /// <summary>Remove every fire (new run, new wizard).</summary>
    public void Clear()
    {
        foreach (var f in _flames)
        {
            try
            {
                if (f.Go != null) Object.Destroy(f.Go);
            }
            catch
            {
                // already gone
            }
        }

        _flames.Clear();
        _template = null;
        _templateFor = 0;
    }

    // ------------------------------------------------------------------------------------------

    private Flame? FreeFlame(float now)
    {
        foreach (var f in _flames)
            if (!f.Burning && now >= f.FreeAt && f.Go != null)
                return f;
        return null;
    }

    private Flame? Create(Template t, Transform parent)
    {
        ModLog.StepOnce("game-fire-clone", "fx: cloning the wizard's FireVisualizer");
        var go = Object.Instantiate(t.Source, parent);
        go.name = "SBD_GameFire";

        var visualizer = go.GetComponent<FireVisualizer>();
        if (visualizer == null)
        {
            Object.Destroy(go);
            Disable("the cloned FireVisualizer couldn't be read");
            return null;
        }

        GameObject? effect = null;
        try
        {
            var vfx = visualizer.fireEffect;
            if (vfx != null) effect = vfx.gameObject;
        }
        catch
        {
            // optional: only used to switch the effect on if PlayEffect didn't
        }

        var flame = new Flame(go, visualizer, effect);
        _flames.Add(flame);
        return flame;
    }

    /// <summary>
    /// The game's PlayEffect is expected to switch Effect_Fire on. If it didn't (e.g. the game activates it elsewhere),
    /// switch it on ourselves so the trail still shows; logged once so it can be checked.
    /// </summary>
    private void EnsureEffectActive(Flame f)
    {
        if (f.Effect == null || f.Effect.activeSelf) return;
        f.Effect.SetActive(true);
        if (_activationWarned) return;
        _activationWarned = true;
        ModLog.Info("Game fire trail: PlayEffect left Effect_Fire switched off; the mod switches it on itself.");
    }

    private Template? GetTemplate(PlayerInfo p)
    {
        if (_template != null && _templateFor == p.Id && _template.Source != null) return _template;
        _template = null;
        _templateFor = p.Id;

        var source = p.Root.GetComponentInChildren<FireVisualizer>(true);
        if (source == null)
        {
            Disable("the wizard has no FireVisualizer");
            return null;
        }

        var wanted = ModConfig.GameFireTheme.Value.Trim();
        if (!Enum.TryParse<FireTheme>(wanted, ignoreCase: true, out var theme))
        {
            theme = FireTheme.Fire;
            ModLog.Warn($"Game fire trail: unknown Effects.GameFireTheme '{wanted}', using '{theme}'. Options: {string.Join(", ", Enum.GetNames(typeof(FireTheme)))}.");
        }

        var src = source.transform;
        _template = new Template(source.gameObject, theme, src.position - p.RootT.position, src.rotation, src.lossyScale);
        Status = $"the game's own fire (FireVisualizer '{source.gameObject.name}', theme {theme}, scale {ModConfig.GameFireScale.Value:0.##})";
        ModLog.Info("Blazing Dash trail: " + Status);
        return _template;
    }

    private void Disable(string why)
    {
        _broken = true;
        Status = "not used: " + why + "; the mod's own flames are used instead";
        ModLog.Warn("Blazing Dash trail: " + Status);
    }

    private sealed class Template
    {
        public Template(GameObject source, FireTheme theme, Vector3 offset, Quaternion rotation, Vector3 scale)
        {
            Source = source;
            Theme = theme;
            Offset = offset;
            Rotation = rotation;
            Scale = scale;
        }

        public GameObject Source { get; }
        public FireTheme Theme { get; }
        public Vector3 Offset { get; }
        public Quaternion Rotation { get; }
        public Vector3 Scale { get; }
    }

    private sealed class Flame
    {
        public Flame(GameObject go, FireVisualizer visualizer, GameObject? effect)
        {
            Go = go;
            Visualizer = visualizer;
            Effect = effect;
        }

        public GameObject Go { get; }
        public FireVisualizer Visualizer { get; }
        public GameObject? Effect { get; }
        public bool Burning;
        public float StopAt;
        public float FreeAt;
    }
}
