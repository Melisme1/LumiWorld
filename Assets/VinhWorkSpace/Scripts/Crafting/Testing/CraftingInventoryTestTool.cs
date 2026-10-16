using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LumiWorld.Economy.Crafting.Testing
{
    /// <summary>
    /// Công cụ hỗ trợ test nạp tài nguyên vào ResourceInventory:
    /// - Phím tắt trong game (New Input System):
    ///     F5: +50 Tất cả nguyên liệu thô (Lá non, Quả sồi, Mâm xôi, Lúa mì, Hoa...)
    ///     F6: +20 Tất cả thành phẩm chế biến (Bánh, Siro, Giấy, Bột mì...)
    ///     F7: +100 Tất cả tài nguyên trong game
    ///     F8: Dọn sạch kho về 0
    ///     F4: Bật/Tắt bảng điều khiển Cheat mini trên màn hình
    /// - Top Menu Unity: Tools -> LumiWorld -> Cheats -> ...
    /// - Inspector: Nhấp chuột phải vào component chọn ContextMenu hoặc nhập ID cụ thể
    /// - Tự động khởi tạo trong Scene khi vào Play Mode.
    /// </summary>
    [DisallowMultipleComponent]
    public class CraftingInventoryTestTool : MonoBehaviour
    {
        [Header("Cấu hình số lượng phím tắt")]
        [SerializeField] private int rawAmount = 50;
        [SerializeField] private int productAmount = 20;

        [Header("Nạp tài nguyên cụ thể")]
        [SerializeField] private string customResourceId = "resource.leafwood.tender_leaves";
        [SerializeField] private int customAmount = 20;

        private static CraftingInventoryTestTool _instance;

        public static readonly string[] RawResourceIds = new string[]
        {
            "resource.leafwood.tender_leaves",
            "resource.leafwood.acorns",
            "resource.leafwood.raspberries",
            "resource.leafwood.oak_wood",
            "resource.leafwood.sticks",
            "resource.leafwood.fallen_bark",
            "resource.bloomfield.dandelion_flowers",
            "resource.bloomfield.wild_daisies",
            "resource.bloomfield.lavender_sprigs",
            "resource.bloomfield.nectar",
            "resource.windheath.mountain_blueberries",
            "resource.windheath.rosemary",
            "resource.windheath.juniper_berries",
            "resource.windheath.foxtail_plumes",
            "resource.plain.wheat",
            "resource.windheath.flax_stalks",
            "resource.windheath.lichen"
        };

        public static readonly string[] ProductResourceIds = new string[]
        {
            "resource.leafwood.tender_leaves_crispy",
            "resource.leafwood.roasted_acorns",
            "resource.leafwood.dried_raspberries",
            "resource.windheath.dried_mountain_blueberries",
            "resource.bloomfield.dried_dandelion",
            "resource.bloomfield.nectar_essence",
            "resource.plain.wheat_flour",
            "resource.bloomfield.herbal_flower_powder",
            "resource.leafwood.bark_paper",
            "resource.food.dried_wild_berries_mix",
            "resource.leafwood.honey_glazed_acorns",
            "resource.forest.wild_herbal_syrup",
            "resource.craft.pressed_flower_paper",
            "resource.craft.scented_paper",
            "resource.food.wild_berry_nut_cake"
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (_instance == null)
            {
                var existing = FindAnyObjectByType<CraftingInventoryTestTool>();
                if (existing != null)
                {
                    _instance = existing;
                }
                else
                {
                    GameObject go = new GameObject("[CraftingDevTool]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<CraftingInventoryTestTool>();
                }
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        // Phím tắt và giao diện test đã được hợp nhất vào LumiWorldTestDashboard (Phím F1 hoặc ~)
        private void Update()
        {
        }

        /// <summary>
        /// Thêm số lượng chỉ định cho 1 tài nguyên vào kho đồ.
        /// </summary>
        public static int AddResource(string resourceId, int amount)
        {
            if (string.IsNullOrEmpty(resourceId) || amount <= 0) return 0;

            ResourceData res = CraftingCatalog.GetResource(resourceId);
            if (res == null)
            {
                res = ResourceCatalog.Find(resourceId);
            }

            if (res != null)
            {
                int added = ResourceInventory.Instance.Add(res, amount);
                int current = ResourceInventory.Instance.GetAmount(resourceId);
                Debug.Log($"<color=#4ade80><b>[LumiWorld Test]</b></color> +{added} <b>{res.resourceName}</b> ({resourceId}) ➔ Hiện có: {current}");
                return added;
            }

            Debug.LogWarning($"[LumiWorld Test] Không tìm thấy dữ liệu ResourceData cho ID: '{resourceId}'");
            return 0;
        }

        /// <summary>
        /// Nạp nhanh toàn bộ 17 nguyên liệu thô vào kho.
        /// </summary>
        [ContextMenu("Test: +50 Tất cả nguyên liệu thô (F5)")]
        public void AddAllRawMaterialsBtn() => AddAllRawMaterials(rawAmount);

        public static void AddAllRawMaterials(int count = 50)
        {
            int total = 0;
            foreach (string id in RawResourceIds)
            {
                total += AddResource(id, count);
            }
            Debug.Log($"<color=#38bdf8><b>[LumiWorld Test]</b> Đã nạp +{count} cho toàn bộ {RawResourceIds.Length} nguyên liệu thô (Tổng nạp: {total})</color>");
        }

        /// <summary>
        /// Nạp nhanh toàn bộ 15 thành phẩm chế biến vào kho.
        /// </summary>
        [ContextMenu("Test: +20 Tất cả thành phẩm chế biến (F6)")]
        public void AddAllProductsBtn() => AddAllProducts(productAmount);

        public static void AddAllProducts(int count = 20)
        {
            int total = 0;
            foreach (string id in ProductResourceIds)
            {
                total += AddResource(id, count);
            }
            Debug.Log($"<color=#a78bfa><b>[LumiWorld Test]</b> Đã nạp +{count} cho toàn bộ {ProductResourceIds.Length} thành phẩm chế biến (Tổng nạp: {total})</color>");
        }

        /// <summary>
        /// Nạp tất cả tài nguyên (cả nguyên liệu thô và thành phẩm).
        /// </summary>
        [ContextMenu("Test: +100 Tất cả tài nguyên (F7)")]
        public void AddAllBtn() => AddAll(100);

        public static void AddAll(int count = 100)
        {
            AddAllRawMaterials(count);
            AddAllProducts(count);
        }

        /// <summary>
        /// Xóa sạch mọi tài nguyên trong kho về 0.
        /// </summary>
        [ContextMenu("Test: Xóa sạch kho tài nguyên (F8)")]
        public void ClearInventoryBtn() => ClearInventory();

        public static void ClearInventory()
        {
            var inv = ResourceInventory.Instance;
            if (inv != null)
            {
                inv.Clear();
            }
            Debug.Log("<color=#f87171><b>[LumiWorld Test]</b> Đã dọn dẹp sạch toàn bộ kho tài nguyên (0 items).</color>");
        }

        [ContextMenu("Test: Nạp tài nguyên tùy chỉnh")]
        public void AddCustomResource()
        {
            AddResource(customResourceId, customAmount);
        }

        // Giao diện đã được tích hợp tập trung vào LumiWorldTestDashboard (Phím F1 hoặc ~)
#if UNITY_EDITOR
        [MenuItem("Tools/LumiWorld/Cheats/Thêm 50 Tất Cả Nguyên Liệu Thô (F5)")]
        private static void MenuAddRaw()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("LumiWorld", "Vui lòng vào Play Mode để nạp tài nguyên vào ResourceInventory!", "OK");
                return;
            }
            AddAllRawMaterials(50);
        }

        [MenuItem("Tools/LumiWorld/Cheats/Thêm 20 Tất Cả Thành Phẩm Chế Biến (F6)")]
        private static void MenuAddProducts()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("LumiWorld", "Vui lòng vào Play Mode để nạp tài nguyên vào ResourceInventory!", "OK");
                return;
            }
            AddAllProducts(20);
        }

        [MenuItem("Tools/LumiWorld/Cheats/Thêm 100 Tất Cả Tài Nguyên (F7)")]
        private static void MenuAddAll()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("LumiWorld", "Vui lòng vào Play Mode để nạp tài nguyên vào ResourceInventory!", "OK");
                return;
            }
            AddAll(100);
        }

        [MenuItem("Tools/LumiWorld/Cheats/Xóa Sạch Kho Tài Nguyên (F8)")]
        private static void MenuClear()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("LumiWorld", "Vui lòng vào Play Mode để xóa kho ResourceInventory!", "OK");
                return;
            }
            ClearInventory();
        }
#endif
    }
}
