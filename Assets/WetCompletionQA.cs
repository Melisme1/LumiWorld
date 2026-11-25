#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;

// Temporary test bootstrap. Removed after native MapBuilding verification.
[DefaultExecutionOrder(-32000)]
public sealed class WetCompletionQA : MonoBehaviour
{
    public const string Root = "D:/Dev/LumiWorld/.scratch/WetComplete-20261124";
    public static void Set(object target,string name,object value) =>
        target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);

    void Awake()
    {
        if (EconomySaveSystem.HasInstance) return;
        var go = new GameObject("QA Economy");
        go.SetActive(false);
        var economy = go.AddComponent<EconomySaveSystem>();
        var store = new EconomyStore();
        var data = store.Load() ?? new EconomySaveData {
            coins=1000, starterKitGranted=true, starterHandGranted=true, harmonyInitialized=true
        };
        Set(economy,"store",store);
        Set(economy,"data",data);
        go.SetActive(true);
    }

    void Start()
    {
        Set(WorldSaveSystem.Instance,"store",new WorldStore());
    }

    public sealed class EconomyStore : IEconomySaveStore
    {
        public EconomySaveData Load() => File.Exists(Root+"/economy.qa.json") ?
            JsonUtility.FromJson<EconomySaveData>(File.ReadAllText(Root+"/economy.qa.json")) : null;
        public bool Save(EconomySaveData data) { File.WriteAllText(Root+"/economy.qa.json",JsonUtility.ToJson(data));return true; }
    }
    public sealed class WorldStore : IWorldSaveStore
    {
        public WorldSaveData Load() => File.Exists(Root+"/world.qa.json") ?
            JsonUtility.FromJson<WorldSaveData>(File.ReadAllText(Root+"/world.qa.json")) : null;
        public bool Save(WorldSaveData data) { File.WriteAllText(Root+"/world.qa.json",JsonUtility.ToJson(data));return true; }
    }
}
#endif
