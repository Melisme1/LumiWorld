using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Component nâng cao chất lượng thị giác cho động vật trong LumiWorld (Góc nhìn Top-Down).
/// 
/// ĐẶC BIỆT: TÁCH RỜI HOÀN TOÀN KHỎI MODEL 3D
/// - Không sửa file FBX, GLB, Mesh, Texture hay Material gốc trên ổ cứng.
/// - Tán cây bán trong suốt (Canopy Fade): Khi thú đi vào dưới tán cây (HeroOak, Beech...), 
///   lá cây trên đầu thú sẽ tự động mờ dần (35% opacity) để người chơi nhìn xuyên qua thấy rõ thú bên dưới.
/// - Khi thú bước ra ngoài, tán lá sẽ mượt mà trở lại đặc 100% như cũ.
/// - Chấm ngọc / Đốm sáng bồng bềnh trên đầu (Overhead Crest): Tự động tính độ cao đỉnh đầu/sừng của thú
///   và đặt đốm sáng bay lơ lửng cao hơn hẳn, nhấp nhô nhẹ nhàng, giúp định vị thú cực kỳ dễ dàng từ góc nhìn Top-Down.
/// - Viền sáng tôn dáng (HDR Glow Rim): Viền sáng bao quanh cơ thể giúp thú nổi bật rõ ràng qua tán lá mờ.
/// - Tự động dọn dẹp sạch sẽ khi thoát game, mô hình gốc vẫn nguyên vẹn 100%.
/// </summary>
[DisallowMultipleComponent]
public class AnimalVisualEnhancer : MonoBehaviour
{
    [Header("1. Tán cây bán trong suốt khi thú đi vào (Canopy Fade)")]
    [Tooltip("Khi con thú đi dưới tán cây, lá cây sẽ tự động mờ dần để camera nhìn xuyên qua thấy rõ con thú")]
    [SerializeField] private bool enableCanopyFade = true;

    [Tooltip("Độ mờ của tán lá khi thú ở bên dưới (0.35 = 35% mờ ảo thấy rõ thú)")]
    [Range(0.15f, 0.70f)]
    [SerializeField] private float fadedCanopyAlpha = 0.35f;

    [Tooltip("Tốc độ chuyển đổi độ mờ (càng lớn mờ càng nhanh)")]
    [SerializeField] private float fadeSpeed = 5.0f;

    [Tooltip("Bán kính phát hiện tán cây xung quanh con thú (mét)")]
    [SerializeField] private float canopyCheckRadius = 1.8f;

    [Header("2. Chấm ngọc bồng bềnh trên đầu (Overhead Crest)")]
    [Tooltip("Bật chấm ngọc phát quang lơ lửng trên đỉnh đầu con thú để dễ dàng định vị từ trên cao")]
    [SerializeField] private bool enableOverheadCrest = true;

    [Tooltip("Độ cao nâng thêm phía trên đỉnh đầu/sừng con thú (mét). Tự động cộng dồn với chiều cao thực tế của model.")]
    [SerializeField] private float crestHeightOffset = 0.35f;

    [Tooltip("Kích thước chấm ngọc")]
    [Range(0.04f, 0.25f)]
    [SerializeField] private float crestScale = 0.10f;

    [Tooltip("Màu phát sáng của chấm ngọc (HDR Solar Gold rực rỡ)")]
    [ColorUsage(true, true)]
    [SerializeField] private Color crestColor = new Color(2.5f, 2.1f, 1.0f, 1.0f);

    [Tooltip("Tốc độ bồng bềnh nhấp nhô")]
    [SerializeField] private float crestBobSpeed = 2.8f;

    [Tooltip("Biên độ bồng bềnh (mét)")]
    [SerializeField] private float crestBobAmplitude = 0.04f;

    [Header("3. Viền sáng tôn dáng (Glow Rim Silhouette)")]
    [Tooltip("Bật viền sáng quanh lưng, sừng và bờ vai giúp thú nổi bật rõ rệt qua tán lá")]
    [SerializeField] private bool enableRimGlow = true;

    [Tooltip("Màu viền sáng (Nên chọn màu vàng nắng ấm hoặc trắng ngà)")]
    [ColorUsage(true, true)]
    [SerializeField] private Color rimColor = new Color(1.8f, 1.4f, 0.7f, 1.0f); // HDR Golden

    [Tooltip("Độ sắc nét của viền")]
    [Range(1.0f, 5.0f)]
    [SerializeField] private float rimPower = 2.2f;

    [Tooltip("Cường độ sáng của viền")]
    [Range(0.5f, 4.0f)]
    [SerializeField] private float rimIntensity = 2.5f;

    // Quản lý trạng thái mờ của các tán cây trong Scene (Static để nhiều con thú không xung đột nhau)
    private class TreeFadeInfo
    {
        public Renderer renderer;
        public Material[] materials;
        public float currentAlpha = 1.0f;
        public readonly HashSet<AnimalVisualEnhancer> occludingAnimals = new HashSet<AnimalVisualEnhancer>();
    }

    private static readonly Dictionary<Renderer, TreeFadeInfo> ActiveFadingTrees = new Dictionary<Renderer, TreeFadeInfo>();

    // Bộ đệm Renderer lân cận để kiểm tra che khuất
    private readonly List<Renderer> candidateRenderers = new List<Renderer>();
    private float nextCandidateScanTime = 0f;
    private HexWorldGenerator cachedWorldGen;

    // Đối tượng sinh ra tại Runtime
    private GameObject crestObj;
    private Material crestMaterialInstance;
    private float detectedModelHeight = 0.65f;

    private Material rimMaterialInstance;
    private readonly List<Renderer> affectedRenderers = new List<Renderer>();
    private readonly HashSet<Renderer> myCurrentlyOccludingTrees = new HashSet<Renderer>();

    private Camera mainCamera;

    private void Awake()
    {
        // Bỏ qua nếu là đối tượng bóng mờ xem trước (Ghost Preview)
        if (gameObject.name.Contains("Ghost") || gameObject.name.Contains("Preview"))
        {
            return;
        }

        mainCamera = Camera.main;

        // Đảm bảo không bao giờ có bất kỳ Tile hay khối đất nào bị kẹt trạng thái Transparent
        RestoreAllTilesToOpaque();

        // Kích hoạt Viền sáng Rim Glow (Additive Material trên Slot 2)
        if (enableRimGlow)
        {
            ApplyRimGlowOverlay();
        }
    }

    private void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        cachedWorldGen = FindAnyObjectByType<HexWorldGenerator>();
        RefreshCandidateRenderers();

        // Tự động tính chiều cao thực tế của đỉnh đầu/sừng con thú
        detectedModelHeight = ComputeModelTopHeight();

        // Tạo chấm ngọc bồng bềnh trên đầu
        if (enableOverheadCrest)
        {
            SetupOverheadCrest();
        }
    }

    // Trạng thái tương tác với Placement Guide (Khi người chơi cầm thẻ Thú)
    private bool isGreetingHighlighted = false;
    private float excitementTimer = 0f;
    private Vector3 originalLocalScale = Vector3.one;
    private bool capturedOriginalScale = false;

    private void LateUpdate()
    {
        if (enableCanopyFade)
        {
            UpdateCanopyOcclusion();
        }

        if (enableOverheadCrest && crestObj != null)
        {
            AnimateOverheadCrest();
        }

        // Hiệu ứng nhảy nhót vui vẻ khi người chơi hover trúng ô nhà của con thú
        if (excitementTimer > 0f)
        {
            excitementTimer -= Time.deltaTime;
            float bounceProgress = 1f - Mathf.Clamp01(excitementTimer / 0.45f);
            float squash = Mathf.Sin(bounceProgress * Mathf.PI) * 0.18f;
            transform.localScale = new Vector3(
                originalLocalScale.x * (1f - squash * 0.4f),
                originalLocalScale.y * (1f + squash),
                originalLocalScale.z * (1f - squash * 0.4f)
            );
            if (excitementTimer <= 0f)
            {
                transform.localScale = originalLocalScale;
            }
        }
    }

    #region 1. Overhead Crest (Chấm ngọc bồng bềnh trên đầu)

    private float ComputeModelTopHeight()
    {
        Renderer[] rends = GetComponentsInChildren<Renderer>(false);
        float maxY = 0.55f;
        for (int i = 0; i < rends.Length; i++)
        {
            Renderer r = rends[i];
            if (r == null) continue;
            if (crestObj != null && r.transform.IsChildOf(crestObj.transform)) continue;

            float localTop = transform.InverseTransformPoint(r.bounds.max).y;
            if (localTop > maxY)
            {
                maxY = localTop;
            }
        }
        return maxY;
    }

    private void SetupOverheadCrest()
    {
        // Tạo hình cầu ngọc nhỏ (tinh thể phát quang)
        crestObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crestObj.name = "Runtime_OverheadCrest";
        crestObj.transform.SetParent(transform, false);

        // Hủy bỏ Collider để không ảnh hưởng raycast/physics
        Collider col = crestObj.GetComponent<Collider>();
        if (col != null)
        {
            Destroy(col);
        }

        // Tạo hình thoi/giọt ngọc thuôn nhẹ theo chiều dọc
        crestObj.transform.localScale = new Vector3(crestScale * 0.85f, crestScale * 1.25f, crestScale * 0.85f);
        crestObj.transform.localPosition = new Vector3(0f, detectedModelHeight + crestHeightOffset, 0f);

        // Tạo Material phát sáng Unlit HDR (sáng rực rỡ với Bloom của game)
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlitShader == null)
        {
            unlitShader = Shader.Find("Unlit/Color");
        }

        if (unlitShader != null)
        {
            crestMaterialInstance = new Material(unlitShader);
            crestMaterialInstance.name = "Runtime_Crest_Material";
            if (crestMaterialInstance.HasProperty("_BaseColor"))
            {
                crestMaterialInstance.SetColor("_BaseColor", crestColor);
            }
            if (crestMaterialInstance.HasProperty("_Color"))
            {
                crestMaterialInstance.SetColor("_Color", crestColor);
            }

            Renderer crestRend = crestObj.GetComponent<Renderer>();
            if (crestRend != null)
            {
                crestRend.material = crestMaterialInstance;
                crestRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                crestRend.receiveShadows = false;
            }
        }
    }

    private void AnimateOverheadCrest()
    {
        if (crestObj == null) return;

        // Hiệu ứng nhấp nhô bồng bềnh theo nhịp thở (Sine Wave)
        float currentBobSpeed = isGreetingHighlighted ? crestBobSpeed * 1.8f : crestBobSpeed;
        float currentBobAmp = isGreetingHighlighted ? crestBobAmplitude * 1.6f : crestBobAmplitude;
        float bob = Mathf.Sin(Time.time * currentBobSpeed) * currentBobAmp;
        crestObj.transform.localPosition = new Vector3(0f, detectedModelHeight + crestHeightOffset + bob, 0f);

        // Xoay quanh trục Y (Xoay nhanh hơn khi đang được gọi tên chào mừng)
        float rotSpeed = isGreetingHighlighted ? 120f : 50f;
        crestObj.transform.Rotate(Vector3.up, rotSpeed * Time.deltaTime, Space.Self);

        // Nhấp nháy ánh hào quang khi chào mừng người chơi cầm thẻ
        if (isGreetingHighlighted && crestMaterialInstance != null)
        {
            float pulse = 2.4f + 0.8f * Mathf.Sin(Time.time * 6f);
            Color highlightC = crestColor * pulse;
            highlightC.a = 1.0f;
            if (crestMaterialInstance.HasProperty("_BaseColor")) crestMaterialInstance.SetColor("_BaseColor", highlightC);
            if (crestMaterialInstance.HasProperty("_Color")) crestMaterialInstance.SetColor("_Color", highlightC);
        }
    }

    /// <summary>
    /// Bật/Tắt hiệu ứng chào mừng khi người chơi cầm thẻ Thú (Creature Placement Guide):
    /// Chấm ngọc trên đầu và viền sáng phát quang rực rỡ hơn (x2.5), nhấp nháy để người chơi nhận ra ngay.
    /// </summary>
    public void SetGreetingHighlight(bool active)
    {
        isGreetingHighlighted = active;
        if (!active)
        {
            if (crestMaterialInstance != null)
            {
                if (crestMaterialInstance.HasProperty("_BaseColor")) crestMaterialInstance.SetColor("_BaseColor", crestColor);
                if (crestMaterialInstance.HasProperty("_Color")) crestMaterialInstance.SetColor("_Color", crestColor);
            }
            if (rimMaterialInstance != null)
            {
                rimMaterialInstance.SetFloat("_RimIntensity", rimIntensity);
            }
            if (capturedOriginalScale)
            {
                transform.localScale = originalLocalScale;
                excitementTimer = 0f;
            }
        }
        else
        {
            if (rimMaterialInstance != null)
            {
                rimMaterialInstance.SetFloat("_RimIntensity", rimIntensity * 2.2f);
            }
        }
    }

    /// <summary>
    /// Thú nhún nhảy phấn khích khi người chơi rê chuột trúng vào ô lãnh địa của nó
    /// </summary>
    public void TriggerExtraExcitement()
    {
        if (!capturedOriginalScale)
        {
            originalLocalScale = transform.localScale;
            capturedOriginalScale = true;
        }
        excitementTimer = 0.45f;
    }

    /// <summary>
    /// Đồng bộ hiệu ứng màu sắc của Chấm ngọc trên đầu và Viền sáng theo Cấp sao (1★ - 5★)
    /// </summary>
    public void ApplyRankVisuals(AnimalRank rank, int starLevel, Color rankColor)
    {
        this.crestColor = rankColor;

        // Cấp sao càng cao, kích thước chấm ngọc càng lớn và uy nghi
        float baseScale = starLevel switch
        {
            1 => 0.08f,
            2 => 0.09f,
            3 => 0.10f,
            4 => 0.12f,
            5 => 0.14f,
            _ => 0.10f
        };
        this.crestScale = baseScale;

        if (crestObj != null)
        {
            crestObj.transform.localScale = new Vector3(crestScale * 0.85f, crestScale * 1.25f, crestScale * 0.85f);
        }

        if (crestMaterialInstance != null)
        {
            if (crestMaterialInstance.HasProperty("_BaseColor"))
            {
                crestMaterialInstance.SetColor("_BaseColor", rankColor);
            }
            if (crestMaterialInstance.HasProperty("_Color"))
            {
                crestMaterialInstance.SetColor("_Color", rankColor);
            }
        }

        // Tinh chỉnh viền sáng Rim Glow để tôn thêm vẻ đẹp theo cấp sao
        if (rimMaterialInstance != null)
        {
            Color tintedRim = Color.Lerp(rimColor, rankColor, 0.40f);
            rimMaterialInstance.SetColor("_RimColor", tintedRim);
            if (starLevel >= 4)
            {
                rimMaterialInstance.SetFloat("_RimIntensity", rimIntensity * 1.35f);
            }
        }
    }

    #endregion

    #region 2. Canopy Fade Implementation (Tán cây bán trong suốt)

    /// <summary>
    /// Kiểm tra xem Renderer có thuộc về khối đất lục giác (Hex Tile), nền đất hoặc cầu nối địa hình hay không.
    /// TUYỆT ĐỐI KHÔNG BAO GIỜ áp dụng hiệu ứng làm mờ / trong suốt (Canopy Fade) lên các khối Tile!
    /// </summary>
    public static bool IsTileOrTerrainRenderer(Renderer r)
    {
        if (r == null) return true;

        // 1. Kiểm tra Component HexTileInfo
        if (r.GetComponent<HexTileInfo>() != null || r.GetComponentInParent<HexTileInfo>() != null)
        {
            // Nếu có HexTileInfo, chỉ chấp nhận nếu renderer này nằm sâu trong container thực vật (Habitat_ / Flora_)
            Transform t = r.transform;
            bool insideHabitat = false;
            while (t != null && t.GetComponent<HexTileInfo>() == null)
            {
                string tName = t.name;
                if (tName.StartsWith("Habitat_") || tName.StartsWith("EdgeFlora_") || tName.StartsWith("CornerFlora_") || tName.StartsWith("Prop_"))
                {
                    insideHabitat = true;
                    break;
                }
                t = t.parent;
            }
            if (!insideHabitat) return true; // Đây chính là thân/mesh của khối Tile lục giác!
        }

        // 2. Kiểm tra tên GameObject của renderer và các cấp cha
        string goName = r.gameObject.name;
        if (goName.StartsWith("Hex_") ||
            goName.StartsWith("Tile_") ||
            goName.StartsWith("SeamBridge") ||
            goName.StartsWith("CornerFiller") ||
            goName.StartsWith("Preserve_") ||
            goName.Equals("default", System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 3. Kiểm tra các container địa hình kết nối
        Transform curr = r.transform;
        while (curr != null)
        {
            string cName = curr.name;
            if (cName.StartsWith("SeamBridge") || cName.StartsWith("CornerFiller") || cName == "Biome_Cluster_Connections")
            {
                return true;
            }
            if (cName.StartsWith("Habitat_") || cName.StartsWith("EdgeFlora_") || cName.StartsWith("CornerFlora_"))
            {
                return false; // Nằm trong container thực vật -> Cho phép xét tán cây
            }
            curr = curr.parent;
        }

        return false;
    }

    /// <summary>
    /// Đảm bảo toàn bộ các khối lục giác và nền đất trên map không bao giờ bị dính trạng thái Transparent
    /// </summary>
    public static void RestoreAllTilesToOpaque()
    {
        List<Renderer> toRemove = null;
        foreach (var pair in ActiveFadingTrees)
        {
            if (pair.Key == null || IsTileOrTerrainRenderer(pair.Key))
            {
                if (pair.Value != null && pair.Value.materials != null)
                {
                    RestoreTreeMaterialsToOpaque(pair.Value.materials);
                }
                if (toRemove == null) toRemove = new List<Renderer>();
                toRemove.Add(pair.Key);
            }
        }

        if (toRemove != null)
        {
            for (int i = 0; i < toRemove.Count; i++)
            {
                ActiveFadingTrees.Remove(toRemove[i]);
            }
        }
    }

    private void RefreshCandidateRenderers()
    {
        candidateRenderers.Clear();

        if (cachedWorldGen == null)
        {
            cachedWorldGen = FindAnyObjectByType<HexWorldGenerator>();
        }

        Vector3 myPos = transform.position;
        float searchRadius = canopyCheckRadius + 1.8f;

        // 1. Quét cây từ các ô lục giác lân cận (ô hiện tại + 6 ô hàng xóm)
        // CHỈ quét bên trong container Habitat_ (chứa cây cối), tuyệt đối KHÔNG quét thân khối Hex Tile!
        if (cachedWorldGen != null && cachedWorldGen.MapTiles != null)
        {
            HexCoordinates myHex = HexMetrics.WorldToHex(myPos);
            List<HexCoordinates> hexesToCheck = new List<HexCoordinates>(7) { myHex };
            for (int i = 0; i < 6; i++)
            {
                hexesToCheck.Add(myHex.GetNeighbor(i));
            }

            for (int h = 0; h < hexesToCheck.Count; h++)
            {
                if (cachedWorldGen.MapTiles.TryGetValue(hexesToCheck[h], out GameObject tileObj) && tileObj != null)
                {
                    for (int c = 0; c < tileObj.transform.childCount; c++)
                    {
                        Transform child = tileObj.transform.GetChild(c);
                        if (child == null || !child.name.StartsWith("Habitat_")) continue;

                        Renderer[] rends = child.GetComponentsInChildren<Renderer>(false);
                        for (int r = 0; r < rends.Length; r++)
                        {
                            Renderer rend = rends[r];
                            if (rend == null || rend.transform.IsChildOf(transform) || IsTileOrTerrainRenderer(rend)) continue;
                            if (Vector3.Distance(rend.bounds.center, myPos) <= searchRadius)
                            {
                                if (!candidateRenderers.Contains(rend))
                                {
                                    candidateRenderers.Add(rend);
                                }
                            }
                        }
                    }
                }
            }
        }

        // 2. Quét cây kết nối giữa các ô (Connections Container) - Bỏ qua SeamBridge và CornerFiller
        GameObject connObj = GameObject.Find("Biome_Cluster_Connections");
        if (connObj != null)
        {
            for (int c = 0; c < connObj.transform.childCount; c++)
            {
                Transform child = connObj.transform.GetChild(c);
                if (child == null) continue;
                if (!child.name.StartsWith("EdgeFlora") && !child.name.StartsWith("CornerFlora")) continue;

                Renderer[] connRends = child.GetComponentsInChildren<Renderer>(false);
                for (int r = 0; r < connRends.Length; r++)
                {
                    Renderer rend = connRends[r];
                    if (rend == null || IsTileOrTerrainRenderer(rend)) continue;
                    if (Vector3.Distance(rend.bounds.center, myPos) <= searchRadius)
                    {
                        if (!candidateRenderers.Contains(rend))
                        {
                            candidateRenderers.Add(rend);
                        }
                    }
                }
            }
        }

        // 3. Fallback: Nếu không tìm thấy qua generator, quét từ GameObject cha (loại trừ Tile)
        if (candidateRenderers.Count == 0 && transform.parent != null)
        {
            Renderer[] pRends = transform.parent.GetComponentsInChildren<Renderer>(false);
            for (int r = 0; r < pRends.Length; r++)
            {
                Renderer rend = pRends[r];
                if (rend == null || rend.transform.IsChildOf(transform) || IsTileOrTerrainRenderer(rend)) continue;
                if (Vector3.Distance(rend.bounds.center, myPos) <= searchRadius)
                {
                    candidateRenderers.Add(rend);
                }
            }
        }
    }

    private void UpdateCanopyOcclusion()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        // Cập nhật lại danh sách cây lân cận mỗi 0.3s
        if (Time.time >= nextCandidateScanTime || candidateRenderers.Count == 0)
        {
            nextCandidateScanTime = Time.time + 0.3f;
            RefreshCandidateRenderers();
        }

        Vector3 animalHead = transform.position + Vector3.up * detectedModelHeight;
        Vector3 camPos = mainCamera.transform.position;
        Vector3 toCam = (camPos - animalHead).normalized;
        float camDist = Vector3.Distance(camPos, animalHead);
        Ray toCamRay = new Ray(animalHead, toCam);

        HashSet<Renderer> newlyOccludingTrees = new HashSet<Renderer>();

        // Kiểm tra xem Renderer nào là tán cây che khuất con thú (TUYỆT ĐỐI BỎ QUA TILE)
        for (int i = 0; i < candidateRenderers.Count; i++)
        {
            Renderer r = candidateRenderers[i];
            if (r == null || r.transform.IsChildOf(transform) || IsTileOrTerrainRenderer(r)) continue;

            Bounds b = r.bounds;

            // Chỉ xét những vật thể đủ cao (cây, bụi cây cao) chứ không xét cỏ sát mặt đất
            if (b.size.y < 0.45f || b.max.y < animalHead.y - 0.05f) continue;

            // 1. Kiểm tra thú có đang đứng bên dưới tán lá không (Directly Under Canopy)
            float dx = animalHead.x - b.center.x;
            float dz = animalHead.z - b.center.z;
            float xzDistSqr = dx * dx + dz * dz;
            float canopyRadius = Mathf.Max(b.extents.x, b.extents.z) * 1.25f;
            bool isUnder = (xzDistSqr <= canopyRadius * canopyRadius) && (b.max.y > animalHead.y + 0.1f);

            // 2. Kiểm tra tán lá có đang chắn giữa Camera và Con thú không (Camera Sightline Block)
            bool blocksRay = b.IntersectRay(toCamRay, out float hitDist) && (hitDist > 0.05f && hitDist < camDist - 0.15f);

            if (isUnder || blocksRay)
            {
                newlyOccludingTrees.Add(r);
            }
        }

        // Cập nhật trạng thái vào bộ nhớ tập trung
        // A. Thêm các cây mới bị che
        foreach (Renderer r in newlyOccludingTrees)
        {
            if (!myCurrentlyOccludingTrees.Contains(r))
            {
                myCurrentlyOccludingTrees.Add(r);
                RegisterTreeOcclusion(r, this);
            }
        }

        // B. Bỏ các cây không còn che nữa
        List<Renderer> toRemove = null;
        foreach (Renderer r in myCurrentlyOccludingTrees)
        {
            if (!newlyOccludingTrees.Contains(r))
            {
                if (toRemove == null) toRemove = new List<Renderer>();
                toRemove.Add(r);
                UnregisterTreeOcclusion(r, this);
            }
        }
        if (toRemove != null)
        {
            for (int i = 0; i < toRemove.Count; i++)
            {
                myCurrentlyOccludingTrees.Remove(toRemove[i]);
            }
        }

        // Cập nhật độ mờ dần (Alpha Lerp) cho toàn bộ các cây đang hoạt động
        UpdateActiveTreeFading();
    }

    private static void RegisterTreeOcclusion(Renderer r, AnimalVisualEnhancer animal)
    {
        if (r == null || IsTileOrTerrainRenderer(r)) return;
        if (!ActiveFadingTrees.TryGetValue(r, out TreeFadeInfo info))
        {
            info = new TreeFadeInfo
            {
                renderer = r,
                materials = r.materials, // Runtime clone instance
                currentAlpha = 1.0f
            };
            ActiveFadingTrees.Add(r, info);
        }
        info.occludingAnimals.Add(animal);
    }

    private static void UnregisterTreeOcclusion(Renderer r, AnimalVisualEnhancer animal)
    {
        if (r != null && ActiveFadingTrees.TryGetValue(r, out TreeFadeInfo info))
        {
            info.occludingAnimals.Remove(animal);
        }
    }

    private void UpdateActiveTreeFading()
    {
        List<Renderer> finishedTrees = null;

        foreach (var pair in ActiveFadingTrees)
        {
            Renderer r = pair.Key;
            TreeFadeInfo info = pair.Value;

            if (r == null || IsTileOrTerrainRenderer(r))
            {
                if (info != null && info.materials != null)
                {
                    RestoreTreeMaterialsToOpaque(info.materials);
                }
                if (finishedTrees == null) finishedTrees = new List<Renderer>();
                finishedTrees.Add(r);
                continue;
            }

            // Mục tiêu độ mờ: 35% nếu có thú ở dưới, 100% (đặc) nếu không còn thú
            float targetAlpha = (info.occludingAnimals.Count > 0) ? fadedCanopyAlpha : 1.0f;
            info.currentAlpha = Mathf.MoveTowards(info.currentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);

            ApplyAlphaToTreeMaterials(info.materials, info.currentAlpha);

            // Nếu đã phục hồi 100% về đặc và không có thú -> gỡ bỏ khỏi danh sách theo dõi
            if (info.occludingAnimals.Count == 0 && info.currentAlpha >= 0.999f)
            {
                RestoreTreeMaterialsToOpaque(info.materials);
                if (finishedTrees == null) finishedTrees = new List<Renderer>();
                finishedTrees.Add(r);
            }
        }

        if (finishedTrees != null)
        {
            for (int i = 0; i < finishedTrees.Count; i++)
            {
                ActiveFadingTrees.Remove(finishedTrees[i]);
            }
        }
    }

    private static void ApplyAlphaToTreeMaterials(Material[] mats, float alpha)
    {
        if (mats == null) return;

        bool isTransparent = alpha < 0.99f;
        for (int i = 0; i < mats.Length; i++)
        {
            Material m = mats[i];
            if (m == null) continue;

            if (isTransparent)
            {
                // Chuyển sang Transparent mode trong URP Lit
                m.SetFloat("_Surface", 1f); // Transparent
                m.SetFloat("_Blend", 0f); // Alpha blend
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            if (m.HasProperty("_BaseColor"))
            {
                Color c = m.GetColor("_BaseColor");
                c.a = alpha;
                m.SetColor("_BaseColor", c);
            }
            if (m.HasProperty("_Color"))
            {
                Color c = m.GetColor("_Color");
                c.a = alpha;
                m.SetColor("_Color", c);
            }
        }
    }

    private static void RestoreTreeMaterialsToOpaque(Material[] mats)
    {
        if (mats == null) return;

        for (int i = 0; i < mats.Length; i++)
        {
            Material m = mats[i];
            if (m == null) continue;

            // Chuyển lại về Opaque mode chuẩn của URP Lit
            m.SetFloat("_Surface", 0f); // Opaque
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            m.SetInt("_ZWrite", 1);
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Opaque");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;

            if (m.HasProperty("_BaseColor"))
            {
                Color c = m.GetColor("_BaseColor");
                c.a = 1.0f;
                m.SetColor("_BaseColor", c);
            }
            if (m.HasProperty("_Color"))
            {
                Color c = m.GetColor("_Color");
                c.a = 1.0f;
                m.SetColor("_Color", c);
            }
        }
    }

    #endregion

    #region 3. Rim Glow Overlay Implementation

    private void ApplyRimGlowOverlay()
    {
        Shader rimShader = Shader.Find("Custom/AnimalRimGlow");
        if (rimShader == null)
        {
            Debug.LogWarning("[AnimalVisualEnhancer] Không tìm thấy Shader Custom/AnimalRimGlow!");
            return;
        }

        rimMaterialInstance = new Material(rimShader);
        rimMaterialInstance.name = "Runtime_AnimalRimGlow_Instance";
        rimMaterialInstance.SetColor("_RimColor", rimColor);
        rimMaterialInstance.SetFloat("_RimPower", rimPower);
        rimMaterialInstance.SetFloat("_RimIntensity", rimIntensity);

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            if (crestObj != null && r.transform.IsChildOf(crestObj.transform)) continue;

            // Thêm material Rim vào slot bổ sung lúc runtime (Không lưu vào asset gốc)
            Material[] origMats = r.materials;
            Material[] newMats = new Material[origMats.Length + 1];
            for (int i = 0; i < origMats.Length; i++)
            {
                newMats[i] = origMats[i];
            }
            newMats[origMats.Length] = rimMaterialInstance;
            r.materials = newMats;

            affectedRenderers.Add(r);
        }
    }

    #endregion

    private void OnDisable()
    {
        CleanupOcclusion();
    }

    private void OnDestroy()
    {
        CleanupOcclusion();

        if (crestObj != null)
        {
            Destroy(crestObj);
        }
        if (crestMaterialInstance != null)
        {
            Destroy(crestMaterialInstance);
        }

        if (rimMaterialInstance != null)
        {
            Destroy(rimMaterialInstance);
        }
    }

    private void CleanupOcclusion()
    {
        foreach (Renderer r in myCurrentlyOccludingTrees)
        {
            UnregisterTreeOcclusion(r, this);
        }
        myCurrentlyOccludingTrees.Clear();
    }
}
