namespace SpellBrigadeDash.Game;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>
/// The Spell Brigade plays all its audio through FMOD, not Unity's AudioSources, so the dash sound goes through
/// <c>FMODUnity.RuntimeManager</c> as well. If anything about FMOD is missing, the mod simply stays silent.
///
/// The event is Sound.FmodEvent from the config (an "event:/..." path), or "auto": the first event in the
/// game's loaded banks whose path matches Sound.ClipNameHints (dash, dodge, teleport, whoosh...).
/// </summary>
internal static class FmodSound
{
    private static bool? _available;
    private static string? _eventPath;
    private static bool _picked;
    private static bool _broken;

    public static string Status { get; private set; } = "not used yet";

    public static bool Available
    {
        get
        {
            if (_available == null)
            {
                try
                {
                    _available = FmodInitialized();
                }
                catch (Exception ex)
                {
                    _available = false;
                    Status = "FMOD not reachable: " + ex.Message;
                    ModLog.Warn("Dash sound: " + Status);
                }
            }

            return _available.Value;
        }
    }

    // Separate method: if the FMOD interop assembly were missing, the TypeLoadException surfaces here, inside the try above.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool FmodInitialized()
    {
        _ = FMODUnity.RuntimeManager.IsInitialized;
        return true;
    }

    /// <summary>Pick the event ahead of time (reading the bank list can take a moment), e.g. a few seconds into a run.</summary>
    public static void Prepare()
    {
        if (!_picked && !_broken && Available) Pick();
    }

    public static void Play(Vector3 position)
    {
        if (_broken || !Available) return;
        if (!_picked) Pick();
        if (string.IsNullOrEmpty(_eventPath)) return;

        try
        {
            FMODUnity.RuntimeManager.PlayOneShot(_eventPath!, position);
        }
        catch (Exception ex)
        {
            _broken = true;
            Status = $"playing '{_eventPath}' failed: {ex.Message}";
            ModLog.Warn("Dash sound: " + Status);
        }
    }

    /// <summary>Play any FMOD event by path (e.g. the Blazing Dash fire sound). A path that fails once is skipped from then on.</summary>
    public static void PlayEvent(string path, Vector3 position)
    {
        path = path.Trim();
        if (path.Length == 0 || FailedPaths.Contains(path) || !Available) return;
        try
        {
            FMODUnity.RuntimeManager.PlayOneShot(path, position);
        }
        catch (Exception ex)
        {
            FailedPaths.Add(path);
            ModLog.Warn($"FMOD event '{path}' failed ({ex.Message}); it won't be played again this session.");
        }
    }

    private static readonly HashSet<string> FailedPaths = new(StringComparer.Ordinal);

    private static void Pick()
    {
        _picked = true;
        var configured = ModConfig.SoundFmodEvent.Value.Trim();
        if (configured.Length > 0 && !configured.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            _eventPath = configured;
            Status = $"using configured FMOD event '{configured}'";
            ModLog.Info("Dash sound: " + Status);
            return;
        }

        var events = ListEvents();
        foreach (var hint in ModConfig.SplitList(ModConfig.SoundClipHints.Value.ToLowerInvariant()))
        {
            _eventPath = events.FirstOrDefault(e =>
            {
                var l = e.ToLowerInvariant();
                return l.Contains(hint) && !l.Contains("loop") && !l.Contains("music") && !l.Contains("amb");
            });
            if (_eventPath != null) break;
        }

        Status = _eventPath != null
            ? $"auto-picked FMOD event '{_eventPath}' (out of {events.Count}); set Sound.FmodEvent to choose another"
            : $"no dash-like FMOD event among {events.Count} (set Sound.FmodEvent)";
        ModLog.Info("Dash sound: " + Status);
    }

    /// <summary>Every event path in the game's loaded FMOD banks (empty if FMOD can't be reached).</summary>
    public static List<string> ListEvents()
    {
        var result = new List<string>();
        if (!Available) return result;
        try
        {
            var system = FMODUnity.RuntimeManager.StudioSystem;
            system.getBankList(out var banks);
            if (banks == null) return result;
            foreach (var bank in banks)
            {
                bank.getEventList(out var descriptions);
                if (descriptions == null) continue;
                foreach (var description in descriptions)
                {
                    description.getPath(out var path);
                    if (!string.IsNullOrEmpty(path)) result.Add(path);
                    if (result.Count >= 5000) return result;
                }
            }
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Listing FMOD events failed: {ex.Message}");
        }

        result.Sort(StringComparer.Ordinal);
        return result;
    }
}
