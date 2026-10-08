using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hệ thống hướng dẫn và nhận diện trực quan VÙNG đặt Thú (Creature Placement Zone Guide):
/// 
/// ĐẶC BIỆT NÂNG CẤP HOÀN THIỆN:
/// 1. VÙNG ĐẤT LỤC GIÁC PHÁT SÁNG (Ground Hex Highlight):
///    - Toàn bộ bề mặt ô lục giác sáng bừng viền dạ quang (Emerald Green neon cho ô hợp lệ, Coral Red cho ô đã có thú).
///    - Dải ánh sáng nhịp thở lan tỏa trên mặt đất, giúp người chơi quan sát thấy rõ ranh giới từng ô từ mọi góc độ.
/// 
/// 2. PIN CHỈ DẪN TRÊN TÁN CÂY (Elevated Overhead Floating Badge):
///    - Pin định vị nổi cao hẳn trên ngọn cây (surfaceY + 1.85m), có cột sáng (Vertical Stem) neo thẳng xuống tâm ô.
///    - KHÔNG BAO GIỜ bị cây cối, đá hay tán lá che khuất, bất kể camera quay hướng nào.
///    - Áp dụng Overlay Material (ZTest Always) đảm bảo luôn nổi 100% trên màn hình.
/// 
/// 3. HIỂN THỊ ĐẦY ĐỦ VÀ ĐỒNG NHẤT 100%:
///    - Cả 2 ô hàng trên đều hiển thị rõ ràng Pin xanh + Vùng đất xanh.
///    - Cả 2 ô hàng dưới đều hiển thị rõ ràng Pin đỏ + Tên con thú đang cư trú + Vùng đất đỏ cảnh báo.
/// 
/// 4. DỌN DẸP SẠCH SẼ:
///    - Khi thả chuột hoặc huỷ đặt: Bản đồ lập tức sạch bóng 100%, không để lại bất kỳ rác hay hiệu ứng thừa nào.
/// </summary>
public class HexCreatureTileGuide : MonoBehaviour
{
    private static HexCreatureTileGuide _instance;
    private static bool isApplicationQuitting = false;

    public static bool HasInstance => _instance != null && !isApplicationQuitting;

    public static HexCreatureTileGuide Instance
    {
        get
        {
            if (isApplicationQuitting) return null;
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<HexCreatureTileGuide>();
                if (_instance == null && !isApplicationQuitting)
                {
                    GameObject go = new GameObject("HexCreatureTileGuide");
                    _instance = go.AddComponent<HexCreatureTileGuide>();
                }
            }
            return _instance;
        }
    }

    private class TileGuideVisualInfo
    {
        public HexCoordinates coords;
        public bool isOccupied;
        public AnimalVisualEnhancer associatedAnimal;

        // Ground visual (Mặt đất)
        public GameObject groundObj;
        public MeshRenderer groundRenderer;
        public Color baseGroundColor;

        // Vertical Guide Beam (Cột sáng neo đất)
        public GameObject stemObj;
        public LineRenderer stemRenderer;

        // Floating Overhead Badge (Pin trên cao)
        public GameObject badgeObj;
        public RectTransform rootRect;
        public Vector3 baseBadgePos;
        public Image auraRingImage;
        public Image discBackingImage;
        public Image iconImage;
        public Image pillBgImage;
        public TextMeshProUGUI labelText;

        // Hover animation state
        public float currentScale = 1.0f;
    }

    private readonly List<TileGuideVisualInfo> activeGuides = new List<TileGuideVisualInfo>();
    private readonly HashSet<AnimalVisualEnhancer> highlightedAnimals = new HashSet<AnimalVisualEnhancer>();

    private GameObject guideCanvasObj;
    private Canvas guideCanvas;
    private Camera cachedCam;
    private bool isGuideActive = false;
    private HexCoordinates lastHoveredHex;

    // Static Procedural Assets Cache
    private static Mesh sharedHexGroundMesh;
    private static Material cachedGroundMaterial;
    private static Material cachedAlwaysOnTopUIMat;
    private static Sprite cachedGreenPawSprite;
    private static Sprite cachedRedPawSprite;
    private static Sprite cachedGlowRingSprite;
    private static Sprite cachedCircleDiscSprite;
    private static Sprite cachedPillBgSprite;

    private MaterialPropertyBlock propBlock;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        cachedCam = Camera.main;
        propBlock = new MaterialPropertyBlock();
    }

    private void OnApplicationQuit()
    {
        isApplicationQuitting = true;
    }

    private void OnDestroy()
    {
        isGuideActive = false;
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void LateUpdate()
    {
        if (!isGuideActive || activeGuides.Count == 0) return;

        if (cachedCam == null) cachedCam = Camera.main;
        if (cachedCam == null) return;

        Quaternion camRot = cachedCam.transform.rotation;
        float time = Time.time;

        for (int i = 0; i < activeGuides.Count; i++)
        {
            TileGuideVisualInfo info = activeGuides[i];
            if (info == null) continue;

            bool isHovered = info.coords.Equals(lastHoveredHex);
            float targetScale = isHovered ? 1.25f : 1.0f;
            info.currentScale = Mathf.Lerp(info.currentScale, targetScale, Time.deltaTime * 12f);

            // 1. Nhấp nhô bồng bềnh trên ngọn cây (Floating Bob)
            if (info.badgeObj != null)
            {
                info.badgeObj.transform.rotation = camRot;

                float bob = 0.08f * Mathf.Sin(time * 2.8f + i * 0.45f);
                info.badgeObj.transform.position = info.baseBadgePos + Vector3.up * bob;

                // Nhịp thở scale
                float pulse = info.currentScale * (1.0f + 0.06f * Mathf.Sin(time * 3.8f + i * 0.5f));
                info.rootRect.localScale = new Vector3(pulse, pulse, 1f) * 0.0050f;
            }

            // 2. Nhịp thở phát quang viền đất (Ground Hex Zone Glow)
            if (info.groundRenderer != null)
            {
                float groundPulse = isHovered
                    ? 1.0f
                    : 0.72f + 0.22f * Mathf.Sin(time * 3.2f + i * 0.4f);

                Color curColor = info.baseGroundColor;
                curColor.a *= groundPulse;

                info.groundRenderer.GetPropertyBlock(propBlock);
                propBlock.SetColor("_Color", curColor);
                propBlock.SetColor("_BaseColor", curColor);
                info.groundRenderer.SetPropertyBlock(propBlock);
            }

            // 3. Cập nhật vị trí đỉnh cột sáng nối theo badge
            if (info.stemRenderer != null && info.badgeObj != null)
            {
                Vector3 curBadgePos = info.badgeObj.transform.position;
                info.stemRenderer.SetPosition(1, curBadgePos + Vector3.down * 0.32f);
            }
        }
    }

    /// <summary>
    /// Bật hiển thị chỉ dẫn toàn diện khi người chơi bắt đầu kéo thẻ bài Thú
    /// </summary>
    public void ShowGuide(CardData creatureCard, HexWorldGenerator worldGen, HexPlacementValidator validator)
    {
        HideGuide();

        if (creatureCard == null || creatureCard.cardType != CardType.Creature) return;
        if (worldGen == null || worldGen.MapTiles == null) return;
        if (cachedCam == null) cachedCam = Camera.main;

        EnsureProceduralAssets();
        EnsureCanvasCreated();

        isGuideActive = true;
        guideCanvasObj.SetActive(true);

        foreach (var pair in worldGen.MapTiles)
        {
            HexCoordinates coords = pair.Key;
            GameObject tileObj = pair.Value;
            if (tileObj == null) continue;

            // 1. Ô ĐÃ CÓ THÚ SINH SỐNG (Báo đỏ ngắn gọn: "Đã có thú rồi")
            if (validator != null && validator.HasPlacedCreature(tileObj))
            {
                AnimalVisualEnhancer animal = tileObj.GetComponentInChildren<AnimalVisualEnhancer>();
                CreateTileGuideVisual(coords, tileObj, true, "Đã có thú rồi", animal);

                if (animal != null)
                {
                    animal.SetGreetingHighlight(true);
                    highlightedAnimals.Add(animal);
                }
            }
            // 2. Ô TRỐNG HỢP LỆ (Có thể đặt thú này -> Sáng bừng xanh lá toàn bộ)
            else if (validator != null && validator.ValidatePlacement(coords, worldGen, creatureCard, out _))
            {
                CreateTileGuideVisual(coords, tileObj, false, "Đặt vào đây", null);
            }
        }
    }

    /// <summary>
    /// Cập nhật ô đang được chuột trỏ tới để tạo hiệu ứng nảy nhấn mạnh và phản hồi thú
    /// </summary>
    public void UpdateHover(HexCoordinates hoveredHex)
    {
        if (!isGuideActive) return;
        if (hoveredHex.Equals(lastHoveredHex)) return;
        lastHoveredHex = hoveredHex;

        for (int i = 0; i < activeGuides.Count; i++)
        {
            TileGuideVisualInfo info = activeGuides[i];
            if (info == null) continue;

            bool isHovered = info.coords.Equals(hoveredHex);
            if (isHovered && info.associatedAnimal != null)
            {
                // Thú nhảy cẫng thêm một nhịp khi người chơi trỏ trúng ô lãnh địa của nó
                info.associatedAnimal.TriggerExtraExcitement();
            }
        }
    }

    /// <summary>
    /// Tắt hoàn toàn chỉ dẫn, trả lại bản đồ sạch sẽ 100%
    /// </summary>
    public void HideGuide()
    {
        isGuideActive = false;
        if (isApplicationQuitting) return;

        // Tắt hào quang của toàn bộ các con thú
        foreach (var animal in highlightedAnimals)
        {
            if (animal != null)
            {
                animal.SetGreetingHighlight(false);
            }
        }
        highlightedAnimals.Clear();

        // Xóa sạch toàn bộ visual mặt đất, cột sáng và badge
        for (int i = 0; i < activeGuides.Count; i++)
        {
            TileGuideVisualInfo info = activeGuides[i];
            if (info == null) continue;

            if (info.groundObj != null) Destroy(info.groundObj);
            if (info.stemObj != null) Destroy(info.stemObj);
            if (info.badgeObj != null) Destroy(info.badgeObj);
        }
        activeGuides.Clear();

        if (guideCanvasObj != null)
        {
            guideCanvasObj.SetActive(false);
        }
    }

    /// <summary>
    /// Tạo trọn bộ 3 thành phần chỉ dẫn cho 1 ô lục giác:
    /// 1. Vùng lục giác phát sáng ôm trọn mặt đất (Ground Hex Zone)
    /// 2. Cột sáng nối thẳng lên trời (Vertical Stem)
    /// 3. Pin biểu tượng nổi cao trên ngọn cây (Overhead Floating Badge)
    /// </summary>
    private void CreateTileGuideVisual(HexCoordinates coords, GameObject tileObj, bool isOccupied, string label, AnimalVisualEnhancer animal)
    {
        // 1. Xác định bề mặt thực tế chuẩn xác của ô cỏ (không bị dôi bởi props)
        float surfaceY = HexMetrics.TileHeight;
        if (tileObj != null)
        {
            var (topY, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(tileObj);
            surfaceY = topY;
        }

        Vector3 centerWorldPos = tileObj != null
            ? new Vector3(tileObj.transform.position.x, surfaceY, tileObj.transform.position.z)
            : HexMetrics.HexToWorldPosition(coords, surfaceY);

        // Bảng màu phân biệt rõ ràng: Xanh lục bảo (Hợp lệ) / Đỏ cam cảnh báo (Đã có thú)
        Color themeColor = isOccupied
            ? new Color(0.98f, 0.32f, 0.32f, 1f)   // Đỏ cam
            : new Color(0.18f, 0.98f, 0.52f, 1f);  // Xanh lục bảo neon

        Color groundMeshBaseColor = isOccupied
            ? new Color(0.98f, 0.30f, 0.28f, 0.78f)
            : new Color(0.15f, 0.98f, 0.55f, 0.85f);

        // -------------------------------------------------------------
        // PHẦN 1: VÙNG ĐẤT LỤC GIÁC PHÁT SÁNG (GROUND HEX ZONE HIGHLIGHT)
        // -------------------------------------------------------------
        GameObject groundObj = new GameObject($"GroundHexZone_{coords.Q}_{coords.R}_{(isOccupied ? "Occupied" : "Free")}");
        groundObj.transform.SetParent(transform, false);
        groundObj.transform.position = new Vector3(centerWorldPos.x, surfaceY + 0.038f, centerWorldPos.z);

        MeshFilter mf = groundObj.AddComponent<MeshFilter>();
        mf.sharedMesh = sharedHexGroundMesh;

        MeshRenderer mr = groundObj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = cachedGroundMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor("_Color", groundMeshBaseColor);
        block.SetColor("_BaseColor", groundMeshBaseColor);
        mr.SetPropertyBlock(block);

        // -------------------------------------------------------------
        // PHẦN 2: PIN CHỈ DẪN NỔI CAO TRÊN TÁN CÂY (FLOATING BADGE)
        // -------------------------------------------------------------
        // Nổi cao 1.85m phía trên mặt đất để vượt qua mọi tán cây cổ thụ
        Vector3 badgeWorldPos = new Vector3(centerWorldPos.x, surfaceY + 1.85f, centerWorldPos.z);

        GameObject badgeObj = new GameObject($"Badge_{coords.Q}_{coords.R}_{(isOccupied ? "Occupied" : "Free")}", typeof(RectTransform));
        badgeObj.transform.SetParent(guideCanvas.transform, false);
        badgeObj.transform.position = badgeWorldPos;

        RectTransform rootRect = badgeObj.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(120f, 120f);
        rootRect.localScale = Vector3.one * 0.0050f;

        // Vòng phát quang ngoài cùng (Glow Aura Ring)
        GameObject auraGo = new GameObject("AuraRing", typeof(RectTransform));
        auraGo.transform.SetParent(badgeObj.transform, false);
        RectTransform auraRect = auraGo.GetComponent<RectTransform>();
        auraRect.sizeDelta = new Vector2(115f, 115f);
        Image auraImg = auraGo.AddComponent<Image>();
        auraImg.sprite = cachedGlowRingSprite;
        auraImg.color = isOccupied ? new Color(1f, 0.35f, 0.35f, 0.90f) : new Color(0.25f, 1f, 0.55f, 0.95f);
        auraImg.material = cachedAlwaysOnTopUIMat;
        auraImg.raycastTarget = false;

        // Đĩa nền tròn kính tối màu (Solid Dark Glass Disc)
        GameObject discGo = new GameObject("BackingDisc", typeof(RectTransform));
        discGo.transform.SetParent(badgeObj.transform, false);
        RectTransform discRect = discGo.GetComponent<RectTransform>();
        discRect.sizeDelta = new Vector2(88f, 88f);
        Image discImg = discGo.AddComponent<Image>();
        discImg.sprite = cachedCircleDiscSprite;
        discImg.color = isOccupied ? new Color(0.18f, 0.06f, 0.06f, 0.94f) : new Color(0.04f, 0.16f, 0.10f, 0.94f);
        discImg.material = cachedAlwaysOnTopUIMat;
        discImg.raycastTarget = false;

        // Icon dấu chân thú (Paw Icon)
        GameObject iconGo = new GameObject("PawIcon", typeof(RectTransform));
        iconGo.transform.SetParent(badgeObj.transform, false);
        RectTransform iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(52f, 52f);
        iconRect.anchoredPosition = new Vector2(0f, 3f);
        Image iconImg = iconGo.AddComponent<Image>();
        iconImg.sprite = isOccupied ? cachedRedPawSprite : cachedGreenPawSprite;
        iconImg.material = cachedAlwaysOnTopUIMat;
        iconImg.raycastTarget = false;

        // Thẻ nhãn hình con nhộng (Pill Label Tag)
        GameObject pillGo = new GameObject("PillLabel", typeof(RectTransform));
        pillGo.transform.SetParent(badgeObj.transform, false);
        RectTransform pillRect = pillGo.GetComponent<RectTransform>();
        pillRect.sizeDelta = new Vector2(175f, 36f);
        pillRect.anchoredPosition = new Vector2(0f, -44f);
        Image pillImg = pillGo.AddComponent<Image>();
        pillImg.sprite = cachedPillBgSprite;
        pillImg.color = isOccupied ? new Color(0.16f, 0.05f, 0.05f, 0.94f) : new Color(0.03f, 0.14f, 0.08f, 0.94f);
        pillImg.material = cachedAlwaysOnTopUIMat;
        pillImg.raycastTarget = false;

        // Chữ chú thích bên trong thẻ nhãn
        GameObject textGo = new GameObject("LabelText", typeof(RectTransform));
        textGo.transform.SetParent(pillGo.transform, false);
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(170f, 32f);
        textRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 13f;
        tmp.fontSizeMax = 17f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = isOccupied ? new Color(1.0f, 0.48f, 0.45f, 1f) : new Color(0.42f, 1.0f, 0.65f, 1f);
        tmp.outlineWidth = 0.28f;
        tmp.outlineColor = new Color32(10, 15, 25, 240);
        tmp.material = cachedAlwaysOnTopUIMat;
        tmp.raycastTarget = false;

        // -------------------------------------------------------------
        // PHẦN 3: CỘT SÁNG NEO ĐẤT (VERTICAL GUIDE BEAM)
        // -------------------------------------------------------------
        GameObject stemGo = new GameObject("GuideStem");
        stemGo.transform.SetParent(transform, false);

        LineRenderer lr = stemGo.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.SetPosition(0, new Vector3(centerWorldPos.x, surfaceY + 0.06f, centerWorldPos.z));
        lr.SetPosition(1, badgeWorldPos + Vector3.down * 0.32f);
        lr.startWidth = 0.045f;
        lr.endWidth = 0.020f;
        lr.sharedMaterial = cachedGroundMaterial;
        lr.startColor = new Color(themeColor.r, themeColor.g, themeColor.b, 0.65f);
        lr.endColor = new Color(themeColor.r, themeColor.g, themeColor.b, 0.18f);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        // Lưu thông tin để vận hành trong LateUpdate
        activeGuides.Add(new TileGuideVisualInfo
        {
            coords = coords,
            isOccupied = isOccupied,
            associatedAnimal = animal,

            groundObj = groundObj,
            groundRenderer = mr,
            baseGroundColor = groundMeshBaseColor,

            stemObj = stemGo,
            stemRenderer = lr,

            badgeObj = badgeObj,
            rootRect = rootRect,
            baseBadgePos = badgeWorldPos,
            auraRingImage = auraImg,
            discBackingImage = discImg,
            iconImage = iconImg,
            pillBgImage = pillImg,
            labelText = tmp,
            currentScale = 1.0f
        });
    }

    private PlacedCard GetCreaturePlacedCard(GameObject tileObj)
    {
        if (tileObj == null) return null;
        PlacedCard[] cards = tileObj.GetComponentsInChildren<PlacedCard>();
        foreach (var c in cards)
        {
            if (c != null && c.CardType == CardType.Creature) return c;
        }
        return null;
    }

    private void EnsureCanvasCreated()
    {
        if (guideCanvasObj == null)
        {
            guideCanvasObj = new GameObject("CreaturePlacementGuideCanvas");
            guideCanvasObj.transform.SetParent(transform, false);

            guideCanvas = guideCanvasObj.AddComponent<Canvas>();
            guideCanvas.renderMode = RenderMode.WorldSpace;
            guideCanvas.sortingOrder = 550; // Luôn hiển thị trên cùng

            guideCanvasObj.AddComponent<CanvasScaler>();
        }
    }

    private static void EnsureProceduralAssets()
    {
        // 1. Mesh Vùng Lục Giác Mặt Đất
        if (sharedHexGroundMesh == null)
        {
            sharedHexGroundMesh = CreateHexGroundHighlightMesh();
        }

        // 2. Material Mặt Đất (Unlit Transparent, vertex colored)
        if (cachedGroundMaterial == null)
        {
            Shader s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Unlit/Color");
            cachedGroundMaterial = new Material(s);
            cachedGroundMaterial.renderQueue = 3100;
        }

        // 3. Material Overlay Always-On-Top cho Canvas UI (Không bị cây cối che khuất)
        if (cachedAlwaysOnTopUIMat == null)
        {
            Shader uiShader = Shader.Find("UI/Default");
            if (uiShader != null)
            {
                cachedAlwaysOnTopUIMat = new Material(uiShader);
                cachedAlwaysOnTopUIMat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            }
        }

        // 4. Các Sprite thủ tục
        if (cachedGreenPawSprite == null)
            cachedGreenPawSprite = CreatePawIconSprite(new Color(0.25f, 0.98f, 0.52f, 1f), 64);
        if (cachedRedPawSprite == null)
            cachedRedPawSprite = CreatePawIconSprite(new Color(0.98f, 0.32f, 0.32f, 1f), 64);

        if (cachedGlowRingSprite == null)
            cachedGlowRingSprite = CreateGlowRingSprite(128);

        if (cachedCircleDiscSprite == null)
            cachedCircleDiscSprite = CreateSolidCircleSprite(128);

        if (cachedPillBgSprite == null)
            cachedPillBgSprite = CreatePillBackgroundSprite(160, 48);
    }

    /// <summary>
    /// Sinh Mesh lục giác nhọn đỉnh (Pointed-top) chuẩn xác ôm trọn mặt đất của ô lục giác:
    /// Gồm đĩa nền mờ ở giữa + dải viền ngoài phát sáng rực rỡ + mép viền ngoài tan dần mềm mại (anti-aliased fade).
    /// </summary>
    private static Mesh CreateHexGroundHighlightMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "HexGroundHighlightMesh";

        List<Vector3> vertices = new List<Vector3>();
        List<Color> colors = new List<Color>();
        List<int> triangles = new List<int>();

        float baseOuterR = HexMetrics.OuterRadius;
        float rInner = baseOuterR * 0.74f;
        float rOuter = baseOuterR * 0.95f;
        float rFade = rOuter + 0.08f;

        // 6 góc của lục giác nhọn đỉnh (Pointed-Top)
        Vector2[] dirs = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            float rad = (30f + i * 60f) * Mathf.Deg2Rad;
            dirs[i] = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        // Đỉnh 0: Tâm lục giác
        vertices.Add(Vector3.zero);
        colors.Add(new Color(1f, 1f, 1f, 0.22f));

        // Đỉnh 1..6: Vòng trong (Inner Ring)
        for (int i = 0; i < 6; i++)
        {
            vertices.Add(new Vector3(dirs[i].x * rInner, 0f, dirs[i].y * rInner));
            colors.Add(new Color(1f, 1f, 1f, 0.42f));
        }

        // Đỉnh 7..12: Vòng ngoài phát sáng (Outer Glowing Border)
        for (int i = 0; i < 6; i++)
        {
            vertices.Add(new Vector3(dirs[i].x * rOuter, 0f, dirs[i].y * rOuter));
            colors.Add(new Color(1f, 1f, 1f, 0.95f));
        }

        // Đỉnh 13..18: Viền tan dần (Fade Feather Rim)
        for (int i = 0; i < 6; i++)
        {
            vertices.Add(new Vector3(dirs[i].x * rFade, 0f, dirs[i].y * rFade));
            colors.Add(new Color(1f, 1f, 1f, 0.0f));
        }

        // 1. Tam giác đĩa trung tâm (Center Disc)
        for (int i = 0; i < 6; i++)
        {
            int next = (i + 1) % 6;
            triangles.Add(0);
            triangles.Add(1 + next);
            triangles.Add(1 + i);
        }

        // 2. Dải viền sáng (Inner Ring -> Outer Ring)
        for (int i = 0; i < 6; i++)
        {
            int next = (i + 1) % 6;
            int inA = 1 + i;
            int inB = 1 + next;
            int outA = 7 + i;
            int outB = 7 + next;

            triangles.Add(inA);
            triangles.Add(outB);
            triangles.Add(outA);

            triangles.Add(inA);
            triangles.Add(inB);
            triangles.Add(outB);
        }

        // 3. Dải viền mờ mềm mại (Outer Ring -> Fade Rim)
        for (int i = 0; i < 6; i++)
        {
            int next = (i + 1) % 6;
            int outA = 7 + i;
            int outB = 7 + next;
            int fadeA = 13 + i;
            int fadeB = 13 + next;

            triangles.Add(outA);
            triangles.Add(fadeB);
            triangles.Add(fadeA);

            triangles.Add(outA);
            triangles.Add(outB);
            triangles.Add(fadeB);
        }

        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();

        return mesh;
    }

    private static Sprite CreatePawIconSprite(Color color, int size = 64)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = size * 0.5f;
        Color[] pixels = new Color[size * size];

        Vector2 mainPadCenter = new Vector2(center, size * 0.38f);
        float mainPadRadius = size * 0.22f;

        Vector2[] toes = new Vector2[]
        {
            new Vector2(center - size * 0.26f, size * 0.62f),
            new Vector2(center - size * 0.10f, size * 0.76f),
            new Vector2(center + size * 0.10f, size * 0.76f),
            new Vector2(center + size * 0.26f, size * 0.62f)
        };
        float toeRadius = size * 0.085f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pt = new Vector2(x + 0.5f, y + 0.5f);
                float alpha = 0f;

                float mDx = (pt.x - mainPadCenter.x);
                float mDy = (pt.y - mainPadCenter.y) * 1.25f;
                float mDist = Mathf.Sqrt(mDx * mDx + mDy * mDy);
                if (mDist <= mainPadRadius + 1f)
                {
                    alpha = Mathf.Max(alpha, Mathf.Clamp01(mainPadRadius + 1f - mDist));
                }

                for (int t = 0; t < toes.Length; t++)
                {
                    float d = Vector2.Distance(pt, toes[t]);
                    if (d <= toeRadius + 1f)
                    {
                        alpha = Mathf.Max(alpha, Mathf.Clamp01(toeRadius + 1f - d));
                    }
                }

                Color c = color;
                c.a *= alpha;
                pixels[y * size + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateGlowRingSprite(int size = 128)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = size * 0.5f;
        Color[] pixels = new Color[size * size];

        float outerR = size * 0.46f;
        float innerR = size * 0.36f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pt = new Vector2(x + 0.5f, y + 0.5f);
                float dist = Vector2.Distance(pt, new Vector2(center, center));

                float alpha = 0f;
                if (dist >= innerR && dist <= outerR)
                {
                    float mid = (innerR + outerR) * 0.5f;
                    float halfSpan = (outerR - innerR) * 0.5f;
                    alpha = 1f - Mathf.Abs(dist - mid) / halfSpan;
                }
                else if (dist > outerR && dist <= outerR + 4f)
                {
                    alpha = Mathf.Clamp01((outerR + 4f - dist) / 4f) * 0.5f;
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateSolidCircleSprite(int size = 128)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = size * 0.5f;
        Color[] pixels = new Color[size * size];
        float radius = size * 0.47f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pt = new Vector2(x + 0.5f, y + 0.5f);
                float dist = Vector2.Distance(pt, new Vector2(center, center));
                float alpha = Mathf.Clamp01(radius + 1f - dist);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreatePillBackgroundSprite(int width = 160, int height = 48)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[width * height];
        float radius = height * 0.5f - 1f;

        Vector2 leftCenter = new Vector2(radius + 1f, height * 0.5f);
        Vector2 rightCenter = new Vector2(width - radius - 1f, height * 0.5f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 pt = new Vector2(x + 0.5f, y + 0.5f);
                float alpha = 0f;

                if (pt.x < leftCenter.x)
                {
                    float d = Vector2.Distance(pt, leftCenter);
                    alpha = Mathf.Clamp01(radius + 1f - d);
                }
                else if (pt.x > rightCenter.x)
                {
                    float d = Vector2.Distance(pt, rightCenter);
                    alpha = Mathf.Clamp01(radius + 1f - d);
                }
                else
                {
                    float dY = Mathf.Abs(pt.y - height * 0.5f);
                    alpha = Mathf.Clamp01(radius + 1f - dY);
                }

                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
    }
}
