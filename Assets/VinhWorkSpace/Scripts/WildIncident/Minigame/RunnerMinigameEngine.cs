using System;
using System.Collections.Generic;

public enum RunnerEnvironmentMode
{
    Ground = 0, // Trên cạn: chạy nhảy vượt chướng ngại vật (Google Dino)
    Sky = 1     // Trên trời: bay lượn né mây giông / chướng ngại vật trên không
}

public enum RunnerObstacleKind
{
    Rock = 0,
    ThornBush = 1,
    FallenLog = 2,
    StormCloud = 3,
    HighBranch = 4
}

public class RunnerObstacleState
{
    public int Id;
    public RunnerObstacleKind Kind;
    public float X;
    public float Y;
    public float Width;
    public float Height;
    public bool Passed;
}

/// <summary>
/// Lõi tính toán thuần C# cho Minigame Chạy / Bay Vượt Chướng Ngại Vật (ObstacleRunner):
/// - Tách biệt theo môi trường (Trên cạn / Trên trời).
/// - Thanh tiến trình đếm theo thời gian (0 -> 100%) kèm tốc độ tăng dần về cuối.
/// - Khoảng cách sinh chướng ngại vật tính theo quãng đường để luôn có khoảng trống hợp lý.
/// </summary>
public class RunnerMinigameEngine
{
    // Kích thước không gian logic (đơn vị px trên viewport UI)
    public const float TrackWidth = 920f;
    public const float TrackHeight = 290f;
    public const float PlayerX = 110f;
    public const float PlayerSize = 68f;

    // Vật lý Trên Cạn (Ground)
    private const float GroundJumpVelocity = 700f;
    private const float GroundGravity = -1950f;

    // Vật lý Trên Trời (Sky)
    private const float SkyThrustAccel = 1750f;
    private const float SkyGravity = -980f;
    private const float SkyMaxUpVelocity = 420f;
    private const float SkyMaxDownVelocity = -440f;

    // Tốc độ cuộn và hệ số tăng tốc
    private const float BaseScrollSpeed = 380f;
    private const float MaxSpeedBonus = 0.75f; // Cuối thanh đạt 1.75x

    private readonly Random random = new Random();
    private readonly List<RunnerObstacleState> obstacles = new List<RunnerObstacleState>();

    public RunnerEnvironmentMode Mode { get; private set; } = RunnerEnvironmentMode.Ground;
    public float Duration { get; private set; } = 18f;
    public float Elapsed { get; private set; } = 0f;
    public float Progress => Duration > 0f ? Math.Min(1f, Math.Max(0f, Elapsed / Duration)) : 1f;
    public float SpeedMultiplier => 1f + MaxSpeedBonus * Progress;
    public float CurrentScrollSpeed => BaseScrollSpeed * SpeedMultiplier;

    public int MaxLives { get; private set; } = 3;
    public int Lives { get; private set; } = 3;
    public float InvulnerableTimer { get; private set; } = 0f;
    public bool IsInvulnerable => InvulnerableTimer > 0f;
    public bool IsFinished { get; private set; } = false;

    public float PlayerY { get; private set; } = 0f;
    public float VelocityY { get; private set; } = 0f;
    public bool IsGrounded => Mode == RunnerEnvironmentMode.Ground && PlayerY <= 0.01f;
    public bool IsHoldingThrust { get; private set; } = false;

    public int ObstaclesCleared { get; private set; } = 0;
    public IReadOnlyList<RunnerObstacleState> Obstacles => obstacles;

    private int nextObstacleId = 1;
    private float distanceUntilNextSpawn = 360f;
    private float jumpBufferTimer = 0f;

    public event Action OnPlayerJumped;
    public event Action OnPlayerLanded;
    public event Action<RunnerObstacleState> OnObstacleSpawned;
    public event Action<int> OnObstacleDespawned;
    public event Action<int> OnLivesChanged;
    public event Action<float, float> OnProgressChanged; // (progress 0..1, speedMult)
    public event Action<bool, string> OnSessionFinished;

    public void Initialize(RunnerEnvironmentMode mode, float duration = 18f, int maxLives = 3)
    {
        Mode = mode;
        Duration = duration > 1f ? duration : 18f;
        MaxLives = maxLives > 0 ? maxLives : 3;
        Lives = MaxLives;
        Elapsed = 0f;
        InvulnerableTimer = 0f;
        IsFinished = false;
        ObstaclesCleared = 0;
        obstacles.Clear();
        nextObstacleId = 1;

        if (Mode == RunnerEnvironmentMode.Ground)
        {
            PlayerY = 0f;
            VelocityY = 0f;
        }
        else
        {
            PlayerY = (TrackHeight - PlayerSize) * 0.48f;
            VelocityY = 0f;
        }

        IsHoldingThrust = false;
        jumpBufferTimer = 0f;
        distanceUntilNextSpawn = 340f;

        OnLivesChanged?.Invoke(Lives);
        OnProgressChanged?.Invoke(0f, 1f);
    }

    public void SetThrust(bool holding)
    {
        if (IsFinished) return;
        bool wasHolding = IsHoldingThrust;
        IsHoldingThrust = holding;

        if (holding && !wasHolding)
        {
            if (Mode == RunnerEnvironmentMode.Ground)
            {
                jumpBufferTimer = 0.14f; // Cho phép bấm nhảy sớm nhẹ trước khi chạm đất
                TryJump();
            }
            else
            {
                // Tạo cảm giác phản hồi tức thì khi bắt đầu nhấn bay
                if (VelocityY < 160f) VelocityY = 160f;
                OnPlayerJumped?.Invoke();
            }
        }
    }

    public void TryJump()
    {
        if (IsFinished) return;

        if (Mode == RunnerEnvironmentMode.Ground)
        {
            if (IsGrounded)
            {
                VelocityY = GroundJumpVelocity;
                OnPlayerJumped?.Invoke();
            }
        }
        else
        {
            VelocityY = Math.Min(SkyMaxUpVelocity, Math.Max(VelocityY + 210f, 230f));
            OnPlayerJumped?.Invoke();
        }
    }

    public void Tick(float deltaTime)
    {
        if (IsFinished || deltaTime <= 0f) return;

        // Giới hạn dt để tránh nhảy frame làm xuyên hitbox
        float dt = Math.Min(deltaTime, 0.05f);

        Elapsed += dt;
        if (InvulnerableTimer > 0f)
        {
            InvulnerableTimer = Math.Max(0f, InvulnerableTimer - dt);
        }
        if (jumpBufferTimer > 0f)
        {
            jumpBufferTimer = Math.Max(0f, jumpBufferTimer - dt);
        }

        UpdatePlayerPhysics(dt);
        UpdateObstacles(dt);

        OnProgressChanged?.Invoke(Progress, SpeedMultiplier);

        if (Elapsed >= Duration)
        {
            Finish(true, "Victory! You kept pace and tamed the wild creature!");
        }
    }

    private void UpdatePlayerPhysics(float dt)
    {
        if (Mode == RunnerEnvironmentMode.Ground)
        {
            bool wasAirborne = PlayerY > 0.01f;
            VelocityY += GroundGravity * dt;
            PlayerY += VelocityY * dt;

            if (PlayerY <= 0f)
            {
                PlayerY = 0f;
                VelocityY = 0f;
                if (wasAirborne)
                {
                    OnPlayerLanded?.Invoke();
                    if (jumpBufferTimer > 0f)
                    {
                        jumpBufferTimer = 0f;
                        TryJump();
                    }
                }
            }
            else if (PlayerY > TrackHeight - PlayerSize)
            {
                PlayerY = TrackHeight - PlayerSize;
                if (VelocityY > 0f) VelocityY = 0f;
            }
        }
        else
        {
            float accel = IsHoldingThrust ? SkyThrustAccel : SkyGravity;
            VelocityY += accel * dt;
            if (VelocityY > SkyMaxUpVelocity) VelocityY = SkyMaxUpVelocity;
            if (VelocityY < SkyMaxDownVelocity) VelocityY = SkyMaxDownVelocity;

            PlayerY += VelocityY * dt;
            float maxY = TrackHeight - PlayerSize - 8f;
            if (PlayerY < 8f)
            {
                PlayerY = 8f;
                if (VelocityY < 0f) VelocityY = 0f;
            }
            else if (PlayerY > maxY)
            {
                PlayerY = maxY;
                if (VelocityY > 0f) VelocityY = 0f;
            }
        }
    }

    private void UpdateObstacles(float dt)
    {
        float moveDist = CurrentScrollSpeed * dt;

        // 1. Di chuyển chướng ngại vật & kiểm tra va chạm
        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            RunnerObstacleState obs = obstacles[i];
            obs.X -= moveDist;

            if (!obs.Passed && obs.X + obs.Width < PlayerX)
            {
                obs.Passed = true;
                ObstaclesCleared++;
            }

            if (!IsInvulnerable && CheckCollision(obs))
            {
                Lives = Math.Max(0, Lives - 1);
                InvulnerableTimer = 1.0f;
                OnLivesChanged?.Invoke(Lives);

                if (Lives <= 0)
                {
                    Finish(false, "Too many hits! The wild creature escaped!");
                    return;
                }
            }

            if (obs.X + obs.Width < -60f)
            {
                int id = obs.Id;
                obstacles.RemoveAt(i);
                OnObstacleDespawned?.Invoke(id);
            }
        }

        // 2. Sinh chướng ngại vật mới theo quãng đường nhảy thực tế (đủ thời gian rơi xuống & bấm nhảy tiếp)
        distanceUntilNextSpawn -= moveDist;
        if (distanceUntilNextSpawn <= 0f && Elapsed < Duration - 1.3f)
        {
            SpawnObstacle();
            float minGap;
            if (Mode == RunnerEnvironmentMode.Ground)
            {
                float jumpAirTime = (2f * GroundJumpVelocity) / (-GroundGravity); // ~0.72s
                minGap = CurrentScrollSpeed * (jumpAirTime + 0.30f);
            }
            else
            {
                minGap = CurrentScrollSpeed * 0.85f;
            }
            float maxGap = minGap + 190f;
            distanceUntilNextSpawn = minGap + (float)random.NextDouble() * (maxGap - minGap);
        }
    }

    private void SpawnObstacle()
    {
        RunnerObstacleState obs = new RunnerObstacleState
        {
            Id = nextObstacleId++,
            X = TrackWidth + 30f
        };

        if (Mode == RunnerEnvironmentMode.Ground)
        {
            int variant = random.Next(0, 3);
            if (variant == 0)
            {
                obs.Kind = RunnerObstacleKind.Rock;
                obs.Width = 46f;
                obs.Height = 52f;
                obs.Y = 0f;
            }
            else if (variant == 1)
            {
                obs.Kind = RunnerObstacleKind.ThornBush;
                obs.Width = 54f;
                obs.Height = 68f;
                obs.Y = 0f;
            }
            else
            {
                obs.Kind = RunnerObstacleKind.FallenLog;
                obs.Width = 74f;
                obs.Height = 44f;
                obs.Y = 0f;
            }
        }
        else
        {
            bool topBand = random.Next(0, 2) == 0;
            obs.Kind = topBand ? RunnerObstacleKind.HighBranch : RunnerObstacleKind.StormCloud;
            obs.Width = topBand ? 66f : 78f;
            obs.Height = topBand ? 86f : 64f;

            if (topBand)
            {
                // Treo ở nửa trên để người chơi lượn xuống dưới
                obs.Y = TrackHeight - obs.Height - random.Next(12, 48);
            }
            else
            {
                // Lơ lửng ở nửa dưới để người chơi bay lên trên
                obs.Y = random.Next(16, 84);
            }
        }

        obstacles.Add(obs);
        OnObstacleSpawned?.Invoke(obs);
    }

    private bool CheckCollision(RunnerObstacleState obs)
    {
        // Hitbox gọn hơn hình ảnh (token tròn & viền obstacle có phần bo góc) để né cận cảnh công bằng
        float pPad = PlayerSize * 0.24f;
        float pLeft = PlayerX + pPad;
        float pRight = PlayerX + PlayerSize - pPad;
        float pBottom = PlayerY + pPad * 0.7f;
        float pTop = PlayerY + PlayerSize - pPad;

        float oPadX = obs.Width * 0.24f;
        float oPadTop = obs.Height * 0.22f;
        float oLeft = obs.X + oPadX;
        float oRight = obs.X + obs.Width - oPadX;
        float oBottom = obs.Y + obs.Height * 0.06f;
        float oTop = obs.Y + obs.Height - oPadTop;

        return pLeft < oRight && pRight > oLeft && pBottom < oTop && pTop > oBottom;
    }

    private void Finish(bool isSuccess, string message)
    {
        if (IsFinished) return;
        IsFinished = true;
        OnSessionFinished?.Invoke(isSuccess, message);
    }
}
