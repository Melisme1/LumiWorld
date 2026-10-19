using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// AI điều khiển hành vi động vật tự nhiên trong LumiWorld:
/// - Tự động nhận diện các animation di chuyển (Walk, Run, Swim, Fly...) và tĩnh (Idle, Ăn, Ngồi...)
/// - Đồng bộ chuẩn xác giữa di chuyển thực tế và Animation (Không bao giờ bị chạy tại chỗ / treadmill)
/// - Tự động xoay người mượt mà (Smooth Rotation) về hướng di chuyển, giữ dáng Idle khi đang quay tại chỗ
/// - Tự động dừng và chuyển sang Idle khi bị con thú khác chắn đường hoặc chạm ranh giới ô
/// - Chỉ đi lang thang trong phạm vi các ô lục giác CÙNG HABITAT (Rừng, Đồng cỏ...)
/// - Giữ thú luôn nằm an toàn trong ô (Soft Boundary Guard): không bị rơi ra ngoài viền, không giật hình
/// - Tự động bám theo độ cao bề mặt đất (Surface Y) của từng ô lục giác
/// - Cơ chế chống đè lấn liên tục (Always-Active Overlap Resolution) chạy mỗi frame
/// - Né tránh chướng ngại vật dạng lách sang bên (Tangential / Lateral Steering)
/// - Tránh chọn trùng điểm đến (Destination Conflict Avoidance)
/// - Hoàn toàn không dùng Physics Collider / Rigidbody -> Siêu nhẹ về hiệu năng
/// </summary>
[RequireComponent(typeof(Animation))]
public class AnimalMovementAI : MonoBehaviour
{
    [Header("Movement Speeds")]
    [Tooltip("Tốc độ đi bộ bình thường (mét/giây)")]
    [SerializeField] private float walkSpeed = 0.5f;

    [Tooltip("Tốc độ chạy nhanh (mét/giây)")]
    [SerializeField] private float runSpeed = 1.0f;

    [Tooltip("Tốc độ bơi (dành cho cá, thiên nga, rái cá...)")]
    [SerializeField] private float swimSpeed = 0.6f;

    [Tooltip("Tốc độ bay / lượn (dành cho chim, chuồn chuồn, bướm...)")]
    [SerializeField] private float flySpeed = 0.9f;

    [Header("Locomotion Type & Flight Settings")]
    [Tooltip("Kiểu vận động của con thú: Ground (Đi bộ) hoặc Flying (Bay lượn)")]
    [SerializeField] private AnimalLocomotionType locomotionType = AnimalLocomotionType.Ground;

    [Tooltip("Tự động nhận diện kiểu bay nếu model có clip Fly / Glide / Flap (mặc định tắt để tôn trọng cấu hình loài)")]
    [SerializeField] private bool autoDetectFlying = false;

    [Tooltip("Độ cao bay trên không so với mặt đất (mét)")]
    [SerializeField] private float flightAltitude = 1.35f;

    [Tooltip("Tần số nhịp đập cánh / dập dềnh bồng bềnh (Hz)")]
    [SerializeField] private float flightBobbingFrequency = 3.2f;

    [Tooltip("Biên độ nhấp nhô bồng bềnh theo phương thẳng đứng (mét)")]
    [SerializeField] private float flightBobbingAmplitude = 0.08f;

    [Tooltip("Góc nghiêng cánh tối đa khi bo cua (độ Roll)")]
    [SerializeField] private float maxBankAngle = 22f;

    [Tooltip("Tốc độ nghiêng cánh (độ/giây)")]
    [SerializeField] private float bankSpeed = 6.0f;

    [Tooltip("Tỉ lệ % hạ cánh đậu nghỉ trên mặt đất sau mỗi chuyến bay")]
    [Range(0f, 1f)]
    [SerializeField] private float landChance = 0.45f;

    [Tooltip("Tốc độ chuyển đổi độ cao bay (mét/giây)")]
    [SerializeField] private float altitudeSmoothSpeed = 2.8f;

    [Header("Rotation Settings")]
    [Tooltip("Tốc độ xoay hướng (độ/giây). Càng lớn quay càng nhanh.")]
    [SerializeField] private float rotationSpeed = 160f;

    [Tooltip("Nếu bật: Con thú sẽ quay đầu về hướng đi trước khi bắt đầu bước chân (trông rất tự nhiên)")]
    [SerializeField] private bool turnBeforeWalking = true;

    [Tooltip("Góc lệch tối đa (độ) cho phép thú bắt đầu bước đi khi đang quay")]
    [SerializeField] private float turnThresholdAngle = 60f;

    [Header("Elevation & Offset")]
    [Tooltip("Độ cao nâng Y để chân chạm mặt cỏ (Bù trừ pivot nằm giữa bụng của model 3D)")]
    [SerializeField] private float yOffset = 0.35f;

    [Tooltip("Tự động tính yOffset dựa trên kích thước thực tế của SkinnedMeshRenderer (mặc định tắt để tôn trọng cấu hình loài)")]
    [SerializeField] private bool autoDetectYOffset = false;

    [Header("Wander Settings")]
    [Tooltip("Tỉ lệ % thú chọn đi lại trong CHÍNH Ô HIỆN TẠI thay vì bước sang ô láng giềng (0.5 = 50%)")]
    [Range(0f, 1f)]
    [SerializeField] private float stayInCurrentHexChance = 0.50f;

    [Tooltip("Bán kính tối đa thú có thể đi quanh tâm ô lục giác (mét). Nên để 0.40m - 0.50m để cách xa mép ô an toàn.")]
    [Range(0.2f, 0.65f)]
    [SerializeField] private float maxWanderRadius = 0.45f;

    [Tooltip("Bán kính an toàn tuyệt đối từ tâm ô (mét).")]
    [SerializeField] private float safeHexRadius = 0.78f;

    [Tooltip("Thời gian nghỉ tối thiểu giữa các hành động (giây)")]
    [SerializeField] private float minIdleTime = 1.4f;

    [Tooltip("Thời gian nghỉ tối đa giữa các hành động (giây)")]
    [SerializeField] private float maxIdleTime = 3.0f;

    [Tooltip("Tỉ lệ % thú thỉnh thoảng xoay đầu ngẫu nhiên ngắm cảnh khi đang đứng yên")]
    [Range(0f, 1f)]
    [SerializeField] private float idleTurnChance = 0.30f;

    [Tooltip("Nếu bật: Thú ưu tiên dạo chơi ở nửa trước của ô (hướng về phía Camera) để tránh bị tán cây che khuất")]
    [SerializeField] private bool preferCameraFacingGlade = true;

    [Header("Collision & Overlap Avoidance (Không dùng Collider)")]
    [Tooltip("Bán kính vùng đệm cá nhân của con thú (mét). Khi 2 con chạm vào vùng này sẽ tự động đẩy nhau ra.")]
    [SerializeField] private float personalRadius = 0.22f;

    [Tooltip("Lực né dạt sang bên khi có con thú khác chắn đường")]
    [SerializeField] private float avoidanceWeight = 1.4f;

    [Tooltip("Tốc độ đẩy tách nhau khi bị đè lấn")]
    [SerializeField] private float separationSpeed = 2.2f;

    // Bán kính tiếp xúc vật lý của cơ thể con thú đối với chướng ngại vật tĩnh (thân cây, gốc cây, tảng đá)
    // Giữ ở mức 0.08m để thú có thể luồn lách qua các khe hở tự nhiên mà không bị kẹt hay giật hình
    private const float PropObstacleBodyRadius = 0.08f;

    // Danh sách tĩnh quản lý tất cả các con thú đang hoạt động trong Scene (siêu nhẹ, không tốn CPU)
    private static readonly List<AnimalMovementAI> ActiveAnimals = new List<AnimalMovementAI>();

    /// <summary>
    /// Cấu trúc lưu trữ thông số chướng ngại vật thực tế trên ô (thân cây, gốc cây nhỏ, tảng đá, khúc gỗ...)
    /// </summary>
    public struct TileObstacle
    {
        public Vector3 worldPos;     // Tọa độ gốc trên mặt phẳng thế giới (X, Z)
        public float radius;         // Bán kính cản va chạm (mét)
        public string name;          // Tên prop phục vụ debug / Gizmos
        public Transform transform;  // Tham chiếu để kiểm tra đối tượng còn tồn tại
    }

    // Cache chướng ngại vật tĩnh theo từng ô lục giác (tối ưu hóa 100%, không duyệt renderers mỗi frame)
    private static readonly Dictionary<HexCoordinates, List<TileObstacle>> TileObstaclesCache = new Dictionary<HexCoordinates, List<TileObstacle>>();
    private static readonly Dictionary<HexCoordinates, int> TileObstaclesChildCount = new Dictionary<HexCoordinates, int>();

    // Danh sách tái sử dụng nội bộ để tránh cấp phát bộ nhớ (Zero GC Alloc)
    private readonly List<TileObstacle> tempNearbyObstacles = new List<TileObstacle>();

    // Component nội bộ
    private Animation anim;
    private HexWorldGenerator worldGen;
    private HexCoordinates currentHex;
    private bool isMoving = false;
    private FarmAnimalAI farmAI;
    private Vector3 lastTargetDestination = Vector3.zero;

    /// <summary>
    /// Xóa cache khi có ô lục giác bị thay đổi hoặc biến đổi địa hình
    /// </summary>
    public static void InvalidateObstacleCache(HexCoordinates hex)
    {
        TileObstaclesCache.Remove(hex);
        TileObstaclesChildCount.Remove(hex);
    }

    /// <summary>
    /// Xóa toàn bộ cache chướng ngại vật (ví dụ khi sinh đảo mới)
    /// </summary>
    public static void ClearAllObstacleCaches()
    {
        TileObstaclesCache.Clear();
        TileObstaclesChildCount.Clear();
    }

    /// <summary>
    /// Lấy danh sách chướng ngại vật trong phạm vi xung quanh tọa độ kiểm tra (từ ô hiện tại và các ô láng giềng)
    /// </summary>
    private void GetNearbyObstacles(Vector3 center, float searchRadius, List<TileObstacle> results)
    {
        results.Clear();
        if (worldGen == null || worldGen.MapTiles == null) return;

        HexCoordinates centerHex = HexMetrics.WorldToHex(center);
        AddTileObstaclesIfValid(centerHex, center, searchRadius, results);

        for (int d = 0; d < 6; d++)
        {
            HexCoordinates nHex = centerHex.GetNeighbor(d);
            AddTileObstaclesIfValid(nHex, center, searchRadius, results);
        }
    }

    private void AddTileObstaclesIfValid(HexCoordinates hex, Vector3 center, float searchRadius, List<TileObstacle> results)
    {
        List<TileObstacle> tileObs = GetOrUpdateTileObstacles(hex);
        if (tileObs == null || tileObs.Count == 0) return;

        for (int i = 0; i < tileObs.Count; i++)
        {
            TileObstacle obs = tileObs[i];
            if (obs.transform == null) continue;

            float dx = obs.worldPos.x - center.x;
            float dz = obs.worldPos.z - center.z;
            float maxAllowedDist = searchRadius + obs.radius;
            if (dx * dx + dz * dz <= maxAllowedDist * maxAllowedDist)
            {
                results.Add(obs);
            }
        }
    }

    private List<TileObstacle> GetOrUpdateTileObstacles(HexCoordinates hex)
    {
        if (worldGen == null || worldGen.MapTiles == null) return null;
        if (!worldGen.MapTiles.TryGetValue(hex, out GameObject tileObj) || tileObj == null)
        {
            TileObstaclesCache.Remove(hex);
            TileObstaclesChildCount.Remove(hex);
            return null;
        }

        int currentChildCount = tileObj.transform.childCount;
        for (int i = 0; i < tileObj.transform.childCount; i++)
        {
            Transform c = tileObj.transform.GetChild(i);
            if (c != null && c.name.StartsWith("Habitat_"))
            {
                currentChildCount += c.childCount;
            }
        }

        if (TileObstaclesCache.TryGetValue(hex, out List<TileObstacle> cachedList) &&
            TileObstaclesChildCount.TryGetValue(hex, out int cachedCount))
        {
            if (cachedCount == currentChildCount)
            {
                bool allAlive = true;
                for (int i = 0; i < cachedList.Count; i++)
                {
                    if (cachedList[i].transform == null)
                    {
                        allAlive = false;
                        break;
                    }
                }
                if (allAlive) return cachedList;
            }
        }

        List<TileObstacle> newList = TileObstaclesCache.TryGetValue(hex, out var existing) ? existing : new List<TileObstacle>();
        newList.Clear();

        ScanTransformForObstacles(tileObj.transform, newList);

        TileObstaclesCache[hex] = newList;
        TileObstaclesChildCount[hex] = currentChildCount;
        return newList;
    }

    private static void ScanTransformForObstacles(Transform root, List<TileObstacle> results)
    {
        if (root == null) return;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null) continue;

            if (child.name.StartsWith("Habitat_"))
            {
                for (int p = 0; p < child.childCount; p++)
                {
                    Transform prop = child.GetChild(p);
                    if (prop != null)
                    {
                        EvaluateAndAddObstacle(prop, results);
                    }
                }
                continue;
            }

            if (child.GetComponent<AnimalMovementAI>() != null || child.GetComponent<PlacedCard>() != null)
            {
                continue;
            }

            EvaluateAndAddObstacle(child, results);
        }
    }

    private static void EvaluateAndAddObstacle(Transform prop, List<TileObstacle> results)
    {
        if (prop == null) return;
        string name = prop.name;

        // 1. Kiểm tra cấu hình tường minh trên HexPropConfig nếu có
        HexPropConfig config = prop.GetComponent<HexPropConfig>();
        if (config == null) config = prop.GetComponentInChildren<HexPropConfig>();

        if (config != null)
        {
            if (config.ObstacleMode == PropObstacleMode.Passable)
            {
                return;
            }
            if (config.ObstacleMode == PropObstacleMode.SolidObstacle)
            {
                float r = config.CustomObstacleRadius > 0.01f ? config.CustomObstacleRadius : ComputeDefaultRadius(prop);
                results.Add(new TileObstacle
                {
                    worldPos = prop.position,
                    radius = Mathf.Clamp(r, 0.15f, 0.50f),
                    name = prop.name,
                    transform = prop
                });
                return;
            }
        }

        // 2. Chế độ AutoDetect: Bỏ qua các loại cỏ nền, mầm cỏ, hoa mềm, thảm rêu
        if (IsPassableFlora(name))
        {
            return;
        }

        // 3. Phân loại theo tên mô hình và kích thước thực tế (Chỉ các vật thể cứng thực sự: Thân cây, Gốc cây, Tảng đá, Khúc gỗ)
        float obstacleRadius = -1f;

        // Gốc cây (Stump) - vd: MossySproutStump
        if (name.IndexOf("Stump", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            obstacleRadius = Mathf.Clamp(0.10f * (prop.localScale.x / 0.35f), 0.08f, 0.15f);
        }
        // Tảng đá / Khối đá (Boulder, Rock, Slab) - vd: MossyForestBoulders, HighlandBoulderSlabs
        else if (name.IndexOf("Boulder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("Rock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("Slab", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            obstacleRadius = Mathf.Clamp(0.14f * (prop.localScale.x / 0.35f), 0.10f, 0.20f);
        }
        // Khúc gỗ mục / Thân cây nằm (Log) - vd: HollowMossyLog
        else if (name.IndexOf("Log", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            obstacleRadius = Mathf.Clamp(0.12f * (prop.localScale.x / 0.35f), 0.09f, 0.18f);
        }
        // Thân đại thụ & Cây lớn - vd: HeroOak, Beech, Pine, Sentinel, Hero
        else if (name.IndexOf("Oak", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("GreatOak", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("Sentinel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("Beech", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("Pine", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("Hero", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            obstacleRadius = Mathf.Clamp(0.18f * (prop.localScale.x / 1.0f), 0.14f, 0.26f);
        }
        // Cây thân vừa / nhỏ - vd: Birch, Willow, DwarfBirch, Tree
        else if (name.IndexOf("Birch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("Willow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("Tree", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            obstacleRadius = Mathf.Clamp(0.12f * (prop.localScale.x / 1.0f), 0.09f, 0.16f);
        }

        if (obstacleRadius > 0.05f)
        {
            results.Add(new TileObstacle
            {
                worldPos = prop.position,
                radius = obstacleRadius,
                name = prop.name,
                transform = prop
            });
        }
    }

    private static float ComputeDefaultRadius(Transform prop)
    {
        Renderer rend = prop.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            Bounds b = rend.bounds;
            return Mathf.Clamp(Mathf.Min(b.extents.x, b.extents.z) * 0.40f, 0.14f, 0.35f);
        }
        return 0.20f;
    }

    private static bool IsPassableFlora(string name)
    {
        if (string.IsNullOrEmpty(name)) return true;

        return name.IndexOf("Grass", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Tuft", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("FilledGround", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Clover", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Daisy", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Lavender", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("BellFlower", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Wildflower", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Flower", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Flora", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Pebble", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Toadstool", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Mushroom", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Bluebell", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("PrairieFlower", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Patch", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Carpet", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Tussock", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Scatter", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Fern", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Undergrowth", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Heather", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Shrub", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Bush", StringComparison.OrdinalIgnoreCase) >= 0 ||
               (name.IndexOf("Sprout", StringComparison.OrdinalIgnoreCase) >= 0 && name.IndexOf("Stump", StringComparison.OrdinalIgnoreCase) < 0);
    }

    // Trạng thái bay lượn & độ cao
    private bool isAirborne = false;
    private float currentAltitudeOffset = 0.35f;
    private float targetAltitudeOffset = 0.35f;
    private float currentBankAngle = 0f;
    private float currentWorldY = 0f;

    // Cờ bảo vệ ngăn Start() tự ý ghi đè khi đã được cấu hình từ SpeciesData / Code
    private bool isLocomotionExplicitlyConfigured = false;
    private bool isYOffsetExplicitlyConfigured = false;

    public bool IsMoving => isMoving;
    public float YOffset => yOffset;
    public AnimalLocomotionType LocomotionType => locomotionType;
    public bool IsAirborne => isAirborne;
    public float FlightAltitude => flightAltitude;
    public FarmAnimalAI FarmAI => farmAI;

    /// <summary>
    /// Cho phép script bên ngoài (như FarmAnimalAI) yêu cầu dừng ngay các hành vi dạo chơi/bước đi
    /// </summary>
    public void InterruptWandering()
    {
        isMoving = false;
    }

    /// <summary>
    /// Cập nhật tốc độ di chuyển từ AnimalIndividual (hệ thống phẩm cấp / tính cách)
    /// </summary>
    public void SetSpeeds(float walk, float run)
    {
        this.walkSpeed = Mathf.Max(0.1f, walk);
        this.runSpeed = Mathf.Max(0.2f, run);
    }

    /// <summary>
    /// Thiết lập kiểu di chuyển (Đi bộ, Bay lượn) và độ cao bay từ AnimalIndividual / AnimalSpeciesData
    /// </summary>
    public void SetLocomotion(AnimalLocomotionType type, float altitude)
    {
        // KIỂM TRA ĐIỀU KIỆN BAY: Chỉ loài nào có animation "Flap" (đập cánh) mới được phép bay!
        // Con nào không có animation này thì bắt buộc phải di chuyển trên mặt đất bình thường (Ground).
        if (type == AnimalLocomotionType.Flying && !HasFlapAnimation())
        {
            type = AnimalLocomotionType.Ground;
        }

        this.locomotionType = type;
        this.autoDetectFlying = false; // Khi đã được chỉ định (từ SpeciesData / Individual), KHÔNG được tự động biến thành sinh vật bay
        this.isLocomotionExplicitlyConfigured = true;

        if (locomotionType == AnimalLocomotionType.Flying)
        {
            if (altitude > 0.1f)
            {
                this.flightAltitude = altitude;
            }
            this.isAirborne = true;
            this.targetAltitudeOffset = this.flightAltitude;
            this.currentAltitudeOffset = this.flightAltitude;
            PlayStationaryAnimation();
        }
        else
        {
            this.isAirborne = false;
            this.targetAltitudeOffset = this.yOffset;
            this.currentAltitudeOffset = this.yOffset;
            PlayStationaryAnimation();
        }
    }

    /// <summary>
    /// Gán độ cao Y offset tiếp xúc mặt đất thủ công (cho phép ép buộc 0)
    /// </summary>
    public void SetYOffset(float offset, bool disableAutoDetect = true)
    {
        this.yOffset = offset;
        if (disableAutoDetect)
        {
            this.autoDetectYOffset = false;
        }
        this.isYOffsetExplicitlyConfigured = true;

        if (locomotionType != AnimalLocomotionType.Flying)
        {
            this.targetAltitudeOffset = offset;
            this.currentAltitudeOffset = offset;
        }
    }

    // Danh sách phân loại animation
    private readonly List<string> moveClips = new List<string>();
    private readonly List<string> idleClips = new List<string>();

    private void OnEnable()
    {
        if (!ActiveAnimals.Contains(this))
        {
            ActiveAnimals.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveAnimals.Remove(this);
    }

    private void Awake()
    {
        anim = GetComponent<Animation>();
        if (anim == null || anim.GetClipCount() == 0)
        {
            var childAnim = GetComponentInChildren<Animation>();
            if (childAnim != null && childAnim.GetClipCount() > 0)
            {
                anim = childAnim;
            }
        }
        if (anim == null)
        {
            Debug.LogWarning($"[AnimalMovementAI] Không tìm thấy component Animation trên {gameObject.name}");
        }

        // Tích hợp với FarmAnimalAI (Hệ thống phối hợp 2-trong-1)
        // Khi FarmAnimalAI có mặt, AnimalMovementAI đảm nhận toàn bộ việc di chuyển/lượn cánh/né tránh tự do,
        // và nhường quyền điều khiển cho FarmAnimalAI khi diễn kịch bản kết nối nhóm (MeetUp, CircleDance, Buff).
        farmAI = GetComponent<FarmAnimalAI>();

        // - personalRadius tối đa 0.22m để các con thú không bị giật đẩy nhau trên ô hex bán kính 1m
        // - separationSpeed tối đa 2.0m/s để lực tách thú êm ái, không bị trượt giật patin
        // - rotationSpeed tự nhiên 130 - 180 độ/s và turnThresholdAngle 60 độ để vừa đi vừa cua lái mượt mà
        // - minIdleTime / maxIdleTime duy trì nhịp độ sinh động (1.4s - 3.0s), không để thú bị đứng chết dí lâu
        personalRadius = Mathf.Clamp(personalRadius, 0.15f, 0.22f);
        separationSpeed = Mathf.Clamp(separationSpeed, 1.2f, 2.0f);
        rotationSpeed = Mathf.Clamp(rotationSpeed, 130f, 180f);
        turnThresholdAngle = Mathf.Max(turnThresholdAngle, 55f);
        minIdleTime = Mathf.Clamp(minIdleTime, 1.2f, 2.0f);
        maxIdleTime = Mathf.Clamp(maxIdleTime, 2.2f, 3.5f);
        if (minIdleTime > maxIdleTime) minIdleTime = maxIdleTime * 0.6f;
    }

    private void Start()
    {
        worldGen = FindAnyObjectByType<HexWorldGenerator>();

        // Tự động tính toán yOffset nếu bật auto và chưa được cấu hình thủ công
        if (autoDetectYOffset && !isYOffsetExplicitlyConfigured)
        {
            ComputeAutoYOffset();
        }

        // Phân loại các animation clip có sẵn trong model (tự động phát hiện sinh vật bay)
        ClassifyAvailableAnimations();

        // Bắt buộc: nếu không có clip "Flap" (đập cánh) thì phải di chuyển trên mặt đất bình thường
        if (locomotionType == AnimalLocomotionType.Flying && !HasFlapAnimation())
        {
            locomotionType = AnimalLocomotionType.Ground;
        }

        // Khởi tạo trạng thái bay hoặc đi bộ
        if (locomotionType == AnimalLocomotionType.Flying)
        {
            isAirborne = true;
            targetAltitudeOffset = flightAltitude;
            currentAltitudeOffset = flightAltitude;
            maxWanderRadius = Mathf.Clamp(maxWanderRadius, 0.40f, 0.60f);

            // Bật ngay animation bay đập cánh từ frame đầu tiên (không bao giờ để đơ cánh giữa trời)
            string flyClip = GetFlightClip(preferHover: true);
            if (anim != null && !string.IsNullOrEmpty(flyClip) && anim.GetClip(flyClip) != null)
            {
                anim.Play(flyClip);
            }
        }
        else
        {
            isAirborne = false;
            targetAltitudeOffset = yOffset;
            currentAltitudeOffset = yOffset;
            maxWanderRadius = Mathf.Clamp(maxWanderRadius, 0.35f, 0.50f);

            string groundIdle = GetGroundIdleClip();
            if (anim != null && !string.IsNullOrEmpty(groundIdle) && anim.GetClip(groundIdle) != null)
            {
                anim.Play(groundIdle);
            }
        }

        // Khởi tạo tọa độ ô lục giác ban đầu
        PlacedCard pc = GetComponent<PlacedCard>();
        if (pc != null)
        {
            currentHex = pc.placedHex;
        }
        else
        {
            currentHex = HexMetrics.WorldToHex(transform.position);
            if (worldGen != null && worldGen.MapTiles != null && !worldGen.MapTiles.ContainsKey(currentHex))
            {
                float bestDistSqr = float.MaxValue;
                HexCoordinates bestHex = currentHex;
                foreach (var kvp in worldGen.MapTiles)
                {
                    if (kvp.Value == null) continue;
                    Vector3 tilePos = kvp.Value.transform.position;
                    float dx = tilePos.x - transform.position.x;
                    float dz = tilePos.z - transform.position.z;
                    float dSqr = dx * dx + dz * dz;
                    if (dSqr < bestDistSqr)
                    {
                        bestDistSqr = dSqr;
                        bestHex = kvp.Key;
                    }
                }
                currentHex = bestHex;
            }
        }

        // Đảm bảo vị trí ban đầu nằm an toàn trong ô lục giác
        Vector3 curPos = transform.position;
        Vector3 clampedPos = ClampToValidHabitat(curPos, curPos);
        float initSurfaceY = GetSurfaceYAt(clampedPos);
        currentWorldY = initSurfaceY + currentAltitudeOffset;
        transform.position = new Vector3(clampedPos.x, currentWorldY, clampedPos.z);

        // Tách nhẹ vị trí ban đầu nếu mới spawn ra bị trùng khít với con thú khác
        SeparateInitialPosition();

        // Bắt đầu vòng lặp hành vi AI
        PlayStationaryAnimation();
        StartCoroutine(BehaviorLoopRoutine());
    }

    /// <summary>
    /// Phát animation di chuyển (chỉ gọi khi thực sự bước đi)
    /// </summary>
    private void PlayMoveAnimation(string clipName)
    {
        if (anim == null || string.IsNullOrEmpty(clipName)) return;
        if (anim.GetClip(clipName) != null && !anim.IsPlaying(clipName))
        {
            anim.CrossFade(clipName, 0.2f);
        }
    }

    /// <summary>
    /// Phát animation khi con thú đang dừng chân / lơ lửng tại chỗ:
    /// - Khi đang trên không (isAirborne): BẮT BUỘC tiếp tục đập cánh (Flap/Glide)! Tuyệt đối KHÔNG BAO GIỜ ngừng đập cánh giữa trời!
    /// - Khi đang dưới đất: Phát animation nghỉ ngơi (Idle, Ăn cỏ, Ngắm cảnh).
    /// </summary>
    private void PlayStationaryAnimation()
    {
        if (anim == null) return;

        if (locomotionType == AnimalLocomotionType.Flying && isAirborne)
        {
            string hoverClip = GetFlightClip(preferHover: true);
            if (!string.IsNullOrEmpty(hoverClip) && anim.GetClip(hoverClip) != null)
            {
                anim.CrossFade(hoverClip, 0.30f);
            }
        }
        else
        {
            string idleClip = GetGroundIdleClip();
            if (!string.IsNullOrEmpty(idleClip) && anim.GetClip(idleClip) != null)
            {
                anim.CrossFade(idleClip, 0.35f);
            }
        }
    }

    /// <summary>
    /// Wrapper tương thích ngược để các hàm gọi cũ hoạt động chuẩn xác
    /// </summary>
    private void PlayIdleAnimation()
    {
        PlayStationaryAnimation();
    }

    /// <summary>
    /// Tìm clip bay phù hợp nhất (Flap / Glide / Fly):
    /// - preferHover = true: Ưu tiên Flap (đập cánh liên hồi) cho côn trùng/chuồn chuồn, hoặc Glide cho chim
    /// - preferHover = false: Ưu tiên Glide hoặc Fly khi bay tịnh tiến
    /// </summary>
    private string GetFlightClip(bool preferHover = false)
    {
        if (moveClips.Count == 0) return "";

        if (preferHover)
        {
            string flapClip = moveClips.Find(c => c.IndexOf("Flap", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrEmpty(flapClip)) return flapClip;
        }

        string flyClip = moveClips.Find(c =>
            c.IndexOf("Fly", StringComparison.OrdinalIgnoreCase) >= 0 ||
            c.IndexOf("Glide", StringComparison.OrdinalIgnoreCase) >= 0);
        if (!string.IsNullOrEmpty(flyClip)) return flyClip;

        string anyFlight = moveClips.Find(c => c.IndexOf("Flap", StringComparison.OrdinalIgnoreCase) >= 0);
        if (!string.IsNullOrEmpty(anyFlight)) return anyFlight;

        return moveClips[0];
    }

    /// <summary>
    /// Kiểm tra loài này có clip nghỉ ngơi hợp lệ dưới mặt đất không (như Idle, Ăn cỏ, Ngồi...)
    /// (Chuồn chuồn, Châu chấu chỉ có Flap/Glide nên không có ground idle)
    /// </summary>
    private bool HasGroundCapabilities()
    {
        return idleClips.Count > 0;
    }

    /// <summary>
    /// Kiểm tra loài này có clip đi lại dưới mặt cỏ không (Walk, Trot, Hop, Fetch, Bite)
    /// </summary>
    private bool HasGroundWalk()
    {
        return moveClips.Exists(c =>
            c.IndexOf("Walk", StringComparison.OrdinalIgnoreCase) >= 0 ||
            c.IndexOf("Hop", StringComparison.OrdinalIgnoreCase) >= 0 ||
            c.IndexOf("Trot", StringComparison.OrdinalIgnoreCase) >= 0 ||
            c.IndexOf("Fetch", StringComparison.OrdinalIgnoreCase) >= 0 ||
            c.IndexOf("Bite", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    /// <summary>
    /// Lấy clip nghỉ ngơi hợp lệ dưới đất:
    /// - 88%: Dáng đứng cảnh giác thở nhẹ nhàng (Base Idle / Rest)
    /// - 8%: Thỉnh thoảng mới cúi đầu ăn cỏ hoặc ngồi nghỉ (Eating / Graze / Sit)
    /// - 4%: Cử chỉ hiếm (Rear, Jump, Bark...)
    /// </summary>
    private string GetGroundIdleClip()
    {
        if (idleClips.Count == 0) return GetFlightClip(preferHover: true);

        string baseIdle = idleClips.Find(c =>
            c.Equals("Idle", StringComparison.OrdinalIgnoreCase) ||
            c.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0);

        string eatOrSit = idleClips.Find(c =>
            c.IndexOf("Eat", StringComparison.OrdinalIgnoreCase) >= 0 ||
            c.IndexOf("Graze", StringComparison.OrdinalIgnoreCase) >= 0 ||
            c.IndexOf("Sit", StringComparison.OrdinalIgnoreCase) >= 0);

        float roll = Random.value;
        if (roll < 0.88f && !string.IsNullOrEmpty(baseIdle))
        {
            return baseIdle;
        }
        if (roll < 0.96f && !string.IsNullOrEmpty(eatOrSit))
        {
            return eatOrSit;
        }

        return idleClips[Random.Range(0, idleClips.Count)];
    }

    /// <summary>
    /// Lấy clip đi lại dưới mặt đất:
    /// - Đi cự ly ngắn trong ô: Đi bộ thong thả (Walk / Trot / Fetch / Hop / Bite)
    /// - Đi cự ly dài xuyên ô: 50% chạy nhanh (Run) hoặc lượn cánh (Fall/Glide), 50% dạo bước
    /// </summary>
    private string GetGroundMoveClip(bool isLongDistance = false)
    {
        string walkClip = moveClips.Find(c => c.IndexOf("Walk", StringComparison.OrdinalIgnoreCase) >= 0);
        string trotClip = moveClips.Find(c => c.IndexOf("Trot", StringComparison.OrdinalIgnoreCase) >= 0);
        string runClip = moveClips.Find(c => c.IndexOf("Run", StringComparison.OrdinalIgnoreCase) >= 0);
        string hopClip = moveClips.Find(c => c.IndexOf("Fetch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                             c.IndexOf("Hop", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                             c.IndexOf("Bite", StringComparison.OrdinalIgnoreCase) >= 0);
        string glideClip = moveClips.Find(c => c.IndexOf("Fall", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                              c.IndexOf("Glide", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                              c.IndexOf("Fly", StringComparison.OrdinalIgnoreCase) >= 0);

        if (!isLongDistance)
        {
            if (!string.IsNullOrEmpty(walkClip)) return walkClip;
            if (!string.IsNullOrEmpty(trotClip)) return trotClip;
            if (!string.IsNullOrEmpty(hopClip)) return hopClip;
        }
        else
        {
            if (Random.value < 0.50f && !string.IsNullOrEmpty(runClip))
            {
                return runClip;
            }
            if (Random.value < 0.50f && !string.IsNullOrEmpty(glideClip))
            {
                return glideClip;
            }
            if (!string.IsNullOrEmpty(walkClip)) return walkClip;
            if (!string.IsNullOrEmpty(trotClip)) return trotClip;
            if (!string.IsNullOrEmpty(hopClip)) return hopClip;
        }

        if (moveClips.Count > 0) return moveClips[0];
        if (idleClips.Count > 0) return idleClips[0];
        return GetFlightClip(false);
    }

    /// <summary>
    /// Đảm bảo khi mới sinh ra (spawn), các con thú không bị trồng cây chuối lên cùng một tọa độ
    /// </summary>
    private void SeparateInitialPosition()
    {
        // 1. Tách khỏi các con thú khác nếu bị trùng tọa độ khi spawn
        for (int i = 0; i < ActiveAnimals.Count; i++)
        {
            AnimalMovementAI other = ActiveAnimals[i];
            if (other == null || other == this) continue;

            // Nếu một con bay cao và một con ở mặt đất -> chênh lệch Y > 0.65m -> Bỏ qua va chạm
            float heightDiff = Mathf.Abs(transform.position.y - other.transform.position.y);
            if (heightDiff > 0.65f) continue;

            Vector3 diff = transform.position - other.transform.position;
            diff.y = 0f;
            float dist = diff.magnitude;
            float minAllowed = personalRadius + other.personalRadius;

            if (dist < minAllowed)
            {
                int myIdx = ActiveAnimals.IndexOf(this);
                float angle = (myIdx * (360f / Mathf.Max(ActiveAnimals.Count, 3))) * Mathf.Deg2Rad;
                Vector3 scatter = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (personalRadius * 0.8f);
                Vector3 scatteredPos = ClampToValidHabitat(transform.position + scatter, transform.position);
                float surfaceY = GetSurfaceYAt(scatteredPos);
                currentWorldY = surfaceY + currentAltitudeOffset;
                transform.position = new Vector3(scatteredPos.x, currentWorldY, scatteredPos.z);
                break;
            }
        }

        // 2. Tách khỏi gốc cây, tảng đá nếu spawn đè lên prop
        if (locomotionType != AnimalLocomotionType.Flying || !isAirborne)
        {
            Vector3 center = HexMetrics.HexToWorldPosition(currentHex, transform.position.y);
            for (int pass = 0; pass < 3; pass++)
            {
                GetNearbyObstacles(transform.position, 1.0f, tempNearbyObstacles);
                bool adjusted = false;
                for (int o = 0; o < tempNearbyObstacles.Count; o++)
                {
                    TileObstacle obs = tempNearbyObstacles[o];
                    Vector3 diff = transform.position - obs.worldPos;
                    diff.y = 0f;
                    float dist = diff.magnitude;
                    float minSafe = obs.radius + PropObstacleBodyRadius;
                    if (dist < minSafe)
                    {
                        Vector3 pushNorm = (dist > 0.001f) ? (diff / dist) : (transform.position - center).normalized;
                        if (pushNorm.sqrMagnitude < 0.001f) pushNorm = Vector3.forward;
                        Vector3 safePos = obs.worldPos + pushNorm * (minSafe + 0.04f);
                        Vector3 clamped = ClampToValidHabitat(safePos, transform.position);
                        float surfaceY = GetSurfaceYAt(clamped);
                        currentWorldY = surfaceY + currentAltitudeOffset;
                        transform.position = new Vector3(clamped.x, currentWorldY, clamped.z);
                        adjusted = true;
                        break;
                    }
                }
                if (!adjusted) break;
            }
        }
    }

    /// <summary>
    /// Chống đè lấn liên tục mỗi frame (Always-Active Overlap Resolution):
    /// Chạy ở LateUpdate để xử lý sau mọi di chuyển. Nếu 2 con thú bất kỳ đứng quá gần nhau,
    /// chúng sẽ bị đẩy dạt ra ngoài một cách êm ái, kể cả khi cả 2 đang đứng yên (Idle).
    /// </summary>
    private void LateUpdate()
    {
        // Khi FarmAnimalAI đang điều khiển kịch bản kết nối nhóm (MeetUp, CircleDance, Combo, Cooldown scatter)
        // tạm thời không can thiệp ResolveOverlaps để tránh đẩy dạt thú ra khỏi vòng múa tròn
        if (farmAI != null && farmAI.currentState != AnimalState.WanderRandom) return;

        ResolveOverlaps();
    }

    private void ResolveOverlaps()
    {
        if (ActiveAnimals.Count <= 1) return;

        Vector3 myPos = transform.position;
        Vector3 totalPush = Vector3.zero;
        int overlapCount = 0;

        for (int i = 0; i < ActiveAnimals.Count; i++)
        {
            AnimalMovementAI other = ActiveAnimals[i];
            if (other == null || other == this) continue;

            // Nếu một con đang bay cao và một con ở mặt đất -> chênh lệch Y > 0.65m -> Không đè lấn
            float heightDiff = Mathf.Abs(myPos.y - other.transform.position.y);
            if (heightDiff > 0.65f) continue;

            Vector3 otherPos = other.transform.position;
            Vector3 diff = myPos - otherPos;
            diff.y = 0f;

            float dist = diff.magnitude;
            float minAllowedDist = personalRadius + other.personalRadius;

            if (dist < minAllowedDist)
            {
                Vector3 pushDir;
                if (dist < 0.001f)
                {
                    // Trùng tọa độ chính xác: Dùng index phân tán hướng đẩy đối xứng
                    int idx = ActiveAnimals.IndexOf(this);
                    float angle = (idx * (360f / Mathf.Max(ActiveAnimals.Count, 3))) * Mathf.Deg2Rad;
                    pushDir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)).normalized;
                    dist = 0.001f;
                }
                else
                {
                    pushDir = diff / dist;
                }

                // Độ nông sâu bị đè lấn (Penetration)
                float overlap = minAllowedDist - dist;
                if (overlap > 0.015f) // Ngưỡng đệm triệt tiêu hoàn toàn vi rung giật micro-jitter
                {
                    totalPush += pushDir * overlap;
                    overlapCount++;
                }
            }
        }

        // LƯU Ý QUAN TRỌNG: KHÔNG BAO GIỜ đẩy dạt vật cản tĩnh (cây, đá, gốc cây) ở LateUpdate!
        // Chướng ngại vật tĩnh không chuyển động. Việc đẩy liên tục ở LateUpdate đối đầu với ClampToValidHabitat
        // hoặc các chướng ngại vật đối diện chính là nguyên nhân sinh ra hiện tượng thú bị giật rung lắc liên tục.

        if (overlapCount > 0)
        {
            Vector3 avgPush = totalPush / overlapCount;
            Vector3 targetPos = myPos + avgPush;

            // Đẩy dạt ra mượt mà theo thời gian
            Vector3 pushedPos = Vector3.MoveTowards(myPos, targetPos, separationSpeed * Time.deltaTime);

            // Giữ vị trí trong vùng an toàn của Habitat (không bao giờ để lực đẩy đẩy thú ra rìa ô)
            Vector3 newPos = ClampToValidHabitat(pushedPos, myPos);

            // Cập nhật độ cao và bồng bềnh
            UpdateElevationAndBanking(newPos, Vector3.zero);
            UpdateCurrentHex();
        }
    }

    /// <summary>
    /// Cập nhật độ cao Y mượt mà (bám theo bề mặt ô, độ cao bay, nhấp nhô sin wave)
    /// và góc nghiêng cánh Roll Z khi bo cua
    /// </summary>
    private void UpdateElevationAndBanking(Vector3 flatPos, Vector3 moveDir)
    {
        // 1. Độ cao mục tiêu
        float surfaceY = GetSurfaceYAt(flatPos);
        currentAltitudeOffset = Mathf.MoveTowards(currentAltitudeOffset, targetAltitudeOffset, altitudeSmoothSpeed * Time.deltaTime);

        float targetWorldY = surfaceY + currentAltitudeOffset;
        currentWorldY = Mathf.MoveTowards(currentWorldY, targetWorldY, altitudeSmoothSpeed * 1.5f * Time.deltaTime);

        // Hiệu ứng dập dềnh bồng bềnh (Bobbing Sine Wave)
        float bobbing = (locomotionType == AnimalLocomotionType.Flying && isAirborne)
            ? Mathf.Sin(Time.time * flightBobbingFrequency) * flightBobbingAmplitude
            : 0f;

        transform.position = new Vector3(flatPos.x, currentWorldY + bobbing, flatPos.z);

        // 2. Góc nghiêng cánh khi bo cua (Aerodynamic Banking)
        if (locomotionType == AnimalLocomotionType.Flying && isAirborne && moveDir.sqrMagnitude > 0.001f)
        {
            float turnAngle = Vector3.SignedAngle(transform.forward, moveDir, Vector3.up);
            float targetBank = Mathf.Clamp(-turnAngle * 0.45f, -maxBankAngle, maxBankAngle);
            currentBankAngle = Mathf.MoveTowards(currentBankAngle, targetBank, bankSpeed * 25f * Time.deltaTime);
        }
        else
        {
            // Trở về vị trí thăng bằng
            currentBankAngle = Mathf.MoveTowards(currentBankAngle, 0f, bankSpeed * 20f * Time.deltaTime);
            if (locomotionType == AnimalLocomotionType.Flying && isAirborne && !isMoving)
            {
                Vector3 rot = transform.eulerAngles;
                if (Mathf.Abs(Mathf.DeltaAngle(rot.z, currentBankAngle)) > 0.01f)
                {
                    transform.rotation = Quaternion.Euler(rot.x, rot.y, Mathf.MoveTowardsAngle(rot.z, currentBankAngle, bankSpeed * 20f * Time.deltaTime));
                }
            }
        }
    }

    /// <summary>
    /// Dừng hoàn toàn các animation di chuyển (triệt tiêu triệt để hiện tượng chạy tại chỗ treadmill)
    /// và chuyển êm dịu sang animation đứng yên (Stationary / Idle).
    /// </summary>
    private void StopMoveAnimation(string activeMoveClip = null)
    {
        if (anim == null) return;

        if (!string.IsNullOrEmpty(activeMoveClip) && anim[activeMoveClip] != null)
        {
            anim[activeMoveClip].weight = 0f;
        }

        for (int i = 0; i < moveClips.Count; i++)
        {
            string c = moveClips[i];
            if (!string.IsNullOrEmpty(c) && anim[c] != null)
            {
                anim[c].weight = 0f;
            }
        }

        PlayStationaryAnimation();
    }

    /// <summary>
    /// Chọn animation di chuyển phù hợp với trạng thái hiện tại (Bay trên không vs Đậu dưới đất)
    /// - isLongDistance = false: Đi cự ly ngắn trong ô -> Chỉ chọn Walk/Trot, không bao giờ chạy giật
    /// - isLongDistance = true: Chạy vượt ô / cự ly xa -> Cho phép 50% chạy nhanh (Run)
    /// </summary>
    private string PickMoveClip(bool isLongDistance = false)
    {
        if (moveClips.Count == 0) return "";

        if (locomotionType == AnimalLocomotionType.Flying)
        {
            if (isAirborne)
            {
                return GetFlightClip(preferHover: false);
            }
            else
            {
                return GetGroundMoveClip(isLongDistance);
            }
        }

        return GetGroundMoveClip(isLongDistance);
    }

    /// <summary>
    /// Quy trình hạ cánh đậu nghỉ trên mặt đất:
    /// - Từ từ hạ độ cao xuống yOffset
    /// - TRONG SUỐT QUÁ TRÌNH HẠ CÁNH: Cánh vẫn đập/lượn liên tục!
    /// - Chỉ khi chân đã chạm đất (chênh lệch < 6cm): mới chuyển sang dáng đứng xếp cánh (Idle)!
    /// </summary>
    private IEnumerator LandRoutine()
    {
        if (!isAirborne || !HasGroundCapabilities()) yield break;

        targetAltitudeOffset = yOffset;

        // Vẫn vỗ cánh / lượn khi hạ độ cao
        string descentClip = GetFlightClip(preferHover: true);
        if (anim != null && !string.IsNullOrEmpty(descentClip) && anim.GetClip(descentClip) != null)
        {
            anim.CrossFade(descentClip, 0.2f);
        }

        float landTimer = 0f;
        while (Mathf.Abs(currentAltitudeOffset - yOffset) > 0.06f && landTimer < 2.5f)
        {
            if (farmAI != null && farmAI.currentState != AnimalState.WanderRandom) yield break;
            landTimer += Time.deltaTime;
            UpdateElevationAndBanking(transform.position, Vector3.zero);
            yield return null;
        }

        // Đã chạm mặt đất an toàn
        isAirborne = false;
        currentAltitudeOffset = yOffset;

        // Xếp cánh, đứng nghỉ trên cỏ
        string groundIdle = GetGroundIdleClip();
        if (anim != null && !string.IsNullOrEmpty(groundIdle) && anim.GetClip(groundIdle) != null)
        {
            anim.CrossFade(groundIdle, 0.3f);
        }
    }

    /// <summary>
    /// Quy trình cất cánh bay lên trời:
    /// - Bắt đầu vỗ cánh ngay trên mặt đất trước
    /// - Sau đó nâng dần độ cao bay lên flightAltitude
    /// </summary>
    private IEnumerator TakeoffRoutine()
    {
        if (isAirborne) yield break;

        // 1. Vỗ cánh chuẩn bị cất cánh
        string takeoffClip = GetFlightClip(preferHover: true);
        if (anim != null && !string.IsNullOrEmpty(takeoffClip) && anim.GetClip(takeoffClip) != null)
        {
            anim.CrossFade(takeoffClip, 0.2f);
        }

        yield return new WaitForSeconds(0.25f);

        // 2. Nâng độ cao lên không gian
        isAirborne = true;
        targetAltitudeOffset = flightAltitude;

        float takeoffTimer = 0f;
        float targetY = flightAltitude - 0.15f;
        while (currentAltitudeOffset < targetY && takeoffTimer < 2.0f)
        {
            if (farmAI != null && farmAI.currentState != AnimalState.WanderRandom) yield break;
            takeoffTimer += Time.deltaTime;
            UpdateElevationAndBanking(transform.position, Vector3.zero);
            yield return null;
        }
    }

    /// <summary>
    /// Vòng lặp hành vi chính của con thú
    /// </summary>
    private IEnumerator BehaviorLoopRoutine()
    {
        // Chờ nhẹ 0.25s để bản đồ khởi tạo ổn định
        PlayStationaryAnimation();
        yield return new WaitForSeconds(0.25f);

        bool wasInterruptedByFarmAI = false;

        while (true)
        {
            if (anim == null) yield break;

            // Nếu FarmAnimalAI đang thực hiện kịch bản kết nối nhóm (MeetUp, CircleDance, Combo, Cooldown)
            if (farmAI != null && farmAI.currentState != AnimalState.WanderRandom)
            {
                wasInterruptedByFarmAI = true;
                yield return new WaitForSeconds(0.25f);
                continue;
            }

            // Khi vừa kết thúc kịch bản FarmAnimalAI và trở lại tự do: đồng bộ lại vị trí và animation
            if (wasInterruptedByFarmAI)
            {
                wasInterruptedByFarmAI = false;
                UpdateCurrentHex();
                currentWorldY = transform.position.y;
                if (locomotionType == AnimalLocomotionType.Flying)
                {
                    isAirborne = true;
                    targetAltitudeOffset = flightAltitude;
                    currentAltitudeOffset = flightAltitude;
                }
                PlayStationaryAnimation();
            }

            if (locomotionType == AnimalLocomotionType.Flying)
            {
                yield return StartCoroutine(FlyingBehaviorRoutine());
            }
            else
            {
                yield return StartCoroutine(GroundBehaviorRoutine());
            }
        }
    }

    /// <summary>
    /// Vòng lặp hành vi chuyên biệt cho loài bay lượn (Chim, Bướm, Chuồn chuồn)
    /// </summary>
    private IEnumerator FlyingBehaviorRoutine()
    {
        if (isAirborne)
        {
            // === ĐANG TRÊN KHÔNG ===
            bool canLand = HasGroundCapabilities();
            float rand = Random.value;

            // Nếu loài có thể đậu đất (như Chim Công, Chim Vẹt) và trúng tỉ lệ hạ cánh:
            if (canLand && rand < landChance)
            {
                yield return StartCoroutine(LandRoutine());

                // Đậu nghỉ trên cỏ
                float groundWait = Random.Range(minIdleTime, maxIdleTime);
                float elapsed = 0f;
                while (elapsed < groundWait && !isMoving)
                {
                    if (farmAI != null && farmAI.currentState != AnimalState.WanderRandom) yield break;
                    elapsed += Time.deltaTime;
                    UpdateElevationAndBanking(transform.position, Vector3.zero);
                    yield return null;
                }
            }
            else if (rand < 0.85f && moveClips.Count > 0)
            {
                // Bay tịnh tiến sang vị trí mới trên không
                Vector3 targetWorldPos = PickDestination();
                string flyClip = GetFlightClip(preferHover: false);
                float speed = flySpeed;
                yield return StartCoroutine(MoveToTargetRoutine(targetWorldPos, speed, flyClip));
            }
            else
            {
                // Lơ lửng tại chỗ trên không (ĐẬP CÁNH LIÊN HỒI + NHẤP NHÔ BỒNG BỀNH)
                PlayStationaryAnimation();

                float hoverTime = Random.Range(1.8f, 3.5f);
                float elapsed = 0f;
                while (elapsed < hoverTime && !isMoving)
                {
                    if (farmAI != null && farmAI.currentState != AnimalState.WanderRandom) yield break;
                    elapsed += Time.deltaTime;
                    UpdateElevationAndBanking(transform.position, Vector3.zero);
                    yield return null;
                }
            }
        }
        else
        {
            // === ĐANG Ở DƯỚI ĐẤT (ĐẬU NGHỈ) ===
            float rand = Random.value;

            if (rand < 0.40f && HasGroundWalk())
            {
                // Đi bộ dạo chơi trên mặt cỏ (Chim Công sải bước)
                Vector3 targetWorldPos = PickDestination();
                string walkClip = GetGroundMoveClip(isLongDistance: false);
                yield return StartCoroutine(MoveToTargetRoutine(targetWorldPos, walkSpeed, walkClip));
            }
            else if (rand < 0.65f)
            {
                // Đứng ngắm cảnh trên cỏ
                StopMoveAnimation();
                if (Random.value < idleTurnChance)
                {
                    yield return StartCoroutine(IdleLookAroundRoutine());
                }

                float waitTime = Random.Range(minIdleTime, maxIdleTime);
                float elapsed = 0f;
                while (elapsed < waitTime && !isMoving)
                {
                    if (farmAI != null && farmAI.currentState != AnimalState.WanderRandom) yield break;
                    elapsed += Time.deltaTime;
                    UpdateElevationAndBanking(transform.position, Vector3.zero);
                    yield return null;
                }
            }
            else
            {
                // Cất cánh bay lên trời
                yield return StartCoroutine(TakeoffRoutine());
            }
        }
    }

    /// <summary>
    /// Vòng lặp hành vi cho thú trên cạn (Bò, Hươu, Cáo, Chó hoa...)
    /// </summary>
    private IEnumerator GroundBehaviorRoutine()
    {
        // 85% tỉ lệ bước đi dạo chơi, nhịp sống sinh động không bị đóng băng
        bool willMove = (moveClips.Count > 0) && (Random.value > 0.15f);

        if (willMove)
        {
            Vector3 targetWorldPos = PickDestination();
            float moveDistance = Vector3.Distance(transform.position, targetWorldPos);

            if (moveDistance >= 0.08f)
            {
                bool isLongDistance = moveDistance > 0.85f;
                string moveClip = PickMoveClip(isLongDistance);
                float speed = GetSpeedForClip(moveClip);

                yield return StartCoroutine(MoveToTargetRoutine(targetWorldPos, speed, moveClip));
            }

            // Sau khi bước đi, dừng nghỉ tự nhiên 1.4s - 3.0s
            StopMoveAnimation();

            float waitTime = Random.Range(minIdleTime, maxIdleTime);

            if (Random.value < idleTurnChance)
            {
                yield return StartCoroutine(IdleLookAroundRoutine());
            }

            float idleElapsed = 0f;
            while (idleElapsed < waitTime && !isMoving)
            {
                if (farmAI != null && farmAI.currentState != AnimalState.WanderRandom) yield break;
                idleElapsed += Time.deltaTime;
                UpdateElevationAndBanking(transform.position, Vector3.zero);
                yield return null;
            }
        }
        else
        {
            StopMoveAnimation();

            // Nghỉ ngắn rồi nhanh chóng chọn bước đi tiếp theo, không đứng chết dí
            float waitTime = Random.Range(1.0f, 1.8f);

            if (Random.value < idleTurnChance)
            {
                yield return StartCoroutine(IdleLookAroundRoutine());
            }

            float idleElapsed = 0f;
            while (idleElapsed < waitTime && !isMoving)
            {
                if (farmAI != null && farmAI.currentState != AnimalState.WanderRandom) yield break;
                idleElapsed += Time.deltaTime;
                UpdateElevationAndBanking(transform.position, Vector3.zero);
                yield return null;
            }
        }
    }

    /// <summary>
    /// Wrapper tương thích ngược
    /// </summary>
    private IEnumerator MoveToTargetRoutine(float speed, string moveClip)
    {
        Vector3 targetWorldPos = PickDestination();
        return MoveToTargetRoutine(targetWorldPos, speed, moveClip);
    }

    /// <summary>
    /// Quá trình di chuyển: Xoay hướng mượt mà, tịnh tiến, né tránh thông minh và bám mặt cỏ / độ cao bay
    /// Đồng bộ hoàn hảo với Animation (không bao giờ chạy tại chỗ treadmill)
    /// </summary>
    private IEnumerator MoveToTargetRoutine(Vector3 targetWorldPos, float speed, string moveClip)
    {
        isMoving = true;

        // Nếu điểm đến quá gần vị trí hiện tại (< 8cm) thì bỏ qua, không đi
        if (Vector3.Distance(transform.position, targetWorldPos) < 0.08f)
        {
            StopMoveAnimation(moveClip);
            isMoving = false;
            yield break;
        }

        float maxDuration = 5.0f; // Tránh kẹt vô tận
        float elapsed = 0f;
        float blockedTimer = 0f;
        float stuckCheckTimer = 0f;
        int consecutiveStuckCount = 0;
        Vector3 windowStartPos = transform.position;
        Vector3 smoothedAvoidance = Vector3.zero;

        // BƯỚC 1: Xoay đầu về hướng đích trước (chỉ khi đi bộ trên mặt đất)
        // Sinh vật đang bay lượn trên không sẽ xoay tự nhiên kết hợp nghiêng cánh khi bay
        bool shouldTurnInPlace = turnBeforeWalking && (!isAirborne || locomotionType != AnimalLocomotionType.Flying);
        if (shouldTurnInPlace)
        {
            Vector3 initialDir = targetWorldPos - transform.position;
            initialDir.y = 0f;

            if (initialDir.sqrMagnitude > 0.001f)
            {
                PlayIdleAnimation();

                Quaternion faceRot = Quaternion.LookRotation(initialDir);
                while (Quaternion.Angle(transform.rotation, faceRot) > turnThresholdAngle && elapsed < 0.8f)
                {
                    if (farmAI != null && farmAI.currentState != AnimalState.WanderRandom)
                    {
                        StopMoveAnimation(moveClip);
                        isMoving = false;
                        yield break;
                    }
                    elapsed += Time.deltaTime;
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, faceRot, rotationSpeed * Time.deltaTime);
                    UpdateElevationAndBanking(transform.position, Vector3.zero);
                    yield return null;
                }
            }
        }

        // Bắt đầu bước chân / sải cánh -> Kích hoạt animation di chuyển
        PlayMoveAnimation(moveClip);

        // BƯỚC 2: Vừa bước đi vừa né tránh chướng ngại vật động vật, gốc cây và ranh giới ô
        while (elapsed < maxDuration)
        {
            if (farmAI != null && farmAI.currentState != AnimalState.WanderRandom)
            {
                StopMoveAnimation(moveClip);
                isMoving = false;
                yield break;
            }
            elapsed += Time.deltaTime;

            Vector3 currentPos = transform.position;
            Vector3 diff = targetWorldPos - currentPos;
            Vector3 flatDir = new Vector3(diff.x, 0f, diff.z);

            // Đã đến điểm đích (khoảng cách ngang < 6cm)
            if (flatDir.sqrMagnitude < 0.0036f)
            {
                break;
            }

            // Tính toán vector né tránh dạt ngang (Tangential Steering) + kiểm tra bị chắn trực diện bởi thú khác hoặc vật cản
            Vector3 avoidance = ComputeAvoidanceVector(currentPos, flatDir.normalized, out bool isBlockedDirectly);

            // NẾU BỊ CHẮN ĐƯỜNG TRỰC DIỆN (bởi con thú khác đối đầu hoặc gốc cây / tảng đá):
            if (isBlockedDirectly)
            {
                // DỪNG LẠI VÀ TRIỆT TIÊU MOVE ANIMATION NGAY LẬP TỨC (Không bao giờ tạo dáng chạy tại chỗ hoặc rung giật)
                StopMoveAnimation(moveClip);

                blockedTimer += Time.deltaTime;
                // Nếu bị kẹt quá 0.30s -> hủy hành trình để đổi hướng mới
                if (blockedTimer > 0.30f)
                {
                    break;
                }

                UpdateElevationAndBanking(currentPos, Vector3.zero);
                yield return null;
                continue;
            }
            else
            {
                blockedTimer = 0f;
                PlayMoveAnimation(moveClip);
            }

            // Hướng di chuyển: Lọc mượt avoidance để chuyển hướng êm dịu, không giật góc quay
            smoothedAvoidance = Vector3.MoveTowards(smoothedAvoidance, avoidance, 6.0f * Time.deltaTime);

            Vector3 moveDir = flatDir.normalized;
            if (smoothedAvoidance.sqrMagnitude > 0.001f)
            {
                moveDir = (flatDir.normalized + smoothedAvoidance * avoidanceWeight).normalized;
            }

            // Xoay hướng mượt mà về hướng di chuyển thực tế (kết hợp nghiêng cánh Banking)
            if (moveDir.sqrMagnitude > 0.001f)
            {
                Quaternion desiredRot = Quaternion.LookRotation(moveDir);
                if (locomotionType == AnimalLocomotionType.Flying && isAirborne)
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRot * Quaternion.Euler(0f, 0f, currentBankAngle), rotationSpeed * Time.deltaTime);
                }
                else
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRot, rotationSpeed * Time.deltaTime);
                }
            }

            // Tịnh tiến bước chân
            Vector3 step = moveDir * (speed * Time.deltaTime);
            if (step.sqrMagnitude > flatDir.sqrMagnitude)
            {
                step = flatDir;
            }

            // Vị trí ứng viên mới
            Vector3 candidatePos = currentPos + step;

            // CHỐNG XUYÊN THẤU VÀ TRƯỢT THEO BỀ MẶT VẬT CẢN (Hard Collision & Surface Slide)
            if (locomotionType != AnimalLocomotionType.Flying || !isAirborne)
            {
                GetNearbyObstacles(candidatePos, 0.7f, tempNearbyObstacles);
                for (int pass = 0; pass < 2; pass++)
                {
                    bool hadCollision = false;
                    for (int o = 0; o < tempNearbyObstacles.Count; o++)
                    {
                        TileObstacle obs = tempNearbyObstacles[o];
                        Vector3 obsDiff = candidatePos - obs.worldPos;
                        obsDiff.y = 0f;
                        float dist = obsDiff.magnitude;
                        float minSafeDist = obs.radius + PropObstacleBodyRadius;

                        if (dist < minSafeDist)
                        {
                            hadCollision = true;
                            Vector3 pushNorm;
                            if (dist < 0.001f)
                            {
                                Vector3 fromOld = currentPos - obs.worldPos;
                                fromOld.y = 0f;
                                pushNorm = fromOld.sqrMagnitude > 0.001f ? fromOld.normalized : -moveDir;
                            }
                            else
                            {
                                pushNorm = obsDiff / dist;
                            }

                            // 1. Ép vị trí nằm trên chu vi an toàn của vật cản
                            candidatePos = new Vector3(obs.worldPos.x + pushNorm.x * minSafeDist, candidatePos.y, obs.worldPos.z + pushNorm.z * minSafeDist);

                            // 2. Loại bỏ thành phần bước chân đâm xuyên vào lòng vật cản (Surface Slide)
                            float intoNormal = Vector3.Dot(step, -pushNorm);
                            if (intoNormal > 0f)
                            {
                                step += pushNorm * intoNormal;
                            }
                        }
                    }
                    if (!hadCollision) break;
                }
            }

            // Kiểm tra vị trí mới: Phải nằm an toàn trong khu vực Habitat hợp lệ
            Vector3 nextPos = ClampToValidHabitat(candidatePos, currentPos);

            // Nếu vị trí bị chặn đứng (không thể bước tiếp do chạm mép ô hoặc bị kẹt góc)
            float stepDistSqr = (nextPos - currentPos).sqrMagnitude;
            if (stepDistSqr < 0.000025f)
            {
                // Thử trượt dọc viền ô (Tangent Slide) nếu bị chặn bởi mép ô
                Vector3 hexCenter = HexMetrics.HexToWorldPosition(currentHex, currentPos.y);
                Vector3 radial = currentPos - hexCenter;
                radial.y = 0f;
                bool borderSlid = false;
                if (radial.sqrMagnitude > 0.01f)
                {
                    Vector3 tangent = Vector3.Cross(Vector3.up, radial).normalized;
                    Vector3 tangentStep = Vector3.Project(step, tangent);
                    Vector3 slidePos = ClampToValidHabitat(currentPos + tangentStep, currentPos);
                    if ((slidePos - currentPos).sqrMagnitude > 0.000025f)
                    {
                        nextPos = slidePos;
                        borderSlid = true;
                    }
                }

                if (!borderSlid)
                {
                    // Hoàn toàn không thể bước tiếp -> Chuyển sang Idle ngay lập tức và hủy bước sau 0.20s
                    StopMoveAnimation(moveClip);
                    blockedTimer += Time.deltaTime;
                    if (blockedTimer > 0.20f)
                    {
                        break;
                    }
                    UpdateElevationAndBanking(currentPos, Vector3.zero);
                    yield return null;
                    continue;
                }
            }

            // Cập nhật vị trí X, Z và tính độ cao Y bề mặt đất tương ứng + bồng bềnh + banking
            UpdateElevationAndBanking(nextPos, moveDir);
            UpdateCurrentHex();

            // KIỂM TRA ĐỘ DỊCH CHUYỂN THỰC TẾ TRONG KHÔNG GIAN THẾ GIỚI
            // (Windowed Progress Check: Đo độ tiến triển thực sau mỗi cửa sổ 0.25s, không bị lừa bởi rung lắc qua lại)
            stuckCheckTimer += Time.deltaTime;
            if (stuckCheckTimer >= 0.25f)
            {
                float netMoved = Vector3.Distance(transform.position, windowStartPos);
                if (netMoved < 0.03f)
                {
                    consecutiveStuckCount++;
                    if (consecutiveStuckCount >= 2)
                    {
                        // Đã bị kẹt đứng yên hoặc rung giật quá 0.5s -> Thoát để chuyển về Idle và chọn hướng mới
                        break;
                    }
                }
                else
                {
                    consecutiveStuckCount = 0;
                }

                stuckCheckTimer = 0f;
                windowStartPos = transform.position;
            }

            yield return null;
        }

        // KHI KẾT THÚC BƯỚC ĐI (Đến đích, bị chặn, hoặc chạm viền):
        // LUÔN LUÔN dập tắt move clip weight và chuyển về Idle ngay lập tức (nếu không bị FarmAnimalAI can thiệp)!
        StopMoveAnimation(moveClip);
        UpdateCurrentHex();
        isMoving = false;
    }

    /// <summary>
    /// Giữ cho vị trí luôn nằm an toàn trong khu vực Habitat hợp lệ.
    /// Triệt tiêu hoàn toàn hiện tượng rung lắc/giật hình khi chạm mép ô lục giác.
    /// </summary>
    private Vector3 ClampToValidHabitat(Vector3 candidatePos, Vector3 fallbackPos)
    {
        HexCoordinates candHex = HexMetrics.WorldToHex(candidatePos);

        // Trường hợp 1: Vị trí đích nằm trong ô hợp lệ (cùng Habitat)
        if (CanMoveToHex(candHex))
        {
            // Kiểm tra khoảng cách tới tâm ô
            Vector3 hexCenter = HexMetrics.HexToWorldPosition(candHex, candidatePos.y);
            Vector3 fromCenter = candidatePos - hexCenter;
            fromCenter.y = 0f;

            // Nếu ô bên cạnh theo hướng này KHÔNG CÙNG HABITAT -> giới hạn safeHexRadius (0.70m)
            if (fromCenter.magnitude > safeHexRadius)
            {
                // Kiểm tra xem vị trí xa hơn có bước sang ô khác hợp lệ không
                HexCoordinates furtherHex = HexMetrics.WorldToHex(hexCenter + fromCenter.normalized * 1.45f);
                if (!CanMoveToHex(furtherHex))
                {
                    // Ô bên ngoài không hợp lệ -> chặn lại ở bán kính an toàn safeHexRadius
                    fromCenter = fromCenter.normalized * safeHexRadius;
                    return new Vector3(hexCenter.x + fromCenter.x, candidatePos.y, hexCenter.z + fromCenter.z);
                }
            }

            return candidatePos;
        }

        // Trường hợp 2: Vị trí ứng viên đã lọt ra ngoài ô không hợp lệ (đất trống / khác habitat)
        // Kéo về lại sát mép an toàn bên trong ô currentHex
        Vector3 curCenter = HexMetrics.HexToWorldPosition(currentHex, fallbackPos.y);
        Vector3 curOffset = fallbackPos - curCenter;
        curOffset.y = 0f;

        if (curOffset.magnitude > safeHexRadius)
        {
            curOffset = curOffset.normalized * safeHexRadius;
        }

        return new Vector3(curCenter.x + curOffset.x, fallbackPos.y, curCenter.z + curOffset.z);
    }

    /// <summary>
    /// Kiểm tra xem con thú có được phép bước chân vào ô lục giác này không
    /// (Chỉ được phép nếu ô đó tồn tại và CÙNG LOẠI HABITAT)
    /// </summary>
    public bool CanMoveToHex(HexCoordinates hex)
    {
        if (hex == currentHex) return true;
        if (worldGen == null || worldGen.MapTiles == null) return false;
        if (!worldGen.MapTiles.TryGetValue(hex, out GameObject tileObj) || tileObj == null) return false;

        CardData curHabitat = GetCurrentHabitat();
        if (curHabitat == null) return false;

        PlacedCard nCard = HexBiomeClusterConnector.GetHabitatCardOnTile(tileObj);
        if (nCard != null && nCard.cardData != null)
        {
            if (!HexBiomeClusterConnector.AreHabitatsMatching(curHabitat, nCard.cardData))
                return false;

            // Nếu các ô đã phân chia Cụm Biome độc lập (clusterId), động vật chỉ di chuyển trong nội bộ cụm của nó
            if (worldGen.MapTiles.TryGetValue(currentHex, out GameObject curTileObj) && curTileObj != null)
            {
                PlacedCard curCard = HexBiomeClusterConnector.GetHabitatCardOnTile(curTileObj);
                if (curCard != null && curCard.clusterId > 0 && nCard.clusterId > 0 && curCard.clusterId != nCard.clusterId)
                {
                    return false;
                }
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Lấy dữ liệu Habitat của ô hiện tại
    /// </summary>
    private CardData GetCurrentHabitat()
    {
        if (worldGen == null || worldGen.MapTiles == null) return null;
        if (worldGen.MapTiles.TryGetValue(currentHex, out GameObject curTileObj) && curTileObj != null)
        {
            PlacedCard curCard = HexBiomeClusterConnector.GetHabitatCardOnTile(curTileObj);
            if (curCard != null) return curCard.cardData;
        }
        return null;
    }

    /// <summary>
    /// Cập nhật ô lục giác hiện tại dựa trên vị trí thực tế
    /// </summary>
    private void UpdateCurrentHex()
    {
        HexCoordinates hex = HexMetrics.WorldToHex(transform.position);
        if (hex != currentHex && CanMoveToHex(hex))
        {
            currentHex = hex;
        }
    }

    /// <summary>
    /// Tính toán vector né tránh: Né các con thú khác (Tangential Steering) + Đẩy nhẹ vào trong khi áp sát mép viền (Soft Boundary Steering)
    /// </summary>
    private Vector3 ComputeAvoidanceVector(Vector3 currentPos, Vector3 forwardDir, out bool isBlockedDirectly)
    {
        isBlockedDirectly = false;
        Vector3 avoidance = Vector3.zero;

        // 1. NÉ TRÁNH CÁC CON THÚ KHÁC
        for (int i = 0; i < ActiveAnimals.Count; i++)
        {
            AnimalMovementAI other = ActiveAnimals[i];
            if (other == null || other == this) continue;

            // Nếu một con đang bay cao và một con ở mặt đất -> chênh lệch Y > 0.65m -> Không chạm nhau trong không gian 3D
            float heightDiff = Mathf.Abs(transform.position.y - other.transform.position.y);
            if (heightDiff > 0.65f) continue;

            Vector3 otherPos = other.transform.position;
            Vector3 toOther = otherPos - currentPos;
            toOther.y = 0f;

            float dist = toOther.magnitude;
            float combinedRadius = personalRadius + other.personalRadius;
            float detectionRange = combinedRadius * 1.6f;

            if (dist < detectionRange && dist > 0.001f)
            {
                Vector3 toOtherNorm = toOther / dist;
                float forwardDot = Vector3.Dot(forwardDir, toOtherNorm);

                // Con thú kia đang ở bán cầu phía trước
                if (forwardDot > 0.05f)
                {
                    // Vector vuông góc sang 2 bên (Trái / Phải)
                    Vector3 sideDir = Vector3.Cross(Vector3.up, forwardDir).normalized;

                    // Nếu con thú kia lệch về bên phải, ta lách sang trái; ngược lại lách sang phải
                    float sideDot = Vector3.Dot(toOther, sideDir);
                    Vector3 steerSide = (sideDot >= 0f) ? -sideDir : sideDir;

                    // Lực lách càng mạnh khi khoảng cách càng gần
                    float steerUrgency = Mathf.Clamp01((detectionRange - dist) / detectionRange);
                    avoidance += steerSide * (steerUrgency * 1.8f);

                    // Bị chắn trực diện khi chạm sát nhau phía trước mũi (< combinedRadius * 1.02f và forwardDot > 0.60f)
                    if (dist < combinedRadius * 1.02f && forwardDot > 0.60f)
                    {
                        isBlockedDirectly = true;
                    }
                }
                else
                {
                    // Ở bên hông hoặc phía sau: chỉ đẩy dạt ra nhẹ nhàng
                    float repStrength = Mathf.Clamp01((combinedRadius - dist) / combinedRadius);
                    avoidance += (-toOtherNorm) * repStrength;
                }
            }
        }

        // 2. NÉ TRÁNH CHƯỚNG NGẠI VẬT TRÊN Ô (Thân cây, gốc cây nhỏ, tảng đá, khúc gỗ...)
        if (locomotionType != AnimalLocomotionType.Flying || !isAirborne)
        {
            GetNearbyObstacles(currentPos, 1.0f, tempNearbyObstacles);
            for (int o = 0; o < tempNearbyObstacles.Count; o++)
            {
                TileObstacle obs = tempNearbyObstacles[o];
                Vector3 toObs = obs.worldPos - currentPos;
                toObs.y = 0f;
                float dist = toObs.magnitude;
                float minSafe = obs.radius + PropObstacleBodyRadius;
                float detectionDist = minSafe + 0.35f;

                if (dist < detectionDist && dist > 0.001f)
                {
                    Vector3 toObsNorm = toObs / dist;
                    float forwardDot = Vector3.Dot(forwardDir, toObsNorm);

                    // Vật cản nằm ở bán cầu phía trước -> Lực bẻ lái ngang êm ái
                    if (forwardDot > 0.05f)
                    {
                        Vector3 sideDir = Vector3.Cross(Vector3.up, forwardDir).normalized;
                        float sideDot = Vector3.Dot(toObs, sideDir);

                        // Tránh lật hướng né liên tục khi đối đầu trực diện (Hysteresis)
                        Vector3 steerSide;
                        if (Mathf.Abs(sideDot) < 0.03f)
                        {
                            steerSide = (GetHashCode() % 2 == 0) ? -sideDir : sideDir;
                        }
                        else
                        {
                            steerSide = (sideDot >= 0f) ? -sideDir : sideDir;
                        }

                        float urgency = Mathf.Clamp01((detectionDist - dist) / (detectionDist - minSafe));
                        avoidance += steerSide * (urgency * 1.2f);

                        // BỊ CHẶN TRỰC DIỆN:
                        // Chỉ tính là bị chặn trực diện khi đã áp sát (< minSafe + 0.02m) và hướng đi đâm thẳng vào tâm (forwardDot > 0.65f)
                        // Nếu góc chéo, thuật toán né tránh tiếp tuyến sẽ tự động trượt mượt mà lách qua vật cản mà không khựng lại
                        if (dist < minSafe + 0.02f && forwardDot > 0.65f)
                        {
                            isBlockedDirectly = true;
                        }
                    }
                    else
                    {
                        // Ở bên hông hoặc sau lưng: đẩy dạt nhẹ
                        float rep = Mathf.Clamp01((minSafe - dist) / minSafe);
                        avoidance += (-toObsNorm) * (rep * 0.8f);
                    }
                }
            }
        }

        // 3. NÉ TRÁNH BIÊN GIỚI NGOÀI Ô (Soft Boundary Cushion)
        Vector3 hexCenter = HexMetrics.HexToWorldPosition(currentHex, currentPos.y);
        Vector3 fromCenter = currentPos - hexCenter;
        fromCenter.y = 0f;
        float distFromCenter = fromCenter.magnitude;
        float boundaryThreshold = safeHexRadius * 0.80f;

        if (distFromCenter > boundaryThreshold)
        {
            // Kiểm tra xem hướng phía trước có dẫn ra ngoài ô không hợp lệ không
            HexCoordinates aheadHex = HexMetrics.WorldToHex(currentPos + forwardDir * 0.6f);
            if (!CanMoveToHex(aheadHex))
            {
                // Sinh lực đẩy quay ngược về tâm ô
                Vector3 toCenterDir = -fromCenter.normalized;
                float boundaryFactor = Mathf.Clamp01((distFromCenter - boundaryThreshold) / (safeHexRadius - boundaryThreshold));
                avoidance += toCenterDir * (boundaryFactor * 2.0f);
            }
        }

        return avoidance;
    }

    /// <summary>
    /// Chọn điểm đến ngẫu nhiên: Ưu tiên các vị trí thoáng, hoàn toàn nằm trong Habitat hợp lệ
    /// </summary>
    private Vector3 PickDestination()
    {
        HexCoordinates targetHex = currentHex;

        if (worldGen != null && worldGen.MapTiles != null)
        {
            // 1. Xác định Habitat của ô hiện tại
            CardData currentHabitat = GetCurrentHabitat();

            // 2. Tìm tất cả các ô liền kề (trong 6 hướng) có CÙNG HABITAT
            List<HexCoordinates> matchingNeighbors = new List<HexCoordinates>();
            for (int dir = 0; dir < 6; dir++)
            {
                HexCoordinates nHex = currentHex.GetNeighbor(dir);
                if (CanMoveToHex(nHex))
                {
                    matchingNeighbors.Add(nHex);
                }
            }

            // 3. Quyết định đi trong ô hiện tại hay bước sang ô láng giềng
            if (matchingNeighbors.Count > 0 && Random.value > stayInCurrentHexChance)
            {
                targetHex = matchingNeighbors[Random.Range(0, matchingNeighbors.Count)];
            }
        }

        // 4. Lấy độ cao bề mặt của ô đích
        float targetSurfaceY = HexMetrics.TileHeight;
        if (worldGen != null && worldGen.MapTiles.TryGetValue(targetHex, out GameObject tTileObj) && tTileObj != null)
        {
            var (topY, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(tTileObj);
            targetSurfaceY = topY;
        }

        Vector3 hexCenter = HexMetrics.HexToWorldPosition(targetHex, targetSurfaceY);

        // Sinh vật bay đang lượn trên trời: Tự do chọn điểm trên không trong bán kính ô
        if (locomotionType == AnimalLocomotionType.Flying && isAirborne)
        {
            Vector2 randomOffset = Random.insideUnitCircle * maxWanderRadius;
            Vector3 airCandidate = new Vector3(hexCenter.x + randomOffset.x, hexCenter.y, hexCenter.z + randomOffset.y);
            HexCoordinates candHex = HexMetrics.WorldToHex(airCandidate);
            if (CanMoveToHex(candHex))
            {
                lastTargetDestination = airCandidate;
                return airCandidate;
            }
            lastTargetDestination = hexCenter;
            return hexCenter;
        }

        // Động vật trên cạn / chim đậu đất: Dùng hệ thống quét 360 độ (Sector Scan) & Thoát góc kẹp (Backtrack / Escape)
        lastTargetDestination = PickGroundDestination(targetHex, hexCenter);
        return lastTargetDestination;
    }

    /// <summary>
    /// Tìm điểm đến tự nhiên trên mặt đất bằng phương pháp quét 360 độ (16 Sector Scan):
    /// - Đo khoảng cách thông thoáng thực tế theo từng hướng (né tránh cây, đá, gốc cây, mép ô và thú khác).
    /// - Nhận biết các khe hở / hành lang di chuyển thoáng đãng (Clear Corridors).
    /// - Tự động phát hiện góc kẹt (khi phía trước và 2 bên bị vây kín) và kích hoạt cơ chế Backtrack / Escape:
    ///   chủ động quay đầu đi ngược ra bãi cỏ mở phía sau để tự giải thoát, không bao giờ bị đứng yên chết dí.
    /// </summary>
    private Vector3 PickGroundDestination(HexCoordinates targetHex, Vector3 hexCenter)
    {
        Vector3 currentPos = transform.position;
        const int numSectors = 16;
        float maxSearchRange = (targetHex != currentHex) ? 1.75f : maxWanderRadius;

        // Lấy danh sách vật cản trong bán kính lân cận
        GetNearbyObstacles(currentPos, 1.8f, tempNearbyObstacles);

        // Hướng mong muốn chính (Primary Bias Direction)
        Vector3 biasDir;
        if (targetHex != currentHex)
        {
            biasDir = hexCenter - currentPos;
            biasDir.y = 0f;
            biasDir = biasDir.sqrMagnitude > 0.001f ? biasDir.normalized : transform.forward;
        }
        else
        {
            biasDir = preferCameraFacingGlade ? (new Vector3(0f, 0f, -1f) + transform.forward * 0.4f).normalized : transform.forward;
        }

        float bestClearDist = -1f;
        int bestClearIdx = -1;

        Vector3[] sectorDirs = new Vector3[numSectors];
        float[] clearDists = new float[numSectors];

        for (int i = 0; i < numSectors; i++)
        {
            float angleDeg = i * (360f / numSectors);
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            sectorDirs[i] = dir;

            float clearDist = maxSearchRange;

            // 1. Kiểm tra va chạm với vật cản tĩnh (Thân cây, gốc cây, tảng đá, khúc gỗ)
            for (int o = 0; o < tempNearbyObstacles.Count; o++)
            {
                TileObstacle obs = tempNearbyObstacles[o];
                Vector3 toObs = obs.worldPos - currentPos;
                toObs.y = 0f;

                float proj = Vector3.Dot(toObs, dir);
                if (proj > 0f) // Vật cản nằm ở bán cầu phía trước tia quét
                {
                    float perpSqr = toObs.sqrMagnitude - proj * proj;
                    float safeClearance = obs.radius + PropObstacleBodyRadius + 0.02f;

                    if (perpSqr < safeClearance * safeClearance)
                    {
                        // Tia quét cắt qua vành bảo vệ của vật thể
                        float halfChord = Mathf.Sqrt(Mathf.Max(0f, safeClearance * safeClearance - perpSqr));
                        float hitDist = Mathf.Max(0f, proj - halfChord);
                        if (hitDist < clearDist)
                        {
                            clearDist = hitDist;
                        }
                    }
                }
            }

            // 2. Kiểm tra biên giới Habitat hợp lệ và mép an toàn ô
            float stepSize = 0.12f;
            for (float d = stepSize; d <= clearDist; d += stepSize)
            {
                Vector3 samplePos = currentPos + dir * d;
                HexCoordinates sampleHex = HexMetrics.WorldToHex(samplePos);
                if (!CanMoveToHex(sampleHex))
                {
                    clearDist = Mathf.Max(0f, d - stepSize);
                    break;
                }

                // Không vượt quá mép an toàn safeHexRadius của ô đích TRỪ KHI hướng đó dẫn sang ô láng giềng hợp lệ
                Vector3 hCenter = HexMetrics.HexToWorldPosition(sampleHex, samplePos.y);
                Vector3 fromHCenter = samplePos - hCenter;
                fromHCenter.y = 0f;
                if (fromHCenter.magnitude > safeHexRadius)
                {
                    HexCoordinates furtherHex = HexMetrics.WorldToHex(hCenter + fromHCenter.normalized * 1.45f);
                    if (!CanMoveToHex(furtherHex))
                    {
                        clearDist = Mathf.Max(0f, d - stepSize);
                        break;
                    }
                }
            }

            // 3. Kiểm tra các con thú khác (tránh chọn hướng đi đâm vào đồng loại)
            for (int a = 0; a < ActiveAnimals.Count; a++)
            {
                AnimalMovementAI other = ActiveAnimals[a];
                if (other == null || other == this) continue;
                if (Mathf.Abs(currentPos.y - other.transform.position.y) > 0.65f) continue;

                Vector3 toOther = other.transform.position - currentPos;
                toOther.y = 0f;
                float proj = Vector3.Dot(toOther, dir);
                if (proj > 0f)
                {
                    float perpSqr = toOther.sqrMagnitude - proj * proj;
                    float safeSep = personalRadius + other.personalRadius + 0.05f;
                    if (perpSqr < safeSep * safeSep)
                    {
                        float halfChord = Mathf.Sqrt(Mathf.Max(0f, safeSep * safeSep - perpSqr));
                        float hitDist = Mathf.Max(0f, proj - halfChord);
                        if (hitDist < clearDist)
                        {
                            clearDist = hitDist;
                        }
                    }
                }
            }

            clearDists[i] = clearDist;

            if (clearDist > bestClearDist)
            {
                bestClearDist = clearDist;
                bestClearIdx = i;
            }
        }

        // KIỂM TRA TÌNH TRẠNG KẸT VÀ LỰA CHỌN HÀNH LANG DI CHUYỂN:
        // Hướng được xem là "thoáng" khi có thể bước đi tối thiểu 20cm
        const float openThreshold = 0.20f;
        List<int> openSectors = new List<int>();
        for (int i = 0; i < numSectors; i++)
        {
            if (clearDists[i] >= openThreshold)
            {
                openSectors.Add(i);
            }
        }

        // TRƯỜNG HỢP 1: BỊ KẸT TRONG GÓC CỤT (Phía trước và hai bên đều bị vướng cây/đá/mép ô)
        // -> KÍCH HOẠT CƠ CHẾ UNSTUCK / BACKTRACK:
        // Tự động chọn hướng có khoảng cách thông thoáng lớn nhất (thường là lối mở phía sau lưng nơi thú vừa đi vào)
        // để quay đầu bước ra bãi cỏ mở, không bao giờ đứng yên chịu trận!
        if (openSectors.Count == 0 || bestClearDist < openThreshold)
        {
            int escapeIdx = (bestClearIdx >= 0) ? bestClearIdx : 0;
            Vector3 escapeDir = sectorDirs[escapeIdx];
            float escapeDist = Mathf.Clamp(clearDists[escapeIdx] * 0.85f, 0.15f, 0.35f);
            return currentPos + escapeDir * escapeDist;
        }

        // TRƯỜNG HỢP 2: CÓ CÁC HÀNH LANG THOÁNG ĐÃNG (Clear Corridors)
        // Đánh giá điểm số cho từng hành lang thoáng để chọn hướng đi tự nhiên nhất
        int chosenIdx = openSectors[0];
        float bestScore = -float.MaxValue;

        for (int s = 0; s < openSectors.Count; s++)
        {
            int idx = openSectors[s];
            Vector3 dir = sectorDirs[idx];
            float dist = clearDists[idx];

            float biasDot = Vector3.Dot(dir, biasDir);
            float fwdDot = Vector3.Dot(dir, transform.forward);

            // Điểm số: Ưu tiên đường càng thoáng + hợp hướng mong muốn (bãi cỏ / ô láng giềng) + mượt mà với hướng nhìn hiện tại
            float score = (dist / maxSearchRange) * 2.0f
                        + (biasDot + 1f) * 1.2f
                        + (fwdDot + 1f) * 0.6f
                        + Random.Range(0f, 0.5f); // Một chút ngẫu nhiên để bước đi sống động

            if (score > bestScore)
            {
                bestScore = score;
                chosenIdx = idx;
            }
        }

        Vector3 chosenDir = sectorDirs[chosenIdx];
        float chosenClearDist = clearDists[chosenIdx];

        // Sải bước:
        // - Khi bước sang ô láng giềng (targetHex != currentHex): Đi dứt khoát 70% - 95% khoảng cách hành lang thoáng đãng để vượt sang ô mới
        // - Khi dạo chơi trong cùng ô: Đi nhẹ nhàng 45% - 75%
        float minRatio = (targetHex != currentHex) ? 0.70f : 0.45f;
        float maxRatio = (targetHex != currentHex) ? 0.95f : 0.75f;
        float minCap = (targetHex != currentHex) ? 0.80f : 0.20f;
        float stepLength = Random.Range(Mathf.Min(minCap, chosenClearDist * minRatio), chosenClearDist * maxRatio);
        return currentPos + chosenDir * stepLength;
    }

    /// <summary>
    /// Xoay người nhẹ nhàng nhìn quanh khi đang đứng yên (tốc độ chậm rãi, thư thái tự nhiên)
    /// </summary>
    private IEnumerator IdleLookAroundRoutine()
    {
        // Khi đang trên không, không xoay đầu kiểu thú đứng đất
        if (isAirborne) yield break;

        float turnAngle = Random.Range(-20f, 20f);
        Quaternion targetRot = transform.rotation * Quaternion.Euler(0f, turnAngle, 0f);

        float duration = 1.6f;
        float elapsed = 0f;
        Quaternion startRot = transform.rotation;

        while (elapsed < duration && !isMoving)
        {
            if (farmAI != null && farmAI.currentState != AnimalState.WanderRandom) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            UpdateElevationAndBanking(transform.position, Vector3.zero);
            yield return null;
        }
    }

    /// <summary>
    /// Lấy độ cao mặt cỏ tại vị trí thế giới bất kỳ
    /// </summary>
    private float GetSurfaceYAt(Vector3 worldPos)
    {
        HexCoordinates hex = HexMetrics.WorldToHex(worldPos);
        if (worldGen != null && worldGen.MapTiles.TryGetValue(hex, out GameObject tileObj) && tileObj != null)
        {
            var (topY, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(tileObj);
            return topY;
        }
        return HexMetrics.TileHeight;
    }

    /// <summary>
    /// Xác định tốc độ di chuyển tương ứng với tên animation clip
    /// </summary>
    private float GetSpeedForClip(string clipName)
    {
        if (string.IsNullOrEmpty(clipName)) return (locomotionType == AnimalLocomotionType.Flying) ? flySpeed : walkSpeed;

        if (clipName.IndexOf("Run", StringComparison.OrdinalIgnoreCase) >= 0) return runSpeed;
        if (clipName.IndexOf("Swim", StringComparison.OrdinalIgnoreCase) >= 0) return swimSpeed;
        if (clipName.IndexOf("Fly", StringComparison.OrdinalIgnoreCase) >= 0 ||
            clipName.IndexOf("Glide", StringComparison.OrdinalIgnoreCase) >= 0 ||
            clipName.IndexOf("Flap", StringComparison.OrdinalIgnoreCase) >= 0) return flySpeed;
        if (clipName.IndexOf("Fetch", StringComparison.OrdinalIgnoreCase) >= 0 ||
            clipName.IndexOf("Hop", StringComparison.OrdinalIgnoreCase) >= 0 ||
            clipName.IndexOf("Bite", StringComparison.OrdinalIgnoreCase) >= 0) return walkSpeed * 1.15f;

        if (locomotionType == AnimalLocomotionType.Flying && isAirborne) return flySpeed;

        return walkSpeed;
    }

    /// <summary>
    /// Tự động quét và phân loại animation thành 2 nhóm: Di chuyển & Đứng yên
    /// </summary>
    private void ClassifyAvailableAnimations()
    {
        moveClips.Clear();
        idleClips.Clear();

        if (anim == null) return;

        foreach (AnimationState state in anim)
        {
            string cName = state.name;

            // BỎ QUA HOÀN TOÀN Rest_Pose / BindPose (vì đây là T-pose tĩnh của exporter 3D, không phải animation diễn hoạt)
            // BỎ QUA Death / Die để thú không tự nhiên gục chết khi đứng nghỉ
            if (cName.IndexOf("Rest_Pose", StringComparison.OrdinalIgnoreCase) >= 0 ||
                cName.IndexOf("RestPose", StringComparison.OrdinalIgnoreCase) >= 0 ||
                cName.IndexOf("BindPose", StringComparison.OrdinalIgnoreCase) >= 0 ||
                cName.IndexOf("Death", StringComparison.OrdinalIgnoreCase) >= 0 ||
                cName.IndexOf("Die", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            if (IsMovementAnimation(cName))
            {
                moveClips.Add(cName);
            }
            else
            {
                idleClips.Add(cName);
            }
        }

        // KIỂM TRA ĐIỀU KIỆN BAY: Chỉ loài nào có animation clip "Flap" (đập cánh) mới là sinh vật bay (Flying).
        // Bất kỳ loài nào không có animation này đều được ép buộc di chuyển trên mặt đất bình thường (Ground).
        bool hasFlapClip = HasFlapAnimation();

        if (!hasFlapClip)
        {
            locomotionType = AnimalLocomotionType.Ground;
            isAirborne = false;
            targetAltitudeOffset = yOffset;
            currentAltitudeOffset = yOffset;
        }
        else if (autoDetectFlying || locomotionType == AnimalLocomotionType.Flying)
        {
            locomotionType = AnimalLocomotionType.Flying;
            isAirborne = true;
            targetAltitudeOffset = flightAltitude;
            currentAltitudeOffset = flightAltitude;
        }

        // Fallback an toàn nếu không phân loại được
        if (moveClips.Count == 0 && idleClips.Count == 0)
        {
            foreach (AnimationState state in anim)
            {
                if (state.name.IndexOf("Rest", StringComparison.OrdinalIgnoreCase) < 0 &&
                    state.name.IndexOf("Death", StringComparison.OrdinalIgnoreCase) < 0 &&
                    state.name.IndexOf("Die", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    idleClips.Add(state.name);
                }
            }
        }
    }

    /// <summary>
    /// Kiểm tra xem mô hình động vật có animation clip "Flap" (đập cánh) hay không.
    /// Theo thiết kế: Chỉ những loài thực sự có animation "Flap" mới là sinh vật bay (Flying),
    /// còn các loài không có animation này bắt buộc phải di chuyển trên mặt đất bình thường (Ground).
    /// </summary>
    public bool HasFlapAnimation()
    {
        if (anim == null) anim = GetComponentInChildren<Animation>();
        if (anim == null) return false;

        foreach (AnimationState state in anim)
        {
            if (state != null && !string.IsNullOrEmpty(state.name))
            {
                if (state.name.IndexOf("Flap", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private bool IsMovementAnimation(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        return name.IndexOf("Walk", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Run", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Trot", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Swim", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Fly", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Glide", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Flap", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Fetch", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Fall", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Hop", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Bite", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Tự động tính độ cao yOffset nếu model dùng pivot giữa bụng (Scale * 1.0f)
    /// </summary>
    private void ComputeAutoYOffset()
    {
        SkinnedMeshRenderer smr = GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr != null && smr.sharedMesh != null)
        {
            float localMinY = smr.sharedMesh.bounds.min.y;
            float worldMinY = localMinY * transform.localScale.y;
            if (worldMinY < -0.05f)
            {
                yOffset = Mathf.Abs(worldMinY);
            }
        }
        else
        {
            // Fallback theo quy tắc model Meshy AI chuẩn hóa: MinY = -1.0 * scale.y
            yOffset = transform.localScale.y * 1.0f;
        }
    }

    /// <summary>
    /// Vẽ gizmos hỗ trợ debug phạm vi đi lại và bong bóng né tránh trong Editor
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // Vòng màu xanh lá / lục lam: Giới hạn đi lại quanh tâm ô
        Gizmos.color = (locomotionType == AnimalLocomotionType.Flying) ? Color.cyan : Color.green;
        Vector3 center = transform.position;
        center.y = currentWorldY - currentAltitudeOffset;
        Gizmos.DrawWireSphere(center, maxWanderRadius);

        // Vòng màu vàng cam: Vùng đệm cá nhân né va chạm
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, personalRadius);

        // Vòng màu đỏ cam: Các chướng ngại vật props xung quanh được AI nhận diện
        Gizmos.color = new Color(1f, 0.25f, 0.15f, 0.85f);
        List<TileObstacle> debugObs = new List<TileObstacle>();
        GetNearbyObstacles(transform.position, 1.8f, debugObs);
        for (int i = 0; i < debugObs.Count; i++)
        {
            TileObstacle obs = debugObs[i];
            Vector3 obsCenter = obs.worldPos;
            obsCenter.y = transform.position.y;
            Gizmos.DrawWireSphere(obsCenter, obs.radius);
            Gizmos.DrawWireSphere(obsCenter, obs.radius + PropObstacleBodyRadius);
        }

        if (locomotionType == AnimalLocomotionType.Flying)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireCube(new Vector3(center.x, center.y + flightAltitude, center.z), new Vector3(maxWanderRadius * 2f, 0.15f, maxWanderRadius * 2f));
        }

        // Tia màu tím / hồng: Hướng và điểm đích được Sector Scan lựa chọn
        if (lastTargetDestination != Vector3.zero)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, lastTargetDestination);
            Gizmos.DrawWireSphere(lastTargetDestination, 0.08f);
        }
    }
}
