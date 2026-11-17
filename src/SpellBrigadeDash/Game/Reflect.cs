namespace SpellBrigadeDash.Game;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

/// <summary>A component on a GameObject, with its native class name and (if found) an interop-typed wrapper.</summary>
internal sealed class ComponentView
{
    public ComponentView(Component component)
    {
        Component = component;
        Chain = Il2CppNames.Chain(component);
        FullName = Chain.Count > 0 ? Chain[0] : "?";
        Wrapped = Il2CppNames.WrapAsOwnType(component, out var type);
        InteropType = type;
    }

    public Component Component { get; }
    public string FullName { get; }
    public string ShortName => FullName.Contains('.') ? FullName[(FullName.LastIndexOf('.') + 1)..] : FullName;
    public List<string> Chain { get; }
    public Type? InteropType { get; }
    public object? Wrapped { get; }

    public static List<ComponentView> Of(GameObject go)
    {
        var list = new List<ComponentView>();
        foreach (var c in go.GetComponents<Component>())
        {
            if (c == null) continue;
            try
            {
                list.Add(new ComponentView(c));
            }
            catch (Exception ex)
            {
                ModLog.Debug($"ComponentView failed: {ex.Message}");
            }
        }

        return list;
    }
}

/// <summary>Reads game fields/properties by name via reflection on interop types (interop exposes IL2CPP fields as properties).</summary>
internal static class Reflect
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
    private static readonly Dictionary<(Type, string), Func<object, bool>?> BoolCache = new();

    /// <summary>
    /// Look for a bool member called <paramref name="memberName"/> on any of the components.
    /// A leading "!" inverts the value. Returns true if the member exists.
    /// </summary>
    public static bool TryReadBool(IEnumerable<ComponentView> views, string memberName, out bool value, out string where)
    {
        var invert = memberName.StartsWith('!');
        var name = invert ? memberName[1..] : memberName;
        foreach (var v in views)
        {
            if (v.Wrapped == null || v.InteropType == null) continue;
            var getter = BoolGetter(v.InteropType, name);
            if (getter == null) continue;
            try
            {
                value = getter(v.Wrapped) ^ invert;
                where = $"{v.ShortName}.{name}";
                return true;
            }
            catch
            {
                // member threw (e.g. object destroyed) - try the next component
            }
        }

        value = false;
        where = "";
        return false;
    }

    /// <summary>Check a list of member names; returns the first one found.</summary>
    public static bool TryReadAnyBool(IEnumerable<ComponentView> views, IEnumerable<string> memberNames, out bool value, out string where)
    {
        var list = views as IList<ComponentView> ?? views.ToList();
        foreach (var name in memberNames)
            if (TryReadBool(list, name, out value, out where))
                return true;
        value = false;
        where = "";
        return false;
    }

    public static Func<object, bool>? BoolGetter(Type type, string name)
    {
        if (BoolCache.TryGetValue((type, name), out var cached)) return cached;
        Func<object, bool>? getter = null;
        try
        {
            var prop = type.GetProperties(Flags).FirstOrDefault(p => p.Name == name && p.PropertyType == typeof(bool) && p.CanRead && p.GetIndexParameters().Length == 0);
            if (prop != null)
            {
                getter = o => (bool)prop.GetValue(o)!;
            }
            else
            {
                var field = type.GetFields(Flags).FirstOrDefault(f => f.Name == name && f.FieldType == typeof(bool));
                if (field != null)
                {
                    getter = o => (bool)field.GetValue(o)!;
                }
                else
                {
                    var method = type.GetMethods(Flags).FirstOrDefault(m => m.Name == name && m.ReturnType == typeof(bool) && m.GetParameters().Length == 0 && !m.IsGenericMethodDefinition);
                    if (method != null) getter = o => (bool)method.Invoke(o, null)!;
                }
            }
        }
        catch (Exception ex)
        {
            ModLog.Debug($"BoolGetter({type.Name}.{name}) failed: {ex.Message}");
        }

        BoolCache[(type, name)] = getter;
        return getter;
    }

    /// <summary>The type and its base types, stopping at Unity/interop base classes.</summary>
    public static IEnumerable<Type> SelfAndGameBases(Type type)
    {
        for (var t = type; t != null && t != typeof(object) && t != typeof(Il2CppObjectBase); t = t.BaseType)
        {
            if (!Il2CppNames.IsGameType(t)) yield break;
            yield return t;
        }
    }
}
