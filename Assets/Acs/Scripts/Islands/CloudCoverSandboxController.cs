using UnityEngine;
using UnityEngine.InputSystem;

namespace LumiWorld.Acs.Islands
{
    /// <summary>
    /// Interactive Play-Mode HUD controller for Assets/Acs/Art/Test/CloudCover_Test.unity.
    /// Lets the user inspect Starter Island surrounded by full world cloud fog,
    /// Lock / Unlock the neighboring Wet Island to watch clouds part & cover,
    /// switch between Painterly 2.5D / Smooth 3D SDF / Soft Mist Only presets,
    /// jump camera between Starter / Wet / Overview, and tune parameters live.
    /// </summary>
    public sealed class CloudCoverSandboxController : MonoBehaviour
    {
        [SerializeField] private CozyWorldCloudSystem cloudSystem;
        [SerializeField] private GameObject footprintRingsRoot;
        [SerializeField] private Vector3 starterCenter = Vector3.zero;
        [SerializeField] private Vector3 wetCenter = new Vector3(90f, 0f, 0f);

        private bool showHud = true;
        private bool showFootprintRings = false;

        private static readonly string[] PresetLabels =
        {
            "1. Painterly 2.5D",
            "2. Smooth 3D SDF",
            "3. Mist Only"
        };

        private GUIStyle boxStyle;
        private GUIStyle titleStyle;
        private GUIStyle sectionStyle;
        private GUIStyle hintStyle;
        private GUIStyle toggleStyle;
        private GUIStyle statusUnlockedStyle;
        private GUIStyle statusLockedStyle;
        private Texture2D hudBgTex;
        private HexCameraController cameraController;

        private const float HudWidth = 370f;
        private const float HudHeight = 560f;

        public void ConfigureReferences(
            CozyWorldCloudSystem system,
            GameObject footprintRings,
            Vector3 starterWorldPos,
            Vector3 wetWorldPos)
        {
            cloudSystem = system;
            footprintRingsRoot = footprintRings;
            starterCenter = starterWorldPos;
            wetCenter = wetWorldPos;
            cameraController = FindAnyObjectByType<HexCameraController>();
            if (footprintRingsRoot != null)
            {
                footprintRingsRoot.SetActive(showFootprintRings);
            }
        }

        private void Awake()
        {
            cameraController = FindAnyObjectByType<HexCameraController>();
        }

        private void OnDisable()
        {
            if (cameraController != null) cameraController.enabled = true;
        }

        private void OnDestroy()
        {
            if (hudBgTex != null)
            {
                Destroy(hudBgTex);
                hudBgTex = null;
            }
            boxStyle = null;
        }

        private void Update()
        {
            if (cloudSystem == null) return;

            if (cameraController == null) cameraController = FindAnyObjectByType<HexCameraController>();
            if (cameraController != null && Mouse.current != null)
            {
                Vector2 mp = Mouse.current.position.ReadValue();
                float guiY = Screen.height - mp.y;
                Rect activeHudRect = showHud
                    ? new Rect(14f, 14f, HudWidth, HudHeight)
                    : new Rect(14f, 14f, 200f, 30f);
                bool pointerInHud = activeHudRect.Contains(new Vector2(mp.x, guiY));
                bool wantCamEnabled = cameraController.IsFocusing || !pointerInHud;
                if (cameraController.enabled != wantCamEnabled)
                {
                    cameraController.enabled = wantCamEnabled;
                }
            }

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.hKey.wasPressedThisFrame) showHud = !showHud;
            if (kb.spaceKey.wasPressedThisFrame) cloudSystem.ToggleZoneUnlocked("wet");

            if (kb.digit1Key.wasPressedThisFrame) cloudSystem.SetStyle(WorldCloudStyle.Painterly25D);
            if (kb.digit2Key.wasPressedThisFrame) cloudSystem.SetStyle(WorldCloudStyle.Smooth3DSdf);
            if (kb.digit3Key.wasPressedThisFrame) cloudSystem.SetStyle(WorldCloudStyle.SoftMistOnly);

            if (kb.qKey.wasPressedThisFrame) FocusCamera(starterCenter, 32f, 0.9f);
            if (kb.eKey.wasPressedThisFrame) FocusCamera(wetCenter, 36f, 1.0f);
            if (kb.rKey.wasPressedThisFrame) FocusCamera((starterCenter + wetCenter) * 0.5f, 96f, 1.1f);
        }

        public void FocusCamera(Vector3 target, float distance, float duration)
        {
            if (cameraController == null) cameraController = FindAnyObjectByType<HexCameraController>();
            if (cameraController == null) return;
            cameraController.enabled = true;
            cameraController.FocusOnTarget(target, distance, duration);
        }

        private void EnsureStyles()
        {
            Color inkDark = new Color(0.16f, 0.14f, 0.12f, 1f);
            Color inkMuted = new Color(0.32f, 0.29f, 0.25f, 1f);

            if (boxStyle == null || hudBgTex == null)
            {
                if (hudBgTex == null)
                {
                    hudBgTex = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
                    hudBgTex.SetPixel(0, 0, new Color(0.97f, 0.955f, 0.92f, 0.94f));
                    hudBgTex.Apply();
                }

                boxStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = hudBgTex, textColor = inkDark },
                    padding = new RectOffset(12, 12, 10, 10)
                };
            }

            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = inkDark }
                };
            }

            if (sectionStyle == null)
            {
                sectionStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = inkDark }
                };
            }

            if (hintStyle == null)
            {
                hintStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    wordWrap = true,
                    normal = { textColor = inkMuted }
                };
            }

            if (toggleStyle == null)
            {
                toggleStyle = new GUIStyle(GUI.skin.toggle)
                {
                    fontSize = 12,
                    normal = { textColor = inkDark },
                    onNormal = { textColor = inkDark },
                    hover = { textColor = inkDark },
                    onHover = { textColor = inkDark },
                    active = { textColor = inkDark },
                    onActive = { textColor = inkDark },
                    focused = { textColor = inkDark },
                    onFocused = { textColor = inkDark }
                };
            }

            if (statusUnlockedStyle == null)
            {
                statusUnlockedStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.11f, 0.48f, 0.25f, 1f) }
                };
            }

            if (statusLockedStyle == null)
            {
                statusLockedStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.66f, 0.34f, 0.12f, 1f) }
                };
            }
        }

        private void OnGUI()
        {
            if (cloudSystem == null) return;
            EnsureStyles();

            if (!showHud)
            {
                GUI.Box(new Rect(14f, 14f, 200f, 30f), "Nhấn [H] để hiện bảng chỉnh", boxStyle);
                return;
            }

            GUILayout.BeginArea(new Rect(14f, 14f, HudWidth, HudHeight), boxStyle);

            GUILayout.Label("LumiWorld — World Cloud Sea Sandbox", titleStyle);
            GUILayout.Label("Kéo chuột trái: di chuyển · Chuột phải/Alt: xoay · Cuộn: zoom · [H]: ẩn UI", hintStyle);
            GUILayout.Space(6f);

            // 1. Camera Quick Views
            GUILayout.Label("Góc nhìn nhanh ([Q] / [E] / [R]):", sectionStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Đảo Mở Đầu", GUILayout.Height(26f)))
                FocusCamera(starterCenter, 32f, 0.9f);
            if (GUILayout.Button("Đảo Kế Bên (Wet)", GUILayout.Height(26f)))
                FocusCamera(wetCenter, 36f, 1.0f);
            if (GUILayout.Button("Toàn Cảnh 2 Đảo", GUILayout.Height(26f)))
                FocusCamera((starterCenter + wetCenter) * 0.5f, 96f, 1.1f);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);

            // 2. Lock / Unlock Neighboring Island (Wet Island)
            bool wetUnlocked = cloudSystem.IsZoneUnlocked("wet");
            float wetOpenPct = cloudSystem.GetZoneOpenProgress("wet");
            GUILayout.Label("Mở khóa / Che mây Đảo Kế Bên ([Space]):", sectionStyle);
            GUILayout.Label(
                wetUnlocked
                    ? $"Trạng thái Wet Island: ĐÃ MỞ KHÓA ({wetOpenPct:P0})"
                    : $"Trạng thái Wet Island: ĐANG KHÓA — Phủ mây kín ({wetOpenPct:P0})",
                wetUnlocked ? statusUnlockedStyle : statusLockedStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(wetUnlocked ? "🔒 Khóa lại (Phủ mây kín Wet)" : "🔓 Mở khóa Đảo Kế Bên (Tan mây)", GUILayout.Height(30f)))
            {
                cloudSystem.ToggleZoneUnlocked("wet");
            }
            GUILayout.EndHorizontal();

            // Optional Starter lock toggle for full fog comparison
            bool starterUnlocked = cloudSystem.IsZoneUnlocked("starter");
            bool nextStarterUnlocked = GUILayout.Toggle(starterUnlocked, " Đảo Mở Đầu (Starter) đang mở khóa", toggleStyle);
            if (nextStarterUnlocked != starterUnlocked)
            {
                cloudSystem.SetZoneUnlocked("starter", nextStarterUnlocked, instant: false);
            }

            GUILayout.Space(8f);

            // 3. Cloud Bank Style Presets (1 / 2 / 3)
            GUILayout.Label("Kiểu khối mây Layer 2 (Phím 1 / 2 / 3):", sectionStyle);
            int curStyle = (int)cloudSystem.Style;
            int nextStyle = GUILayout.Toolbar(curStyle, PresetLabels, GUILayout.Height(26f));
            if (nextStyle != curStyle)
            {
                cloudSystem.SetStyle((WorldCloudStyle)nextStyle);
            }

            GUILayout.Space(6f);

            // 4. Layer visibility toggles (readable dark text)
            bool seaOn = GUILayout.Toggle(cloudSystem.ShowSeaLayer, " Layer 1: Biển khói mờ phủ full vùng khóa", toggleStyle);
            bool bankOn = GUILayout.Toggle(cloudSystem.ShowBankLayer, $" Layer 2: Khối mây bông xếp chồng ({cloudSystem.ActiveBankInstanceCount} cụm)", toggleStyle);
            if (seaOn != cloudSystem.ShowSeaLayer || bankOn != cloudSystem.ShowBankLayer)
            {
                cloudSystem.SetLayerVisibility(seaOn, bankOn);
            }

            bool nextRings = GUILayout.Toggle(showFootprintRings, " Hiện vòng ranh giới đảo (Starter & Wet)", toggleStyle);
            if (nextRings != showFootprintRings)
            {
                showFootprintRings = nextRings;
                if (footprintRingsRoot != null) footprintRingsRoot.SetActive(showFootprintRings);
            }

            GUILayout.Space(8f);

            // 5. Live sliders
            CloudClearZone starterZone = cloudSystem.FindZone("starter");
            CloudClearZone wetZone = cloudSystem.FindZone("wet");

            if (starterZone != null)
            {
                GUILayout.Label($"Vùng mở Đảo Mở Đầu (Starter): {starterZone.clearRadius:F1}m", hintStyle);
                float nStartR = GUILayout.HorizontalSlider(starterZone.clearRadius, 14f, 42f);
                if (!Mathf.Approximately(nStartR, starterZone.clearRadius))
                    cloudSystem.SetZoneClearRadius("starter", nStartR);
            }

            if (wetZone != null)
            {
                GUILayout.Label($"Vùng mở Đảo Kế Bên (Wet): {wetZone.clearRadius:F1}m", hintStyle);
                float nWetR = GUILayout.HorizontalSlider(wetZone.clearRadius, 16f, 48f);
                if (!Mathf.Approximately(nWetR, wetZone.clearRadius))
                    cloudSystem.SetZoneClearRadius("wet", nWetR);
            }

            float ht = cloudSystem.BaseHeight;
            float soft = cloudSystem.EdgeSoftness;
            float scale = cloudSystem.BankScale;
            float spd = cloudSystem.DriftSpeed;

            GUILayout.Label($"Độ cao tầng mây (Base Height): {ht:F2}m", hintStyle);
            float nHt = GUILayout.HorizontalSlider(ht, 1.0f, 5.0f);

            GUILayout.Label($"Độ mềm rìa mây (Edge Softness): {soft:F2}", hintStyle);
            float nSoft = GUILayout.HorizontalSlider(soft, 0.10f, 0.48f);

            GUILayout.Label($"Cỡ khối mây Layer 2 (Bank Scale): {scale:F2}x", hintStyle);
            float nScale = GUILayout.HorizontalSlider(scale, 0.60f, 1.65f);

            GUILayout.Label($"Tốc độ trôi mây (Drift Speed): {spd:F2}x", hintStyle);
            float nSpd = GUILayout.HorizontalSlider(spd, 0f, 2.5f);

            if (!Mathf.Approximately(nHt, ht) || !Mathf.Approximately(nSoft, soft) ||
                !Mathf.Approximately(nScale, scale) || !Mathf.Approximately(nSpd, spd))
            {
                cloudSystem.SetLiveTuning(nHt, nSoft, nScale, nSpd);
            }

            GUILayout.EndArea();
        }
    }
}
