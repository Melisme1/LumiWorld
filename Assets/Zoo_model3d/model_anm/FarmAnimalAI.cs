using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Trạng thái hoạt động của thú nuôi trong LumiWorld
/// </summary>
public enum AnimalState 
{ 
    WanderRandom,       // Đi dạo tự do & phát animation sẵn có trong môi trường
    MeetUp,             // Đi lại gần nhau khi phát hiện đối tác hợp lệ và đối mặt
    CircleDance20s,     // Đi/bơi vòng tròn theo chiều kim đồng hồ quanh nhau đúng 20 giây
    DoScriptedAnim,     // Diễn hành động tương tác theo kịch bản & kích hoạt Buff Gameplay
    Cooldown            // Thời gian nghỉ để tách nhau ra, tránh lặp lại liên tục
}

/// <summary>
/// 4 Dạng Môi Trường (Biome) trong LumiWorld
/// </summary>
public enum BiomeType 
{ 
    Mountain,       // Vùng Núi Cao
    Forest,         // Vùng Rừng Rậm
    FlowerValley,   // Vùng Thung Lũng Hoa
    Water           // Vùng Nước / Đầm Lầy
}

/// <summary>
/// AI điều khiển thú nuôi 3D trong LumiWorld:
/// - Mỗi loài thuộc 1 môi trường (Biome) riêng biệt.
/// - Khi đặt 2 ô cùng môi trường cạnh nhau, thú có thể đi qua lại giữa các ô của nhau nhưng KHÔNG BAO GIỜ qua ô khác môi trường.
/// - Khi chạm mặt nhau, thực hiện động tác đi vòng tròn (thú trên cạn) hoặc bơi vòng tròn (thú dưới nước) theo chiều kim đồng hồ quanh nhau trong đúng 20 giây.
/// - Kết thúc 20 giây: Diễn hành động đặc trưng theo kịch bản và kích hoạt Buff Gameplay (tăng coin, giảm thời gian hồi, v.v.).
/// - Khi bình thường: Tự do đi dạo và diễn các animation phong phú có sẵn trong model.
/// </summary>
[RequireComponent(typeof(Animation))]
public class FarmAnimalAI : MonoBehaviour
{
    [Header("================ 1. CẤU HÌNH LOÀI & BIOME ================")]
    [Tooltip("Mã định danh của loài (vd: chimvet7mau_anm, canoc_anm, bo_anm...)")]
    public string animalID = "";

    [Tooltip("Môi trường sinh sống của loài thú")]
    public BiomeType biome = BiomeType.Mountain;

    [Tooltip("Là loài sống dưới nước (bơi thay vì đi bộ)")]
    public bool isAquatic = false;

    [Header("================ 1.1 THÔNG SỐ BAY (FLYING SPECIES) ================")]
    [Tooltip("Là loài bay trên không (chuồn chuồn, v.v.)")]
    public bool isFlying = false;

    [Tooltip("Độ cao bay lơ lửng trên mặt ô lục giác (mét)")]
    public float flightAltitude = 1.35f;

    [Tooltip("Biên độ nhấp nhô bồng bềnh khi bay (mét)")]
    public float flightBobbingAmplitude = 0.08f;

    [Tooltip("Tần số nhịp đập cánh dập dềnh (Hz)")]
    public float flightBobbingFrequency = 3.2f;

    /// <summary>
    /// Tính toán độ cao Y mục tiêu (kèm nhấp nhô bồng bềnh nếu là loài bay)
    /// </summary>
    public float GetCurrentTargetY(float surfaceY)
    {
        if (isFlying)
        {
            float bobbing = Mathf.Sin(Time.time * flightBobbingFrequency) * flightBobbingAmplitude;
            return surfaceY + flightAltitude + bobbing;
        }
        return surfaceY + yOffset;
    }

    [Header("================ 2. ANIMATION CLIPS ================")]
    [Tooltip("Tên clip di chuyển chính (Walk, Glide, Flap, Swim_Horizontal...)")]
    public string moveAnimName = "Walk";

    [Tooltip("Tên clip đứng yên cơ bản (Idle, Rest_Pose...)")]
    public string idleAnimName = "Idle";

    [Header("================ 3. TAGS Ô MÔI TRƯỜNG ================")]
    public string mountainTileTag = "MountainTile";
    public string forestTileTag = "ForestTile";
    public string flowerTileTag = "FlowerTile";
    public string waterTileTag = "WaterTile";

    [Header("================ 4. THÔNG SỐ DI CHUYỂN ================")]
    [Tooltip("Tốc độ di chuyển cơ bản (m/s)")]
    public float moveSpeed = 0.75f;

    [Tooltip("Bán kính an toàn dạo chơi trong lòng ô lục giác (0.40m - 0.50m để không đi ra sát mép)")]
    public float innerHexRadius = 0.48f;

    [Tooltip("Tỉ lệ % thú đi lại trong chính ô hiện tại thay vì bước sang ô láng giềng (0.7 = 70%)")]
    [Range(0f, 1f)]
    public float stayInCurrentHexChance = 0.70f;

    [Tooltip("Bán kính vòng tròn khi tương tác 20s")]
    public float circleRadius = 0.45f;

    [Tooltip("Thời gian kết nối xoay vòng tròn")]
    public float connectionDuration = 20.0f;

    [Tooltip("Độ cao nâng Y để chân/bụng chạm mặt ô lục giác")]
    public float yOffset = 0.05f;

    [Header("================ 5. TRẠNG THÁI HIỆN TẠI ================")]
    public AnimalState currentState = AnimalState.WanderRandom;
    public List<FarmAnimalAI> groupPartners = new List<FarmAnimalAI>();

    [Header("================ 6. THỜI GIAN HỒI CONNECTION (COOLDOWN) ================")]
    [Tooltip("Bật chế độ test nhanh (15s cho 2 thú, 35s cho 3 thú) để kiểm tra animation")]
    public bool fastCooldownForTesting = false;

    [Tooltip("Đang trong thời gian chờ hồi connection")]
    public bool isConnectionOnCooldown = false;

    [Tooltip("Thời gian chờ hồi connection còn lại (giây)")]
    public float cooldownRemainingSeconds = 0f;

    [Tooltip("Thời gian chờ hồi hiển thị (Giờ:Phút:Giây)")]
    public string formattedCooldownRemaining = "Sẵn sàng (Ready)";

    // Quản lý tĩnh danh sách thú trong Scene
    private static readonly List<FarmAnimalAI> allAnimals = new List<FarmAnimalAI>();

    private Animation anim;
    private HexWorldGenerator worldGen;
    private AnimalMovementAI movementAI;
    private readonly List<string> availableMoveClips = new List<string>();
    private readonly List<string> availableIdleClips = new List<string>();

    // Tọa độ góc cho vòng tròn xoay 20s
    private float circleAngle = 0f;

    void OnEnable()
    {
        if (!allAnimals.Contains(this)) allAnimals.Add(this);
    }

    void OnDisable()
    {
        allAnimals.Remove(this);
    }

    void Awake()
    {
        anim = GetComponent<Animation>();
        worldGen = FindAnyObjectByType<HexWorldGenerator>();
        movementAI = GetComponent<AnimalMovementAI>();
        AutoDetectSpeciesAndBiome();

        // Đồng bộ thuộc tính bay từ AnimalMovementAI nếu có
        if (movementAI != null && movementAI.LocomotionType == AnimalLocomotionType.Flying)
        {
            isFlying = true;
            if (movementAI.FlightAltitude > 0.1f)
            {
                flightAltitude = movementAI.FlightAltitude;
            }
        }
    }

    void Start()
    {
        CacheAndClassifyAnimations();
        SnapToTileSurface();
        StartCoroutine(MainAILoop());
    }

    void Update()
    {
        // Đếm ngược thời gian hồi connection
        if (cooldownRemainingSeconds > 0f)
        {
            cooldownRemainingSeconds -= Time.deltaTime;
            isConnectionOnCooldown = true;

            int hours = (int)(cooldownRemainingSeconds / 3600);
            int mins = (int)((cooldownRemainingSeconds % 3600) / 60);
            int secs = (int)(cooldownRemainingSeconds % 60);

            if (hours > 0)
                formattedCooldownRemaining = $"{hours}h {mins:D2}m {secs:D2}s";
            else
                formattedCooldownRemaining = $"{mins:D2}m {secs:D2}s";

            if (cooldownRemainingSeconds <= 0f)
            {
                cooldownRemainingSeconds = 0f;
                isConnectionOnCooldown = false;
                formattedCooldownRemaining = "Sẵn sàng (Ready)";
                Debug.Log($"<color=#34D399><b>[LumiWorld Connection]</b> ✨ {animalID} ({biome}) đã hồi xong thời gian chờ! Sẵn sàng kết nối khi chạm mặt.</color>");
            }
        }
    }

    // =========================================================================
    // TỰ ĐỘNG NHẬN DIỆN LOÀI & BIOME DỰA TRÊN TÊN PREFAB / GAMEOBJECT
    // =========================================================================
    void AutoDetectSpeciesAndBiome()
    {
        string objName = gameObject.name.ToLower().Replace("(clone)", "").Trim();

        if (string.IsNullOrEmpty(animalID))
        {
            animalID = objName;
        }

        // 1. Vùng Núi Cao (Mountain Biome)
        if (objName.Contains("chimvet") || objName.Contains("vet"))
        {
            animalID = "chimvet7mau_anm";
            biome = BiomeType.Mountain;
            isAquatic = false;
        }
        else if (objName.Contains("chimcong") || objName.Contains("cong"))
        {
            animalID = "chimcong_anm";
            biome = BiomeType.Mountain;
            isAquatic = false;
        }
        else if (objName.Contains("chuongchuong") || objName.Contains("chuonchuon"))
        {
            animalID = "chuongchuong_anm";
            biome = BiomeType.Mountain;
            isAquatic = false;
            isFlying = true;
            if (flightAltitude <= 0.1f) flightAltitude = 1.35f;
            moveAnimName = "Flap";
        }
        else if (objName.Contains("chauchau"))
        {
            animalID = "chauchau_anm";
            biome = BiomeType.Mountain;
            isAquatic = false;
        }
        // 2. Vùng Rừng Rậm (Forest Biome)
        else if (objName.Contains("socbay") || objName.Contains("soc"))
        {
            animalID = "socbay_anm";
            biome = BiomeType.Forest;
            isAquatic = false;
        }
        else if (objName.Contains("bolongdai"))
        {
            animalID = "bolongdai_anm";
            biome = BiomeType.Forest;
            isAquatic = false;
        }
        else if (objName.Contains("bo_anm") || objName.StartsWith("bo"))
        {
            animalID = "bo_anm";
            biome = BiomeType.Forest;
            isAquatic = false;
        }
        else if (objName.Contains("nai") || objName.Contains("huu"))
        {
            animalID = "nai_anm";
            biome = BiomeType.Forest;
            isAquatic = false;
        }
        else if (objName.Contains("caoco"))
        {
            animalID = "caoco_anm";
            biome = BiomeType.Forest;
            isAquatic = false;
        }
        else if (objName.Contains("cao"))
        {
            animalID = "cao_anm";
            biome = BiomeType.Forest;
            isAquatic = false;
        }
        else if (objName.Contains("chohoa") || objName.Contains("cho"))
        {
            animalID = "chohoa_anm";
            biome = BiomeType.Forest;
            isAquatic = false;
        }
        // 3. Vùng Thung Lũng Hoa (Flower Valley Biome)
        else if (objName.Contains("saubuom") || objName.Contains("sau"))
        {
            animalID = "saubuom_anm";
            biome = BiomeType.FlowerValley;
            isAquatic = false;
        }
        else if (objName.Contains("echhoa"))
        {
            animalID = "echhoa_anm";
            biome = BiomeType.FlowerValley;
            isAquatic = false;
        }
        else if (objName.Contains("capybara"))
        {
            animalID = "capybara_anm";
            biome = BiomeType.FlowerValley;
            isAquatic = false;
        }
        else if (objName.Contains("raica"))
        {
            animalID = "raica_anm";
            biome = BiomeType.FlowerValley;
            isAquatic = false;
        }
        // 4. Vùng Nước / Đầm Lầy (Water Biome)
        else if (objName.Contains("canoc"))
        {
            animalID = "canoc_anm";
            biome = BiomeType.Water;
            isAquatic = true;
        }
        else if (objName.Contains("echduoica") || objName.Contains("ech_nuoc"))
        {
            animalID = "echduoica_anm";
            biome = BiomeType.Water;
            isAquatic = true;
        }
        else if (objName.Contains("thiennga"))
        {
            animalID = "thiennga_anm";
            biome = BiomeType.Water;
            isAquatic = true;
        }
    }

    // =========================================================================
    // QUÉT VÀ PHÂN LOẠI ANIMATION SẴN CÓ TRONG MODEL
    // =========================================================================
    void CacheAndClassifyAnimations()
    {
        availableMoveClips.Clear();
        availableIdleClips.Clear();

        if (anim == null) return;

        foreach (AnimationState state in anim)
        {
            string clipName = state.name;

            if (clipName.Contains("Walk") || clipName.Contains("Run") || clipName.Contains("Trot") ||
                clipName.Contains("Swim") || clipName.Contains("Glide") || clipName.Contains("Flap") ||
                clipName.Contains("Fly") || clipName.Contains("Fetch"))
            {
                availableMoveClips.Add(clipName);
            }
            else
            {
                availableIdleClips.Add(clipName);
            }
        }

        // Ưu tiên chọn clip di chuyển chuẩn xác
        if (isAquatic)
        {
            if (HasAnim("Swim_Horizontal")) moveAnimName = "Swim_Horizontal";
            else if (HasAnim("Swim")) moveAnimName = "Swim";
            else if (availableMoveClips.Count > 0) moveAnimName = availableMoveClips[0];
        }
        else
        {
            if (animalID == "chuongchuong_anm")
            {
                if (HasAnim("Flap")) moveAnimName = "Flap";
                else if (HasAnim("Glide")) moveAnimName = "Glide";
                if (HasAnim("Flap")) idleAnimName = "Flap";
            }
            else if (animalID == "chauchau_anm")
            {
                if (HasAnim("Flap")) moveAnimName = "Flap";
                else if (HasAnim("Glide")) moveAnimName = "Glide";
            }
            else if (animalID == "chimvet7mau_anm" || animalID == "chimcong_anm")
            {
                if (HasAnim("Walk")) moveAnimName = "Walk";
                else if (HasAnim("Glide")) moveAnimName = "Glide";
            }
            else
            {
                if (HasAnim("Walk")) moveAnimName = "Walk";
                else if (availableMoveClips.Count > 0) moveAnimName = availableMoveClips[0];
            }
        }

        if (HasAnim("Idle")) idleAnimName = "Idle";
        else if (HasAnim("Rest_Pose")) idleAnimName = "Rest_Pose";
        else if (availableIdleClips.Count > 0) idleAnimName = availableIdleClips[0];
    }

    // =========================================================================
    // VÒNG LẶP CHÍNH CỦA AI
    // =========================================================================
    IEnumerator MainAILoop()
    {
        yield return new WaitForSeconds(Random.Range(0.2f, 0.8f));

        while (true)
        {
            switch (currentState)
            {
                case AnimalState.WanderRandom:
                    if (movementAI != null && movementAI.enabled)
                    {
                        // AnimalMovementAI quản lý toàn bộ việc di chuyển, lượn cánh, cất/hạ cánh và né tránh.
                        // FarmAnimalAI định kỳ quét bạn bè xung quanh để kích hoạt kết nối khi gặp nhau.
                        CheckForConnectionPartners();
                        yield return new WaitForSeconds(Random.Range(0.8f, 1.2f));
                    }
                    else
                    {
                        yield return StartCoroutine(DoWanderAndIdleRoutine());
                        CheckForConnectionPartners();
                    }
                    break;

                case AnimalState.MeetUp:
                    yield return StartCoroutine(DoMeetUpRoutine());
                    break;

                case AnimalState.CircleDance20s:
                    yield return StartCoroutine(DoClockwiseCircleDance20sRoutine());
                    break;

                case AnimalState.DoScriptedAnim:
                    yield return StartCoroutine(DoScriptedComboAndBuffRoutine());
                    break;

                case AnimalState.Cooldown:
                    // Tản ra xa nhau, diễn animation cơ bản và trở lại tự do trong khi thời gian hồi đếm ngược
                    yield return StartCoroutine(DoDisperseAndScatterRoutine());
                    break;
            }

            yield return new WaitForSeconds(0.2f);
        }
    }

    // =========================================================================
    // 1. DI CHUYỂN TỰ DO & DIỄN ANIMATION CÓ SẴN (WANDER RANDOM)
    // =========================================================================
    IEnumerator DoWanderAndIdleRoutine()
    {
        // 60% tỉ lệ di chuyển, 40% tỉ lệ đứng yên diễn animation
        if (Random.value < 0.60f)
        {
            Vector3 targetPos = GetRandomSafeBiomePoint();

            // Nếu tìm thấy điểm đến hợp lệ và cách xa vị trí hiện tại > 0.3m
            if (Vector3.Distance(FlatVector(transform.position), FlatVector(targetPos)) > 0.35f)
            {
                string animToUse = moveAnimName;
                if (availableMoveClips.Count > 0 && Random.value < 0.35f)
                {
                    animToUse = availableMoveClips[Random.Range(0, availableMoveClips.Count)];
                }

                yield return StartCoroutine(MoveSafelyToTarget(targetPos, animToUse, Random.Range(3.0f, 5.5f)));
            }
            else
            {
                PlayAnimIfExist(idleAnimName);
                yield return new WaitForSeconds(Random.Range(1.5f, 3.0f));
            }
        }
        else
        {
            // Chọn ngẫu nhiên 1 animation đứng yên có sẵn trong model (Ăn, ngồi, hú, nghỉ...)
            string chosenIdle = idleAnimName;
            if (availableIdleClips.Count > 0)
            {
                chosenIdle = availableIdleClips[Random.Range(0, availableIdleClips.Count)];
            }

            PlayAnimIfExist(chosenIdle);
            yield return new WaitForSeconds(Random.Range(2.5f, 4.5f));
        }
    }

    // =========================================================================
    // 2. DI CHUYỂN AN TOÀN - CHỈ TRONG MÔI TRƯỜNG CỦA MÌNH & CÁC Ô CÙNG LOẠI
    // =========================================================================

    // =========================================================================
    // 2. DI CHUYỂN AN TOÀN - CHỈ TRONG Ô ĐÃ ĐẶT TÀI NGUYÊN MÔI TRƯỜNG
    // =========================================================================

    /// <summary>
    /// Kiểm tra xem một ô lục giác ĐÃ ĐƯỢC ĐẶT TÀI NGUYÊN MÔI TRƯỜNG chưa.
    /// Ô đất trống (chưa đặt bài, hoặc đất thô Arid) -> TUYỆT ĐỐI FALSE!
    /// </summary>
    public bool HasHabitatResourceOnTile(GameObject tileObj, out CardData habitatCard)
    {
        habitatCard = null;
        if (tileObj == null) return false;

        string tName = tileObj.name.ToLower();
        // Ô đất khô cằn / thô (Arid) chưa đặt tài nguyên -> Không cho phép!
        if (tName.Contains("arid")) return false;

        // 1. Kiểm tra PlacedCard loại Terrain có Habitat Props / tài nguyên
        PlacedCard card = HexBiomeClusterConnector.GetHabitatCardOnTile(tileObj);
        if (card == null)
        {
            card = tileObj.GetComponentInChildren<PlacedCard>();
        }

        if (card != null && card.cardData != null && card.cardData.HasHabitatProps())
        {
            habitatCard = card.cardData;
            return true;
        }

        // 2. Kiểm tra container sinh thái "Habitat_" (chỉ sinh ra khi đã đặt tài nguyên lên ô)
        for (int i = 0; i < tileObj.transform.childCount; i++)
        {
            Transform child = tileObj.transform.GetChild(i);
            if (child.name.StartsWith("Habitat_"))
            {
                if (card != null && card.cardData != null)
                {
                    habitatCard = card.cardData;
                }
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Kiểm tra vị trí có nằm trên ô môi trường hợp lệ hay không.
    /// QUY TẮC CỐT LÕI:
    /// - CHỈ NHỮNG Ô ĐÃ ĐẶT TÀI NGUYÊN XUỐNG thì mới được coi là môi trường!
    /// - Các ô đất trống (mặc dù cùng tầng hoặc khác tầng) TUYỆT ĐỐI KHÔNG ĐƯỢC DI CHUYỂN VÀO!
    /// </summary>
    public bool IsPointOnMyEnvironmentTile(Vector3 point, out float surfaceY)
    {
        surfaceY = transform.position.y;

        // Bắn Raycast từ Y = 15.0f xuống thẳng đứng
        Vector3 rayStart = new Vector3(point.x, 15.0f, point.z);

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 30.0f))
        {
            GameObject hitObj = hit.collider.gameObject;
            string hitName = hitObj.name.ToLower();

            // 1. NẾU LÀ Ô ĐẤT TRỐNG / ARID -> CHẶN NGAY LẬP TỨC 100%!
            if (hitName.Contains("arid"))
            {
                return false;
            }

            // 2. Xác định GameObject của khối lục giác gốc
            GameObject tileObj = hitObj;
            if (hit.collider.transform.parent != null && hit.collider.transform.parent.name.StartsWith("Hex_"))
            {
                tileObj = hit.collider.transform.parent.gameObject;
            }

            if (worldGen != null && worldGen.MapTiles != null)
            {
                HexCoordinates hexAtPoint = HexMetrics.WorldToHex(point);
                if (worldGen.MapTiles.TryGetValue(hexAtPoint, out GameObject mapTile) && mapTile != null)
                {
                    tileObj = mapTile;
                }
            }

            // 3. BẮT BUỘC Ô NÀY PHẢI ĐÃ ĐẶT TÀI NGUYÊN MÔI TRƯỜNG!
            if (HasHabitatResourceOnTile(tileObj, out CardData habitatCard))
            {
                // Kiểm tra tài nguyên có khớp với Biome của con thú không
                if (IsCardMatchingBiome(habitatCard))
                {
                    surfaceY = hit.point.y;
                    return true;
                }
                else
                {
                    // Ô này có tài nguyên nhưng là KHÁC MÔI TRƯỜNG -> Chặn lại ngay!
                    return false;
                }
            }

            // 4. Kiểm tra Tag nếu Designer có gán Tag riêng cho ô tài nguyên
            string requiredTag = GetTagForCurrentBiome();
            if (SafeCompareTag(hitObj, requiredTag))
            {
                surfaceY = hit.point.y;
                return true;
            }

            // 5. Đối với Water Biome: chấp nhận mặt nước nếu có collider nước
            if (biome == BiomeType.Water && (hitName.Contains("water") || hitName.Contains("ocean") || hitName.Contains("river")))
            {
                surfaceY = hit.point.y;
                return true;
            }
        }

        // Mọi ô đất trống chưa đặt tài nguyên -> TUYỆT ĐỐI TỪ CHỐI!
        return false;
    }

    bool SafeCompareTag(GameObject obj, string tagToCheck)
    {
        if (obj == null || string.IsNullOrEmpty(tagToCheck)) return false;
        try
        {
            return obj.CompareTag(tagToCheck);
        }
        catch
        {
            return false;
        }
    }

    bool MatchesBiomeKeywords(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        switch (biome)
        {
            case BiomeType.Mountain:
                // Windheath là thẻ tài nguyên núi cao đá xám
                return name.Contains("windheath") || name.Contains("mountain") || name.Contains("highland");

            case BiomeType.Forest:
                // Leafwood là thẻ tài nguyên rừng rậm cây xanh
                return name.Contains("leafwood") || name.Contains("forest");

            case BiomeType.FlowerValley:
                // Bloomfield là thẻ tài nguyên thung lũng hoa tím
                return name.Contains("bloomfield") || name.Contains("flower");

            case BiomeType.Water:
                return name.Contains("water") || name.Contains("ocean") || name.Contains("damlay") || name.Contains("river") || name.Contains("lake");

            default:
                return false;
        }
    }

    bool IsCardMatchingBiome(CardData cardData)
    {
        if (cardData == null) return false;
        string cName = (cardData.cardName + " " + cardData.name + " " + cardData.cardID).ToLower();
        return MatchesBiomeKeywords(cName);
    }

    /// <summary>
    /// Tìm ô đã đặt tài nguyên cùng Biome gần nhất trong Scene
    /// </summary>
    Vector3 FindNearestValidHabitatTile(Vector3 fromPos)
    {
        if (worldGen == null || worldGen.MapTiles == null) return Vector3.zero;

        Vector3 bestPos = Vector3.zero;
        float minDist = float.MaxValue;

        foreach (var pair in worldGen.MapTiles)
        {
            GameObject tObj = pair.Value;
            if (tObj == null) continue;

            if (HasHabitatResourceOnTile(tObj, out CardData card) && IsCardMatchingBiome(card))
            {
                float d = Vector3.Distance(FlatVector(fromPos), FlatVector(tObj.transform.position));
                if (d < minDist)
                {
                    minDist = d;
                    var (topY, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(tObj);
                    bestPos = HexMetrics.HexToWorldPosition(pair.Key, topY);
                }
            }
        }

        return bestPos;
    }

    /// <summary>
    /// Tìm điểm an toàn trong lòng ô hiện tại hoặc bước sang ô láng giềng CÙNG MÔI TRƯỜNG.
    /// Giới hạn bán kính nhỏ gọn (innerHexRadius = 0.48m) để thú không bao giờ chạm mép vách đá.
    /// </summary>
    Vector3 GetRandomSafeBiomePoint()
    {
        HexCoordinates currentHex = HexMetrics.WorldToHex(transform.position);
        Vector3 currentHexCenter = HexMetrics.HexToWorldPosition(currentHex, transform.position.y);

        // Kiểm tra xem chính ô hiện tại thú đang đứng CÓ PHẢI LÀ Ô ĐÃ ĐẶT TÀI NGUYÊN KHÔNG?
        bool isCurrentOnValidTile = IsPointOnMyEnvironmentTile(currentHexCenter, out float curSurfaceY);

        // NẾU THÚ ĐANG LỠ ĐỨNG Ở Ô ĐẤT TRỐNG (ví dụ do spawn hoặc đặt thẻ lên đất trống):
        if (!isCurrentOnValidTile)
        {
            // Tìm ô có tài nguyên môi trường gần nhất để quay về đó ngay lập tức!
            Vector3 targetResourceTile = FindNearestValidHabitatTile(transform.position);
            if (targetResourceTile != Vector3.zero)
            {
                return targetResourceTile;
            }

            // Nếu trong game chưa có ô tài nguyên nào được đặt -> Đứng yên tại chỗ, tuyệt đối không đi lung tung!
            return transform.position;
        }

        // KHI THÚ ĐANG Ở TRONG Ô TÀI NGUYÊN HỢP LỆ:
        // Trường hợp 1: Ưu tiên 70% dạo quanh trong lòng ô hiện tại (an toàn 100%, không ra mép)
        bool wanderInCurrent = (Random.value < stayInCurrentHexChance);

        if (!wanderInCurrent)
        {
            // Trường hợp 2: Thử bước sang ô láng giềng ĐÃ ĐẶT TÀI NGUYÊN CÙNG LOẠI
            List<HexCoordinates> validNeighborHexes = new List<HexCoordinates>();

            for (int dir = 0; dir < 6; dir++)
            {
                HexCoordinates nHex = currentHex.GetNeighbor(dir);
                Vector3 nCenter = HexMetrics.HexToWorldPosition(nHex, curSurfaceY);

                // Ô láng giềng BẮT BUỘC phải là ô đã đặt tài nguyên cùng Biome!
                if (IsPointOnMyEnvironmentTile(nCenter, out float nSurfaceY))
                {
                    // Chênh lệch độ cao giữa 2 ô không được quá lớn (không nhảy vực đối với thú đi đất)
                    if (isFlying || Mathf.Abs(nSurfaceY - curSurfaceY) <= 0.35f)
                    {
                        validNeighborHexes.Add(nHex);
                    }
                }
            }

            // Nếu có ô láng giềng cùng tài nguyên môi trường -> Chọn điểm trong lòng ô đó!
            if (validNeighborHexes.Count > 0)
            {
                HexCoordinates chosenNeighbor = validNeighborHexes[Random.Range(0, validNeighborHexes.Count)];
                Vector3 neighborCenter = HexMetrics.HexToWorldPosition(chosenNeighbor, curSurfaceY);

                Vector2 offset = Random.insideUnitCircle * innerHexRadius;
                Vector3 targetInNeighbor = neighborCenter + new Vector3(offset.x, 0, offset.y);

                if (IsPointOnMyEnvironmentTile(targetInNeighbor, out float targetY))
                {
                    targetInNeighbor.y = GetCurrentTargetY(targetY);
                    return targetInNeighbor;
                }
            }
        }

        // Fallback: Dạo quanh tâm ô hiện tại trong bán kính an toàn innerHexRadius (0.45m)
        for (int i = 0; i < 15; i++)
        {
            Vector2 rand2D = Random.insideUnitCircle * innerHexRadius;
            Vector3 candidate = currentHexCenter + new Vector3(rand2D.x, 0, rand2D.y);

            if (IsPointOnMyEnvironmentTile(candidate, out float targetY))
            {
                if (isFlying || Mathf.Abs(targetY - curSurfaceY) <= 0.35f)
                {
                    candidate.y = GetCurrentTargetY(targetY);
                    return candidate;
                }
            }
        }

        return transform.position;
    }

    /// <summary>
    /// Thử bước một bước nhỏ tới vị trí mục tiêu.
    /// Nếu bước tiếp theo chạm sang ô đất trống, ra mép, hoặc nhảy vực -> DỪNG LẠI NGAY LẬP TỨC!
    /// </summary>
    bool TryStepTowards(Vector3 target)
    {
        Vector3 nextPos = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);

        // 1. Kiểm tra xem vị trí bước tiếp theo có nằm trên ô đã đặt tài nguyên hợp lệ không
        if (IsPointOnMyEnvironmentTile(nextPos, out float surfaceY))
        {
            // 2. Chống rơi vách dốc / bậc thang (chỉ áp dụng chặn thú đi bộ, thú bay lượn trên cao tự do)
            if (!isFlying && IsPointOnMyEnvironmentTile(transform.position, out float curSurfaceY))
            {
                if (Mathf.Abs(surfaceY - curSurfaceY) > 0.35f)
                {
                    return false; // Chặn đứng lại ngay trước mép vách nếu đi bộ!
                }
            }

            nextPos.y = GetCurrentTargetY(surfaceY);
            transform.position = nextPos;
            RotateSmoothlyTowards(target);
            return true;
        }

        // Chặn đứng lại ngay lập tức nếu bước ra khỏi ô tài nguyên!
        return false;
    }

    IEnumerator MoveSafelyToTarget(Vector3 target, string animClip, float maxDuration)
    {
        PlayAnimIfExist(animClip);
        float timer = 0f;

        while (Vector3.Distance(FlatVector(transform.position), FlatVector(target)) > 0.20f && timer < maxDuration)
        {
            bool success = TryStepTowards(target);
            if (!success) break; // Gặp ranh giới môi trường khác -> Dừng lại chọn điểm khác

            timer += Time.deltaTime;
            yield return null;
        }
    }

    // =========================================================================
    // 3. PHÁT HIỆN ĐỐI TÁC & KÍCH HOẠT CONNECTION (KHI ĐỐI MẶT NHAU)
    // =========================================================================
    void CheckForConnectionPartners()
    {
        if (currentState != AnimalState.WanderRandom) return;
        // Nếu bản thân đang trong thời gian hồi chiêu -> Tuyệt đối không kích hoạt connection!
        if (isConnectionOnCooldown || cooldownRemainingSeconds > 0f) return;

        List<FarmAnimalAI> nearby = new List<FarmAnimalAI>();
        nearby.Add(this);

        for (int i = 0; i < allAnimals.Count; i++)
        {
            FarmAnimalAI other = allAnimals[i];
            if (other == null || other == this) continue;

            // Bạn đồng hành cũng phải đang tự do và KHÔNG trong thời gian hồi chiêu!
            if (other.currentState == AnimalState.WanderRandom && 
                !other.isConnectionOnCooldown && 
                other.cooldownRemainingSeconds <= 0f && 
                other.biome == this.biome)
            {
                float dist = Vector3.Distance(FlatVector(transform.position), FlatVector(other.transform.position));
                // Khoảng cách trong tầm đối mặt / chạm mặt (<= 2.5m)
                if (dist <= 2.5f)
                {
                    if (!nearby.Contains(other)) nearby.Add(other);
                }
            }
        }

        // Kiểm tra xem danh sách có tạo thành cặp/nhóm tương tác hợp lệ hay không
        if (TryExtractValidCombo(nearby, out List<FarmAnimalAI> matchedGroup))
        {
            // Kiểm tra chắc chắn lại một lần nữa: toàn bộ thành viên trong nhóm không ai bị cooldown
            for (int i = 0; i < matchedGroup.Count; i++)
            {
                var m = matchedGroup[i];
                if (m == null || m.isConnectionOnCooldown || m.cooldownRemainingSeconds > 0f)
                {
                    return;
                }
            }

            foreach (var member in matchedGroup)
            {
                member.groupPartners = new List<FarmAnimalAI>(matchedGroup);
                member.currentState = AnimalState.MeetUp;
                if (member.movementAI != null)
                {
                    member.movementAI.InterruptWandering();
                }
            }
        }
    }

    // =========================================================================
    // 4. TIẾP CẬN & ĐỐI MẶT NHAU (MEET UP)
    // =========================================================================
    IEnumerator DoMeetUpRoutine()
    {
        PlayAnimIfExist(moveAnimName);

        Vector3 centerGroup = Vector3.zero;
        foreach (var p in groupPartners) if (p != null) centerGroup += p.transform.position;
        if (groupPartners.Count > 0) centerGroup /= groupPartners.Count;

        float timeout = 0f;

        // Tiến lại gần tâm nhóm
        while (groupPartners.Count > 0 && Vector3.Distance(FlatVector(transform.position), FlatVector(centerGroup)) > 0.45f && timeout < 3.0f)
        {
            timeout += Time.deltaTime;
            if (!TryStepTowards(centerGroup)) break;
            yield return null;
        }

        // ĐỒNG BỘ GÓC XUẤT PHÁT: DÀN ĐỀU CÁC CON THÚ TRÊN ĐƯỜNG TRÒN
        // (2 con thì cách nhau 180°, 3 con thì cách nhau 120°) để đầu con này nhìn thẳng vào đuôi con kia!
        int myIndex = groupPartners.IndexOf(this);
        if (myIndex < 0) myIndex = 0;

        float baseAngle = 0f;
        if (groupPartners.Count > 0 && groupPartners[0] != null)
        {
            Vector3 diff0 = groupPartners[0].transform.position - centerGroup;
            baseAngle = Mathf.Atan2(diff0.z, diff0.x);
        }

        float angleStep = (Mathf.PI * 2f) / Mathf.Max(1, groupPartners.Count);
        circleAngle = baseAngle + (myIndex * angleStep);

        float actualRadius = Mathf.Clamp(circleRadius, 0.45f, 0.65f);
        Vector3 startCirclePos = centerGroup + new Vector3(Mathf.Cos(circleAngle), 0, Mathf.Sin(circleAngle)) * actualRadius;

        // Xoay hướng mặt ngay lập tức theo cung tròn thuận chiều kim đồng hồ (hướng đuổi bắt)
        Vector3 prepLookTarget = centerGroup + new Vector3(Mathf.Cos(circleAngle - 0.40f), 0, Mathf.Sin(circleAngle - 0.40f)) * actualRadius;
        Vector3 prepLookDir = prepLookTarget - startCirclePos;
        prepLookDir.y = 0;
        if (prepLookDir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(prepLookDir);
        }

        PlayAnimIfExist(idleAnimName);
        yield return new WaitForSeconds(0.4f);

        currentState = AnimalState.CircleDance20s;
    }

    // =========================================================================
    // 5. XOAY VÒNG TRÒN ĐUỔI BẮT (ĐẦU CON NÀY DÍ ĐÍT CON KIA) ĐÚNG 20S
    // =========================================================================
    IEnumerator DoClockwiseCircleDance20sRoutine()
    {
        // Chọn animation di chuyển/chạy đuổi thích hợp nhất
        string danceAnim = GetChasingDanceAnim();
        PlayAnimIfExist(danceAnim);

        float timer = 0f;
        float actualRadius = Mathf.Clamp(circleRadius, 0.45f, 0.65f);

        // Tính tâm cố định của vòng tròn từ lúc bắt đầu (giữ cố định để quỹ đạo tròn không bị méo giật)
        Vector3 center = Vector3.zero;
        int validCount = 0;
        foreach (var p in groupPartners)
        {
            if (p != null)
            {
                center += p.transform.position;
                validCount++;
            }
        }
        if (validCount > 0) center /= validCount;

        // Tốc độ chạy đuổi nhau (nhanh nhẹn, tự nhiên, hào hứng)
        float chaseSpeed = Mathf.Max(moveSpeed * 1.35f, 1.15f);
        // Tốc độ góc đồng bộ hoàn hảo theo công thức v = omega * r => omega = v / r
        // Nhờ vậy bước chân đi thẳng 100%, tuyệt đối không bị trượt ngang (strafing)!
        float angularSpeed = chaseSpeed / actualRadius;

        while (timer < connectionDuration)
        {
            timer += Time.deltaTime;

            // Góc giảm dần theo thời gian = Chạy thuận chiều kim đồng hồ
            circleAngle -= angularSpeed * Time.deltaTime;

            // Vị trí mục tiêu tiếp theo trên đường tròn
            Vector3 circleTarget = center + new Vector3(Mathf.Cos(circleAngle) * actualRadius, 0, Mathf.Sin(circleAngle) * actualRadius);

            // ĐIỂM NHÌN PHÍA TRƯỚC (ĐẦU DÍ ĐÍT CON PHÍA TRƯỚC):
            // Nhìn vào điểm cung tròn phía trước mặt ~25-30 độ (đúng theo hướng chạy đuổi theo đuôi con kia)
            Vector3 lookAheadPoint = center + new Vector3(Mathf.Cos(circleAngle - 0.45f) * actualRadius, 0, Mathf.Sin(circleAngle - 0.45f) * actualRadius);
            Vector3 lookDir = lookAheadPoint - transform.position;
            lookDir.y = 0;

            // Xoay đầu mặt dứt khoát về phía con phía trước đang chạy
            if (lookDir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), 18f * Time.deltaTime);
            }

            // Di chuyển bước chân tiến thẳng về phía trước theo vòng tròn
            Vector3 nextPos = Vector3.MoveTowards(transform.position, circleTarget, chaseSpeed * 1.25f * Time.deltaTime);

            if (IsPointOnMyEnvironmentTile(nextPos, out float surfaceY))
            {
                nextPos.y = GetCurrentTargetY(surfaceY);
                transform.position = nextPos;
            }
            else
            {
                transform.position = nextPos;
            }

            yield return null;
        }

        currentState = AnimalState.DoScriptedAnim;
    }

    /// <summary>
    /// Chọn animation chạy đuổi / bơi hào hứng nhất cho từng loài khi dí nhau
    /// </summary>
    string GetChasingDanceAnim()
    {
        if (isAquatic)
        {
            if (HasAnim("Swim_Horizontal")) return "Swim_Horizontal";
            if (HasAnim("Swim")) return "Swim";
        }
        else
        {
            // Các loài có animation chạy (Run / Trot) -> Ưu tiên chạy đuổi nhau cực kỳ sống động!
            if (HasAnim("Run")) return "Run";
            if (HasAnim("Trot")) return "Trot";
            if (HasAnim("Flap")) return "Flap";
            if (HasAnim("Glide") && animalID.Contains("buom")) return "Glide";
            if (HasAnim("Walk")) return "Walk";
            if (availableMoveClips.Count > 0) return availableMoveClips[0];
        }
        return moveAnimName;
    }

    // =========================================================================
    // 6. DIỄN HÀNH ĐỘNG ĐẶC TRƯNG THEO KỊCH BẢN & SINH BUFF GAMEPLAY
    // =========================================================================
    IEnumerator DoScriptedComboAndBuffRoutine()
    {
        string comboKey = GetComboIdentifier(groupPartners);

        // 1. Tính thời gian hồi chiêu theo cấp độ tầng Biome và số lượng thú (2 con hay 3 con)
        float cooldownDuration = CalculateConnectionCooldown(comboKey, groupPartners.Count);
        string formattedCooldown = FormatCooldownDuration(cooldownDuration);

        // 2. Đồng bộ thời gian hồi chiêu cho TẤT CẢ các con thú trong nhóm connection
        foreach (var p in groupPartners)
        {
            if (p != null)
            {
                p.SetConnectionCooldown(cooldownDuration);
            }
        }

        // 3. Diễn animation theo kịch bản cho từng loài
        PerformScriptedAction(comboKey);

        // 4. Chỉ để 1 con đại diện kích hoạt thông báo Buff kèm thời gian hồi chiêu
        if (groupPartners.Count > 0 && groupPartners[0] == this)
        {
            ApplyGameplayBuff(comboKey, formattedCooldown);
        }

        // Thời gian diễn động tác kịch bản
        yield return new WaitForSeconds(4.5f);

        currentState = AnimalState.Cooldown;
    }

    void PerformScriptedAction(string comboKey)
    {
        switch (comboKey)
        {
            // 1. MOUNTAIN BIOME
            case "Parrot_Peacock":
                if (animalID == "chimcong_anm")
                {
                    // Chim công xòe đuôi rực rỡ
                    PlayAnimIfExist(HasAnim("Glide") ? "Glide" : "Walk");
                }
                else if (animalID == "chimvet7mau_anm")
                {
                    // Chim vẹt vừa hót vừa thả ánh sáng màu sắc
                    PlayAnimIfExist(HasAnim("Glide") ? "Glide" : "Walk");
                }
                break;

            case "Dragonfly_Grasshopper":
                // Châu chấu & Chuồn chuồn đập cánh bắt nhịp theo nhau
                PlayAnimIfExist(HasAnim("Flap") ? "Flap" : "Glide");
                break;

            // 2. FOREST BIOME
            case "Cows_Deer":
                if (animalID == "bo_anm" || animalID == "bolongdai_anm")
                {
                    // Bò cỏ và Bò lông dài thong thả gặm cỏ
                    PlayAnimIfExist(HasAnim("Rest_Pose") ? "Rest_Pose" : "Sit");
                }
                else if (animalID == "nai_anm")
                {
                    // Nai dung giăng chơi đùa bên cạnh
                    PlayAnimIfExist(HasAnim("Eating") ? "Eating" : (HasAnim("Rear") ? "Rear" : "Trot"));
                }
                break;

            case "Foxes_FlowerDog":
                // 3 con nằm cùng nhau gác rừng hoặc chạy tương tác vui nhộn
                if (animalID == "chohoa_anm")
                {
                    PlayAnimIfExist(HasAnim("Bark") ? "Bark" : "Sit");
                }
                else
                {
                    PlayAnimIfExist(HasAnim("Sit") ? "Sit" : (HasAnim("Rest_Pose") ? "Rest_Pose" : "Jump"));
                }
                break;

            case "Squirrel_Deer":
                if (animalID == "socbay_anm")
                {
                    // Sóc bay giang cánh bay
                    PlayAnimIfExist(HasAnim("Fall") ? "Fall" : (HasAnim("Fetch") ? "Fetch" : "Bark"));
                }
                else if (animalID == "nai_anm")
                {
                    // Nai chạy theo
                    PlayAnimIfExist(HasAnim("Run") ? "Run" : "Trot");
                }
                break;

            // 3. FLOWER VALLEY BIOME
            case "Capy_Otter_Frog":
                if (animalID == "capybara_anm")
                {
                    // Capybara nằm thong dong giữa thảm hoa
                    PlayAnimIfExist(HasAnim("Rest_Pose") ? "Rest_Pose" : "Idle");
                }
                else if (animalID == "echhoa_anm")
                {
                    // Ếch hoa đứng yên
                    PlayAnimIfExist(HasAnim("Idle") ? "Idle" : "Rest_Pose");
                }
                else if (animalID == "raica_anm")
                {
                    // Rái cá nằm nhảy lên vui nhộn
                    PlayAnimIfExist(HasAnim("Jump") ? "Jump" : "Fetch");
                }
                break;

            case "Caterpillar_Frog":
                if (animalID == "saubuom_anm")
                {
                    // Sâu bướm bò đi
                    PlayAnimIfExist(HasAnim("Walk") ? "Walk" : "Fly_Glide");
                }
                else if (animalID == "echhoa_anm")
                {
                    // Ếch hoa đứng yên
                    PlayAnimIfExist(HasAnim("Idle") ? "Idle" : "Rest_Pose");
                }
                break;

            // 4. WATER BIOME
            case "Swan_WaterFrog":
                if (animalID == "thiennga_anm")
                {
                    // Thiên nga bơi tạo sóng nhẹ
                    PlayAnimIfExist(HasAnim("Swim_Horizontal") ? "Swim_Horizontal" : "Idle");
                }
                else if (animalID == "echduoica_anm")
                {
                    // Ếch dưới nước bơi xung quanh
                    PlayAnimIfExist(HasAnim("Swim_Horizontal") ? "Swim_Horizontal" : "Swim_Vertical");
                }
                break;

            case "Puffer_WaterFrog":
                // Mỗi khi Ếch bơi lại gần, Cá nóc nằm ngửa ra, Ếch giật mình cũng ngửa theo
                PlayAnimIfExist(HasAnim("Dead_Floating") ? "Dead_Floating" : "Idle");
                break;

            default:
                PlayAnimIfExist(idleAnimName);
                break;
        }
    }

    void ApplyGameplayBuff(string comboKey, string cooldownInfo = "")
    {
        string buffTitle = "";
        string buffDesc = "";
        string hexColor = "#FBBF24";

        switch (comboKey)
        {
            case "Parrot_Peacock":
                buffTitle = "Vũ Điệu Sắc Màu & Tiếng Hót";
                buffDesc = "Tăng +5% sản lượng tài nguyên Vùng Núi Cao!";
                hexColor = "#F472B6";
                TriggerScoreReward(10);
                break;

            case "Dragonfly_Grasshopper":
                buffTitle = "Cặp Đôi Tiết Khí Núi Cao";
                buffDesc = "Tăng +15% tốc độ thu hoạch tài nguyên ô Núi Cao liền kề!";
                hexColor = "#38BDF8";
                TriggerScoreReward(10);
                break;

            case "Cows_Deer":
                buffTitle = "Tam Tấu Gặm Cỏ & Sinh Thái Rừng";
                buffDesc = "Tạo 'Phân bón Hữu cơ Cao cấp', giảm thời gian hồi vàng cho ô Rừng giáp ranh!";
                hexColor = "#4ADE80";
                TriggerScoreReward(15);
                break;

            case "Foxes_FlowerDog":
                buffTitle = "Đội Cảnh Vệ Rừng Xanh";
                buffDesc = "Bảo vệ nông sản ô Rừng tươi xốp, giảm thời gian hồi tài nguyên!";
                hexColor = "#FB923C";
                TriggerScoreReward(15);
                break;

            case "Squirrel_Deer":
                buffTitle = "Chuyền Cành Cung Cấp Năng Lượng";
                buffDesc = "Kích hoạt Buff: Tăng +10% coin thu được từ ô này!";
                hexColor = "#FACC15";
                TriggerScoreReward(10);
                break;

            case "Capy_Otter_Frog":
                buffTitle = "Hội Thư Giãn Dưới Vòm Hoa (Chilling Zone)";
                buffDesc = "Tỏa năng lượng hoa giúp các thú bên cạnh giảm -10% thời gian hồi vàng!";
                hexColor = "#C084FC";
                TriggerScoreReward(15);
                break;

            case "Caterpillar_Frog":
                buffTitle = "Tiến Hóa & Thụ Phấn";
                buffDesc = "Sản sinh tài nguyên: Tiền thu được tăng +10%!";
                hexColor = "#A3E635";
                TriggerScoreReward(10);
                break;

            case "Swan_WaterFrog":
                buffTitle = "Vũ Điệu Đầm Lầy";
                buffDesc = "Lọc sạch nguồn nước, giúp tài nguyên dưới nước thu hoạch nhanh hơn +10%!";
                hexColor = "#22D3EE";
                TriggerScoreReward(10);
                break;

            case "Puffer_WaterFrog":
                buffTitle = "Cảnh Báo Phình To";
                buffDesc = "Cá nóc & Ếch nằm ngửa: Tài nguyên nhận được tăng x1.5 lần!";
                hexColor = "#F59E0B";
                TriggerScoreReward(20);
                break;
        }

        string displayDesc = buffDesc;
        if (!string.IsNullOrEmpty(cooldownInfo))
        {
            displayDesc += $"\n[Hồi chiêu: {cooldownInfo}]";
        }

        Debug.Log($"<color={hexColor}><b>[LumiWorld Connection]</b> ✨ <b>{buffTitle}</b>: {buffDesc} | Thời gian hồi: {cooldownInfo}</color>");
        ShowFloatingBuffBanner(buffTitle, displayDesc, hexColor);
    }

    void TriggerScoreReward(int points)
    {
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.SetScore(ScoreManager.Instance.CurrentScore + points);
        }
    }

    /// <summary>
    /// Hiển thị biểu ngữ Buff nổi 3D đẹp mắt trên đầu thú
    /// </summary>
    void ShowFloatingBuffBanner(string title, string desc, string hexColor)
    {
        GameObject bannerObj = new GameObject("BuffBanner");
        bannerObj.transform.position = transform.position + Vector3.up * 1.8f;

        TextMesh tm = bannerObj.AddComponent<TextMesh>();
        tm.text = $"★ {title} ★\n{desc}";
        tm.characterSize = 0.08f;
        tm.fontSize = 28;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;

        ColorUtility.TryParseHtmlString(hexColor, out Color color);
        tm.color = color;

        StartCoroutine(AnimateBannerRoutine(bannerObj));
    }

    IEnumerator AnimateBannerRoutine(GameObject banner)
    {
        if (banner == null) yield break;

        Camera cam = Camera.main;
        float duration = 4.0f;
        float elapsed = 0f;
        Vector3 startPos = banner.transform.position;

        while (elapsed < duration && banner != null)
        {
            elapsed += Time.deltaTime;
            banner.transform.position = startPos + Vector3.up * (elapsed * 0.25f);

            if (cam != null)
            {
                banner.transform.rotation = Quaternion.LookRotation(banner.transform.position - cam.transform.position);
            }

            yield return null;
        }

        if (banner != null) Destroy(banner);
    }

    // =========================================================================
    // 7. COOLDOWN VÀ TẢN RA XA NHAU (DISPERSE & SCATTER)
    // =========================================================================

    /// <summary>
    /// Tính toán thời gian hồi chiêu Connection theo cấp độ tầng Biome và số lượng thú:
    /// - Cấp độ từ thấp lên cao (Thung lũng -> Vùng nước -> Rừng rậm -> Núi cao): từ 5 phút đến 5 tiếng
    /// - Combo 3 thú có thời gian hồi lâu hơn hẳn tính bằng tiếng (1.5h - 5h)
    /// </summary>
    float CalculateConnectionCooldown(string comboKey, int memberCount)
    {
        // Chế độ test nhanh để kiểm tra animation trong Unity Editor (15s cho 2 thú, 35s cho 3 thú)
        if (fastCooldownForTesting)
        {
            return memberCount >= 3 ? 35f : 15f;
        }

        switch (biome)
        {
            case BiomeType.FlowerValley: // Tầng 1: Thung lũng hoa (thấp nhất)
                if (memberCount >= 3)
                    return 1.5f * 3600f; // 1.5 giờ (5400s) cho Capy + Rái cá + Ếch hoa
                else
                    return 5f * 60f;      // 5 phút (300s) cho Sâu bướm + Ếch hoa

            case BiomeType.Water:        // Tầng 2: Vùng Nước / Đầm Lầy
                if (memberCount >= 3)
                    return 2f * 3600f;   // 2 giờ (7200s)
                else
                    return 15f * 60f;    // 15 phút (900s) cho Thiên nga / Cá nóc + Ếch nước

            case BiomeType.Forest:       // Tầng 3: Vùng Rừng Rậm
                if (memberCount >= 3)
                    return 3f * 3600f;   // 3 giờ (10800s) cho Bò cỏ + Bò lông dài + Nai, hoặc Cáo + Cáo cỏ + Chó hoa
                else
                    return 30f * 60f;    // 30 phút (1800s) cho Sóc bay + Nai

            case BiomeType.Mountain:     // Tầng 4: Vùng Núi Cao (cao nhất)
                if (memberCount >= 3)
                    return 5f * 3600f;   // 5 giờ (18000s) cho combo 3 thú trên núi
                else
                    return 60f * 60f;    // 1 giờ (3600s) cho Vẹt + Công, Chuồn chuồn + Châu chấu

            default:
                return memberCount >= 3 ? 3600f : 300f;
        }
    }

    /// <summary>
    /// Định dạng thời gian hồi chiêu thành chuỗi dễ đọc (Giờ:Phút hoặc Phút:Giây)
    /// </summary>
    public string FormatCooldownDuration(float seconds)
    {
        int hours = (int)(seconds / 3600);
        int mins = (int)((seconds % 3600) / 60);
        int secs = (int)(seconds % 60);

        if (hours > 0)
        {
            if (mins > 0) return $"{hours}h {mins:D2}m";
            return $"{hours} tiếng";
        }
        else if (mins > 0)
        {
            if (secs > 0) return $"{mins}p {secs:D2}s";
            return $"{mins} phút";
        }
        else
        {
            return $"{secs} giây";
        }
    }

    /// <summary>
    /// Gán thời gian chờ hồi chiêu cho con thú
    /// </summary>
    public void SetConnectionCooldown(float duration)
    {
        cooldownRemainingSeconds = duration;
        isConnectionOnCooldown = true;
        formattedCooldownRemaining = FormatCooldownDuration(duration);
    }

    /// <summary>
    /// Sau khi kết thúc connection 20s và diễn xong kịch bản:
    /// Các con thú tản ra xa nhau, di chuyển tới các điểm an toàn trong ô môi trường,
    /// diễn các animation cơ bản phong phú (ăn cỏ, ngồi, nghỉ ngơi, nhìn ngắm...)
    /// và trở về trạng thái tự do (WanderRandom). Trong suốt thời gian hồi, chúng sẽ KHÔNG kích hoạt connection nữa.
    /// </summary>
    IEnumerator DoDisperseAndScatterRoutine()
    {
        // 1. Tính hướng tản ra xa tâm nhóm
        Vector3 groupCenter = Vector3.zero;
        int count = 0;
        foreach (var p in groupPartners)
        {
            if (p != null)
            {
                groupCenter += p.transform.position;
                count++;
            }
        }
        if (count > 0) groupCenter /= count;

        Vector3 awayDir = FlatVector(transform.position - groupCenter).normalized;
        if (awayDir.sqrMagnitude < 0.01f)
        {
            float randAngle = Random.Range(0f, Mathf.PI * 2f);
            awayDir = new Vector3(Mathf.Cos(randAngle), 0, Mathf.Sin(randAngle));
        }

        // Tìm điểm tản ra an toàn trong ô tài nguyên môi trường (cách 0.6m - 1.2m)
        Vector3 scatterTarget = transform.position + awayDir * Random.Range(0.6f, 1.2f);
        if (!IsPointOnMyEnvironmentTile(scatterTarget, out float sY))
        {
            // Nếu hướng tản ra chạm mép ô -> Lấy 1 điểm ngẫu nhiên an toàn trong lòng ô
            scatterTarget = GetRandomSafeBiomePoint();
        }
        else
        {
            scatterTarget.y = GetCurrentTargetY(sY);
        }

        // Bước chân tản ra xa nhau
        string moveClip = moveAnimName;
        if (availableMoveClips.Count > 0)
        {
            moveClip = availableMoveClips[Random.Range(0, availableMoveClips.Count)];
        }
        yield return StartCoroutine(MoveSafelyToTarget(scatterTarget, moveClip, Random.Range(2.0f, 3.5f)));

        // 2. Diễn các animation cơ bản phong phú sau khi tản ra (nghỉ ngơi, ăn, ngồi...)
        string idleClip = idleAnimName;
        if (availableIdleClips.Count > 0)
        {
            idleClip = availableIdleClips[Random.Range(0, availableIdleClips.Count)];
        }
        PlayAnimIfExist(idleClip);
        yield return new WaitForSeconds(Random.Range(2.5f, 4.5f));

        // 3. Hoàn tất tản ra: giải phóng nhóm và trở lại trạng thái WanderRandom tự do
        groupPartners.Clear();
        currentState = AnimalState.WanderRandom;
    }

    IEnumerator DoCooldownRoutine(float duration)
    {
        groupPartners.Clear();
        yield return new WaitForSeconds(duration);
        currentState = AnimalState.WanderRandom;
    }

    // =========================================================================
    // TIỆN ÍCH SO KHỚP COMBO THEO KỊCH BẢN
    // =========================================================================
    bool TryExtractValidCombo(List<FarmAnimalAI> pool, out List<FarmAnimalAI> result)
    {
        result = new List<FarmAnimalAI>();

        // 1. VÙNG NÚI CAO (Mountain)
        if (biome == BiomeType.Mountain)
        {
            if (TryMatchSubgroup(pool, out result, "chimvet7mau_anm", "chimcong_anm")) return true;
            if (TryMatchSubgroup(pool, out result, "chuongchuong_anm", "chauchau_anm")) return true;
        }
        // 2. VÙNG RỪNG RẬM (Forest)
        else if (biome == BiomeType.Forest)
        {
            // Tam tấu Bò cỏ + Bò lông dài + Nai
            if (TryMatchSubgroup(pool, out result, "bo_anm", "bolongdai_anm", "nai_anm")) return true;
            // Đội Cảnh vệ Cáo + Cáo cỏ + Chó hoa
            if (TryMatchSubgroup(pool, out result, "cao_anm", "caoco_anm", "chohoa_anm")) return true;
            // Sóc bay + Nai
            if (TryMatchSubgroup(pool, out result, "socbay_anm", "nai_anm")) return true;
        }
        // 3. VÙNG THUNG LŨNG HOA (Flower Valley)
        else if (biome == BiomeType.FlowerValley)
        {
            // Capybara + Rái cá + Ếch hoa
            if (TryMatchSubgroup(pool, out result, "capybara_anm", "raica_anm", "echhoa_anm")) return true;
            // Sâu bướm + Ếch hoa
            if (TryMatchSubgroup(pool, out result, "saubuom_anm", "echhoa_anm")) return true;
        }
        // 4. VÙNG NƯỚC / ĐẦM LẦY (Water)
        else if (biome == BiomeType.Water)
        {
            // Thiên nga + Ếch dưới nước
            if (TryMatchSubgroup(pool, out result, "thiennga_anm", "echduoica_anm")) return true;
            // Cá nóc + Ếch dưới nước
            if (TryMatchSubgroup(pool, out result, "canoc_anm", "echduoica_anm")) return true;
        }

        return false;
    }

    bool TryMatchSubgroup(List<FarmAnimalAI> pool, out List<FarmAnimalAI> matched, params string[] requiredIDs)
    {
        matched = new List<FarmAnimalAI>();
        List<FarmAnimalAI> tempPool = new List<FarmAnimalAI>(pool);

        foreach (string req in requiredIDs)
        {
            FarmAnimalAI found = tempPool.Find(a => a != null && a.animalID == req);
            if (found != null)
            {
                matched.Add(found);
                tempPool.Remove(found);
            }
            else
            {
                matched.Clear();
                return false;
            }
        }

        return true;
    }

    string GetComboIdentifier(List<FarmAnimalAI> list)
    {
        List<string> ids = new List<string>();
        foreach (var a in list) if (a != null) ids.Add(a.animalID);

        if (ids.Contains("chimvet7mau_anm") && ids.Contains("chimcong_anm")) return "Parrot_Peacock";
        if (ids.Contains("chuongchuong_anm") && ids.Contains("chauchau_anm")) return "Dragonfly_Grasshopper";
        if (ids.Contains("bo_anm") && ids.Contains("bolongdai_anm") && ids.Contains("nai_anm")) return "Cows_Deer";
        if (ids.Contains("cao_anm") && ids.Contains("caoco_anm") && ids.Contains("chohoa_anm")) return "Foxes_FlowerDog";
        if (ids.Contains("socbay_anm") && ids.Contains("nai_anm")) return "Squirrel_Deer";
        if (ids.Contains("capybara_anm") && ids.Contains("raica_anm") && ids.Contains("echhoa_anm")) return "Capy_Otter_Frog";
        if (ids.Contains("saubuom_anm") && ids.Contains("echhoa_anm")) return "Caterpillar_Frog";
        if (ids.Contains("thiennga_anm") && ids.Contains("echduoica_anm")) return "Swan_WaterFrog";
        if (ids.Contains("canoc_anm") && ids.Contains("echduoica_anm")) return "Puffer_WaterFrog";

        return "GenericCombo";
    }

    // =========================================================================
    // TIỆN ÍCH BỔ TRỢ
    // =========================================================================
    string GetTagForCurrentBiome()
    {
        switch (biome)
        {
            case BiomeType.Mountain: return mountainTileTag;
            case BiomeType.Forest: return forestTileTag;
            case BiomeType.FlowerValley: return flowerTileTag;
            case BiomeType.Water: return waterTileTag;
            default: return mountainTileTag;
        }
    }

    void SnapToTileSurface()
    {
        if (movementAI != null && movementAI.enabled)
        {
            // Nhường việc khởi tạo vị trí ban đầu và yOffset cho AnimalMovementAI
            return;
        }

        if (IsPointOnMyEnvironmentTile(transform.position, out float surfaceY))
        {
            transform.position = new Vector3(transform.position.x, GetCurrentTargetY(surfaceY), transform.position.z);
        }
        else
        {
            // Nếu ban đầu đang bị đặt ở ô đất trống, tự động đưa thú về ô có tài nguyên môi trường của nó gần nhất
            Vector3 nearestResourceTile = FindNearestValidHabitatTile(transform.position);
            if (nearestResourceTile != Vector3.zero)
            {
                float targetY = isFlying ? nearestResourceTile.y + flightAltitude : nearestResourceTile.y + yOffset;
                transform.position = new Vector3(nearestResourceTile.x, targetY, nearestResourceTile.z);
            }
        }
    }

    void LateUpdate()
    {
        // Khi bay trên không và không phải đang múa xoay vòng tròn: giữ độ cao bồng bềnh êm ái
        if (isFlying && currentState != AnimalState.CircleDance20s)
        {
            if (movementAI != null && movementAI.enabled && currentState == AnimalState.WanderRandom)
            {
                // AnimalMovementAI đã tự xử lý UpdateElevationAndBanking trong lúc wander!
                return;
            }

            if (IsPointOnMyEnvironmentTile(transform.position, out float sY))
            {
                float targetY = GetCurrentTargetY(sY);
                Vector3 p = transform.position;
                p.y = Mathf.Lerp(p.y, targetY, 6f * Time.deltaTime);
                transform.position = p;
            }
        }
    }

    void RotateSmoothlyTowards(Vector3 target)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 12f * Time.deltaTime);
        }
    }

    Vector3 FlatVector(Vector3 v) => new Vector3(v.x, 0, v.z);

    bool HasAnim(string clipName)
    {
        return anim != null && anim.GetClip(clipName) != null;
    }

    void PlayAnimIfExist(string clipName)
    {
        if (anim != null && anim.GetClip(clipName) != null)
        {
            anim.CrossFade(clipName, 0.2f);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = isAquatic ? Color.cyan : Color.green;
        Gizmos.DrawWireSphere(transform.position, innerHexRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, circleRadius);
    }
}