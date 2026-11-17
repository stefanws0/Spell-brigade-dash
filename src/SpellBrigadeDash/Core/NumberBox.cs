namespace SpellBrigadeDash.Core;

using System;

/// <summary>
/// Boxes a number as exactly the type a reflected member expects. Reflection's SetValue doesn't convert between
/// numeric types, and a <c>?:</c> chain like <c>t == int ? (int)v : t == double ? (double)v : v</c> silently widens
/// every branch to double, which is what broke the dash's HP cost in 0.3.2.
/// </summary>
public static class NumberBox
{
    public static object As(float value, Type type)
    {
        if (type == typeof(float)) return value;
        if (type == typeof(int)) return (int)value;
        if (type == typeof(double)) return (double)value;
        throw new ArgumentException($"Not a supported number type: {type.Name}", nameof(type));
    }
}
