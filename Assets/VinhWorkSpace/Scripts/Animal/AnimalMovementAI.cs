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
    [SerializeField] private float rotationSpeed = 220f;

    [Tooltip("Nếu bật: Con thú sẽ quay đầu về hướng đi trước khi bắt đầu bước chân (trông rất tự nhiên)")]
    [SerializeField] private bool turnBeforeWalking = true;

    [Tooltip("Góc lệch tối đa (độ) cho phép thú bắt đầu bước đi khi đang quay")]
    [SerializeField] private float turnThresholdAngle = 30f;

    [Header("Elevation & Offset")]
    [Tooltip("Độ cao nâng Y để chân chạm mặt cỏ (Bù trừ pivot nằm giữa bụng của model 3D)")]
    [SerializeField] private float yOffset = 0.35f;

    [Tooltip("Tự động tính yOffset dựa trên kích thước thực tế của SkinnedMeshRenderer (mặc định tắt để tôn trọng cấu hình loài)")]
    [SerializeField] private bool autoDetectYOffset = false;

    [Header("Wander Settings")]
    [Tooltip("Tỉ lệ % thú chọn đi lại trong CHÍNH Ô HIỆN TẠI thay vì bước sang ô láng giềng (0.6 = 60%)")]
    [Range(0f, 1f)]
    [SerializeField] private float stayInCurrentHexChance = 0.60f;

    [Tooltip("Bán kính tối đa thú có thể đi quanh tâm ô lục giác (mét). Nên để 0.40m - 0.50m để cách xa mép ô an toàn.")]
    [Range(0.2f, 0.65f)]
    [SerializeField] private float maxWanderRadius = 0.45f;

    [Tooltip("Bán kính an toàn tuyệt đối từ tâm ô (mét).")]
    [SerializeField] private float safeHexRadius = 0.78f;

    [Tooltip("Thời gian nghỉ tối thiểu giữa các hành động (giây)")]
    [SerializeField] private float minIdleTime = 2.5f;

    [Tooltip("Thời gian nghỉ tối đa giữa các hành động (giây)")]
    [SerializeField] private float maxIdleTime = 5.5f;

    [Tooltip("Tỉ lệ % thú thỉnh thoảng xoay đầu ngẫu nhiên ngắm cảnh khi đang đứng yên")]
    [Range(0f, 1f)]
    [SerializeField] private float idleTurnChance = 0.40f;

    [Tooltip("Nếu bật: Thú ưu tiên dạo chơi ở nửa trước của ô (hướng về phía Camera) để tránh bị tán cây che khuất")]
    [SerializeField] private bool preferCameraFacingGlade = true;

    [Header("Collision & Overlap Avoidance (Không dùng Collider)")]
    [Tooltip("Bán kính vùng đệm cá nhân của con thú (mét). Khi 2 con chạm vào vùng này sẽ tự động đẩy nhau ra.")]
    [SerializeField] private float personalRadius = 0.38f;

    [Tooltip("Lực né dạt sang bên khi có con thú khác chắn đường")]
    [SerializeField] private float avoidanceWeight = 1.4f;

    [Tooltip("Tốc độ đẩy tách nhau khi bị đè lấn")]
    [SerializeField] private float separationSpeed = 5.0f;

    // Danh sách tĩnh quản lý tất cả các con thú đang hoạt động trong Scene (siêu nhẹ, không tốn CPU)
    private static readonly List<AnimalMovementAI> ActiveAnimals = new List<AnimalMovementAI>();

    // Component nội bộ
    private Animation anim;
    private HexWorldGenerator worldGen;
    private HexCoordinates currentHex;
    private bool isMoving = false;

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
        this.locomotionType = type;
        this.autoDetectFlying = false; // Khi đã được chỉ định (từ SpeciesData / Individual), KHÔNG được tự động biến thành sinh vật bay
        this.isLocomotionExplicitlyConfigured = true;

        if (altitude > 0.1f)
        {
            this.flightAltitude = altitude;
        }
        if (locomotionType == AnimalLocomotionType.Flying)
        {
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
        if (anim == null)
        {
            Debug.LogWarning($"[AnimalMovementAI] Không tìm thấy component Animation trên {gameObject.name}");
        }

        // Tự động vô hiệu hóa FarmAnimalAI cũ nếu còn tồn tại trên prefab để tránh xung đột animation
        FarmAnimalAI oldFarmAI = GetComponent<FarmAnimalAI>();
        if (oldFarmAI != null)
        {
            oldFarmAI.enabled = false;
        }
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
                if (!anim.IsPlaying(hoverClip))
                {
                    anim.CrossFade(hoverClip, 0.2f);
                }
            }
        }
        else
        {
            string idleClip = GetGroundIdleClip();
            if (!string.IsNullOrEmpty(idleClip) && anim.GetClip(idleClip) != null)
            {
                if (!anim.IsPlaying(idleClip))
                {
                    anim.CrossFade(idleClip, 0.25f);
                }
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
    /// Kiểm tra loài này có clip đi lại dưới mặt cỏ không (Walk, Trot, Hop)
    /// </summary>
    private bool HasGroundWalk()
    {
        return moveClips.Exists(c =>
            c.IndexOf("Walk", StringComparison.OrdinalIgnoreCase) >= 0 ||
            c.IndexOf("Hop", StringComparison.OrdinalIgnoreCase) >= 0 ||
            c.IndexOf("Trot", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    /// <summary>
    /// Lấy clip nghỉ ngơi hợp lệ dưới đất
    /// </summary>
    private string GetGroundIdleClip()
    {
        if (idleClips.Count > 0)
        {
            return idleClips[Random.Range(0, idleClips.Count)];
        }
        return GetFlightClip(preferHover: true);
    }

    /// <summary>
    /// Lấy clip đi lại dưới mặt đất
    /// </summary>
    private string GetGroundMoveClip()
    {
        List<string> groundClips = moveClips.FindAll(c =>
            c.IndexOf("Walk", StringComparison.OrdinalIgnoreCase) >= 0 ||
            c.IndexOf("Hop", StringComparison.OrdinalIgnoreCase) >= 0 ||
            c.IndexOf("Trot", StringComparison.OrdinalIgnoreCase) >= 0 ||
            c.IndexOf("Run", StringComparison.OrdinalIgnoreCase) >= 0 ||
            c.IndexOf("Fetch", StringComparison.OrdinalIgnoreCase) >= 0 ||
            c.IndexOf("Fall", StringComparison.OrdinalIgnoreCase) >= 0 ||
            c.IndexOf("Bite", StringComparison.OrdinalIgnoreCase) >= 0);

        if (groundClips.Count > 0)
        {
            return groundClips[Random.Range(0, groundClips.Count)];
        }

        if (moveClips.Count > 0)
        {
            return moveClips[0];
        }

        if (idleClips.Count > 0)
        {
            return idleClips[0];
        }

        return GetFlightClip(false);
    }

    /// <summary>
    /// Đảm bảo khi mới sinh ra (spawn), các con thú không bị trồng cây chuối lên cùng một tọa độ
    /// </summary>
    private void SeparateInitialPosition()
    {
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
    }

    /// <summary>
    /// Chống đè lấn liên tục mỗi frame (Always-Active Overlap Resolution):
    /// Chạy ở LateUpdate để xử lý sau mọi di chuyển. Nếu 2 con thú bất kỳ đứng quá gần nhau,
    /// chúng sẽ bị đẩy dạt ra ngoài một cách êm ái, kể cả khi cả 2 đang đứng yên (Idle).
    /// </summary>
    private void LateUpdate()
    {
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
                totalPush += pushDir * overlap;
                overlapCount++;
            }
        }

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
    /// Chọn animation di chuyển phù hợp với trạng thái hiện tại (Bay trên không vs Đậu dưới đất)
    /// </summary>
    private string PickMoveClip()
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
                return GetGroundMoveClip();
            }
        }

        return moveClips[Random.Range(0, moveClips.Count)];
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

        while (true)
        {
            if (anim == null) yield break;

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
                    elapsed += Time.deltaTime;
                    UpdateElevationAndBanking(transform.position, Vector3.zero);
                    yield return null;
                }
            }
            else if (rand < 0.85f && moveClips.Count > 0)
            {
                // Bay tịnh tiến sang vị trí mới trên không
                string flyClip = GetFlightClip(preferHover: false);
                float speed = flySpeed;
                yield return StartCoroutine(MoveToTargetRoutine(speed, flyClip));
            }
            else
            {
                // Lơ lửng tại chỗ trên không (ĐẬP CÁNH LIÊN HỒI + NHẤP NHÔ BỒNG BỀNH)
                PlayStationaryAnimation();

                float hoverTime = Random.Range(1.8f, 3.5f);
                float elapsed = 0f;
                while (elapsed < hoverTime && !isMoving)
                {
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
                string walkClip = GetGroundMoveClip();
                yield return StartCoroutine(MoveToTargetRoutine(walkSpeed, walkClip));
            }
            else if (rand < 0.65f)
            {
                // Đứng ngắm cảnh trên cỏ
                PlayStationaryAnimation();
                if (Random.value < idleTurnChance)
                {
                    yield return StartCoroutine(IdleLookAroundRoutine());
                }

                float waitTime = Random.Range(minIdleTime, maxIdleTime);
                float elapsed = 0f;
                while (elapsed < waitTime && !isMoving)
                {
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
        bool willMove = (moveClips.Count > 0) && (Random.value > 0.35f);

        if (willMove)
        {
            string moveClip = PickMoveClip();
            float speed = GetSpeedForClip(moveClip);
            yield return StartCoroutine(MoveToTargetRoutine(speed, moveClip));
        }
        else
        {
            PlayStationaryAnimation();

            float waitTime = Random.Range(minIdleTime, maxIdleTime);

            if (Random.value < idleTurnChance)
            {
                yield return StartCoroutine(IdleLookAroundRoutine());
            }

            float idleElapsed = 0f;
            while (idleElapsed < waitTime && !isMoving)
            {
                idleElapsed += Time.deltaTime;
                UpdateElevationAndBanking(transform.position, Vector3.zero);
                yield return null;
            }
        }
    }

    /// <summary>
    /// Quá trình di chuyển: Xoay hướng mượt mà, tịnh tiến, né tránh thông minh và bám mặt cỏ / độ cao bay
    /// Đồng bộ hoàn hảo với Animation (không chạy tại chỗ)
    /// </summary>
    private IEnumerator MoveToTargetRoutine(float speed, string moveClip)
    {
        isMoving = true;
        Vector3 targetWorldPos = PickDestination();

        // Nếu điểm đến quá gần vị trí hiện tại (< 8cm) thì bỏ qua, không đi
        if (Vector3.Distance(transform.position, targetWorldPos) < 0.08f)
        {
            PlayIdleAnimation();
            isMoving = false;
            yield break;
        }

        float maxDuration = 5.0f; // Tránh kẹt vô tận
        float elapsed = 0f;
        float blockedTimer = 0f;
        float realStuckTimer = 0f;
        Vector3 lastObservedPos = transform.position;

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
            elapsed += Time.deltaTime;

            Vector3 currentPos = transform.position;
            Vector3 diff = targetWorldPos - currentPos;
            Vector3 flatDir = new Vector3(diff.x, 0f, diff.z);

            // Đã đến điểm đích (khoảng cách ngang < 6cm)
            if (flatDir.sqrMagnitude < 0.0036f)
            {
                break;
            }

            // Tính toán vector né tránh dạt ngang (Tangential Steering) + kiểm tra bị chắn trực diện
            Vector3 avoidance = ComputeAvoidanceVector(currentPos, flatDir.normalized, out bool isBlockedDirectly);

            // NẾU BỊ CHẮN ĐƯỜNG TRỰC DIỆN (bởi thú khác hoặc gốc cây):
            if (isBlockedDirectly)
            {
                // DỪNG LẠI VÀ CHUYỂN SANG IDLE NGAY LẬP TỨC (Không bao giờ tạo dáng chạy tại chỗ)
                PlayIdleAnimation();

                blockedTimer += Time.deltaTime;
                // Nếu bị kẹt quá 0.4s mà vật cản chưa thoáng -> hủy hành trình để đổi hướng mới
                if (blockedTimer > 0.4f)
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

            // Hướng di chuyển: Kết hợp hướng đích và hướng dạt ngang né tránh
            Vector3 moveDir = flatDir.normalized;
            if (avoidance.sqrMagnitude > 0.001f)
            {
                moveDir = (flatDir.normalized + avoidance * avoidanceWeight).normalized;
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

            // Kiểm tra vị trí mới: Phải nằm an toàn trong khu vực Habitat hợp lệ
            Vector3 candidatePos = currentPos + step;
            Vector3 nextPos = ClampToValidHabitat(candidatePos, currentPos);

            // Nếu vị trí bị chặn đứng bởi mép ô (nextPos == currentPos) -> Thử trượt dọc viền ô (Tangent Slide)
            if ((nextPos - currentPos).sqrMagnitude < 0.00001f)
            {
                Vector3 hexCenter = HexMetrics.HexToWorldPosition(currentHex, currentPos.y);
                Vector3 radial = currentPos - hexCenter;
                radial.y = 0f;
                if (radial.sqrMagnitude > 0.01f)
                {
                    Vector3 tangent = Vector3.Cross(Vector3.up, radial).normalized;
                    Vector3 tangentStep = Vector3.Project(step, tangent);
                    Vector3 slidePos = ClampToValidHabitat(currentPos + tangentStep, currentPos);
                    if ((slidePos - currentPos).sqrMagnitude > 0.00001f)
                    {
                        nextPos = slidePos;
                    }
                }
            }

            // Cập nhật vị trí X, Z và tính độ cao Y bề mặt đất tương ứng + bồng bềnh + banking
            UpdateElevationAndBanking(nextPos, moveDir);
            UpdateCurrentHex();

            // KIỂM TRA ĐỘ DỊCH CHUYỂN THỰC TẾ TRONG KHÔNG GIAN THẾ GIỚI
            // (Khắc phục hoàn toàn lỗi 2 con chạm nhau hoặc kẹt mép khiến animation chạy vẫn kích hoạt nhưng vị trí đứng yên)
            float realMovedDist = Vector3.Distance(transform.position, lastObservedPos);
            if (realMovedDist < 0.012f)
            {
                realStuckTimer += Time.deltaTime;
                if (realStuckTimer > 0.30f)
                {
                    // Đã bị kẹt đứng yên quá 0.30s -> Lập tức thoát để chuyển về Idle
                    break;
                }
            }
            else
            {
                realStuckTimer = 0f;
                lastObservedPos = transform.position;
            }

            yield return null;
        }

        // KHI KẾT THÚC BƯỚC ĐI (Đến đích, bị chặn, hoặc chạm viền):
        // LUÔN LUÔN chuyển về Animation Idle ngay lập tức!
        PlayIdleAnimation();
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
                HexCoordinates furtherHex = HexMetrics.WorldToHex(hexCenter + fromCenter.normalized * 1.05f);
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

                    // Bị chắn trực diện khi chạm sát nhau phía trước mũi (< combinedRadius * 1.05f và forwardDot > 0.20f)
                    if (dist < combinedRadius * 1.05f && forwardDot > 0.20f)
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

        // 2. NÉ TRÁNH GỐC CÂY LỚN TRÊN Ô (Tree Trunk Obstacle Steering)
        // Chỉ áp dụng cho thú đi bộ hoặc sinh vật bay khi đang đậu dưới đất
        if (locomotionType != AnimalLocomotionType.Flying || !isAirborne)
        {
            if (worldGen != null && worldGen.MapTiles != null)
            {
                if (worldGen.MapTiles.TryGetValue(currentHex, out GameObject curTileObj) && curTileObj != null)
                {
                    Renderer[] tileRends = curTileObj.GetComponentsInChildren<Renderer>(false);
                    for (int r = 0; r < tileRends.Length; r++)
                    {
                        Renderer tr = tileRends[r];
                        if (tr == null || tr.transform.IsChildOf(transform)) continue;
                        Bounds b = tr.bounds;
                        // Chỉ xét các cây thân to (chiều cao > 0.8m)
                        if (b.size.y < 0.8f) continue;

                        Vector3 trunkPos = new Vector3(b.center.x, currentPos.y, b.center.z);
                        Vector3 toTrunk = trunkPos - currentPos;
                        float trunkDist = toTrunk.magnitude;

                        if (trunkDist < 0.70f && trunkDist > 0.02f)
                        {
                            Vector3 trunkDir = toTrunk / trunkDist;
                            float dot = Vector3.Dot(forwardDir, trunkDir);
                            if (dot > 0.15f)
                            {
                                Vector3 sideDir = Vector3.Cross(Vector3.up, forwardDir).normalized;
                                float sideDot = Vector3.Dot(toTrunk, sideDir);
                                Vector3 steerSide = (sideDot >= 0f) ? -sideDir : sideDir;
                                avoidance += steerSide * 2.2f;

                                // Chặn lại nếu tiến quá sát vào thân cây
                                if (trunkDist < 0.38f && dot > 0.40f)
                                {
                                    isBlockedDirectly = true;
                                }
                            }
                        }
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

        // 5. Chọn điểm đến phân tán trong bán kính an toàn maxWanderRadius
        Vector3 bestCandidate = hexCenter;
        float bestMinDistSqr = -1f;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * maxWanderRadius;

            // Ưu tiên dạo bước ở nửa trước của ô (hướng Nam/Tây Nam ngập nắng đối diện Camera)
            if (preferCameraFacingGlade && Random.value > 0.35f)
            {
                randomOffset.y -= maxWanderRadius * 0.25f;
                if (randomOffset.magnitude > maxWanderRadius)
                {
                    randomOffset = randomOffset.normalized * maxWanderRadius;
                }
            }

            Vector3 candidatePos = new Vector3(hexCenter.x + randomOffset.x, hexCenter.y, hexCenter.z + randomOffset.y);

            // Điểm đến bắt buộc phải nằm trong ô hợp lệ
            HexCoordinates candHex = HexMetrics.WorldToHex(candidatePos);
            if (!CanMoveToHex(candHex)) continue;

            float closestDistSqr = float.MaxValue;
            for (int i = 0; i < ActiveAnimals.Count; i++)
            {
                AnimalMovementAI other = ActiveAnimals[i];
                if (other == null || other == this) continue;

                // Nếu con thú khác ở độ cao khác > 0.65m (ví dụ một con trên trời một con dưới đất) -> không tính là va chạm
                float heightDiff = Mathf.Abs(candidatePos.y - other.transform.position.y);
                if (heightDiff > 0.65f) continue;

                Vector3 otherPos = other.transform.position;
                float dx = candidatePos.x - otherPos.x;
                float dz = candidatePos.z - otherPos.z;
                float dSqr = dx * dx + dz * dz;
                if (dSqr < closestDistSqr)
                {
                    closestDistSqr = dSqr;
                }
            }

            // Nếu tìm thấy điểm cách xa tất cả các con thú khác > (2 * personalRadius) thì chọn luôn
            float safeSep = (personalRadius * 2f);
            if (closestDistSqr >= safeSep * safeSep)
            {
                return candidatePos;
            }

            if (closestDistSqr > bestMinDistSqr)
            {
                bestMinDistSqr = closestDistSqr;
                bestCandidate = candidatePos;
            }
        }

        return bestCandidate;
    }

    /// <summary>
    /// Xoay người nhẹ nhàng nhìn quanh khi đang đứng yên
    /// </summary>
    private IEnumerator IdleLookAroundRoutine()
    {
        // Khi đang trên không, không xoay đầu kiểu thú đứng đất
        if (isAirborne) yield break;

        float turnAngle = Random.Range(-40f, 40f);
        Quaternion targetRot = transform.rotation * Quaternion.Euler(0f, turnAngle, 0f);

        float duration = 1.0f;
        float elapsed = 0f;
        Quaternion startRot = transform.rotation;

        while (elapsed < duration && !isMoving)
        {
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

        // Tự động nhận diện sinh vật bay nếu có clip bay (Fly / Glide / Flap) và CHƯA bị khóa cấu hình loài
        if (autoDetectFlying && !isLocomotionExplicitlyConfigured && locomotionType == AnimalLocomotionType.Ground)
        {
            bool hasFlyClip = false;
            foreach (var c in moveClips)
            {
                if (c.IndexOf("Fly", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    c.IndexOf("Glide", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    c.IndexOf("Flap", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hasFlyClip = true;
                    break;
                }
            }
            if (hasFlyClip)
            {
                locomotionType = AnimalLocomotionType.Flying;
                isAirborne = true;
                targetAltitudeOffset = flightAltitude;
                currentAltitudeOffset = flightAltitude;
            }
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

        if (locomotionType == AnimalLocomotionType.Flying)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireCube(new Vector3(center.x, center.y + flightAltitude, center.z), new Vector3(maxWanderRadius * 2f, 0.15f, maxWanderRadius * 2f));
        }
    }
}
