using System;
using UnityEngine;

/// <summary>
/// Các thể loại Minigame tương tác trong LumiWorld:
/// - RhythmResonance: Vũ điệu hòa nhịp (Minigame 2 - Gõ mũi tên & căn vạch rơi)
/// - RockPaperScissors: Kéo - Búa - Bao (Minigame 1 - Đấu trí theo hệ nguyên tố)
/// - FeedingPity: Cho ăn theo xác suất bảo hiểm (Minigame 3 - Nạp thức ăn & Pity)
/// </summary>
public enum MinigameType
{
    None = 0,
    RhythmResonance = 1,
    RockPaperScissors = 2,
    FeedingPity = 3
}

/// <summary>
/// Dữ liệu đầu vào truyền cho phiên Minigame (Context)
/// </summary>
public class MinigameContext
{
    public string SpeciesName { get; set; } = "Linh Thú";
    public AnimalRank Rank { get; set; } = AnimalRank.Common;
    public string BiomeName { get; set; } = "Đất Hoang";
    public CardData CreatureCard { get; set; }
    public WildPackIncident Incident { get; set; }
    public float TimeLimitSeconds { get; set; } = 24f;
}

/// <summary>
/// Kết quả trả về sau khi kết thúc phiên Minigame
/// </summary>
public class MinigameResult
{
    public bool IsSuccess { get; set; }
    public MinigameType Type { get; set; }
    public int Score { get; set; }
    public float Accuracy { get; set; }
    public string Message { get; set; } = "";
}

/// <summary>
/// Interface trừu tượng chuẩn Strategy Pattern cho mọi Minigame Session trong LumiWorld:
/// Đảm bảo nguyên lý Open-Closed Principle (SOLID): Dễ dàng cắm thêm minigame mới mà không sửa code cũ.
/// </summary>
public interface IMinigameSession
{
    MinigameType Type { get; }
    bool IsActive { get; }

    void StartSession(MinigameContext context, Action<MinigameResult> onComplete);
    void CancelSession();
}
