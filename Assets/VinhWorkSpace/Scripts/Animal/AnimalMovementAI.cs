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

    // Bán kính tiếp xúc vật lý của cơ thể con thú đối với chướng ngại vật tĩnh
    public const float PropObstacleBodyRadius = AnimalObstacleScanner.PropObstacleBodyRadius;

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

    // Danh sách tái sử dụng nội bộ để tránh cấp phát bộ nhớ (Zero GC Alloc)
    internal readonly List<TileObstacle> tempNearbyObstacles = new List<TileObstacle>();

    // Component nội bộ
    internal Animation anim;
    internal HexWorldGenerator worldGen;
    internal HexCoordinates currentHex;
    internal bool isMoving = false;
    private FarmAnimalAI farmAI;
    internal Vector3 lastTargetDestination = Vector3.zero;

    // Trạng thái bay lượn & độ cao
    internal bool isAirborne = false;
    internal float currentAltitudeOffset = 0.35f;
    internal float targetAltitudeOffset = 0.35f;
    internal float currentBankAngle = 0f;
    internal float currentWorldY = 0f;

    // Cờ bảo vệ ngăn Start() tự ý ghi đè khi đã được cấu hình từ SpeciesData / Code
    private bool isLocomotionExplicitlyConfigured = false;
    private bool isYOffsetExplicitlyConfigured = false;

    // Danh sách phân loại animation
    internal readonly List<string> moveClips = new List<string>();
    internal readonly List<string> idleClips = new List<string>();

    public static List<AnimalMovementAI> GetActiveAnimals() => ActiveAnimals;
    public bool IsMoving => isMoving;
    public float YOffset => yOffset;
    public AnimalLocomotionType LocomotionType => locomotionType;
    public bool IsAirborne => isAirborne;
    public float FlightAltitude => flightAltitude;
    public FarmAnimalAI FarmAI => farmAI;
    public float PersonalRadius => personalRadius;
    public float GetPersonalRadius() => personalRadius;
    public float WalkSpeed => walkSpeed;
    public float RunSpeed => runSpeed;
    public float SwimSpeed => swimSpeed;
    public float FlySpeed => flySpeed;
    public float SeparationSpeed => separationSpeed;
    public float AltitudeSmoothSpeed => altitudeSmoothSpeed;
    public float FlightBobbingFrequency => flightBobbingFrequency;
    public float FlightBobbingAmplitude => flightBobbingAmplitude;
    public float MaxBankAngle => maxBankAngle;
    public float BankSpeed => bankSpeed;
    public float SafeHexRadius => safeHexRadius;
    public float MaxWanderRadius => maxWanderRadius;
    public float StayInCurrentHexChance => stayInCurrentHexChance;
    public bool PreferCameraFacingGlade => preferCameraFacingGlade;
    public bool TurnBeforeWalking => turnBeforeWalking;
    public float TurnThresholdAngle => turnThresholdAngle;
    public float RotationSpeed => rotationSpeed;
    public float AvoidanceWeight => avoidanceWeight;
    public float MinIdleTime => minIdleTime;
    public float MaxIdleTime => maxIdleTime;
    public float IdleTurnChance => idleTurnChance;
    public float LandChance => landChance;

    /// <summary>
    /// Xóa cache khi có ô lục giác bị thay đổi hoặc biến đổi địa hình
    /// </summary>
    public static void InvalidateObstacleCache(HexCoordinates hex)
    {
        AnimalObstacleScanner.InvalidateObstacleCache(hex);
    }

    /// <summary>
    /// Xóa toàn bộ cache chướng ngại vật (ví dụ khi sinh đảo mới)
    /// </summary>
    public static void ClearAllObstacleCaches()
    {
        AnimalObstacleScanner.ClearAllObstacleCaches();
    }

    /// <summary>
    /// Lấy danh sách chướng ngại vật trong phạm vi xung quanh tọa độ kiểm tra
    /// </summary>
    public void GetNearbyObstacles(Vector3 center, float searchRadius, List<TileObstacle> results)
    {
        AnimalObstacleScanner.GetNearbyObstacles(worldGen, center, searchRadius, results);
    }

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
        if (type == AnimalLocomotionType.Flying && !HasFlapAnimation())
        {
            type = AnimalLocomotionType.Ground;
        }

        this.locomotionType = type;
        this.autoDetectFlying = false;
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

        farmAI = GetComponent<FarmAnimalAI>();

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

        if (autoDetectYOffset && !isYOffsetExplicitlyConfigured)
        {
            ComputeAutoYOffset();
        }

        // Phân loại các animation clip có sẵn trong model
        AnimalAnimationClassifier.ClassifyAvailableAnimations(
            anim, moveClips, idleClips,
            ref locomotionType, ref isAirborne, ref targetAltitudeOffset, ref currentAltitudeOffset,
            yOffset, flightAltitude, autoDetectFlying);

        if (locomotionType == AnimalLocomotionType.Flying && !HasFlapAnimation())
        {
            locomotionType = AnimalLocomotionType.Ground;
        }

        if (locomotionType == AnimalLocomotionType.Flying)
        {
            isAirborne = true;
            targetAltitudeOffset = flightAltitude;
            currentAltitudeOffset = flightAltitude;
            maxWanderRadius = Mathf.Clamp(maxWanderRadius, 0.40f, 0.60f);

            string flyClip = AnimalAnimationClassifier.GetFlightClip(moveClips, preferHover: true);
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

            string groundIdle = AnimalAnimationClassifier.GetGroundIdleClip(idleClips, moveClips);
            if (anim != null && !string.IsNullOrEmpty(groundIdle) && anim.GetClip(groundIdle) != null)
            {
                anim.Play(groundIdle);
            }
        }

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

        Vector3 curPos = transform.position;
        Vector3 clampedPos = ClampToValidHabitat(curPos, curPos);
        float initSurfaceY = GetSurfaceYAt(clampedPos);
        currentWorldY = initSurfaceY + currentAltitudeOffset;
        transform.position = new Vector3(clampedPos.x, currentWorldY, clampedPos.z);

        AnimalLocomotionRunner.SeparateInitialPosition(this);

        PlayStationaryAnimation();
        StartCoroutine(AnimalLocomotionRunner.BehaviorLoopRoutine(this));
    }

    internal void PlayMoveAnimation(string clipName)
    {
        if (anim == null || string.IsNullOrEmpty(clipName)) return;
        if (anim.GetClip(clipName) != null && !anim.IsPlaying(clipName))
        {
            anim.CrossFade(clipName, 0.2f);
        }
    }

    internal void PlayStationaryAnimation()
    {
        if (anim == null) return;

        if (locomotionType == AnimalLocomotionType.Flying && isAirborne)
        {
            string hoverClip = AnimalAnimationClassifier.GetFlightClip(moveClips, preferHover: true);
            if (!string.IsNullOrEmpty(hoverClip) && anim.GetClip(hoverClip) != null)
            {
                anim.CrossFade(hoverClip, 0.30f);
            }
        }
        else
        {
            string idleClip = AnimalAnimationClassifier.GetGroundIdleClip(idleClips, moveClips);
            if (!string.IsNullOrEmpty(idleClip) && anim.GetClip(idleClip) != null)
            {
                anim.CrossFade(idleClip, 0.35f);
            }
        }
    }

    internal void PlayIdleAnimation()
    {
        PlayStationaryAnimation();
    }

    internal void StopMoveAnimation(string activeMoveClip = null)
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

    internal string PickMoveClip(bool isLongDistance = false)
    {
        if (moveClips.Count == 0) return "";

        if (locomotionType == AnimalLocomotionType.Flying)
        {
            if (isAirborne)
            {
                return AnimalAnimationClassifier.GetFlightClip(moveClips, preferHover: false);
            }
            else
            {
                return AnimalAnimationClassifier.GetGroundMoveClip(moveClips, idleClips, isLongDistance);
            }
        }

        return AnimalAnimationClassifier.GetGroundMoveClip(moveClips, idleClips, isLongDistance);
    }

    private void LateUpdate()
    {
        if (farmAI != null && farmAI.currentState != AnimalState.WanderRandom) return;
        AnimalLocomotionRunner.ResolveOverlaps(this);
    }

    public Vector3 ClampToValidHabitat(Vector3 candidatePos, Vector3 fallbackPos)
    {
        return AnimalLocomotionRunner.ClampToValidHabitat(this, candidatePos, fallbackPos);
    }

    public float GetSurfaceYAt(Vector3 worldPos)
    {
        return AnimalLocomotionRunner.GetSurfaceYAt(this, worldPos);
    }

    public Vector3 PickDestination()
    {
        return AnimalLocomotionRunner.PickDestination(this);
    }

    public void UpdateElevationAndBanking(Vector3 flatPos, Vector3 moveDir)
    {
        AnimalLocomotionRunner.UpdateElevationAndBanking(this, flatPos, moveDir);
    }

    internal void UpdateCurrentHexFromPosition()
    {
        UpdateCurrentHex();
    }

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

    private void UpdateCurrentHex()
    {
        HexCoordinates hex = HexMetrics.WorldToHex(transform.position);
        if (hex != currentHex && CanMoveToHex(hex))
        {
            currentHex = hex;
        }
    }

    public bool HasFlapAnimation()
    {
        if (anim == null) anim = GetComponentInChildren<Animation>();
        return AnimalAnimationClassifier.HasFlapAnimation(anim);
    }

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
            yOffset = transform.localScale.y * 1.0f;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = (locomotionType == AnimalLocomotionType.Flying) ? Color.cyan : Color.green;
        Vector3 center = transform.position;
        center.y = currentWorldY - currentAltitudeOffset;
        Gizmos.DrawWireSphere(center, maxWanderRadius);

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, personalRadius);

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

        if (lastTargetDestination != Vector3.zero)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, lastTargetDestination);
            Gizmos.DrawWireSphere(lastTargetDestination, 0.08f);
        }
    }
}
