using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace LumiWorld.Acs
{
    public static class ResourceInventoryBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded; Spawn();
        }
        private static void Loaded(Scene scene, LoadSceneMode mode) => Spawn();
        private static void Spawn()
        {
            if (Object.FindAnyObjectByType<ResourceInventoryUIController>(FindObjectsInactive.Include) != null) return;
            var production = Object.FindAnyObjectByType<ResourceProductionRuntime>(); if (production == null) return;
            var skin = Resources.Load<ResourceInventoryUISkin>("UI/ResourceInventoryUISkin");
            if (skin == null || skin.uiPrefab == null)
            { Debug.LogError("[LumiWorld UI] Thiếu UI Skin/prefab. Chạy Tools > LumiWorld > Resources > Setup Inventory UI Skin."); return; }
            Canvas screenCanvas = null;
            foreach (var canvas in Object.FindObjectsByType<Canvas>())
                if (canvas.isRootCanvas && canvas.renderMode != RenderMode.WorldSpace && canvas.gameObject.scene == production.gameObject.scene)
                { screenCanvas = canvas; break; }
            if (screenCanvas == null || EventSystem.current == null)
            { Debug.LogError("[LumiWorld UI] Reuse Canvas/EventSystem của MapBuilding: kiểm tra hai object trong scene."); return; }
            var root = Object.Instantiate(skin.uiPrefab, screenCanvas.transform, false); root.name = "ResourceInventoryUIRoot";
            root.GetComponent<ResourceInventoryUIController>().Initialize(production, skin);
        }
    }
}
