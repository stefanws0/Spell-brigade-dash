namespace SpellBrigadeDash.Fx;

using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Textures, sprites, shaders and materials generated at runtime, so the mod ships no asset bundles.
/// All sprite textures are white with an alpha shape; the colour comes from the SpriteRenderer.
/// </summary>
internal static class FxAssets
{
    private delegate float AlphaFn(float x, float y);

    private static Sprite? _softCircle;
    private static Sprite? _sparkle;
    private static Sprite? _streak;
    private static Sprite? _flame;
    private static readonly System.Collections.Generic.Dictionary<int, Sprite> RoundedCache = new();
    private static Texture2D? _roundedTex;
    private static bool _roundedUiFailed;
    private static bool _shadersResolved;
    private static Shader? _ghostShader3D;
    private static Shader? _silhouetteShader2D;
    private static Material? _glow;

    /// <summary>Soft round blob (dust). 1 world unit across at scale 1.</summary>
    public static Sprite SoftCircle => _softCircle ??= MakeSprite("SBD_SoftCircle", 64, 64, new Vector2(0.5f, 0.5f), 64f, (x, y) =>
    {
        var d = Mathf.Sqrt(x * x + y * y); // -1..1 space
        var a = Mathf.Clamp01(1f - d);
        return a * a * (3f - 2f * a);
    });

    /// <summary>Four-pointed twinkle star with a soft glowing core.</summary>
    public static Sprite Sparkle => _sparkle ??= MakeSprite("SBD_Sparkle", 64, 64, new Vector2(0.5f, 0.5f), 64f, (x, y) =>
    {
        var armH = Gauss(x, 0.55f) * Gauss(y, 0.07f);
        var armV = Gauss(y, 0.55f) * Gauss(x, 0.07f);
        var core = Gauss(Mathf.Sqrt(x * x + y * y), 0.22f);
        return Mathf.Clamp01(armH + armV + core * 0.9f);
    });

    /// <summary>Thin light streak pointing along +X: bright head on the right, fading tail on the left.</summary>
    public static Sprite Streak => _streak ??= MakeSprite("SBD_Streak", 128, 16, new Vector2(0.5f, 0.5f), 128f, (x, y) =>
    {
        var u = (x + 1f) * 0.5f; // 0 tail .. 1 head
        var along = MathF.Pow(u, 1.8f) * Mathf.Clamp01((1f - u) / 0.08f);
        return along * Gauss(y, 0.45f);
    });

    /// <summary>Teardrop flame tongue, pivot at the bottom so it grows up from the ground.</summary>
    public static Sprite Flame => _flame ??= MakeSprite("SBD_Flame", 64, 128, new Vector2(0.5f, 0.05f), 128f, (x, y) =>
    {
        var v = (y + 1f) * 0.5f; // 0 bottom .. 1 top
        // width profile: round bottom, long tapering tip
        var width = 0.85f * Mathf.Sqrt(Mathf.Clamp01(v / 0.25f)) * MathF.Pow(Mathf.Clamp01(1f - v), 0.9f);
        if (width <= 0.001f) return 0f;
        var edge = Mathf.Clamp01(1f - Math.Abs(x) / width);
        return Mathf.Clamp01(edge * 1.6f) * Mathf.Clamp01((1f - v) / 0.1f);
    });

    /// <summary>
    /// Rounded shape for UI Images with the given corner radius in screen pixels. It's created with 9-slice
    /// borders, so it stretches to any width while the ends stay round: at radius = height / 2 you get a
    /// perfect pill. Null if this Unity build can't create sliced sprites (the bar then uses square segments).
    /// </summary>
    public static Sprite? RoundedUi(int radiusPx)
    {
        if (_roundedUiFailed) return null;
        if (RoundedCache.TryGetValue(radiusPx, out var cached)) return cached;
        try
        {
            const int size = 64;
            _roundedTex ??= MakeTexture("SBD_Rounded", size, size, (x, y) =>
            {
                var d = Mathf.Sqrt(x * x + y * y) * (size / 2f); // pixels from centre
                return Mathf.Clamp01(size / 2f - d - 0.5f); // 1px anti-aliased edge
            });

            // A UI Image draws a 31px sprite border as 31 / (spritePPU / 100) canvas pixels, so this PPU
            // makes the border exactly radiusPx on screen.
            var ppu = 3100f / Math.Max(1, radiusPx);
            var sprite = Sprite.Create(_roundedTex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu, 0u,
                SpriteMeshType.FullRect, new Vector4(31f, 31f, 31f, 31f));
            sprite.name = $"SBD_Rounded_{radiusPx}";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            RoundedCache[radiusPx] = sprite;
            return sprite;
        }
        catch (Exception ex)
        {
            _roundedUiFailed = true;
            ModLog.Warn($"Rounded bar sprite unavailable ({ex.Message}); using square segments.");
            return null;
        }
    }

    /// <summary>Unlit transparent shader for 3D mesh afterimages (vertex/material colour only).</summary>
    public static Shader? GhostShader3D
    {
        get
        {
            ResolveShaders();
            return _ghostShader3D;
        }
    }

    /// <summary>Shader that draws a sprite's alpha as a flat colour (a silhouette). Null if not in the build.</summary>
    public static Shader? SilhouetteShader2D
    {
        get
        {
            ResolveShaders();
            return _silhouetteShader2D;
        }
    }

    /// <summary>
    /// Shared additive material, so sparkles, streaks and flame cores glow (light adds up instead of covering).
    /// Null if the game ships no additive particle shader; effects then use normal transparency.
    /// </summary>
    public static Material? Glow
    {
        get
        {
            ResolveShaders();
            return _glow;
        }
    }

    private static void ResolveShaders()
    {
        if (_shadersResolved) return;
        _shadersResolved = true;
        _ghostShader3D = FindFirst("Sprites/Default", "UI/Default", "Universal Render Pipeline/Unlit", "Unlit/Transparent", "Unlit/Color");
        _silhouetteShader2D = FindFirst("GUI/Text Shader");
        var additive = FindFirst("Legacy Shaders/Particles/Additive", "Particles/Additive", "Mobile/Particles/Additive",
            "Legacy Shaders/Particles/Additive (Soft)", "Particles/Additive (Soft)");
        if (additive != null)
        {
            try
            {
                _glow = new Material(additive) { hideFlags = HideFlags.HideAndDontSave };
            }
            catch
            {
                _glow = null;
            }
        }

        ModLog.Info($"FX shaders: 3D ghost = {_ghostShader3D?.name ?? "none"}, 2D silhouette = {_silhouetteShader2D?.name ?? "none (tinted sprites instead)"}, " +
                    $"glow = {additive?.name ?? "none (normal transparency)"}");
    }

    private static Shader? FindFirst(params string[] names)
    {
        foreach (var n in names)
        {
            try
            {
                var s = Shader.Find(n);
                if (s != null) return s;
            }
            catch
            {
                // stripped
            }
        }

        return null;
    }

    // ------------------------------------------------------------------------------- helpers

    private static float Gauss(float v, float sigma) => MathF.Exp(-(v * v) / (2f * sigma * sigma));

    /// <summary>White texture whose alpha comes from <paramref name="alpha"/>(x, y), with x and y in -1..1.</summary>
    private static Texture2D MakeTexture(string name, int w, int h, AlphaFn alpha)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.name = name;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        for (var py = 0; py < h; py++)
        for (var px = 0; px < w; px++)
        {
            var x = (px + 0.5f) / w * 2f - 1f;
            var y = (py + 0.5f) / h * 2f - 1f;
            tex.SetPixel(px, py, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(x, y))));
        }

        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        return tex;
    }

    private static Sprite MakeSprite(string name, int w, int h, Vector2 pivot, float pixelsPerUnit, AlphaFn alpha)
    {
        var sprite = Sprite.Create(MakeTexture(name, w, h, alpha), new Rect(0, 0, w, h), pivot, pixelsPerUnit);
        sprite.name = name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    public static Color ParseColor(string hex, Color fallback)
    {
        try
        {
            var h = hex.Trim().TrimStart('#');
            if (h.Length != 6 && h.Length != 8) return fallback;
            var r = int.Parse(h[..2], NumberStyles.HexNumber) / 255f;
            var g = int.Parse(h.Substring(2, 2), NumberStyles.HexNumber) / 255f;
            var b = int.Parse(h.Substring(4, 2), NumberStyles.HexNumber) / 255f;
            var a = h.Length == 8 ? int.Parse(h.Substring(6, 2), NumberStyles.HexNumber) / 255f : 1f;
            return new Color(r, g, b, a);
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    public static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);
}
