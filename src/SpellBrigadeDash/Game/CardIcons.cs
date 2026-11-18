namespace SpellBrigadeDash.Game;

using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using SpellBrigadeDash.Core;
using UnityEngine;

/// <summary>
/// The dash cards' own icons (round white-on-black badges in the style of the game's card icons). Each is looked up
/// as <c>icons/&lt;name&gt;.png</c> next to the mod's DLL first (to try new art without a rebuild), then as a PNG
/// embedded in the DLL from <c>art/icons</c>. Missing icon = null, and the shop card keeps the borrowed game icon.
/// </summary>
internal static class CardIcons
{
    private const string ResourcePrefix = "SpellBrigadeDash.Icons.";

    private static readonly Dictionary<DashCardId, Sprite?> Cache = new();

    /// <summary>
    /// The <c>icons</c> folder next to the DLL, whatever the mod manager named the plugin folder (r2modman installs
    /// into <c>plugins/&lt;Team&gt;-&lt;Package&gt;</c>, a manual install into <c>plugins/SpellBrigadeDash</c>).
    /// </summary>
    public static string OverrideFolder
    {
        get
        {
            var dir = Path.GetDirectoryName(typeof(CardIcons).Assembly.Location);
            return Path.Combine(string.IsNullOrEmpty(dir) ? Path.Combine(Paths.PluginPath, "SpellBrigadeDash") : dir, "icons");
        }
    }

    public static Sprite? For(DashCardId card)
    {
        if (Cache.TryGetValue(card, out var cached) && (cached == null || cached.texture != null)) return cached;
        var file = CardArt.IconFile(card);
        Sprite? sprite = null;
        try
        {
            var bytes = Read(file, out var source);
            if (bytes != null)
            {
                sprite = MakeSprite(file, bytes);
                ModLog.Info(sprite != null
                    ? $"Card icon {file}: {source} ({sprite.texture.width}x{sprite.texture.height})."
                    : $"Card icon {file}: {source} is not a readable PNG; using the game's icon.");
            }
            else
            {
                ModLog.Info($"Card icon {file}: none yet; the card uses the game's Dodge icon.");
            }
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Card icon {file} failed: {ex.Message}; using the game's icon.");
        }

        Cache[card] = sprite;
        return sprite;
    }

    private static byte[]? Read(string file, out string source)
    {
        var path = Path.Combine(OverrideFolder, file);
        if (File.Exists(path))
        {
            source = "override " + path;
            return File.ReadAllBytes(path);
        }

        source = "built in";
        using var stream = typeof(CardIcons).Assembly.GetManifestResourceStream(ResourcePrefix + file);
        if (stream == null) return null;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static Sprite? MakeSprite(string name, byte[] png)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(tex, png)) return null;
        tex.name = "SBD_" + name;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.HideAndDontSave; // survive the game's Resources.UnloadUnusedAssets on scene changes
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "SBD_" + name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
