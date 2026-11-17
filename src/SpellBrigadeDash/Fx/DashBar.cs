namespace SpellBrigadeDash.Fx;

using System;
using System.Collections.Generic;
using SpellBrigadeDash.Core;
using SpellBrigadeDash.Game;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// The segmented dash-energy bar under your wizard. It's a screen-space UI canvas whose position
/// follows the wizard's feet every frame, which keeps it pixel-crisp in both 2D and 3D games.
///
///   ( ███ )( ██░ )(     )   full / refilling / empty, rounded like the game's own bars
///
/// Feedback: a segment flashes when it refills; the bar shakes and flashes red on an empty press;
/// it fades out <see cref="ModConfig.BarHideDelay"/> seconds after all charges are full.
/// </summary>
internal sealed class DashBar
{
    private const float FlashTime = 0.25f;
    private const float EmptyTime = 0.35f;

    private readonly List<Segment> _segments = new();
    private GameObject? _canvasGo;
    private CanvasGroup? _group;
    private RectTransform? _bar;
    private Image? _frame;
    private float _alpha;
    private float _lastActivity = -999f;
    private float _emptyPressAt = -999f;

    private Color _fill, _charging, _back, _segmentBack, _flash, _empty;
    private Sprite? _gameSprite;
    private int _segRadius = -1;
    private int _frameRadius = -1;

    public void OnDash(float now) => _lastActivity = now;

    public void OnEmptyPress(float now)
    {
        _lastActivity = now;
        if (ModConfig.BarFeedback.Value) _emptyPressAt = now;
    }

    public void OnRefilled(DashCharges charges, int count, float now)
    {
        _lastActivity = now;
        if (!ModConfig.BarFeedback.Value) return;
        for (var i = 0; i < count; i++)
        {
            var index = charges.Full - 1 - i;
            if (index >= 0 && index < _segments.Count) _segments[index].FlashAt = now;
        }
    }

    public void Update(PlayerInfo? player, DashCharges charges, bool allowed, float now, float dt)
    {
        if (!EnsureBuilt()) return;

        var show = allowed && player != null && charges.Max > 0;
        Vector3 screen = default;
        if (show)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                show = false;
            }
            else
            {
                screen = cam.WorldToScreenPoint(player!.Feet);
                if (screen.z < 0f) show = false;
            }
        }

        if (!show)
        {
            _alpha = 0f;
            _group!.alpha = 0f;
            return;
        }

        if (_segments.Count != charges.Max) Rebuild(charges.Max);

        // Visibility: auto-hide once full for a while.
        var busy = !charges.IsFull || now - _lastActivity < ModConfig.BarHideDelay.Value || now - _emptyPressAt < EmptyTime;
        var target = !ModConfig.BarAutoHide.Value || busy ? 1f : 0f;
        _alpha = Mathf.MoveTowards(_alpha, target, dt * (target > _alpha ? 10f : 3f));
        _group!.alpha = _alpha;
        if (_alpha <= 0.001f) return;

        // Layout (pixels at 1080p, scaled to the current resolution).
        var s = Screen.height / 1080f * ModConfig.BarScale.Value;
        var width = ModConfig.BarWidth.Value * s;
        var height = ModConfig.BarHeight.Value * s;
        var rounded = _gameSprite != null || ModConfig.BarRounded.Value;
        var pad = Math.Max(1f, (rounded ? 2f : 1.5f) * s);
        var gap = Math.Max(1f, (rounded ? 3f : 2f) * s);
        var n = Math.Max(1, charges.Max);
        var segW = (width - gap * (n - 1)) / n;

        UpdateShapes(height, height + pad * 2f);
        _bar!.sizeDelta = new Vector2(width + pad * 2f, height + pad * 2f);

        var shake = 0f;
        var sinceEmpty = now - _emptyPressAt;
        if (sinceEmpty < EmptyTime)
        {
            var k = 1f - sinceEmpty / EmptyTime;
            shake = Mathf.Sin(sinceEmpty * 70f) * 4f * s * k;
            _frame!.color = Color.Lerp(_back, FxAssets.WithAlpha(_empty, Math.Max(_back.a, _empty.a)), k);
        }
        else
        {
            _frame!.color = _back;
        }

        _bar.position = new Vector3(screen.x + shake, screen.y - ModConfig.BarOffset.Value * s - (height + pad * 2f) * 0.5f, 0f);

        for (var i = 0; i < _segments.Count; i++)
        {
            var seg = _segments[i];
            var x = pad + i * (segW + gap);
            Place(seg.Back, x, pad, segW, height);
            var fill = charges.SegmentFill(i);
            Place(seg.Fill, x, pad, Math.Max(0f, segW * fill), height);
            seg.Fill.gameObject.SetActive(fill > 0.02f);

            var color = fill >= 1f ? _fill : _charging;
            var sinceFlash = now - seg.FlashAt;
            if (sinceFlash < FlashTime) color = Color.Lerp(_flash, _fill, sinceFlash / FlashTime);
            seg.FillImage.color = color;
        }
    }

    public void Destroy()
    {
        if (_canvasGo != null) Object.Destroy(_canvasGo);
        _canvasGo = null;
        _segments.Clear();
    }

    // ------------------------------------------------------------------------------------------

    private bool EnsureBuilt()
    {
        if (_canvasGo != null) return true;
        try
        {
            _fill = FxAssets.ParseColor(ModConfig.BarFillColor.Value, new Color(0.71f, 0.48f, 1f));
            _charging = FxAssets.ParseColor(ModConfig.BarChargingColor.Value, new Color(0.42f, 0.27f, 0.72f));
            _back = FxAssets.ParseColor(ModConfig.BarBackColor.Value, new Color(0.08f, 0.05f, 0.15f, 0.82f));
            _segmentBack = new Color(_back.r * 0.5f + 0.1f, _back.g * 0.5f + 0.07f, _back.b * 0.5f + 0.16f, 0.9f);
            _flash = FxAssets.ParseColor(ModConfig.BarFlashColor.Value, Color.white);
            _empty = FxAssets.ParseColor(ModConfig.BarEmptyColor.Value, new Color(1f, 0.29f, 0.29f));

            ModLog.Step("bar: building canvas");
            _gameSprite = FindGameSprite();
            _canvasGo = new GameObject("SpellBrigadeDash_Bar");
            Object.DontDestroyOnLoad(_canvasGo);
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = ModConfig.BarSortingOrder.Value;
            _group = _canvasGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            var barGo = NewUi("Bar", _canvasGo.transform);
            _bar = barGo.GetComponent<RectTransform>();
            _bar.anchorMin = Vector2.zero;
            _bar.anchorMax = Vector2.zero;
            _bar.pivot = new Vector2(0.5f, 0.5f);
            _frame = AddImage(barGo, _back);
            ModLog.Step("bar: canvas built");
            return true;
        }
        catch (Exception ex)
        {
            ModLog.ErrorThrottled("DashBar.Build", ex, Time.unscaledTime, 60f);
            _canvasGo = null;
            return false;
        }
    }

    private void Rebuild(int count)
    {
        foreach (var seg in _segments)
        {
            Object.Destroy(seg.Back.gameObject);
            Object.Destroy(seg.Fill.gameObject);
        }

        _segments.Clear();
        for (var i = 0; i < count; i++)
        {
            var back = NewUi($"Segment{i}", _bar!);
            var backImg = AddImage(back, _segmentBack);
            var fill = NewUi($"Fill{i}", _bar!);
            var img = AddImage(fill, _fill);
            _segments.Add(new Segment(back.GetComponent<RectTransform>(), backImg, fill.GetComponent<RectTransform>(), img));
        }

        _segRadius = -1; // new images need their shape
        _frameRadius = -1;
    }

    /// <summary>
    /// Gives the frame and segments their rounded shape. The built-in pill is regenerated for the bar's
    /// actual pixel height, so the ends stay perfect half-circles at every resolution and bar size.
    /// </summary>
    private void UpdateShapes(float segmentHeight, float frameHeight)
    {
        if (_gameSprite != null)
        {
            if (_segRadius == 0) return;
            SetShape(_frame!, _gameSprite);
            foreach (var seg in _segments)
            {
                SetShape(seg.BackImage, _gameSprite);
                SetShape(seg.FillImage, _gameSprite);
            }

            _segRadius = 0;
            return;
        }

        if (!ModConfig.BarRounded.Value) return;

        var segR = Math.Max(1, (int)Math.Round(segmentHeight / 2f));
        var frameR = Math.Max(1, (int)Math.Round(frameHeight / 2f));
        if (segR == _segRadius && frameR == _frameRadius) return;
        _segRadius = segR;
        _frameRadius = frameR;

        SetShape(_frame!, FxAssets.RoundedUi(frameR));
        var segSprite = FxAssets.RoundedUi(segR);
        foreach (var seg in _segments)
        {
            SetShape(seg.BackImage, segSprite);
            SetShape(seg.FillImage, segSprite);
        }
    }

    private static void SetShape(Image img, Sprite? shape)
    {
        if (shape == null) return;
        img.sprite = shape;
        var b = shape.border;
        // 9-sliced: the round ends keep their shape at any width
        img.type = b.x > 0f || b.y > 0f || b.z > 0f || b.w > 0f ? Image.Type.Sliced : Image.Type.Simple;
    }

    /// <summary>One of the game's own UI sprites, if configured in Bar.GameSpriteName.</summary>
    private static Sprite? FindGameSprite()
    {
        var name = ModConfig.BarSpriteName.Value.Trim();
        if (name.Length == 0) return null;
        try
        {
            foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
                if (sprite != null && string.Equals(sprite.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    ModLog.Info($"Dash bar uses the game's sprite '{sprite.name}'.");
                    return sprite;
                }
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Looking up Bar.GameSpriteName failed: {ex.Message}");
        }

        ModLog.WarnOnce("bar-sprite", $"Bar.GameSpriteName '{name}' not found; using the built-in rounded shape.");
        return null;
    }

    private static GameObject NewUi(string name, Transform parent)
    {
        var go = new GameObject(name);
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        return go;
    }

    private static Image AddImage(GameObject go, Color color)
    {
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private sealed class Segment
    {
        public Segment(RectTransform back, Image backImage, RectTransform fill, Image fillImage)
        {
            Back = back;
            BackImage = backImage;
            Fill = fill;
            FillImage = fillImage;
        }

        public RectTransform Back { get; }
        public Image BackImage { get; }
        public RectTransform Fill { get; }
        public Image FillImage { get; }
        public float FlashAt { get; set; } = -999f;
    }
}
