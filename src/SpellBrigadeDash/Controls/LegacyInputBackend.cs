namespace SpellBrigadeDash.Controls;

using System;
using UnityEngine;
using LegacyInput = UnityEngine.Input;

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
            _key = Parse(ModConfig.LegacyKey.Value, KeyCode.Space),
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

    public bool DebugPressed(DebugKey key)
    {
        var mod = ToKeyCode(ModConfig.HotkeyModifier.Value);
        if (mod != KeyCode.None && !LegacyInput.GetKey(mod)) return false;
        var k = ToKeyCode(ModConfig.DebugKeyPath(key));
        return k != KeyCode.None && LegacyInput.GetKeyDown(k);
    }

    /// <summary>Maps an Input System path like "&lt;Keyboard&gt;/1" or "&lt;Keyboard&gt;/leftCtrl" to the legacy KeyCode.</summary>
    internal static KeyCode ToKeyCode(string path)
    {
        var name = (path.Contains('/') ? path[(path.LastIndexOf('/') + 1)..] : path).Trim();
        if (name.Length == 0) return KeyCode.None;
        if (name.Length == 1 && char.IsDigit(name[0])) name = "Alpha" + name;
        else if (name.StartsWith("numpad", StringComparison.OrdinalIgnoreCase) && name.Length == 7 && char.IsDigit(name[6])) name = "Keypad" + name[6];
        else name = name switch
        {
            "leftCtrl" => "LeftControl",
            "rightCtrl" => "RightControl",
            "leftAlt" => "LeftAlt",
            "rightAlt" => "RightAlt",
            "leftShift" => "LeftShift",
            "rightShift" => "RightShift",
            _ => name,
        };
        return Enum.TryParse<KeyCode>(name, true, out var k) ? k : KeyCode.None;
    }
}
