namespace SpellBrigadeDash.Game;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Il2CppInterop.Runtime;
using SpellBrigadeDash.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Adds the dash's stats as rows to the game's player stats list (<c>StatPanel_Player</c>: the Escape screen's stats
/// page and the hold-to-show stats panel). The game fills a fixed array of <c>PlayerStatEntry</c> rows, one per game
/// stat, and dash upgrades aren't game stats, so the mod adds rows of its own: copies of the panel's last row, placed
/// right after it, with the title's localizer switched off and the title, value and icon set by the mod
/// (<see cref="DashStatRows"/>). The game never touches them because they're not in its array. No Harmony patches.
/// </summary>
internal static class StatPanelRows
{
    private const string RowPrefix = "SBD_DashRow_";
    private const float ScanSeconds = 2f;
    private const float RefreshSeconds = 0.25f;

    /// <summary>
    /// Rows of room in the game's stats frame at its own spacing. The frame holds exactly 15 (measured 18 Nov, see
    /// <see cref="ListFit.DesignedRoom"/>), but filled to its edge the last row touched the frame (Stefan, 18 Nov:
    /// "a little more space to the bottom"), so half a row is left free.
    /// </summary>
    private const float DesignedRows = 14.5f;

    private static readonly Dictionary<IntPtr, PanelRows> Panels = new();
    private static readonly HashSet<IntPtr> Skipped = new();
    private static float _nextScan;
    private static float _nextRefresh;
    private static bool _off;
    private static string _shown = "";

    // 18 Nov diagnostics: the Escape menu's list never came on screen in three runs while Stefan saw no dash rows there.
    private const int PauseLogLines = 400;
    private static PauseMenuManager? _pause;
    private static bool _pauseOpen;
    private static float _nextPauseLook;
    private static int _pauseLogged;
    private static HashSet<string> _pauseShowing = new();

    public static string Status { get; private set; } = "no stats panel seen yet";

    private sealed class PanelRows
    {
        public PanelRows(StatPanel_Player source, string name, VerticalLayoutGroup? layout, RectTransform? box)
        {
            Source = source;
            Name = name;
            Layout = layout;
            Box = box;
            BaseSpacing = layout != null ? layout.spacing : 0f;
        }

        public StatPanel_Player Source { get; }
        public string Name { get; }
        public Dictionary<string, Row> Rows { get; } = new();

        /// <summary>The list's layout and box: the box has a fixed height, so the rows are fitted by their gap.</summary>
        public VerticalLayoutGroup? Layout { get; }
        public RectTransform? Box { get; }
        public float BaseSpacing { get; }
        public string LastFit { get; set; } = "";
        public float ShownSince { get; set; } = -1f;
        public bool Measured { get; set; }
        public bool? OnScreen { get; set; }
        public int OnScreenLogs { get; set; }
    }

    private sealed class Row
    {
        public Row(GameObject go, TMP_Text? title, TMP_Text? value, Image? icon)
        {
            Go = go;
            Title = title;
            Value = value;
            Icon = icon;
        }

        public GameObject Go { get; }
        public TMP_Text? Title { get; }
        public TMP_Text? Value { get; }
        public Image? Icon { get; }
        public DashCardId? ShownIcon { get; set; }
    }

    public static void Update(DashBaseStats stats, DashProgress progress, float now)
    {
        if (_off || !ModConfig.StatsPanelRows.Value) return;
        try
        {
            if (now >= _nextScan)
            {
                _nextScan = now + ScanSeconds;
                Scan();
            }

            if (now < _nextRefresh || Panels.Count == 0) return;
            _nextRefresh = now + RefreshSeconds;
            var rows = DashStatRows.Build(stats, progress);
            foreach (var panel in Panels.Values)
            {
                if (panel.Source == null) continue;
                Fill(panel, rows);
                Fit(panel);
                if (!ModConfig.DiscoveryEnabled.Value) continue; // the layout measurements are diagnostics
                MeasureOnce(panel, now);
                TrackOnScreen(panel);
            }

            if (ModConfig.DiscoveryEnabled.Value) WatchPauseMenu(now);

            var shown = string.Join(", ", rows.Where(r => r.Visible).Select(r => $"{r.Title} {r.Value}"));
            if (shown != _shown)
            {
                _shown = shown;
                ModLog.Info($"Stats list dash rows: {(shown.Length == 0 ? "hidden (dash locked)" : shown)}.");
            }
        }
        catch (Exception ex)
        {
            _off = true;
            Status = "off after an error (see the log)";
            ModLog.Error($"Stats panel rows switched off for this session after an error: {ex}");
        }
    }

    private static void Scan()
    {
        foreach (var gone in Panels.Where(kv => kv.Value.Source == null).Select(kv => kv.Key).ToList()) Panels.Remove(gone);

        // includeInactive: the Escape screen's panel sits inactive until it opens. Scene objects only (no prefabs).
        var found = Object.FindObjectsOfType(Il2CppType.Of<StatPanel_Player>(), true);
        if (found == null) return;
        foreach (var obj in found)
        {
            var panel = obj?.TryCast<StatPanel_Player>();
            if (panel == null || Panels.ContainsKey(panel.Pointer) || Skipped.Contains(panel.Pointer)) continue;
            var rows = AddRows(panel);
            if (rows == null) Skipped.Add(panel.Pointer);
            else Panels[panel.Pointer] = rows;
        }

        if (_pause == null)
        {
            var menus = Object.FindObjectsOfType(Il2CppType.Of<PauseMenuManager>(), true);
            if (menus != null && menus.Length > 0) _pause = menus[0]?.TryCast<PauseMenuManager>();
        }

        Status = Panels.Count == 0
            ? "no stats panel found yet"
            : $"{Panels.Count} stats panel(s): " + string.Join(", ", Panels.Values.Select(p => Path(p.Source.transform)));
    }

    private static PanelRows? AddRows(StatPanel_Player panel)
    {
        var entries = panel.entries;
        PlayerStatEntry? template = null;
        if (entries != null)
            for (var i = entries.Length - 1; i >= 0 && template == null; i--)
                if (entries[i] != null) template = entries[i];
        if (template == null)
        {
            ModLog.Warn($"Stats panel '{Path(panel.transform)}' has no stat rows to copy; no dash rows added to it.");
            return null;
        }

        var parent = template.transform.parent;
        // A panel copied from one the mod already changed carries untracked dash rows: replace them.
        for (var i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (child != null && child.gameObject.name.StartsWith(RowPrefix, StringComparison.Ordinal)) Object.Destroy(child.gameObject);
        }

        // Rows are made inside an inactive holder, so none of the copied components wake up (the title's localizer
        // would otherwise write the copied stat's name back) before the mod has set them up.
        var holder = new GameObject("SBD_DashRowHolder");
        holder.SetActive(false);
        var name = Path(panel.transform);
        var result = new PanelRows(panel, name, parent.GetComponent<VerticalLayoutGroup>(), parent.TryCast<RectTransform>());
        var index = template.transform.GetSiblingIndex() + 1;
        try
        {
            foreach (var key in DashStatRows.Keys)
            {
                var go = Object.Instantiate(template.gameObject, holder.transform);
                go.name = RowPrefix + key;
                result.Rows[key] = Prepare(go);
                go.SetActive(false);
                go.transform.SetParent(parent, false);
                go.transform.SetSiblingIndex(index++);
            }
        }
        finally
        {
            Object.Destroy(holder);
        }

        ModLog.Info($"Stats panel '{name}': added {result.Rows.Count} dash rows after '{template.gameObject.name}' " +
                    $"({entries!.Length} game rows). Layout: {Describe(parent)}; row {Size(template.transform)}; " +
                    (result.Layout == null ? "no VerticalLayoutGroup, rows can't be fitted." : $"gap {result.BaseSpacing:0.#}, padding {result.Layout.padding.top}/{result.Layout.padding.bottom}."));
        return result;
    }

    private static Row Prepare(GameObject go)
    {
        var entry = go.GetComponent<PlayerStatEntry>();
        TMP_Text? title = null;
        TMP_Text? value = null;
        Image? icon = null;
        if (entry != null)
        {
            value = entry.valueText;
            icon = entry.statIcon;
            var localizer = entry.statTitle;
            if (localizer != null)
            {
                localizer.enabled = false;
                title = localizer.GetComponent<TMP_Text>() ?? localizer.GetComponentInChildren<TMP_Text>(true);
            }

            entry.enabled = false;
        }

        if (title == null || value == null)
        {
            // Fallback: the row's texts in order, title first.
            var texts = go.GetComponentsInChildren<TMP_Text>(true).Where(t => t != null).ToList();
            title ??= texts.FirstOrDefault(t => value == null || t.Pointer != value.Pointer);
            value ??= texts.LastOrDefault(t => title == null || t.Pointer != title.Pointer);
        }

        if (title == null || value == null) ModLog.WarnOnce("statrow-texts", "Stats panel row: couldn't find both of its texts; the dash rows may show the copied stat's name.");
        return new Row(go, title, value, icon);
    }

    private static void Fill(PanelRows panel, IReadOnlyList<DashStatRow> rows)
    {
        foreach (var r in rows)
        {
            if (!panel.Rows.TryGetValue(r.Key, out var row) || row.Go == null) continue;
            if (row.Go.activeSelf != r.Visible) row.Go.SetActive(r.Visible);
            if (!r.Visible) continue;
            if (row.Title != null && row.Title.text != r.Title) row.Title.text = r.Title;
            if (row.Value != null && row.Value.text != r.Value) row.Value.text = r.Value;
            if (row.Icon != null && row.ShownIcon != r.Icon)
            {
                var sprite = CardIcons.For(r.Icon);
                if (sprite != null) row.Icon.sprite = sprite;
                row.ShownIcon = r.Icon;
            }
        }
    }

    /// <summary>
    /// The list's box has a fixed height with room for the game's rows plus about two (17 Nov screenshot: the dash rows
    /// ran out of the frame). Tighten the gap between all rows just enough that the shown rows fit; the game's own gap
    /// comes back when they fit again (e.g. dash locked).
    /// </summary>
    private static void Fit(PanelRows panel)
    {
        var layout = panel.Layout;
        var box = panel.Box;
        if (layout == null || box == null) return;

        var count = 0;
        var rowsHeight = 0f;
        var smallest = float.MaxValue;
        for (var i = 0; i < box.childCount; i++)
        {
            var child = box.GetChild(i);
            if (child == null || !child.gameObject.activeSelf) continue;
            var rt = child.TryCast<RectTransform>();
            if (rt == null) continue;
            var h = rt.rect.height;
            count++;
            rowsHeight += h;
            if (h > 0f) smallest = Math.Min(smallest, h);
        }

        if (smallest == float.MaxValue) return;
        var pad = layout.padding;
        // The list's box runs past the visible frame (in game: off the bottom of the screen), so the room is what the
        // frame was designed for (15 rows at the game's own gap, less half a row of margin), or the box if that is smaller.
        var room = Math.Min(box.rect.height - pad.top - pad.bottom, ListFit.DesignedRoom(DesignedRows, smallest, panel.BaseSpacing));
        // Rows are taller than their text, so a slightly negative gap still leaves space between the lines.
        var spacing = ListFit.Spacing(room, rowsHeight, count, panel.BaseSpacing, -0.2f * smallest);
        if (Math.Abs(layout.spacing - spacing) > 0.05f) layout.spacing = spacing;

        var fit = $"{count}:{spacing:0.0}";
        if (fit == panel.LastFit) return;
        panel.LastFit = fit;
        var over = rowsHeight + spacing * (count - 1) - room;
        ModLog.Info($"Stats panel '{panel.Name}': {count} rows ({rowsHeight:0} high) in {room:0} of room, gap {panel.BaseSpacing:0.#} -> {spacing:0.#}" +
                    (over > 0.5f ? $"; still {over:0} too tall." : "."));
    }

    /// <summary>
    /// 18 Nov: fitting the rows to the list's own box wasn't enough (the in-game panel still ran out of its frame, the
    /// Escape menu showed no dash rows at all). The first time each panel is on screen, log where the box, its parents
    /// (frames, masks, canvas) and every row really are, so the fit can use the visible frame.
    /// </summary>
    private static void MeasureOnce(PanelRows panel, float now)
    {
        if (panel.Measured || panel.Box == null) return;
        if (!panel.Source.gameObject.activeInHierarchy)
        {
            panel.ShownSince = -1f;
            return;
        }

        if (panel.ShownSince < 0f) panel.ShownSince = now;
        if (now - panel.ShownSince < 0.5f) return;
        panel.Measured = true;

        var sb = new StringBuilder();
        sb.Append($"Stats panel '{panel.Name}' measured on screen ({Screen.width}x{Screen.height})");
        var layout = panel.Layout;
        if (layout != null)
            sb.Append($"; layout align {layout.childAlignment}, control height {layout.childControlHeight}, " +
                      $"expand height {layout.childForceExpandHeight}, scale height {layout.childScaleHeight}, gap {layout.spacing:0.#}");
        sb.AppendLine(". World y bottom..top (= screen pixels on an overlay canvas):");
        for (var t = panel.Box.transform; t != null; t = t.parent)
        {
            var rt = t.TryCast<RectTransform>();
            if (rt == null) break;
            var canvas = t.GetComponent<Canvas>();
            sb.AppendLine($"  ^ {t.gameObject.name} [{Components(t)}] {Span(rt)}, scale {t.localScale.y:0.###}" +
                          (canvas != null ? $", canvas {canvas.renderMode} x{canvas.scaleFactor:0.###}" : ""));
        }

        for (var i = 0; i < panel.Box.childCount; i++)
        {
            var child = panel.Box.GetChild(i);
            var rt = child?.TryCast<RectTransform>();
            if (rt != null) sb.AppendLine($"  {(child!.gameObject.activeInHierarchy ? "+" : "-")} {child.gameObject.name} {Span(rt)}");
        }

        // The visible frame isn't the box or one of its parents: list the rest of the canvas (3 levels) to find it.
        Transform? canvasRoot = null;
        for (var t = panel.Box.transform; t != null; t = t.parent)
            if (t.GetComponent<Canvas>() != null) canvasRoot = t;
        if (canvasRoot != null)
        {
            var listed = 0;
            foreach (var rt in canvasRoot.GetComponentsInChildren<RectTransform>(true))
            {
                if (rt == null || listed >= 60) continue;
                var depth = 0;
                var insideBox = false;
                for (var t = rt.transform; t != null && t.Pointer != canvasRoot.Pointer; t = t.parent)
                {
                    depth++;
                    if (t.Pointer == panel.Box.Pointer && rt.Pointer != panel.Box.Pointer) insideBox = true;
                }

                if (depth == 0 || depth > 3 || insideBox) continue;
                listed++;
                sb.AppendLine($"  {new string('.', depth)} {(rt.gameObject.activeInHierarchy ? "+" : "-")} {rt.gameObject.name} [{Components(rt.transform)}] {Span(rt)}");
            }
        }

        ModLog.Info(sb.ToString().TrimEnd());
    }

    /// <summary>Logs (a few times per panel) when a list comes on screen or goes off it: active and not faded out.</summary>
    private static void TrackOnScreen(PanelRows panel)
    {
        var onScreen = panel.Source.gameObject.activeInHierarchy && GroupAlpha(panel.Source.transform) > 0.01f;
        if (panel.OnScreen == onScreen) return;
        var first = panel.OnScreen == null;
        panel.OnScreen = onScreen;
        if ((first && !onScreen) || panel.OnScreenLogs >= 6) return;
        panel.OnScreenLogs++;
        var dashRows = panel.Rows.Values.Count(r => r.Go != null && r.Go.activeSelf);
        ModLog.Info($"Stats panel '{panel.Name}' {(onScreen ? $"on screen, {dashRows} dash row(s) shown" : "off screen")}.");
    }

    /// <summary>
    /// While the Escape menu is open, log what it shows as it changes (5 levels deep, ~400 lines a session at most):
    /// which pages and lists are on screen, to find where its stats list is and why the dash rows don't show there.
    /// </summary>
    private static void WatchPauseMenu(float now)
    {
        if (_pause == null) return;
        if (_pauseLogged >= PauseLogLines) return;
        var open = _pause.IsOpen;
        if (open != _pauseOpen)
        {
            _pauseOpen = open;
            _pauseLogged++;
            ModLog.Info(open ? $"Escape menu opened (time {now:0.0})." : "Escape menu closed.");
            _nextPauseLook = now + 0.3f;
        }

        // Only changes are logged, also across openings, so clicking through its buttons shows what each one opens.
        if (!open || now < _nextPauseLook) return;
        _nextPauseLook = now + 0.5f;
        var root = _pause.transform;
        var showing = new HashSet<string>();
        foreach (var rt in _pause.GetComponentsInChildren<RectTransform>(false))
        {
            if (rt == null || showing.Count >= 150) continue;
            var rel = RelPath(rt.transform, root, out var depth);
            if (depth == 0 || depth > 5 || GroupAlpha(rt.transform) <= 0.01f) continue;
            showing.Add(rel);
        }

        var added = showing.Where(x => !_pauseShowing.Contains(x)).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var gone = _pauseShowing.Where(x => !showing.Contains(x)).OrderBy(x => x, StringComparer.Ordinal).ToList();
        _pauseShowing = showing;
        if (added.Count == 0 && gone.Count == 0) return;
        var sb = new StringBuilder("Escape menu on screen now:");
        foreach (var a in added) sb.Append("\n  + ").Append(a);
        foreach (var g in gone) sb.Append("\n  - ").Append(g);
        _pauseLogged += added.Count + gone.Count;
        ModLog.Info(sb.ToString());
    }

    private static float GroupAlpha(Transform t)
    {
        var alpha = 1f;
        for (var cur = t; cur != null; cur = cur.parent)
        {
            var group = cur.GetComponent<UnityEngine.CanvasGroup>();
            if (group != null && group.enabled) alpha *= group.alpha;
        }

        return alpha;
    }

    private static string RelPath(Transform t, Transform root, out int depth)
    {
        var parts = new List<string>();
        for (var cur = t; cur != null && cur.Pointer != root.Pointer; cur = cur.parent) parts.Add(cur.gameObject.name);
        depth = parts.Count;
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static string Span(RectTransform rt)
    {
        var r = rt.rect;
        var low = rt.TransformPoint(new Vector3(r.x, r.y, 0f));
        var high = rt.TransformPoint(new Vector3(r.x + r.width, r.y + r.height, 0f));
        return $"y {low.y:0}..{high.y:0} x {low.x:0}..{high.x:0} ({r.width:0}x{r.height:0})";
    }

    private static string Components(Transform t)
    {
        var parts = new List<string>();
        foreach (var c in t.gameObject.GetComponents<Component>())
            if (c != null) parts.Add(Il2CppNames.ClassName(Il2CppNames.ClassOf(c)));
        return string.Join(", ", parts);
    }

    private static string Path(Transform t)
    {
        var parts = new List<string>();
        for (var cur = t; cur != null && parts.Count < 6; cur = cur.parent) parts.Add(cur.gameObject.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static string Describe(Transform parent)
    {
        return $"'{parent.gameObject.name}' [{Components(parent)}], {parent.childCount} children, {Size(parent)}";
    }

    private static string Size(Transform t)
    {
        var rt = t.TryCast<RectTransform>();
        return rt == null ? "no RectTransform" : $"{rt.rect.width:0}x{rt.rect.height:0}";
    }
}
