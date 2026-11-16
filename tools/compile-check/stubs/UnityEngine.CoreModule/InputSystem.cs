// COMPILE-CHECK STUB ONLY - approximates the interop API of Unity.InputSystem.dll.
using System;

namespace UnityEngine.InputSystem
{
    public enum InputActionType { Value, Button, PassThrough }

    public sealed class InputAction : Il2CppSystem.Object
    {
        public InputAction(IntPtr pointer) : base(pointer) { }
        public InputAction(string name, InputActionType type, string binding, string interactions, string processors, string expectedControlType) : base(IntPtr.Zero) { }
        public bool triggered => false;
        public void Enable() { }
        public void Disable() { }
    }

    public class InputActionAsset : ScriptableObject
    {
        public InputActionAsset(IntPtr pointer) : base(pointer) { }
        public string ToJson() => null;
    }

    public class InputDevice : Il2CppSystem.Object
    {
        public InputDevice(IntPtr pointer) : base(pointer) { }
    }

    public class Keyboard : InputDevice
    {
        public Keyboard(IntPtr pointer) : base(pointer) { }
        public static Keyboard current => null;
        public Controls.KeyControl wKey => null;
        public Controls.KeyControl aKey => null;
        public Controls.KeyControl sKey => null;
        public Controls.KeyControl dKey => null;
        public Controls.KeyControl upArrowKey => null;
        public Controls.KeyControl downArrowKey => null;
        public Controls.KeyControl leftArrowKey => null;
        public Controls.KeyControl rightArrowKey => null;
        public Controls.KeyControl f5Key => null;
        public Controls.KeyControl f6Key => null;
        public Controls.KeyControl f7Key => null;
        public Controls.KeyControl f8Key => null;
        public Controls.KeyControl f9Key => null;
        public Controls.KeyControl f10Key => null;
    }

    public class Gamepad : InputDevice
    {
        public Gamepad(IntPtr pointer) : base(pointer) { }
        public static Gamepad current => null;
        public Controls.StickControl leftStick => null;
    }

    public class InputControl<TValue> : Il2CppSystem.Object
    {
        public InputControl(IntPtr pointer) : base(pointer) { }
        public TValue ReadValue() => default;
    }
}

namespace UnityEngine.InputSystem.Controls
{
    public class ButtonControl : InputControl<float>
    {
        public ButtonControl(IntPtr pointer) : base(pointer) { }
        public bool isPressed => false;
        public bool wasPressedThisFrame => false;
    }

    public class KeyControl : ButtonControl
    {
        public KeyControl(IntPtr pointer) : base(pointer) { }
    }

    public class Vector2Control : InputControl<Vector2>
    {
        public Vector2Control(IntPtr pointer) : base(pointer) { }
    }

    public class StickControl : Vector2Control
    {
        public StickControl(IntPtr pointer) : base(pointer) { }
    }
}
