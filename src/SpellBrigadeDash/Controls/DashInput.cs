namespace SpellBrigadeDash.Controls;

using System;
using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>Debug actions. Default keys: hold Left Ctrl and press 1-6 (rebindable under [Debug] in the config).</summary>
internal enum DebugKey
{
    Report,
    ToggleDash,
    ExtraCharge,
    QuickRecovery,
    LongStride,
    BlazingDash,
}

internal interface IInputBackend
{
    string Name { get; }
    bool DashPressed();

    /// <summary>Raw movement input: WASD / arrows / left stick, x = right, y = up. Not normalised.</summary>
    Vector2 Move();

    bool DebugPressed(DebugKey key);
}

/// <summary>
/// Reads the dash button and the movement direction. It prefers Unity's new Input System (most modern
/// games, controller support) and falls back to the legacy UnityEngine.Input. Each backend sits in
/// its own class, so a missing Unity.InputSystem.dll can't break loading the mod.
/// </summary>
internal sealed class DashInput
{
    private IInputBackend? _backend;
    private bool _triedInit;

    public string BackendName => _backend?.Name ?? "none";
    public bool DashPressed { get; private set; }
    public Vector2 Move { get; private set; }

    public void Poll()
    {
        if (!_triedInit)
        {
            _triedInit = true;
            ModLog.Step("input: creating backend");
            _backend = TryInputSystem() ?? TryLegacy();
            ModLog.Info($"Input backend: {BackendName}");
        }

        if (_backend == null)
        {
            DashPressed = false;
            Move = Vector2.zero;
            return;
        }

        try
        {
            DashPressed = _backend.DashPressed();
            Move = _backend.Move();
        }
        catch (Exception ex)
        {
            ModLog.ErrorThrottled("input", ex, Time.unscaledTime, 30f);
            DashPressed = false;
            Move = Vector2.zero;
        }
    }

    public bool DebugPressed(DebugKey key)
    {
        try
        {
            return _backend != null && _backend.DebugPressed(key);
        }
        catch
        {
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IInputBackend? TryInputSystem()
    {
        try
        {
            return InputSystemBackend.Create();
        }
        catch (Exception ex)
        {
            ModLog.Info($"New Input System not usable ({ex.GetType().Name}: {ex.Message}); trying legacy input.");
            return null;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IInputBackend? TryLegacy()
    {
        try
        {
            return LegacyInputBackend.Create();
        }
        catch (Exception ex)
        {
            ModLog.Error($"No usable input backend: {ex.Message}. Dash disabled.");
            return null;
        }
    }
}
