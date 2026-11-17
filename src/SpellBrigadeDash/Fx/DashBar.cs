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
///   [███][██░][   ]   full / refilling / empty
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

    private Color _fill, _charging, _back, _flash, _empty;

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
        var pad = Math.Max(1f, 1.5f * s);
        var gap = Math.Max(1f, 2f * s);
        var n = Math.Max(1, charges.Max);
        var segW = (width - gap * (n - 1)) / n;

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
            _fill = FxAssets.ParseColor(ModConfig.BarFillColor.Value, new Color(0.44f, 0.89f, 1f));
            _charging = FxAssets.ParseColor(ModConfig.BarChargingColor.Value, new Color(0.23f, 0.53f, 0.66f));
            _back = FxAssets.ParseColor(ModConfig.BarBackColor.Value, new Color(0.05f, 0.07f, 0.12f, 0.78f));
            _flash = FxAssets.ParseColor(ModConfig.BarFlashColor.Value, Color.white);
            _empty = FxAssets.ParseColor(ModConfig.BarEmptyColor.Value, new Color(1f, 0.29f, 0.29f));

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
        var backColor = new Color(_back.r * 0.5f + 0.08f, _back.g * 0.5f + 0.08f, _back.b * 0.5f + 0.1f, 0.9f);
        for (var i = 0; i < count; i++)
        {
            var back = NewUi($"Segment{i}", _bar!);
            AddImage(back, backColor);
            var fill = NewUi($"Fill{i}", _bar!);
            var img = AddImage(fill, _fill);
            _segments.Add(new Segment(back.GetComponent<RectTransform>(), fill.GetComponent<RectTransform>(), img));
        }
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
        public Segment(RectTransform back, RectTransform fill, Image fillImage)
        {
            Back = back;
            Fill = fill;
            FillImage = fillImage;
        }

        public RectTransform Back { get; }
        public RectTransform Fill { get; }
        public Image FillImage { get; }
        public float FlashAt { get; set; } = -999f;
    }
}
