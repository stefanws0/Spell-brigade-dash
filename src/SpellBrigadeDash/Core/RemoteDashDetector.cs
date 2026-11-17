namespace SpellBrigadeDash.Core;

using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Spots other players' dashes from their synced positions alone, so no network message is needed.
///
/// A dash is a short speed spike far above walking speed. We measure the average speed over a small
/// time window instead of frame to frame. That way it also works when the network delivers positions
/// in steps (e.g. 20 updates/s with frames in between where the player "doesn't move").
///
/// Used for co-op i-frames (each client, the host included, protects a dashing remote wizard) and
/// for drawing other players' afterimages.
/// </summary>
public sealed class RemoteDashDetector
{
    private readonly Dictionary<long, Track> _tracks = new();

    /// <summary>Average speed (units/s) over the window that counts as a dash.</summary>
    public float SpeedThreshold { get; set; } = 15f;

    /// <summary>Displacement within one window larger than this is treated as a teleport, not a dash.</summary>
    public float MaxJump { get; set; } = 9f;

    /// <summary>Length of the averaging window in seconds.</summary>
    public float Window { get; set; } = 0.1f;

    /// <summary>Minimum time between two detected dashes for the same player.</summary>
    public float Cooldown { get; set; } = 0.15f;

    /// <summary>
    /// Feed the player's current position. Returns true exactly once at the start of a detected dash
    /// (rising edge of the speed spike).
    /// </summary>
    public bool Observe(long playerId, Vector3 position, float now)
    {
        if (!_tracks.TryGetValue(playerId, out var t))
        {
            t = new Track();
            _tracks[playerId] = t;
        }

        t.LastSeen = now;
        t.Samples.Add(new Sample(now, position));

        // Keep exactly one sample that is at least one window old (the "anchor"); drop older ones.
        var drop = 0;
        while (drop + 1 < t.Samples.Count - 1 && t.Samples[drop + 1].Time <= now - Window) drop++;
        if (drop > 0) t.Samples.RemoveRange(0, drop);

        var anchor = t.Samples[0];
        var span = now - anchor.Time;
        if (span < Window * 0.5f) return false; // not enough history yet

        var displacement = Vector3.Distance(position, anchor.Position);
        if (displacement > MaxJump)
        {
            // Teleport / respawn: reset so the next frames don't look like a dash.
            t.Samples.Clear();
            t.Samples.Add(new Sample(now, position));
            t.Above = false;
            return false;
        }

        var speed = displacement / span;
        var above = speed >= SpeedThreshold;
        var rising = above && !t.Above;
        t.Above = above;

        if (!rising || now - t.LastDetect < Cooldown) return false;

        t.LastDetect = now;
        return true;
    }

    /// <summary>Forget players that haven't been observed for a while (left the game, died...).</summary>
    public void Prune(float now, float maxAge = 5f)
    {
        var stale = new List<long>();
        foreach (var kv in _tracks)
            if (now - kv.Value.LastSeen > maxAge)
                stale.Add(kv.Key);
        foreach (var id in stale) _tracks.Remove(id);
    }

    public void Clear() => _tracks.Clear();

    // Plain struct on purpose: records / readonly structs need compiler attributes that clash with the game's interop assemblies.
    private struct Sample
    {
        public Sample(float time, Vector3 position)
        {
            Time = time;
            Position = position;
        }

        public float Time;
        public Vector3 Position;
    }

    private sealed class Track
    {
        public readonly List<Sample> Samples = new();
        public bool Above;
        public float LastDetect = float.NegativeInfinity;
        public float LastSeen;
    }
}
