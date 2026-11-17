namespace SpellBrigadeDash.Game;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

/// <summary>
/// Helpers to identify native IL2CPP objects by class name, and to map those names back to the
/// interop .NET types BepInEx generated (BepInEx/interop/*.dll). This is what lets the mod find
/// game classes it was not compiled against.
/// </summary>
internal static class Il2CppNames
{
    private static Dictionary<string, Type>? _byFullName;
    private static Dictionary<string, Type?>? _byShortName; // null value = ambiguous short name
    private static bool _interopLoaded;

    // ---------------------------------------------------------------- native class names

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

    // ---------------------------------------------------------------- interop type lookup

    /// <summary>
    /// Find the interop type for a native class name, e.g. "PlayerController" or "Game.PlayerController".
    /// Tolerates the "Il2Cpp" namespace prefix that some generators add.
    /// </summary>
    public static Type? FindInteropType(string name)
    {
        EnsureIndex();
        if (_byFullName!.TryGetValue(name, out var t)) return t;
        if (_byFullName.TryGetValue("Il2Cpp." + name, out t)) return t;
        if (_byFullName.TryGetValue("Il2Cpp" + name, out t)) return t;
        var shortName = name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;
        return _byShortName!.TryGetValue(shortName, out t) ? t : null;
    }

    /// <summary>Wrap a native object as the interop type that matches its real class, so its fields can be read by reflection.</summary>
    public static object? WrapAsOwnType(Il2CppObjectBase obj, out Type? type)
    {
        type = FindInteropType(FullName(obj));
        return type == null ? null : Wrap(obj, type);
    }

    public static object? Wrap(Il2CppObjectBase obj, Type type)
    {
        try
        {
            return Activator.CreateInstance(type, obj.Pointer);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>All interop types from the game's own assemblies (not Unity, not .NET, not plugins).</summary>
    public static IEnumerable<Type> GameTypes()
    {
        EnsureIndex();
        return _byFullName!.Values.Distinct().Where(IsGameType);
    }

    public static bool IsGameType(Type t)
    {
        var asm = t.Assembly.GetName().Name ?? "";
        return !(asm.StartsWith("UnityEngine", StringComparison.Ordinal)
                 || asm.StartsWith("Unity.", StringComparison.Ordinal)
                 || asm.StartsWith("Il2Cppmscorlib", StringComparison.Ordinal)
                 || asm.StartsWith("Il2CppSystem", StringComparison.Ordinal)
                 || asm.StartsWith("Il2CppMono", StringComparison.Ordinal)
                 || asm.StartsWith("Il2CppInterop", StringComparison.Ordinal)
                 || asm.StartsWith("System", StringComparison.Ordinal)
                 || asm.StartsWith("Mono", StringComparison.Ordinal)
                 || asm.StartsWith("BepInEx", StringComparison.Ordinal)
                 || asm.StartsWith("SpellBrigadeDash", StringComparison.Ordinal)
                 || asm == "netstandard" || asm == "mscorlib" || asm == "0Harmony");
    }

    public static IEnumerable<Type> SafeGetTypes(Assembly asm)
    {
        try
        {
            return asm.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
        catch
        {
            return Array.Empty<Type>();
        }
    }

    /// <summary>
    /// .NET loads interop assemblies lazily, so the game's Assembly-CSharp may not be loaded yet.
    /// Load every DLL in BepInEx/interop by name (BepInEx's resolver serves them) so we can search them.
    /// </summary>
    public static void LoadInteropAssemblies()
    {
        if (_interopLoaded) return;
        _interopLoaded = true;
        try
        {
            var dir = Path.Combine(Paths.BepInExRootPath, "interop");
            if (!Directory.Exists(dir))
            {
                ModLog.Warn($"Interop folder not found at {dir}.");
                return;
            }

            var loaded = new HashSet<string>(AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name ?? ""));
            foreach (var file in Directory.GetFiles(dir, "*.dll"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (loaded.Contains(name)) continue;
                try
                {
                    Assembly.Load(new AssemblyName(name));
                }
                catch (Exception ex)
                {
                    ModLog.Debug($"Could not load interop assembly {name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Loading interop assemblies failed: {ex.Message}");
        }
    }

    /// <summary>Interop assemblies are the ones generated by BepInEx: they reference Il2CppInterop.Runtime.</summary>
    private static bool IsInteropAssembly(Assembly asm)
    {
        try
        {
            return asm.GetReferencedAssemblies().Any(n => n.Name == "Il2CppInterop.Runtime")
                   && !(asm.GetName().Name ?? "").StartsWith("SpellBrigadeDash", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureIndex()
    {
        if (_byFullName != null) return;
        LoadInteropAssemblies();

        _byFullName = new Dictionary<string, Type>(StringComparer.Ordinal);
        _byShortName = new Dictionary<string, Type?>(StringComparer.Ordinal);
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.IsDynamic || !IsInteropAssembly(asm)) continue;
            foreach (var t in SafeGetTypes(asm))
            {
                if (t == null || t.IsGenericTypeDefinition) continue;
                // interop classes derive from Il2CppObjectBase; interop enums and blittable structs don't, keep them too
                if (!typeof(Il2CppObjectBase).IsAssignableFrom(t) && !t.IsEnum && !t.IsValueType) continue;
                var full = (t.FullName ?? t.Name).Replace('+', '.');
                _byFullName.TryAdd(full, t);

                if (_byShortName.TryGetValue(t.Name, out var existing))
                {
                    if (existing != null && existing != t) _byShortName[t.Name] = null;
                }
                else
                {
                    _byShortName[t.Name] = t;
                }
            }
        }

        ModLog.Debug($"Indexed {_byFullName.Count} interop types.");
    }
}
