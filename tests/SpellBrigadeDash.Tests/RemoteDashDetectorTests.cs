using System.Numerics;
using SpellBrigadeDash.Core;
using Xunit;

namespace SpellBrigadeDash.Tests;

public class RemoteDashDetectorTests
{
    private const float Frame = 1f / 60f;

    private static RemoteDashDetector NewDetector() => new()
    {
        SpeedThreshold = 15f, // 60% of a 25 u/s dash
        MaxJump = 12f,
        Window = 0.1f,
    };

    [Fact]
    public void Walking_is_not_a_dash()
    {
        var d = NewDetector();
        var t = 0f;
        var x = 0f;
        for (var i = 0; i < 120; i++, t += Frame)
        {
            x += 6f * Frame; // 6 units/s
            Assert.False(d.Observe(1, new Vector3(x, 0, 0), t));
        }
    }

    [Fact]
    public void A_smooth_dash_is_detected_exactly_once()
    {
        var d = NewDetector();
        var t = 0f;
        var x = 0f;
        var detections = 0;
        for (var i = 0; i < 30; i++, t += Frame) detections += d.Observe(1, new Vector3(x += 5f * Frame, 0, 0), t) ? 1 : 0;
        for (var i = 0; i < 8; i++, t += Frame) detections += d.Observe(1, new Vector3(x += 25f * Frame, 0, 0), t) ? 1 : 0; // 0.13s dash
        for (var i = 0; i < 30; i++, t += Frame) detections += d.Observe(1, new Vector3(x += 5f * Frame, 0, 0), t) ? 1 : 0;
        Assert.Equal(1, detections);
    }

    [Fact]
    public void A_dash_arriving_in_network_steps_is_still_detected()
    {
        // Positions only change every 3rd frame (20 Hz network updates without interpolation).
        var d = NewDetector();
        var t = 0f;
        var x = 0f;
        var shown = 0f;
        var detections = 0;
        for (var i = 0; i < 60; i++, t += Frame)
        {
            var speed = i is >= 30 and < 38 ? 25f : 5f;
            x += speed * Frame;
            if (i % 3 == 0) shown = x;
            detections += d.Observe(7, new Vector3(shown, 0, 0), t) ? 1 : 0;
        }

        Assert.Equal(1, detections);
    }

    [Fact]
    public void A_teleport_is_not_a_dash()
    {
        var d = NewDetector();
        var t = 0f;
        for (var i = 0; i < 20; i++, t += Frame) d.Observe(1, Vector3.Zero, t);
        Assert.False(d.Observe(1, new Vector3(100, 0, 0), t));
        t += Frame;
        Assert.False(d.Observe(1, new Vector3(100, 0, 0), t));
    }

    [Fact]
    public void Players_are_tracked_independently()
    {
        var d = NewDetector();
        var t = 0f;
        var a = 0f;
        var hitsA = 0;
        var hitsB = 0;
        for (var i = 0; i < 40; i++, t += Frame)
        {
            a += (i is >= 20 and < 28 ? 25f : 4f) * Frame;
            hitsA += d.Observe(1, new Vector3(a, 0, 0), t) ? 1 : 0;
            hitsB += d.Observe(2, new Vector3(0, 0, 4f * t), t) ? 1 : 0;
        }

        Assert.Equal(1, hitsA);
        Assert.Equal(0, hitsB);
    }
}
