using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace UnityEngine
{
    public class Object
    {
        public string name = string.Empty;
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
        public GameObject gameObject { get; internal set; }
    }

    public class Sprite : Object
    {
    }

    public class GameObject : Object
    {
        private readonly Dictionary<Type, object> components = new Dictionary<Type, object>();
        public GameObject() { }
        public GameObject(string name) { this.name = name; }

        public T AddComponent<T>() where T : new()
        {
            var inst = new T();
            if (inst is MonoBehaviour mb) mb.gameObject = this;
            components[typeof(T)] = inst;
            return inst;
        }

        public T GetComponent<T>() where T : class
        {
            return components.TryGetValue(typeof(T), out var inst) ? inst as T : null;
        }
    }

    public static class Application
    {
        public static bool isPlaying = true;
        public static string persistentDataPath = Path.Combine(Path.GetTempPath(), "lumi_test_saves");
    }

    public static class JsonUtility
    {
        private static readonly JsonSerializerOptions Opts = new JsonSerializerOptions { IncludeFields = true };
        public static string ToJson(object obj, bool prettyPrint = false) =>
            JsonSerializer.Serialize(obj, obj?.GetType() ?? typeof(object),
                new JsonSerializerOptions { IncludeFields = true, WriteIndented = prettyPrint });

        public static T FromJson<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return default;
            try
            {
                return JsonSerializer.Deserialize<T>(json, Opts);
            }
            catch (JsonException ex)
            {
                throw new ArgumentException("Invalid JSON", ex);
            }
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
        public float x, y, z;
        public static readonly Vector3 zero = new Vector3(0f, 0f, 0f);
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }

    public struct Rect
    {
        public float xMin, yMin, xMax, yMax;
        public float width => xMax - xMin;
        public float height => yMax - yMin;

        public static Rect MinMaxRect(float xmin, float ymin, float xmax, float ymax)
            => new Rect { xMin = xmin, yMin = ymin, xMax = xmax, yMax = ymax };
    }

    public struct RaycastHit
    {
        public Vector3 normal;
    }

    public static class Mathf
    {
        public static int Abs(int value) => Math.Abs(value);
        public static float Abs(float value) => Math.Abs(value);
        public static int Max(int a, int b) => Math.Max(a, b);
        public static int RoundToInt(float value) => (int)Math.Round(value, MidpointRounding.ToEven);
    }

    public static class Debug
    {
        public static void Log(object value, Object context = null) { }
        public static void LogWarning(object value, Object context = null) { }
        public static void LogError(object value, Object context = null) { }
    }

    public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string value) { } }
    public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string value) { } }
    public sealed class TextAreaAttribute : Attribute { public TextAreaAttribute(int min, int max) { } }
    public sealed class MinAttribute : Attribute { public MinAttribute(float value) { } }
    public sealed class SerializeField : Attribute { }
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
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { } }
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

public static class SaveSlot
{
    public const string GuestId = "guest";
}

public static class WorldShareService
{
    public const int MaxSnapshotBytes = 300 * 1024;
    public const int MapColumns = 10;
}

public class CardCatalog
{
    private readonly Dictionary<string, CardData> map = new Dictionary<string, CardData>(StringComparer.Ordinal);
    public void Register(CardData card)
    {
        if (card != null && !string.IsNullOrEmpty(card.cardID)) map[card.cardID] = card;
    }
    public CardData Find(string id) => id != null && map.TryGetValue(id, out var card) ? card : null;
}

public class WorldSaveSystem
{
    public static readonly WorldSaveSystem Instance = new WorldSaveSystem();
    public readonly CardCatalog Cards = new CardCatalog();
}
