namespace SpellBrigadeDash;

using System;
using System.Collections.Generic;
using BepInEx.Logging;

/// <summary>Thin wrapper around the BepInEx logger with "log this only once" helpers.</summary>
internal static class ModLog
{
    private static ManualLogSource? _log;
    private static readonly HashSet<string> Once = new();
    private static readonly Dictionary<string, float> Throttle = new();

    public static void Init(ManualLogSource log) => _log = log;

    public static void Info(string msg) => _log?.LogInfo(msg);
    public static void Warn(string msg) => _log?.LogWarning(msg);
    public static void Error(string msg) => _log?.LogError(msg);
    public static void Debug(string msg) => _log?.LogDebug(msg);

    public static void InfoOnce(string key, string msg)
    {
        if (Once.Add(key)) Info(msg);
    }

    public static void WarnOnce(string key, string msg)
    {
        if (Once.Add(key)) Warn(msg);
    }

    /// <summary>Log an exception at most once every <paramref name="seconds"/> per key, so a per-frame failure can't flood the log.</summary>
    public static void ErrorThrottled(string key, Exception ex, float now, float seconds = 10f)
    {
        if (Throttle.TryGetValue(key, out var last) && now - last < seconds) return;
        Throttle[key] = now;
        Error($"[{key}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
    }
}
