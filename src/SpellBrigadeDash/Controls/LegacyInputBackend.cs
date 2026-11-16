using System;
using UnityEngine;
using LegacyInput = UnityEngine.Input;

namespace SpellBrigadeDash.Controls;

/// <summary>Fallback for games that use the old UnityEngine.Input manager.</summary>
internal sealed class LegacyInputBackend : IInputBackend
{
    private KeyCode _key;
    private KeyCode _padKey;

    public string Name => "Legacy UnityEngine.Input";

    public static LegacyInputBackend Create()
    {
        var b = new LegacyInputBackend
        {
            _key = Parse(ModConfig.LegacyKey.Value, KeyCode.LeftShift),
            _padKey = Parse(ModConfig.LegacyGamepadKey.Value, KeyCode.JoystickButton4),
        };

        // Throws InvalidOperationException if the project is set to "Input System only".
        _ = LegacyInput.GetKey(b._key);
        return b;
    }

    private static KeyCode Parse(string name, KeyCode fallback)
        => Enum.TryParse<KeyCode>(name, true, out var k) ? k : fallback;

    public bool DashPressed() => LegacyInput.GetKeyDown(_key) || (_padKey != KeyCode.None && LegacyInput.GetKeyDown(_padKey));

    public Vector2 Move()
    {
        var v = Vector2.zero;
        if (LegacyInput.GetKey(KeyCode.D) || LegacyInput.GetKey(KeyCode.RightArrow)) v.x += 1f;
        if (LegacyInput.GetKey(KeyCode.A) || LegacyInput.GetKey(KeyCode.LeftArrow)) v.x -= 1f;
        if (LegacyInput.GetKey(KeyCode.W) || LegacyInput.GetKey(KeyCode.UpArrow)) v.y += 1f;
        if (LegacyInput.GetKey(KeyCode.S) || LegacyInput.GetKey(KeyCode.DownArrow)) v.y -= 1f;
        if (v == Vector2.zero)
        {
            try
            {
                v = new Vector2(LegacyInput.GetAxisRaw("Horizontal"), LegacyInput.GetAxisRaw("Vertical"));
            }
            catch
            {
                // axes not defined
            }
        }

        return v;
    }

    public bool DebugPressed(DebugKey key) => LegacyInput.GetKeyDown(key switch
    {
        DebugKey.F5 => KeyCode.F5,
        DebugKey.F6 => KeyCode.F6,
        DebugKey.F7 => KeyCode.F7,
        DebugKey.F8 => KeyCode.F8,
        DebugKey.F9 => KeyCode.F9,
        _ => KeyCode.F10,
    });
}
