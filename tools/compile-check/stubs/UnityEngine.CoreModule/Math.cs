// COMPILE-CHECK STUB ONLY - value types copied from Unity's public API shape.
using System;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x;
        public float y;

        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public static Vector2 zero => new(0, 0);
        public static Vector2 one => new(1, 1);
        public static Vector2 right => new(1, 0);
        public float sqrMagnitude => x * x + y * y;
        public float magnitude => MathF.Sqrt(sqrMagnitude);

        public void Normalize()
        {
            var m = magnitude;
            if (m > 1e-5f) { x /= m; y /= m; }
            else { x = 0; y = 0; }
        }

        public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 a, float d) => new(a.x * d, a.y * d);
        public static bool operator ==(Vector2 a, Vector2 b) => a.x == b.x && a.y == b.y;
        public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);
        public static implicit operator Vector2(Vector3 v) => new(v.x, v.y);
        public static implicit operator Vector3(Vector2 v) => new(v.x, v.y, 0);
        public override bool Equals(object o) => o is Vector2 v && v == this;
        public override int GetHashCode() => 0;
    }

    public struct Vector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vector3 zero => new(0, 0, 0);
        public static Vector3 one => new(1, 1, 1);
        public static Vector3 up => new(0, 1, 0);
        public static Vector3 right => new(1, 0, 0);
        public static Vector3 forward => new(0, 0, 1);
        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => MathF.Sqrt(sqrMagnitude);
        public Vector3 normalized { get { var v = this; v.Normalize(); return v; } }

        public void Normalize()
        {
            var m = magnitude;
            if (m > 1e-5f) { x /= m; y /= m; z /= m; }
            else { x = 0; y = 0; z = 0; }
        }

        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float d) => new(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator *(float d, Vector3 a) => new(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator /(Vector3 a, float d) => new(a.x / d, a.y / d, a.z / d);
        public static bool operator ==(Vector3 a, Vector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public override bool Equals(object o) => o is Vector3 v && v == this;
        public override int GetHashCode() => 0;
        public override string ToString() => $"({x:F2}, {y:F2}, {z:F2})";
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public static Quaternion identity => new() { w = 1 };
        public static Quaternion Euler(float x, float y, float z) => identity;
        public static Quaternion LookRotation(Vector3 forward, Vector3 upwards) => identity;
        public static Quaternion operator *(Quaternion lhs, Quaternion rhs) => lhs;
    }

    public struct Vector4
    {
        public float x, y, z, w;

        public Vector4(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }
    }

    public struct Color
    {
        public float r, g, b, a;

        public Color(float r, float g, float b, float a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public Color(float r, float g, float b) : this(r, g, b, 1f) { }
        public static Color white => new(1, 1, 1, 1);
        public static Color Lerp(Color a, Color b, float t) => new(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
    }

    public struct Rect
    {
        public Rect(float x, float y, float width, float height)
        {
            this.width = width;
            this.height = height;
        }

        public float width { get; set; }
        public float height { get; set; }
    }

    public struct Bounds
    {
        public Vector3 center => default;
        public Vector3 size => default;
        public Vector3 extents => default;
        public Vector3 min => default;
    }

    public static class Mathf
    {
        public static float Clamp(float v, float min, float max) => Math.Clamp(v, min, max);
        public static float Clamp01(float v) => Math.Clamp(v, 0f, 1f);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float MoveTowards(float current, float target, float maxDelta) => Math.Abs(target - current) <= maxDelta ? target : current + Math.Sign(target - current) * maxDelta;
        public static float Sin(float f) => MathF.Sin(f);
        public static float Sqrt(float f) => MathF.Sqrt(f);
    }
}
