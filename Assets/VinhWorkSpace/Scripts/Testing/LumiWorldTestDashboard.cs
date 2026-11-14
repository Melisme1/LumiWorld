using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using LumiWorld.Economy.Crafting.Testing;
using LumiWorld.WildIncident.Testing;
using LumiWorld.WildIncident;
using LumiWorld.Acs;
using LumiWorld.Acs.TimeSystem;

namespace LumiWorld.Testing
{
    /// <summary>
    /// Bảng điều khiển kiểm thử hợp nhất (Unified Test & Debug Dashboard) cho LumiWorld.
    /// Thay vì dùng nhiều phím tắt riêng lẻ gây rối (F4, F5, F6, F7, F8, F9, F10, F11...),
    /// chỉ dùng DUY NHẤT 1 phím tắt (F1 hoặc dấu ngã ~) để bật/tắt toàn bộ menu test.
    /// </summary>
    [DisallowMultipleComponent]
    public class LumiWorldTestDashboard : MonoBehaviour
    {
        private static LumiWorldTestDashboard _instance;
        public static LumiWorldTestDashboard Instance => _instance;

        [Header("Cấu hình phím tắt bật/tắt")]
        [Tooltip("Phím tắt duy nhất để bật/tắt Test Dashboard (mặc định F1)")]
        [SerializeField] private Key toggleKey = Key.F1;
        [SerializeField] private bool allowBackquoteKey = true; // Phím ~ (Backquote)

        [Header("Trạng thái giao diện")]
        [SerializeField] private bool isOpen = false;

        private Rect windowRect = new Rect(20, 40, 560, 580);
        private int selectedTab = 0;
        private readonly string[] tabNames = new string[]
        {
            "📦 Tài Nguyên",
            "🐺 Thú Hoang",
            "🌪️ Thiên Tai",
            "💰 Tiền & Giờ",
            "🃏 Thẻ Bài"
        };

        private Vector2 resourceScrollPos;
        private Vector2 cardScrollPos;
        private string cardSearchText = "";
        private GUIStyle windowStyle;
        private GUIStyle headerStyle;
        private GUIStyle sectionStyle;
        private GUIStyle btnNormal;
        private GUIStyle btnGreen;
        private GUIStyle btnOrange;
        private GUIStyle btnRed;
        private GUIStyle btnPurple;
        private GUIStyle statusStyle;
        private bool stylesInitialized = false;

        // Công cụ test tự gắn trong Editor hoặc bản Development Build để test
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            if (_instance == null && FindAnyObjectByType<LumiWorldTestDashboard>() == null)
            {
                GameObject go = new GameObject("[LumiWorld_TestDashboard]");
                go.AddComponent<LumiWorldTestDashboard>();
                DontDestroyOnLoad(go);
            }
        }
#endif

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // Phím tắt duy nhất để Bật/Tắt Test UI
            bool togglePressed = false;
            if (toggleKey == Key.F1 && kb.f1Key.wasPressedThisFrame) togglePressed = true;
            else if (toggleKey == Key.F2 && kb.f2Key.wasPressedThisFrame) togglePressed = true;
            else if (toggleKey == Key.F3 && kb.f3Key.wasPressedThisFrame) togglePressed = true;
            else if (toggleKey == Key.F4 && kb.f4Key.wasPressedThisFrame) togglePressed = true;
            else if (toggleKey == Key.F12 && kb.f12Key.wasPressedThisFrame) togglePressed = true;

            if (allowBackquoteKey && kb.backquoteKey.wasPressedThisFrame) togglePressed = true;

            if (togglePressed)
            {
                isOpen = !isOpen;
            }

            // Phím tắt nhanh khi đang chơi game: F5 để thêm ngay +1 Thẻ Làng (Medieval_Village)
            if (kb.f5Key.wasPressedThisFrame)
            {
                GiveCardById("Medieval_Village", 1);
            }
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;

            windowStyle = new GUIStyle(GUI.skin.window);
            windowStyle.fontSize = 13;
            windowStyle.fontStyle = FontStyle.Bold;

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            sectionStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 8, 8),
                margin = new RectOffset(0, 0, 4, 4)
            };

            btnNormal = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                fixedHeight = 32
            };

            btnGreen = new GUIStyle(btnNormal);
            btnGreen.normal.textColor = new Color(0.3f, 0.9f, 0.4f);

            btnOrange = new GUIStyle(btnNormal);
            btnOrange.normal.textColor = new Color(1f, 0.7f, 0.2f);

            btnRed = new GUIStyle(btnNormal);
            btnRed.normal.textColor = new Color(1f, 0.4f, 0.4f);

            btnPurple = new GUIStyle(btnNormal);
            btnPurple.normal.textColor = new Color(0.8f, 0.6f, 1f);

            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                wordWrap = true
            };

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (!isOpen) return;

            InitStyles();
            windowRect = GUI.Window(999901, windowRect, DrawDashboardWindow, "🛠️ LUMIWORLD DEV DASHBOARD (Phím F1 hoặc ~ để tắt)");
        }

        private void DrawDashboardWindow(int windowId)
        {
            // Nút đóng góc trên
            if (GUI.Button(new Rect(windowRect.width - 32, 4, 28, 20), "✕"))
            {
                isOpen = false;
            }

            GUILayout.Space(8);

            // Thanh chọn Tab
            selectedTab = GUILayout.Toolbar(selectedTab, tabNames, GUILayout.Height(30));

            GUILayout.Space(8);

            switch (selectedTab)
            {
                case 0:
                    DrawResourceTab();
                    break;
                case 1:
                    DrawWildIncidentTab();
                    break;
                case 2:
                    DrawDisasterTab();
                    break;
                case 3:
                    DrawTimeAndMoneyTab();
                    break;
                case 4:
                    DrawCardsTab();
                    break;
            }

            // Cho phép kéo thả cửa sổ
            GUI.DragWindow(new Rect(0, 0, windowRect.width, 24));
        }

        // ====================================================================
        // TAB 1: TÀI NGUYÊN & KHO ĐỒ
        // ====================================================================
        private void DrawResourceTab()
        {
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("<b>⚡ THAO TÁC NHANH KHO ĐỒ</b>", headerStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🌿 +50 Toàn Bộ Nguyên Liệu Thô", btnGreen))
            {
                CraftingInventoryTestTool.AddAllRawMaterials(50);
            }
            if (GUILayout.Button("🍲 +20 Toàn Bộ Thành Phẩm", btnOrange))
            {
                CraftingInventoryTestTool.AddAllProducts(20);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("⭐ +100 Tất Cả Tài Nguyên", btnPurple))
            {
                CraftingInventoryTestTool.AddAll(100);
            }
            if (GUILayout.Button("🗑️ Dọn Sạch Kho (0)", btnRed))
            {
                CraftingInventoryTestTool.ClearInventory();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUILayout.Space(4);

            // Nạp từng món cụ thể
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("<b>📋 NẠP LẺ TỪNG MÓN PHỔ BIẾN</b>", headerStyle);

            resourceScrollPos = GUILayout.BeginScrollView(resourceScrollPos, GUILayout.Height(250));

            DrawQuickResourceRow("resource.leafwood.raspberries", "Mâm Xôi (Thức ăn thú)");
            DrawQuickResourceRow("resource.leafwood.tender_leaves", "Lá Non Rừng Leafwood");
            DrawQuickResourceRow("resource.leafwood.acorns", "Quả Sồi");
            DrawQuickResourceRow("resource.leafwood.oak_wood", "Gỗ Sồi");
            DrawQuickResourceRow("resource.bloomfield.dandelion_flowers", "Hoa Bồ Công Anh");
            DrawQuickResourceRow("resource.bloomfield.wild_daisies", "Cúc Dại");
            DrawQuickResourceRow("resource.bloomfield.nectar", "Mật Hoa Nectar");
            DrawQuickResourceRow("resource.windheath.mountain_blueberries", "Việt Quất Núi");
            DrawQuickResourceRow("resource.plain.wheat", "Lúa Mì");
            DrawQuickResourceRow("resource.leafwood.bark_paper", "Giấy Vỏ Cây");
            DrawQuickResourceRow("resource.plain.wheat_flour", "Bột Mì");
            DrawQuickResourceRow("resource.food.wild_berry_nut_cake", "Bánh Hạt Quả Rừng");

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawQuickResourceRow(string resourceId, string displayName)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label(displayName, GUILayout.Width(240));

            if (GUILayout.Button("+10", GUILayout.Width(55), GUILayout.Height(24)))
            {
                CraftingInventoryTestTool.AddResource(resourceId, 10);
            }
            if (GUILayout.Button("+50", GUILayout.Width(55), GUILayout.Height(24)))
            {
                CraftingInventoryTestTool.AddResource(resourceId, 50);
            }
            if (GUILayout.Button("+100", GUILayout.Width(60), GUILayout.Height(24)))
            {
                CraftingInventoryTestTool.AddResource(resourceId, 100);
            }
            GUILayout.EndHorizontal();
        }

        // ====================================================================
        // TAB 2: THÚ HOANG & SỰ CỐ
        // ====================================================================
        private void DrawWildIncidentTab()
        {
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("<b>🐺 ĐIỀU KHIỂN SỰ CỐ THÚ HOANG</b>", headerStyle);

            var mgr = WildPackIncidentManager.Instance;
            bool hasIncidents = mgr != null && mgr.ActiveIncidents != null && mgr.ActiveIncidents.Count > 0;
            string status = mgr != null ? (hasIncidents ? $"🔴 ĐANG CÓ BẦY THÚ ({mgr.ActiveIncidents.Count} sự cố)" : "🟢 Bản đồ yên bình (Không có sự cố)") : "⚠️ Chưa tìm thấy WildPackIncidentManager trong Scene";
            GUILayout.Label($"<b>Trạng thái:</b> {status}", statusStyle);

            GUILayout.Space(6);

            if (GUILayout.Button("⚡ Ép Sinh Bầy Thú Hoang Ngay (Force Spawn)", btnOrange))
            {
                WildIncidentTestTool.ForceSpawnImmediate();
            }

            GUILayout.Space(4);

            if (GUILayout.Button("🕊️ Dẹp Toàn Bộ Thú Hoang (Resolve All)", btnGreen))
            {
                WildIncidentTestTool.ResolveAllImmediate();
            }

            GUILayout.Space(4);

            if (GUILayout.Button("⏯️ Bật / Tạm Dừng Tự Động Sinh Sự Cố", btnNormal))
            {
                WildIncidentTestTool.ToggleAutoSpawn();
            }

            GUILayout.EndVertical();
        }

        // ====================================================================
        // TAB 3: THIÊN TAI & VẬT CẢN
        // ====================================================================
        private void DrawDisasterTab()
        {
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("<b>🌪️ ĐIỀU KHIỂN THIÊN TAI (DISASTERS)</b>", headerStyle);

            var mgr = DisasterRuntimeManager.Instance;
            if (mgr == null)
            {
                GUILayout.Label("⚠️ DisasterRuntimeManager chưa có trong Scene hiện tại.", statusStyle);
            }

            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🪨 Sinh Vật Cản (Obstacle)", btnNormal))
            {
                DisasterRuntimeManager.Instance?.TestSpawn(DisasterKind.Obstacle);
            }
            if (GUILayout.Button("🔥 Sinh Cháy Rừng (Fire)", btnRed))
            {
                DisasterRuntimeManager.Instance?.TestSpawn(DisasterKind.ForestFire);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🌪️ Sinh Lốc Xoáy (Cyclone)", btnNormal))
            {
                DisasterRuntimeManager.Instance?.TestSpawn(DisasterKind.Cyclone);
            }
            if (GUILayout.Button("☀️ Sinh Hạn Hán (Drought)", btnOrange))
            {
                DisasterRuntimeManager.Instance?.TestSpawn(DisasterKind.Drought);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🍂 Sinh Đất Cằn (Barren)", btnNormal))
            {
                DisasterRuntimeManager.Instance?.TestSpawn(DisasterKind.BarrenLand);
            }
            if (GUILayout.Button("🧹 Dọn Sạch Thiên Tai (Clear)", btnGreen))
            {
                DisasterRuntimeManager.Instance?.TestClearAll();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        // ====================================================================
        // TAB 4: TIỀN VÀNG & THỜI GIAN
        // ====================================================================
        private void DrawTimeAndMoneyTab()
        {
            // Phần 1: Tiền xu / Vàng
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("<b>💰 VÍ TIỀN LUMI COIN</b>", headerStyle);

            int currentCoins = CurrencyWallet.HasInstance ? CurrencyWallet.Instance.Balance : -1;
            GUILayout.Label($"<b>Số dư hiện tại:</b> {(currentCoins >= 0 ? $"{currentCoins:N0} Coins" : "Chưa nạp ví")}", statusStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("💵 +500 Coins", btnGreen))
            {
                CurrencyWallet.Instance?.Add(500, CoinReason.DevCheat);
            }
            if (GUILayout.Button("💰 +2,000 Coins", btnGreen))
            {
                CurrencyWallet.Instance?.Add(2000, CoinReason.DevCheat);
            }
            if (GUILayout.Button("💎 +10,000 Coins", btnGreen))
            {
                CurrencyWallet.Instance?.Add(10000, CoinReason.DevCheat);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUILayout.Space(8);

            // Phần 2: Thời gian gameplay
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("<b>⏱️ ĐỒNG HỒ & THỜI GIAN GAMEPLAY</b>", headerStyle);

            bool isPaused = GameTimeRuntime.Instance != null && GameTimeRuntime.Instance.IsPaused;
            GUILayout.Label($"<b>Trạng thái đồng hồ:</b> {(isPaused ? "⏸️ ĐANG TẠM DỪNG (PAUSED)" : "▶️ ĐANG CHẠY BÌNH THƯỜNG")}", statusStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(isPaused ? "▶️ Tiếp Tục Chạy (Resume)" : "⏸️ Tạm Dừng (Pause)", isPaused ? btnGreen : btnOrange))
            {
                GameTimeRuntime.Instance?.TogglePause();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        // ====================================================================
        // TAB 5: THẺ BÀI (CARD CHEAT)
        // ====================================================================
        private void DrawCardsTab()
        {
            // Phần 1: Phím tắt & Thao tác nhanh
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("<b>⚡ THAO TÁC NHANH THẺ BÀI (Phím F5: +1 Thẻ Làng)</b>", headerStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🏰 +1 Thẻ Làng (Village)", btnGreen))
            {
                GiveCardById("Medieval_Village", 1);
            }
            if (GUILayout.Button("🏰 +5 Thẻ Làng (Test Cụm)", btnGreen))
            {
                GiveCardById("Medieval_Village", 5);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🌧️ +1 Thẻ Mưa (Rain)", btnNormal))
            {
                GiveCardById("Rain", 1);
            }
            if (GUILayout.Button("🌊 +1 Thẻ Sông (River)", btnNormal))
            {
                GiveCardById("River", 1);
            }
            if (GUILayout.Button("🌲 +1 Thẻ Rừng (Leafwood)", btnNormal))
            {
                GiveCardById("Leafwood", 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUILayout.Space(6);

            // Phần 2: Toàn bộ danh mục thẻ bài trong game
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("<b>📚 DANH MỤC TẤT CẢ THẺ BÀI TRONG GAME</b>", headerStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label("🔍 Tìm thẻ:", GUILayout.Width(75));
            cardSearchText = GUILayout.TextField(cardSearchText);
            if (!string.IsNullOrEmpty(cardSearchText) && GUILayout.Button("✕", GUILayout.Width(25)))
            {
                cardSearchText = "";
            }
            GUILayout.EndHorizontal();

            List<CardData> allCards = GetAllAvailableCards();

            cardScrollPos = GUILayout.BeginScrollView(cardScrollPos, GUILayout.Height(240));

            int count = 0;
            foreach (var card in allCards)
            {
                if (card == null) continue;

                if (!string.IsNullOrEmpty(cardSearchText))
                {
                    bool matchName = card.cardName != null && card.cardName.IndexOf(cardSearchText, System.StringComparison.OrdinalIgnoreCase) >= 0;
                    bool matchId = card.cardID != null && card.cardID.IndexOf(cardSearchText, System.StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!matchName && !matchId) continue;
                }

                count++;
                GUILayout.BeginHorizontal(GUI.skin.box);

                string typeIcon = card.cardType switch
                {
                    CardType.Building => "🏰",
                    CardType.Terrain => "🌲",
                    CardType.Creature => "🦊",
                    CardType.Special => "✨",
                    _ => "🃏"
                };

                GUILayout.Label($"<b>{typeIcon} {card.cardName}</b> <color=#94A3B8>({card.cardID})</color>", GUILayout.Width(260));

                if (GUILayout.Button("+1 Vào Tay", btnGreen, GUILayout.Width(95), GUILayout.Height(24)))
                {
                    GiveCard(card, 1);
                }
                if (GUILayout.Button("+3", btnOrange, GUILayout.Width(45), GUILayout.Height(24)))
                {
                    GiveCard(card, 3);
                }

                GUILayout.EndHorizontal();
            }

            if (count == 0)
            {
                GUILayout.Label("<i>Không tìm thấy thẻ bài nào khớp với từ khóa tìm kiếm.</i>");
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        public static void GiveCard(CardData card, int count = 1)
        {
            if (card == null || count <= 0) return;

            if (CardDeskController.Instance != null && CardDeskController.Instance.HandReady)
            {
                for (int i = 0; i < count; i++)
                {
                    CardDeskController.Instance.AddRewardCard(card);
                }
                Debug.Log($"<color=#10B981>[Card Cheat] Đã thêm {count} lá '{card.cardName}' ({card.cardID}) vào bộ bài trên tay!</color>");
                if (HexPlacementPreviewHUD.Instance != null)
                {
                    HexPlacementPreviewHUD.ShowFeedbackToast($"[Cheat] +{count} {card.cardName}");
                }
            }
            else
            {
                HandCards.AddWithoutSaving(card, count);
                Debug.Log($"<color=#10B981>[Card Cheat] Đã thêm {count} lá '{card.cardName}' vào kho bài lưu!</color>");
            }
        }

        public static void GiveCardById(string cardId, int count = 1)
        {
            CardData card = FindCardById(cardId);
            if (card != null)
            {
                GiveCard(card, count);
            }
            else
            {
                Debug.LogWarning($"<color=#F59E0B>[Card Cheat] Không tìm thấy CardData nào có ID '{cardId}'!</color>");
            }
        }

        public static CardData FindCardById(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return null;

            if (WorldSaveSystem.Instance != null && WorldSaveSystem.Instance.Cards != null)
            {
                CardData found = WorldSaveSystem.Instance.Cards.Find(cardId);
                if (found != null) return found;
            }

            CardData[] loadedCards = Resources.FindObjectsOfTypeAll<CardData>();
            if (loadedCards != null)
            {
                foreach (var c in loadedCards)
                {
                    if (c != null && (c.cardID == cardId || c.name == cardId)) return c;
                }
            }

#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:CardData");
            if (guids != null)
            {
                foreach (var g in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                    CardData c = UnityEditor.AssetDatabase.LoadAssetAtPath<CardData>(path);
                    if (c != null && (c.cardID == cardId || c.name == cardId)) return c;
                }
            }
#endif
            return null;
        }

        private static List<CardData> GetAllAvailableCards()
        {
            List<CardData> result = new List<CardData>();
            HashSet<string> seenIds = new HashSet<string>();

            if (WorldSaveSystem.Instance != null && WorldSaveSystem.Instance.Cards != null)
            {
                foreach (var c in WorldSaveSystem.Instance.Cards.All)
                {
                    if (c != null && !seenIds.Contains(c.cardID))
                    {
                        seenIds.Add(c.cardID);
                        result.Add(c);
                    }
                }
            }

            CardData[] loadedCards = Resources.FindObjectsOfTypeAll<CardData>();
            if (loadedCards != null)
            {
                foreach (var c in loadedCards)
                {
                    if (c != null && !string.IsNullOrEmpty(c.cardID) && !seenIds.Contains(c.cardID))
                    {
                        seenIds.Add(c.cardID);
                        result.Add(c);
                    }
                }
            }

#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:CardData");
            if (guids != null)
            {
                foreach (var g in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                    CardData c = UnityEditor.AssetDatabase.LoadAssetAtPath<CardData>(path);
                    if (c != null && !string.IsNullOrEmpty(c.cardID) && !seenIds.Contains(c.cardID))
                    {
                        seenIds.Add(c.cardID);
                        result.Add(c);
                    }
                }
            }
#endif

            result.Sort((a, b) =>
            {
                int typeCmp = a.cardType.CompareTo(b.cardType);
                if (typeCmp != 0) return typeCmp;
                return string.Compare(a.cardName, b.cardName, System.StringComparison.Ordinal);
            });

            return result;
        }
    }
}
