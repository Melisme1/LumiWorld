using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object
    {
        public string name = string.Empty;
        public static void Destroy(Object value) { }
    }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
    }

    public class MonoBehaviour : Object
    {
    }

    public class Sprite : Object
    {
    }

    public class GameObject : Object
    {
        private readonly Dictionary<Type, object> components = new Dictionary<Type, object>();

        public T AddComponent<T>() where T : new()
        {
            var inst = new T();
            components[typeof(T)] = inst;
            return inst;
        }

        public T GetComponent<T>() where T : class
        {
            return components.TryGetValue(typeof(T), out var inst) ? inst as T : null;
        }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
    }

    public struct Vector2
    {
        public float x, y;
        public static readonly Vector2 zero = new Vector2(0f, 0f);
        public Vector2(float x, float y) { this.x = x; this.y = y; }
    }

    public struct Vector2Int
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
    }

    public struct Vector3
    {
        public float x;
        public float y;
        public float z;

        public static readonly Vector3 zero = new Vector3(0f, 0f, 0f);
        public static readonly Vector3 one = new Vector3(1f, 1f, 1f);

        public float sqrMagnitude => x * x + y * y + z * z;

        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);

        public static float Distance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dy = a.y - b.y;
            float dz = a.z - b.z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }

    public struct RaycastHit
    {
        public Vector3 normal;
    }

    public static class Mathf
    {
        public const float Rad2Deg = 57.29578f;

        public static int Abs(int value) => Math.Abs(value);
        public static float Abs(float value) => Math.Abs(value);
        public static int RoundToInt(float value) => (int)Math.Round(value, MidpointRounding.ToEven);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Repeat(float t, float length) => t - (float)Math.Floor(t / length) * length;

        public static float DeltaAngle(float current, float target)
        {
            float num = Repeat(target - current, 360f);
            if (num > 180f) num -= 360f;
            return num;
        }

        public static float LerpAngle(float a, float b, float t)
        {
            float delta = Repeat(b - a, 360f);
            if (delta > 180f) delta -= 360f;
            float clamped = t < 0f ? 0f : (t > 1f ? 1f : t);
            return a + delta * clamped;
        }
    }

    public static class Random
    {
        private static readonly System.Random Rng = new System.Random(42);
        public static int Range(int minInclusive, int maxExclusive) => Rng.Next(minInclusive, maxExclusive);
        public static float Range(float minInclusive, float maxInclusive)
            => minInclusive + (float)Rng.NextDouble() * (maxInclusive - minInclusive);
    }

    public static class Debug
    {
        public static readonly List<string> Warnings = new List<string>();
        public static void LogWarning(object value, Object context = null) => Warnings.Add(value?.ToString() ?? string.Empty);
        public static void Log(object value, Object context = null) { }
        public static void LogError(object value, Object context = null) { }
    }

    public sealed class SerializeField : Attribute { }
    public sealed class Header : Attribute { public Header(string value) { } }
    public sealed class Tooltip : Attribute { public Tooltip(string value) { } }
    public sealed class TextArea : Attribute { public TextArea(int min, int max) { } }
    public sealed class MinAttribute : Attribute { public MinAttribute(float value) { } }
    public sealed class RangeAttribute : Attribute
    {
        public RangeAttribute(float min, float max) { }
        public RangeAttribute(int min, int max) { }
    }
    public sealed class InspectorNameAttribute : Attribute { public InspectorNameAttribute(string value) { } }
    public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string fileName;
        public string menuName;
        public int order;
    }
}

namespace LumiWorld.Acs
{
    public class BiomeSurface { }
}

public class ResourceData : UnityEngine.ScriptableObject
{
    public string resourceID;
}

public class HexWorldGenerator : UnityEngine.MonoBehaviour
{
}
