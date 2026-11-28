// Managed stand-ins only: no Unity scene lifecycle, asset loading or player save access.
using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public class Object
    {
        public string name;
        public static void DontDestroyOnLoad(Object value) { }
    }
    public class ScriptableObject : Object { }
    public class MonoBehaviour : Object
    {
        public GameObject gameObject = new GameObject("care");
        protected static T FindAnyObjectByType<T>() where T : class => null;
        protected static void Destroy(Object value) { }
    }
    public class GameObject : Object { public GameObject(string name) { this.name = name; } public T AddComponent<T>() where T : new() => new T(); }
    public static class Application { public static bool isPlaying = true; }
    public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string text) { } }
    public sealed class MinAttribute : Attribute { public MinAttribute(float value) { } }
    public sealed class RangeAttribute : Attribute { public RangeAttribute(float min, float max) { } }
    public sealed class CreateAssetMenuAttribute : Attribute { public string fileName, menuName; }
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { } }
    public static class Mathf { public static int Max(int a, int b) => Math.Max(a,b); public static int Min(int a, int b) => Math.Min(a,b); }
    public static class Debug
    {
        public static int Exceptions;
        public static void LogError(string message, Object context) => throw new Exception(message);
        public static void LogException(Exception error, Object context) { Exceptions++; }
    }
}
namespace UnityEngine.Serialization
{ public sealed class FormerlySerializedAsAttribute : Attribute { public FormerlySerializedAsAttribute(string name) { } } }
public readonly struct HexCoordinates : IEquatable<HexCoordinates>
{
    private readonly int x;
    public HexCoordinates(int x) { this.x = x; }
    public bool Equals(HexCoordinates other) => x == other.x;
    public override bool Equals(object other) => other is HexCoordinates hex && Equals(hex);
    public override int GetHashCode() => x;
    public static bool operator ==(HexCoordinates a, HexCoordinates b) => a.Equals(b);
    public static bool operator !=(HexCoordinates a, HexCoordinates b) => !a.Equals(b);
}
public class ResourceData : UnityEngine.Object { public string resourceID; public int maxStackSize = 10000; }
public class AnimalSpeciesData { public string speciesID; }
public class CardData { }
public class ResourceStack { public string resourceId; public int amount; }
public class EconomySaveSystem
{
    public static readonly EconomySaveSystem Instance = new EconomySaveSystem();
    public void Attach(ResourceInventory inventory) { }
}
public static class WorldLaunch { public static bool IsVisit; }
public static class BreedingManager
{
    public static AnimalSpeciesData GetOrCreateBraluffSpecies() => new AnimalSpeciesData { speciesID = "braluff" };
    public static CardData GetOrCreateBraluffCard() => new CardData();
}
namespace LumiWorld.Acs
{
    public class ResourceProductionCatalog
    { public SpeciesMappingRegistry speciesRegistry = new SpeciesMappingRegistry(); public List<ResourceProductionDefinition> resources = new List<ResourceProductionDefinition>(); }
    public static class HabitatTopologyService { public const int MaximumHexCount = 6; }
}
