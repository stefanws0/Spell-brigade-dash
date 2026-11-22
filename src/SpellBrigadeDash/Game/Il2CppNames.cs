namespace SpellBrigadeDash.Game;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

/// <summary>
/// Native IL2CPP class names of game objects (for log lines). The developer build adds a search over the
/// interop assemblies to this class (Discovery/Il2CppNames.Interop.cs); the released mod uses the game's
/// classes directly instead.
/// </summary>
internal static partial class Il2CppNames
{
    public static string ClassName(IntPtr klass)
        => klass == IntPtr.Zero ? "?" : Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(klass)) ?? "?";

    public static string Namespace(IntPtr klass)
        => klass == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_namespace(klass)) ?? "";

    public static string FullName(IntPtr klass)
    {
        var ns = Namespace(klass);
        var name = ClassName(klass);
        return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
    }

    public static IntPtr ClassOf(Il2CppObjectBase obj) => IL2CPP.il2cpp_object_get_class(obj.Pointer);

    public static string FullName(Il2CppObjectBase obj) => FullName(ClassOf(obj));

    public static string ShortName(Il2CppObjectBase obj) => ClassName(ClassOf(obj));

    /// <summary>Class name of the object followed by all its base classes, most-derived first.</summary>
    public static List<string> Chain(Il2CppObjectBase obj)
    {
        var chain = new List<string>();
        var klass = ClassOf(obj);
        var guard = 0;
        while (klass != IntPtr.Zero && guard++ < 32)
        {
            chain.Add(FullName(klass));
            klass = IL2CPP.il2cpp_class_get_parent(klass);
        }

        return chain;
    }
}
