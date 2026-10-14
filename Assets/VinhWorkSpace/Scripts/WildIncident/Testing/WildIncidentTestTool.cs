using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LumiWorld.WildIncident.Testing
{
    /// <summary>
    /// Công cụ Kiểm Thử Cơ Chế Thú Hoang Quậy Phá (Wild Pack Incident Test Tool):
    /// Tách biệt 100% giữa code kiểm thử/cheat và logic gameplay gốc:
    /// - Phím tắt F9: Bật / Tắt bảng điều khiển Debug HUD trên màn hình.
    /// - Phím tắt F10: Ép sinh sự cố thú hoang ngay lập tức (Force Spawn).
    /// - Phím tắt Shift + F10: Bật / Tạm dừng chu kỳ tự động sinh của Gameplay.
    /// - Phím tắt F11: Giải phóng / Dẹp yên toàn bộ đàn thú hoang trên bản đồ.
    /// </summary>
    [DisallowMultipleComponent]
    public class WildIncidentTestTool : MonoBehaviour
    {
        private static WildIncidentTestTool _instance;
        public static WildIncidentTestTool Instance => _instance;

        [Header("Trạng thái Debug HUD")]
        [SerializeField] private bool showGui = false;

        private Rect windowRect = new Rect(20, 160, 380, 420);
        private GUIStyle headerStyle;
        private GUIStyle boxStyle;
        private GUIStyle statusStyle;
        private GUIStyle buttonStyle;
        private GUIStyle subHeaderStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            if (FindAnyObjectByType<WildIncidentTestTool>() == null)
            {
                GameObject go = new GameObject("[WildIncidentTestTool]");
                go.AddComponent<WildIncidentTestTool>();
                DontDestroyOnLoad(go);
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

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // F9: Bật / Tắt Bảng Test HUD
            if (kb.f9Key.wasPressedThisFrame)
            {
                showGui = !showGui;
            }

            // F10: Kích hoạt Spawn Thú Hoang
            if (kb.f10Key.wasPressedThisFrame)
            {
                if (kb.shiftKey.isPressed)
                {
                    ToggleAutoSpawn();
                }
                else
                {
                    ForceSpawnImmediate();
                }
            }

            // F11: Giải phóng / Dẹp toàn bộ thú hoang
            if (kb.f11Key.wasPressedThisFrame)
            {
                ResolveAllImmediate();
            }
        }

        /// <summary>
        /// Ép sinh sự cố đàn thú hoang ngay lập tức.
        /// </summary>
        public static void ForceSpawnImmediate()
        {
            var mgr = WildPackIncidentManager.Instance;
            if (mgr == null)
            {
                Debug.LogWarning("<color=#f87171>[Test Tool - Thú Hoang]</color> Không tìm thấy WildPackIncidentManager trong scene!");
                return;
            }

            bool ok = mgr.ForceSpawnIncident(null, null, null, out var incident, out string failReason);
            if (ok && incident != null)
            {
                Debug.Log($"<color=#38bdf8><b>[Test Tool - Thú Hoang]</b></color> Đã ép sinh thành công đàn thú [Alpha {incident.AlphaRank}] tại cụm <b>{incident.HostCluster?.BiomeName}</b>!");
            }
            else
            {
                Debug.LogWarning($"<color=#fbbf24><b>[Test Tool - Thú Hoang]</b></color> Không thể sinh sự cố: {failReason}");
            }
        }

        /// <summary>
        /// Dẹp sạch toàn bộ sự cố thú hoang trên bản đồ.
        /// </summary>
        public static void ResolveAllImmediate()
        {
            var mgr = WildPackIncidentManager.Instance;
            if (mgr == null) return;

            int count = mgr.ActiveIncidents != null ? mgr.ActiveIncidents.Count : 0;
            mgr.ResolveAllIncidents();
            Debug.Log($"<color=#38bdf8><b>[Test Tool - Thú Hoang]</b></color> Đã giải phóng toàn bộ {count} đàn thú hoang trên bản đồ.");
        }

        /// <summary>
        /// Bật hoặc Tạm dừng cơ chế tự động sinh của Gameplay.
        /// </summary>
        public static void ToggleAutoSpawn()
        {
            var mgr = WildPackIncidentManager.Instance;
            if (mgr == null) return;

            mgr.AutoSpawnEnabled = !mgr.AutoSpawnEnabled;
            string status = mgr.AutoSpawnEnabled ? "<color=#4ade80>ĐANG CHẠY</color>" : "<color=#f87171>ĐÃ TẠM DỪNG</color>";
            Debug.Log($"<color=#38bdf8><b>[Test Tool - Thú Hoang]</b></color> Trạng thái Auto-Spawn Gameplay: {status}");
        }

        private void InitStyles()
        {
            if (headerStyle != null) return;

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            headerStyle.normal.textColor = new Color(0.95f, 0.75f, 0.25f);

            subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            subHeaderStyle.normal.textColor = new Color(0.35f, 0.85f, 0.95f);

            boxStyle = new GUIStyle(GUI.skin.box);

            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                richText = true
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                richText = true
            };
        }

        private void OnGUI()
        {
            if (!showGui) return;
            InitStyles();

            GUI.backgroundColor = new Color(0.08f, 0.12f, 0.22f, 0.95f);
            windowRect = GUI.Window(9992, windowRect, DrawTestWindow, "🛠️ TEST SỰ CỐ THÚ HOANG (F9 ẩn/hiện)", boxStyle);
        }

        private void DrawTestWindow(int windowId)
        {
            GUI.backgroundColor = Color.white;
            var mgr = WildPackIncidentManager.Instance;

            if (mgr == null)
            {
                GUILayout.Label("<color=#f87171>Chưa tìm thấy WildPackIncidentManager trong scene.</color>", statusStyle);
                GUI.DragWindow();
                return;
            }

            // 1. TRẠNG THÁI GAMEPLAY
            GUILayout.Label("📊 TRẠNG THÁI GAMEPLAY TỰ NHIÊN", subHeaderStyle);
            string autoText = mgr.AutoSpawnEnabled
                ? "<color=#4ade80><b>BẬT</b></color> (Tự sinh theo chu kỳ)"
                : "<color=#f87171><b>TẠM DỪNG</b></color> (Tester đang kiểm thử)";
            GUILayout.Label($"• Tự động sinh (Gameplay): {autoText}", statusStyle);

            float curTimer = mgr.CurrentTimer;
            float maxTimer = mgr.IntervalSeconds;
            float remain = mgr.RemainingCooldown;
            GUILayout.Label($"• Đếm giờ sự cố kế tiếp: <b>{remain:F0}s</b> / {maxTimer:F0}s (Đã trôi: {curTimer:F0}s)", statusStyle);

            int scoreMilestone = ScoreManager.Instance != null ? ScoreManager.Instance.GrantedMilestoneCount : 0;
            int reqMilestone = mgr.Config != null ? mgr.Config.requiredUnlockMilestone : 0;
            var clusters = mgr.GetAllActiveClusters();
            int progressionTier = WildIncidentEvaluator.EvaluateProgressionTier(scoreMilestone, clusters);
            string unlockColor = scoreMilestone >= reqMilestone ? "#4ade80" : "#fbbf24";
            GUILayout.Label($"• Tiến trình thế giới: <color=#38bdf8><b>Cấp {progressionTier}</b></color> (Milestone điểm: {scoreMilestone}, Mở khóa: Mốc {reqMilestone})", statusStyle);

            int incidentCount = mgr.ActiveIncidents != null ? mgr.ActiveIncidents.Count : 0;
            int maxIncidents = mgr.Config != null ? mgr.Config.maxConcurrentIncidents : 1;
            GUILayout.Label($"• Số đàn thú đang chiếm cứ: <b>{incidentCount} / {maxIncidents}</b>", statusStyle);

            // 2. TÌNH TRẠNG CỤM BIOME TRÊN MAP
            var clusters = mgr.GetAllActiveClusters();
            int eligibleCount = 0;
            if (clusters != null)
            {
                foreach (var c in clusters)
                {
                    if (c != null && c.HexCount >= (mgr.Config != null ? mgr.Config.minClusterSizeToHost : 3))
                        eligibleCount++;
                }
            }
            GUILayout.Label($"• Cụm Biome trên map: <b>{clusters?.Count ?? 0}</b> cụm (<b>{eligibleCount}</b> cụm đạt chuẩn >= 3 ô)", statusStyle);

            GUILayout.Space(10);

            // 3. CÁC NÚT BẤM THAO TÁC TEST
            GUILayout.Label("🎮 HÀNH ĐỘNG KIỂM THỬ (CHEAT / DEBUG)", subHeaderStyle);

            GUI.backgroundColor = new Color(0.95f, 0.65f, 0.15f);
            if (GUILayout.Button("⚡ <b>SPAWN THÚ HOANG NGAY (F10)</b>", buttonStyle, GUILayout.Height(36)))
            {
                ForceSpawnImmediate();
            }

            GUI.backgroundColor = mgr.AutoSpawnEnabled ? new Color(0.85f, 0.35f, 0.35f) : new Color(0.35f, 0.85f, 0.45f);
            string toggleLabel = mgr.AutoSpawnEnabled ? "⏸️ <b>TẠM DỪNG TỰ ĐỘNG SINH (Shift+F10)</b>" : "▶️ <b>BẬT LẠI TỰ ĐỘNG SINH (Shift+F10)</b>";
            if (GUILayout.Button(toggleLabel, buttonStyle, GUILayout.Height(28)))
            {
                ToggleAutoSpawn();
            }

            GUI.backgroundColor = new Color(0.35f, 0.75f, 0.45f);
            if (GUILayout.Button("🛡️ <b>GIẢI PHÓNG TOÀN BỘ ĐẢO (F11)</b>", buttonStyle, GUILayout.Height(30)))
            {
                ResolveAllImmediate();
            }

            GUI.backgroundColor = new Color(0.45f, 0.65f, 0.95f);
            if (GUILayout.Button("↺ Đặt lại Cooldown về 0", buttonStyle, GUILayout.Height(26)))
            {
                mgr.ResetCooldownTimer();
                Debug.Log("<color=#38bdf8>[Test Tool - Thú Hoang]</color> Đã đặt lại thời gian đếm ngược về 0.");
            }

            GUI.backgroundColor = Color.white;
            GUILayout.Space(8);
            GUILayout.Label("<color=#94a3b8>Phím tắt: F9 (Ẩn/Hiện) · F10 (Spawn) · F11 (Dẹp sạch) · Shift+F10 (Bật/Tắt Auto)</color>", statusStyle);

            GUI.DragWindow();
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/LumiWorld/Cheats/Thú Hoang: Force Spawn Ngay (F10)")]
        private static void MenuForceSpawn() => ForceSpawnImmediate();

        [UnityEditor.MenuItem("Tools/LumiWorld/Cheats/Thú Hoang: Dẹp Sạch Toàn Bộ (F11)")]
        private static void MenuResolveAll() => ResolveAllImmediate();

        [UnityEditor.MenuItem("Tools/LumiWorld/Cheats/Thú Hoang: Bật Tắt Tự Động (Shift+F10)")]
        private static void MenuToggleAuto() => ToggleAutoSpawn();
#endif
    }
}
