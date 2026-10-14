using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine
{
    public class Object
    {
        public string name;
        public static T FindAnyObjectByType<T>() where T : class => null;
        public static void Destroy(Object value) { }
        public static void DontDestroyOnLoad(Object value) { }
    }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
    }

    public class MonoBehaviour : Object
    {
    }

    public static class Mathf
    {
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
        public static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
        public static int CeilToInt(float value) => (int)Math.Ceiling(value);
    }

    public static class Resources
    {
        public static Object[] Assets = Array.Empty<Object>();
        public static T[] LoadAll<T>(string path) => Assets.OfType<T>().ToArray();
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; this.width = w; this.height = h; }
    }

    public enum TextureFormat { RGBA32 }

    public class Texture2D : Object
    {
        public int width = 64, height = 64;
        public Texture2D(int w, int h, TextureFormat fmt, bool mip) { }
        public bool LoadImage(byte[] data) => true;
    }

    public class Sprite : Object
    {
        public static Sprite Create(Texture2D tex, Rect rect, Vector2 pivot, float pixelsPerUnit = 100f) => new Sprite();
    }

    public static class Application
    {
        public static string dataPath = "Assets";
    }

    public static class Debug
    {
        public static void Log(object value, Object context = null) { }
        public static void LogWarning(object value, Object context = null) { }
        public static void LogError(object value, Object context = null) { }
    }

    public sealed class Header : Attribute { public Header(string value) { } }
    public sealed class Tooltip : Attribute { public Tooltip(string value) { } }
    public sealed class TextArea : Attribute { public TextArea(int min, int max) { } }
    public sealed class Min : Attribute { public Min(float value) { } }
    public sealed class CreateAssetMenu : Attribute { public string fileName, menuName; public int order; }
    public sealed class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int order) { } }
    public sealed class DisallowMultipleComponent : Attribute { }
    public sealed class SerializeField : Attribute { }
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public sealed class RuntimeInitializeOnLoadMethod : Attribute
    { public RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType type) { } }
}

public class ResourceData : UnityEngine.ScriptableObject
{
    public string resourceID;
    public string resourceName;
    public string description;
    public UnityEngine.Sprite icon;
    public UnityEngine.Color resourceColor;
    public string hexColor = "#FFFFFF";
    public int baseValue = 5;
    public int maxStackSize = 999;
}

public class ResourceInventory : UnityEngine.MonoBehaviour
{
    private static ResourceInventory _instance;
    public static ResourceInventory Instance => _instance ??= new ResourceInventory();
    public Dictionary<string, int> Amounts = new Dictionary<string, int>(StringComparer.Ordinal);
    public int GetAmount(string id) => Amounts.TryGetValue(id, out int val) ? val : 0;
    public int GetFreeSpace(ResourceData res) => res != null ? res.maxStackSize - GetAmount(res.resourceID) : 999;
    public bool HasAll(IList<ResourceStack> reqs) => reqs.All(r => GetAmount(r.resourceId) >= r.amount);
    public bool TryRemoveAll(IList<ResourceStack> reqs)
    {
        if (!HasAll(reqs)) return false;
        foreach (var r in reqs) Amounts[r.resourceId] -= r.amount;
        return true;
    }
    public int Add(ResourceData res, int amount)
    {
        if (res == null) return 0;
        int cur = GetAmount(res.resourceID);
        Amounts[res.resourceID] = cur + amount;
        return amount;
    }
}

