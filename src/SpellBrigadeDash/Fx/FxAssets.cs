namespace SpellBrigadeDash.Fx;

using System;
using System.Globalization;
using UnityEngine;

/// <summary>Textures, sprites and shaders generated at runtime, so the mod ships no asset bundles.</summary>
internal static class FxAssets
{
    private static Sprite? _softCircle;
    private static bool _shadersResolved;
    private static Shader? _ghostShader3D;
    private static Shader? _silhouetteShader2D;

    /// <summary>64x64 white disc with soft edges. 1 world unit across at scale 1.</summary>
    public static Sprite SoftCircle
    {
        get
        {
            if (_softCircle != null) return _softCircle;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var c = (size - 1) / 2f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c; // 0 centre .. 1 edge
                var a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a); // smoothstep falloff
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }

            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            _softCircle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            _softCircle.hideFlags = HideFlags.HideAndDontSave;
            return _softCircle;
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

    private static void ResolveShaders()
    {
        if (_shadersResolved) return;
        _shadersResolved = true;
        _ghostShader3D = FindFirst("Sprites/Default", "UI/Default", "Universal Render Pipeline/Unlit", "Unlit/Transparent", "Unlit/Color");
        _silhouetteShader2D = FindFirst("GUI/Text Shader");
        ModLog.Info($"FX shaders: 3D ghost = {_ghostShader3D?.name ?? "none"}, 2D silhouette = {_silhouetteShader2D?.name ?? "none (tinted sprites instead)"}");
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
