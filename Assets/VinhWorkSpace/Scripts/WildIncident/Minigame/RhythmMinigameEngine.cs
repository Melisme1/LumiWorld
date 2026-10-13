using System;
using System.Collections.Generic;

public enum RhythmDirection
{
    Left = 0,
    Up = 1,
    Right = 2,
    Down = 3
}

public enum RhythmHitGrade
{
    None = 0,
    Miss = 1,
    Good = 2,
    Great = 3,
    Perfect = 4
}

public enum RhythmPhase
{
    ReadyCountdown = 0,
    SequenceInput = 1,   // Pha 1: Người chơi thong thả gõ chuỗi mũi tên (ngọc chưa chạy, không bị ép giờ)
    OrbGliding = 2,      // Pha 2: Gõ xong nốt -> Ngọc bắt đầu lướt sang phải hướng về hồng tâm (1.4s)
    BarResolved = 3,     // Pha 3: Hiển thị kết quả nhịp (Perfect/Great/Good/Miss) trong 0.5s
    Victory = 4,         // Chiến thắng: Harmony >= 100%
    Timeout = 5          // Hết giờ toàn phiên (25s)
}

/// <summary>
/// Lõi tính toán thuần C# (Pure C# Model/Engine) của Minigame 2 - Vũ Điệu Hòa Nhịp:
/// 1. Thiết kế 2 Pha rõ ràng, trực quan tuyệt đối (Two-Phase Flow):
///    - Pha 1: Gõ các phím mũi tên. Người chơi nhìn chuỗi nốt và gõ từng phím mà không bị áp lực ngọc trôi mất.
///    - Pha 2: Gõ xong nốt cuối cùng -> Viên ngọc bắt đầu lướt êm ái về vạch hồng tâm (1.4s).
///      Người chơi căn đúng khoảnh khắc ngọc chạm hồng tâm để BẤM SPACE!
/// 2. Thuật toán Cửa sổ Sai số Thời gian thực (Precision Timing Window):
///    - Perfect: Δt <= 0.065s (+35% Harmony)
///    - Great:   0.065s < Δt <= 0.140s (+22% Harmony)
///    - Good:    0.140s < Δt <= 0.240s (+12% Harmony)
///    - Miss:    Δt > 0.240s hoặc để ngọc trôi qua (0% Harmony)
/// 3. Phản hồi thân thiện, chống bấm nhầm:
///    - Nếu bấm Space khi CHƯA gõ xong mũi tên: Báo nhắc nhở "Hãy gõ xong các mũi tên trước", KHÔNG phạt Miss!
///    - Cơ chế Debounce 160ms chống click đúp / spam nút.
/// 4. Thuật toán sinh chuỗi Procedural theo phẩm cấp AnimalRank (3 đến 6 nốt).
/// </summary>
public class RhythmMinigameEngine
{
    // Cấu hình thời gian & Vị trí đích (Tốc độ chạy giảm xuống, kéo dài 3.6s để giảm độ khó theo yêu cầu)
    public const float OrbFlightDuration = 3.6f;
    public const float TargetBeatNormalizedPos = 0.78f; // Hoa ngọc / ngọc trai ở vị trí 78% thanh
    public float TargetBeatTime => OrbFlightDuration * TargetBeatNormalizedPos; // ~2.81s

    // Ngưỡng sai số vị trí trực quan (Position-based tolerance)
    // So sánh khoảng cách vị trí người chơi dừng ngọc so với hoa ngọc (0.78f)
    public const float PerfectPosThreshold = 0.045f; // Lệch <= 4.5% chiều dài thanh -> PERFECT (+35%)
    public const float GreatPosThreshold   = 0.095f; // Lệch <= 9.5% chiều dài thanh -> GREAT (+24%)
    public const float GoodPosThreshold    = 0.160f; // Lệch <= 16.0% chiều dài thanh -> GOOD (+14%)

    // Tương thích ngược thời gian
    public static float PerfectWindow => OrbFlightDuration * PerfectPosThreshold;
    public static float GreatWindow   => OrbFlightDuration * GreatPosThreshold;
    public static float GoodWindow    => OrbFlightDuration * GoodPosThreshold;

    // Tỉ lệ cộng thanh Hòa Nhịp
    public const float PerfectGain = 0.35f;
    public const float GreatGain   = 0.24f;
    public const float GoodGain    = 0.14f;

    private readonly Random random = new Random();

    // Trạng thái phiên chơi
    public RhythmPhase Phase { get; private set; } = RhythmPhase.ReadyCountdown;
    public AnimalRank TargetRank { get; private set; } = AnimalRank.Common;
    public float TimeRemaining { get; private set; }
    public float TotalDuration { get; private set; }
    public float HarmonyProgress { get; private set; } = 0f; // 0.0 -> 1.0

    // Tiến độ bay của viên ngọc trong Pha 2
    public float OrbElapsed { get; private set; } = 0f;
    public float FrozenOrbProgress { get; private set; } = 0f;

    public float OrbNormalizedProgress
    {
        get
        {
            if (Phase == RhythmPhase.OrbGliding)
            {
                return Math.Min(1.0f, OrbElapsed / OrbFlightDuration);
            }
            if (Phase == RhythmPhase.BarResolved)
            {
                // Khi nhấn Space: đang đứng ở đâu thì ĐỨNG YÊN CHÍNH XÁC Ở ĐÓ!
                return FrozenOrbProgress;
            }
            return 0f;
        }
    }

    // Chuỗi nốt cần gõ
    private readonly List<RhythmDirection> currentSequence = new List<RhythmDirection>();
    public IReadOnlyList<RhythmDirection> CurrentSequence => currentSequence;
    public int CurrentSequenceIndex { get; private set; } = 0;
    public bool IsSequenceCompleted => CurrentSequenceIndex >= currentSequence.Count;

    // Debounce & State Flags
    private float lastSpacePressTime = -99f;
    private bool beatAlreadyStruck = false;
    private float resolveTimer = 0f;

    // Sự kiện C# thuần (Decoupled Observer Pattern)
    public event Action<RhythmPhase> OnPhaseChanged;
    public event Action<IReadOnlyList<RhythmDirection>> OnNewSequence;
    public event Action<int, RhythmDirection, bool> OnNoteStepValidated; // (index, dir, isCorrect)
    public event Action<RhythmHitGrade, float, string> OnBeatGradeEvaluated; // (grade, deltaSeconds, feedbackText)
    public event Action<string> OnPromptHint;                             // Lời nhắc hướng dẫn người chơi
    public event Action<float> OnHarmonyChanged;                          // progress 0..1
    public event Action<bool, string> OnSessionFinished;                  // (isSuccess, message)

    public void Initialize(AnimalRank rank, float duration = 25f)
    {
        TargetRank = rank;
        TotalDuration = duration;
        TimeRemaining = duration;
        HarmonyProgress = 0f;
        OrbElapsed = 0f;
        FrozenOrbProgress = 0f;
        beatAlreadyStruck = false;
        lastSpacePressTime = -99f;

        Phase = RhythmPhase.ReadyCountdown;
        OnPhaseChanged?.Invoke(Phase);
    }

    public void StartPlaying()
    {
        StartNewSequenceCycle();
    }

    public void Tick(float deltaTime)
    {
        if (Phase == RhythmPhase.Victory || Phase == RhythmPhase.Timeout) return;

        // 1. Trừ đồng hồ tổng của phiên chơi
        TimeRemaining -= deltaTime;
        if (TimeRemaining <= 0f)
        {
            TimeRemaining = 0f;
            FinishSession(false, "Hết thời gian! Đàn thú hoang vẫn chưa hoàn toàn hòa nhịp.");
            return;
        }

        // 2. Xử lý theo từng Pha
        if (Phase == RhythmPhase.OrbGliding)
        {
            OrbElapsed += deltaTime;

            // Kết thúc quãng bay của ngọc (ngọc trôi hết thanh 3.6s)
            if (OrbElapsed >= OrbFlightDuration)
            {
                if (!beatAlreadyStruck)
                {
                    beatAlreadyStruck = true;
                    string reason = IsSequenceCompleted ? "Bỏ lỡ nhịp" : "Chưa kịp gõ nốt";
                    EvaluateBeatStrike(1.0f, isTimeoutMiss: true, missReason: reason);
                }
            }
        }
        else if (Phase == RhythmPhase.BarResolved)
        {
            // Hiển thị kết quả trong 0.6s rồi chuyển chu kỳ tiếp theo
            resolveTimer += deltaTime;
            if (resolveTimer >= 0.60f)
            {
                if (HarmonyProgress >= 1.0f)
                {
                    FinishSession(true, "Tuyệt vời! Giai điệu đã cộng hưởng hoàn hảo với Thú Đầu Đàn.");
                }
                else
                {
                    StartNewSequenceCycle();
                }
            }
        }
    }

    /// <summary>
    /// Xử lý khi người chơi bấm phím hướng (Mũi tên / WASD)
    /// </summary>
    public bool InputDirection(RhythmDirection dir)
    {
        // Cho phép gõ mũi tên ngay khi chu kỳ đang diễn ra (OrbGliding) và chưa hoàn thành chuỗi
        if (Phase != RhythmPhase.OrbGliding || IsSequenceCompleted) return false;

        RhythmDirection expected = currentSequence[CurrentSequenceIndex];
        if (dir == expected)
        {
            int verifiedIndex = CurrentSequenceIndex;
            CurrentSequenceIndex++;
            OnNoteStepValidated?.Invoke(verifiedIndex, dir, true);
            return true;
        }
        else
        {
            // Bấm sai nốt: Reset chuỗi để người chơi gõ lại từ đầu chuỗi này
            int failedIndex = CurrentSequenceIndex;
            CurrentSequenceIndex = 0;
            OnNoteStepValidated?.Invoke(failedIndex, dir, false);
            OnPromptHint?.Invoke("Sai hướng! Hãy gõ lại từ đầu chuỗi.");
            return false;
        }
    }

    /// <summary>
    /// Xử lý khi người chơi bấm phím SPACE hoặc nút Chạm Nhịp
    /// </summary>
    public RhythmHitGrade InputBeatStrike()
    {
        if (Phase == RhythmPhase.Victory || Phase == RhythmPhase.Timeout) return RhythmHitGrade.None;

        if (Phase == RhythmPhase.OrbGliding)
        {
            // Nếu đã gõ nhịp rồi thì không nhận thêm trong chu kỳ này
            if (beatAlreadyStruck) return RhythmHitGrade.None;

            // Debounce chống click đúp / spam nút trong cùng 1 lần bay
            if (lastSpacePressTime >= 0f && (OrbElapsed - lastSpacePressTime) < 0.14f)
            {
                return RhythmHitGrade.None;
            }
            lastSpacePressTime = OrbElapsed;

            // Nếu người chơi bấm Space khi CHƯA gõ xong nốt: KHÔNG PHẠT MISS, ngọc vẫn tiếp tục bay!
            if (!IsSequenceCompleted)
            {
                OnPromptHint?.Invoke("Hãy gõ xong các phím mũi tên trước!");
                return RhythmHitGrade.None;
            }

            beatAlreadyStruck = true;
            // Đóng băng vị trí viên ngọc đúng vị trí người chơi vừa bấm - Đứng yên tại chỗ!
            float hitPos = Math.Min(1.0f, OrbElapsed / OrbFlightDuration);
            FrozenOrbProgress = hitPos;

            return EvaluateBeatStrike(hitPos, isTimeoutMiss: false);
        }

        return RhythmHitGrade.None;
    }

    private RhythmHitGrade EvaluateBeatStrike(float hitPos, bool isTimeoutMiss, string missReason = "")
    {
        if (isTimeoutMiss)
        {
            FrozenOrbProgress = 1.0f;
            OnBeatGradeEvaluated?.Invoke(RhythmHitGrade.Miss, 0f, "MISS");
            Phase = RhythmPhase.BarResolved;
            resolveTimer = 0f;
            OnPhaseChanged?.Invoke(Phase);
            return RhythmHitGrade.Miss;
        }

        // Kiểm tra vị trí người chơi bấm Space dừng ở đâu so với hoa ngọc (TargetBeatNormalizedPos = 0.78f)
        float signedDiff = hitPos - TargetBeatNormalizedPos;
        float deltaPos = (float)Math.Abs(signedDiff);
        RhythmHitGrade grade;
        float gain = 0f;
        string feedbackText;

        if (deltaPos <= PerfectPosThreshold)
        {
            grade = RhythmHitGrade.Perfect;
            gain = PerfectGain;
            feedbackText = "PERFECT! (+35%)";
        }
        else if (deltaPos <= GreatPosThreshold)
        {
            grade = RhythmHitGrade.Great;
            gain = GreatGain;
            feedbackText = "GREAT! (+24%)";
        }
        else if (deltaPos <= GoodPosThreshold)
        {
            grade = RhythmHitGrade.Good;
            gain = GoodGain;
            feedbackText = "GOOD! (+14%)";
        }
        else
        {
            grade = RhythmHitGrade.Miss;
            gain = 0f;
            feedbackText = "MISS";
        }

        if (gain > 0f)
        {
            HarmonyProgress = (float)Math.Min(1.0f, HarmonyProgress + gain);
            OnHarmonyChanged?.Invoke(HarmonyProgress);
        }

        OnBeatGradeEvaluated?.Invoke(grade, deltaPos, feedbackText);

        // Chuyển sang trạng thái BarResolved để hiển thị kết quả trong 0.6s
        Phase = RhythmPhase.BarResolved;
        resolveTimer = 0f;
        OnPhaseChanged?.Invoke(Phase);

        return grade;
    }

    private void StartNewSequenceCycle()
    {
        OrbElapsed = 0f;
        FrozenOrbProgress = 0f;
        CurrentSequenceIndex = 0;
        beatAlreadyStruck = false;
        lastSpacePressTime = -99f;
        resolveTimer = 0f;

        GenerateSequenceForRank(TargetRank);

        // THANH DƯỚI BẮT ĐẦU CHẠY NGAY TỪ ĐẦU LƯỢT!
        Phase = RhythmPhase.OrbGliding;
        OnPhaseChanged?.Invoke(Phase);
        OnNewSequence?.Invoke(currentSequence);
        OnPromptHint?.Invoke("Gõ các phím mũi tên rồi bấm Space khi ngọc chạm hoa!");
    }

    private void GenerateSequenceForRank(AnimalRank rank)
    {
        currentSequence.Clear();

        int noteCount = rank switch
        {
            AnimalRank.Common => 3,
            AnimalRank.Uncommon => 4,
            AnimalRank.Rare => random.Next(4, 6),
            AnimalRank.Epic => 5,
            AnimalRank.Legendary => 6,
            _ => 3
        };

        RhythmDirection prev = (RhythmDirection)(-1);
        int repeatCount = 0;

        for (int i = 0; i < noteCount; i++)
        {
            RhythmDirection candidate;
            int safety = 0;
            do
            {
                candidate = (RhythmDirection)random.Next(0, 4);
                safety++;
            } while (candidate == prev && repeatCount >= 1 && safety < 10);

            if (candidate == prev) repeatCount++;
            else repeatCount = 0;

            prev = candidate;
            currentSequence.Add(candidate);
        }
    }

    private void FinishSession(bool isSuccess, string message)
    {
        Phase = isSuccess ? RhythmPhase.Victory : RhythmPhase.Timeout;
        OnPhaseChanged?.Invoke(Phase);
        OnSessionFinished?.Invoke(isSuccess, message);
    }
}
