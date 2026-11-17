namespace SpellBrigadeDash.Game;

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;

/// <summary>
/// Where an interop method's real machine code lives. Harmony patches on IL2CPP detour that native code, so:
///  * pointer 0 = no code (interface / abstract method): patching it detours address 0,
///  * the same pointer for two methods = the compiler merged identical method bodies, so patching one patches all
///    of them (with mismatched signatures). Both crashed the game in 0.3.0's level-up trace.
/// </summary>
internal static class NativeCode
{
    public static IntPtr Pointer(MethodBase method)
    {
        try
        {
            var field = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(method);
            if (field == null) return IntPtr.Zero;
            var methodInfo = (IntPtr)field.GetValue(null)!;
            // Il2CppMethodInfo starts with the method pointer
            return methodInfo == IntPtr.Zero ? IntPtr.Zero : Marshal.ReadIntPtr(methodInfo);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }
}
