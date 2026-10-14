// Managed stand-ins for lifecycle/adapter checks. These are outside Assets and never ship.
// They do not simulate Unity rendering, native object lifetime or engine serialization.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace UnityEngine
{
    public class Object
    {
        public string name;
        public static T FindAnyObjectByType<T>() where T : class => null;
        public static void Destroy(Object value) { }
        public static void DontDestroyOnLoad(Object value) { }
    }
    public class MonoBehaviour : Object
    {
        public Transform transform = new Transform();
        public GameObject gameObject = new GameObject("test");
    }
    public class Transform { public Transform parent; }
    public class GameObject : Object
    {
        public GameObject(string value) { name = value; }
        public T AddComponent<T>() => throw new InvalidOperationException("Unexpected auto-bootstrap in test: " + typeof(T));
    }
    public static class Application { public static bool isPlaying = true, runInBackground; }
    public static class Time { public static float timeScale = 1, unscaledTime; }
    public static class Debug
    {
        public static int ExceptionCount;
        public static void Log(object value, Object context = null) { }
        public static void LogWarning(object value, Object context = null) { }
        public static void LogException(Exception value, Object context = null) { ExceptionCount++; }
    }
    public static class Mathf
    {
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static int FloorToInt(float value) => (int)Math.Floor(value);
    }
    public static class Random { public static int Range(int min, int max) => min; }
    public static class Resources
    {
        public static Object[] Assets = Array.Empty<Object>();
        public static T[] LoadAll<T>(string path) => Assets.OfType<T>().ToArray();
    }
    public sealed class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int order) { } }
    public sealed class DisallowMultipleComponent : Attribute { }
    public sealed class SerializeField : Attribute { }
    public sealed class Tooltip : Attribute { public Tooltip(string value) { } }
    public sealed class Min : Attribute { public Min(float value) { } }
    public sealed class ContextMenu : Attribute { public ContextMenu(string value) { } }
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public sealed class RuntimeInitializeOnLoadMethod : Attribute
    { public RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType type) { } }
}

public enum CoinReason { StarterKit, OrderReward }
public class CurrencyWallet
{
    public static CurrencyWallet Instance = new CurrencyWallet();
    public int StarterCoins = 100, Balance;
    public event Action<int, int, CoinReason> OnBalanceChanged;
    public void RestoreBalance(int amount) => Balance = amount;
    public void Add(int amount, CoinReason reason) { Balance += amount; OnBalanceChanged?.Invoke(Balance, amount, reason); }
}
public class ResourceInventory
{
    public static ResourceInventory Instance = new ResourceInventory();
    public int Amount = 1000;
    public event Action<string, int, int> OnInventoryChanged;
    public void RestoreAmounts(List<ResourceStack> stacks) { }
    public int GetAmount(string id) => Amount;
    public bool HasAll(List<ResourceStack> stacks) => stacks.All(s => s.amount <= Amount);
    public bool TryRemoveAll(List<ResourceStack> stacks)
    {
        if (!HasAll(stacks)) return false;
        foreach (var stack in stacks) { Amount -= stack.amount; OnInventoryChanged?.Invoke(stack.resourceId, Amount, -stack.amount); }
        return true;
    }
}
public class ResourceData { public string resourceID = "wood", resourceName = "Wood"; public int baseValue = 2; }
public static class ResourceCatalog
{
    public static ResourceData Wood = new ResourceData();
    public static ResourceData Find(string id) => id == "wood" ? Wood : null;
}
public class OrderRequirement { public ResourceData resource; public int minAmount = 1, maxAmount = 1; }
public class OrderTemplate : UnityEngine.Object
{
    public string templateId = "baseline", customerName = "Test";
    public bool isBaseline = true;
    public float rewardMultiplier = 1;
    public List<OrderRequirement> requirements = new List<OrderRequirement>();
}
public class BiomeHarvestManager : UnityEngine.Object { public List<BiomeHarvestCluster> Clusters = new List<BiomeHarvestCluster>(); }
public class BiomeHarvestCluster { public ResourceData ResourceData; public List<object> Animals = new List<object>(); }

public interface IEconomySaveStore { EconomySaveData Load(); bool Save(EconomySaveData data); }
public sealed class LocalEconomySaveStore : IEconomySaveStore
{
    public LocalEconomySaveStore(string playerId) => throw new InvalidOperationException("Test must not open a player profile.");
    public EconomySaveData Load() => throw new InvalidOperationException();
    public bool Save(EconomySaveData data) => throw new InvalidOperationException();
}
internal sealed class MemoryEconomyStore : IEconomySaveStore
{
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };
    public EconomySaveData Saved;
    public bool Fail;
    public int Writes;
    public EconomySaveData Load() => Saved;
    public bool Save(EconomySaveData data)
    {
        Writes++;
        if (Fail) return false;
        Saved = JsonSerializer.Deserialize<EconomySaveData>(JsonSerializer.Serialize(data, Json), Json);
        return true;
    }
}
