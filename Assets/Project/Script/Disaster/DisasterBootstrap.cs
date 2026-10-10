using UnityEngine;
using UnityEngine.SceneManagement;

namespace LumiWorld.Acs
{
    /// <summary>
    /// Tự gắn DisasterRuntimeManager vào scene có bản đồ lục giác (HexWorldGenerator) nếu chưa có.
    /// Nhờ vậy hệ thống thiên tai chạy ngay mà không cần sửa Scene.
    ///
    /// Muốn cấu hình riêng (Balance, clock, tham chiếu) thì tự thêm component vào scene —
    /// bootstrap sẽ không sinh trùng.
    /// </summary>
    public static class DisasterBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Spawn();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Spawn();

        private static void Spawn()
        {
            if (Object.FindAnyObjectByType<DisasterRuntimeManager>(FindObjectsInactive.Include) != null) return;

            var world = Object.FindAnyObjectByType<HexWorldGenerator>();
            if (world == null) return; // Chỉ sinh khi scene có bản đồ lục giác.

            // Đặt ở root scene (không phải con của MapGenerator) để dễ thấy trong Hierarchy.
            var go = new GameObject("DisasterRuntimeManager");
            go.AddComponent<DisasterRuntimeManager>();
        }
    }
}
