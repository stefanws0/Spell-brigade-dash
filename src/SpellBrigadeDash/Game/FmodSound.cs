namespace SpellBrigadeDash.Game;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>
/// The Spell Brigade plays all its audio through FMOD, not Unity's AudioSources, so the dash sound goes
/// through FMODUnity.RuntimeManager as well. Everything is reflection on the interop types, so the mod
/// doesn't need FMOD at build time and simply stays silent if anything is missing.
///
/// The event is Sound.FmodEvent from the config (an "event:/..." path), or "auto": the first event in the
/// game's loaded banks whose path matches Sound.ClipNameHints (dash, dodge, teleport, whoosh...).
/// The discovery report lists every event, so a better one can be picked by name.
/// </summary>
internal static class FmodSound
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public;

    private static bool _resolved;
    private static Type? _runtimeManager;
    private static string? _eventPath;
    private static bool _picked;
    private static bool _broken;

    public static string Status { get; private set; } = "not used yet";

    public static bool Available
    {
        get
        {
            if (!_resolved)
            {
                _resolved = true;
                _runtimeManager = Il2CppNames.FindInteropType("FMODUnity.RuntimeManager");
            }

            return _runtimeManager != null;
        }
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
            // Preferred: RuntimeManager.PlayOneShot(string path, Vector3 position)
            var playOneShot = _runtimeManager!.GetMethods(Static).FirstOrDefault(m =>
                m.Name == "PlayOneShot" && m.GetParameters().Length == 2 &&
                m.GetParameters()[0].ParameterType == typeof(string) && m.GetParameters()[1].ParameterType == typeof(Vector3));
            if (playOneShot != null)
            {
                playOneShot.Invoke(null, new object[] { _eventPath!, position });
                return;
            }

            // Fallback: instance = RuntimeManager.CreateInstance(path); instance.start(); instance.release();
            var create = _runtimeManager.GetMethods(Static).FirstOrDefault(m =>
                m.Name == "CreateInstance" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
            if (create == null) throw new MissingMethodException("RuntimeManager has neither PlayOneShot(string, Vector3) nor CreateInstance(string)");
            var instance = create.Invoke(null, new object[] { _eventPath! });
            instance?.GetType().GetMethod("start", Instance, null, Type.EmptyTypes, null)?.Invoke(instance, null);
            instance?.GetType().GetMethod("release", Instance, null, Type.EmptyTypes, null)?.Invoke(instance, null);
        }
        catch (Exception ex)
        {
            _broken = true;
            Status = $"playing '{_eventPath}' failed: {(ex.InnerException ?? ex).Message}";
            ModLog.Warn("Dash sound: " + Status);
        }
    }

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
            : $"no dash-like FMOD event among {events.Count} (see the discovery report and set Sound.FmodEvent)";
        ModLog.Info("Dash sound: " + Status);
    }

    /// <summary>Every event path in the game's loaded FMOD banks (empty if the FMOD API can't be reached).</summary>
    public static List<string> ListEvents()
    {
        var result = new List<string>();
        if (!Available) return result;
        try
        {
            var system = _runtimeManager!.GetProperty("StudioSystem", Static)?.GetValue(null);
            if (system == null) return result;
            foreach (var bank in OutArray(system, "getBankList"))
            foreach (var description in OutArray(bank, "getEventList"))
            {
                var path = OutString(description, "getPath");
                if (!string.IsNullOrEmpty(path)) result.Add(path!);
                if (result.Count >= 5000) return result;
            }
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Listing FMOD events failed: {(ex.InnerException ?? ex).Message}");
        }

        result.Sort(StringComparer.Ordinal);
        return result;
    }

    // FMOD's C# API returns results through out parameters: getBankList(out Bank[] array), getPath(out string path).
    private static IEnumerable<object> OutArray(object target, string method)
    {
        var m = target.GetType().GetMethods(Instance).FirstOrDefault(x => x.Name == method && x.GetParameters().Length == 1 && x.GetParameters()[0].IsOut);
        if (m == null) yield break;
        var args = new object?[1];
        m.Invoke(target, args);
        if (args[0] is not IEnumerable items) yield break;
        foreach (var item in items)
            if (item != null)
                yield return item;
    }

    private static string? OutString(object target, string method)
    {
        var m = target.GetType().GetMethods(Instance).FirstOrDefault(x => x.Name == method && x.GetParameters().Length == 1 && x.GetParameters()[0].IsOut);
        if (m == null) return null;
        var args = new object?[1];
        m.Invoke(target, args);
        return args[0]?.ToString();
    }
}
