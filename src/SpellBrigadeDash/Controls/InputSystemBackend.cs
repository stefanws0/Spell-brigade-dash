using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SpellBrigadeDash.Controls;

/// <summary>Unity's new Input System. The dash keys are our own InputActions, so rebinding is just a control path in the config.</summary>
internal sealed class InputSystemBackend : IInputBackend
{
    private InputAction? _keyboardDash;
    private InputAction? _gamepadDash;

    public string Name => "Unity Input System";

    public static InputSystemBackend Create()
    {
        // Touch the API once so a missing/stripped Input System throws here and not every frame.
        _ = Keyboard.current;

        var backend = new InputSystemBackend
        {
            _keyboardDash = MakeAction("SpellBrigadeDash.Keyboard", ModConfig.KeyboardBinding.Value),
            _gamepadDash = MakeAction("SpellBrigadeDash.Gamepad", ModConfig.GamepadBinding.Value),
        };
        if (backend._keyboardDash == null && backend._gamepadDash == null)
            throw new InvalidOperationException("No dash binding could be created.");
        return backend;
    }

    private static InputAction? MakeAction(string name, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            // All parameters passed explicitly: IL2CPP interop does not keep C# default values.
            var action = new InputAction(name, InputActionType.Button, path.Trim(), null, null, null);
            action.Enable();
            return action;
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Could not bind dash to '{path}': {ex.Message}");
            return null;
        }
    }

    public bool DashPressed() => Triggered(_keyboardDash) || Triggered(_gamepadDash);

    private static bool Triggered(InputAction? action)
    {
        if (action == null) return false;
        try
        {
            return action.triggered;
        }
        catch
        {
            return false;
        }
    }

    public Vector2 Move()
    {
        var v = Vector2.zero;
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (Down(kb.dKey) || Down(kb.rightArrowKey)) v.x += 1f;
            if (Down(kb.aKey) || Down(kb.leftArrowKey)) v.x -= 1f;
            if (Down(kb.wKey) || Down(kb.upArrowKey)) v.y += 1f;
            if (Down(kb.sKey) || Down(kb.downArrowKey)) v.y -= 1f;
        }

        var pad = Gamepad.current;
        if (pad != null)
        {
            try
            {
                var stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > v.sqrMagnitude) v = stick;
            }
            catch
            {
                // generic ReadValue may be unavailable in some IL2CPP builds; keyboard still works
            }
        }

        return v;
    }

    public bool DebugPressed(DebugKey key)
    {
        var kb = Keyboard.current;
        if (kb == null) return false;
        var control = key switch
        {
            DebugKey.F5 => kb.f5Key,
            DebugKey.F6 => kb.f6Key,
            DebugKey.F7 => kb.f7Key,
            DebugKey.F8 => kb.f8Key,
            DebugKey.F9 => kb.f9Key,
            DebugKey.F10 => kb.f10Key,
            _ => null,
        };
        return control != null && control.wasPressedThisFrame;
    }

    private static bool Down(ButtonControl? c) => c != null && c.isPressed;
}
