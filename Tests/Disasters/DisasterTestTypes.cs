using System;

namespace UnityEngine
{
    public class Object { public string name = string.Empty; }
    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
    }
    public class GameObject : Object { }

    public struct Color
    {
        public float r, g, b, a;
        public static readonly Color white = new Color(1f, 1f, 1f, 1f);
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
    }

    public static class Mathf
    {
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }

    public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string value) { } }
    public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string value) { } }
    public sealed class MinAttribute : Attribute { public MinAttribute(float value) { } }
    public sealed class RangeAttribute : Attribute { public RangeAttribute(float min, float max) { } }
    public sealed class ContextMenuAttribute : Attribute { public ContextMenuAttribute(string itemName) { } }
    public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string fileName;
        public string menuName;
    }
}

public struct HexCoordinates
{
    public int q, r;
    public HexCoordinates(int q, int r) { this.q = q; this.r = r; }
}

namespace LumiWorld.Acs
{
    public enum BiomeAffinity
    {
        None = 0,
        Leafwood = 1,
        Bloomfield = 2,
        Windheath = 3,
        Reedmarsh = 4,
        Lilywater = 5,
        Streamstone = 6,
        Rainleaf = 7
    }
}
