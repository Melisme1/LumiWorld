using System;
using System.Collections.Generic;
using UnityEngine;
using LumiWorld.Acs;

/// <summary>
/// Quản lý dữ liệu Tiến trình Chăm sóc (Care Progress) và Nâng Rank (Rank I -> II -> III) cho từng cá thể sinh vật:
/// - Mỗi cá thể sinh vật có thanh tiến trình Care riêng biệt (0 - 100).
/// - Khi cho ăn (Feed), thanh Care tăng lên.
/// - Đủ 100 Care + Coins -> Mở khóa nút Nâng Rank (Rank Up) từ Rank I -> Rank II -> Rank III.
/// - Thăng hạng trực tiếp CreatureQualityRank của GDD và tăng đóng góp sản lượng tài nguyên!
/// </summary>
public class CreatureCareManager : MonoBehaviour
{
    private static CreatureCareManager instance;
    private static bool isApplicationQuitting = false;

    public static bool HasInstance => instance != null && !isApplicationQuitting;

    public static CreatureCareManager Instance
    {
        get
        {
            if (isApplicationQuitting) return null;

            if (instance == null)
            {
                instance = FindAnyObjectByType<CreatureCareManager>();
                if (instance == null)
                {
                    if (!Application.isPlaying || isApplicationQuitting)
                        return null;

                    GameObject go = new GameObject("CreatureCareManager");
                    instance = go.AddComponent<CreatureCareManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    private readonly Dictionary<string, int> creatureCareProgress = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public event Action<string, int> OnCareProgressChanged;
    public event Action<string, CreatureQualityRank> OnRankUpgraded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
        isApplicationQuitting = false;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        isApplicationQuitting = false;
        DontDestroyOnLoad(gameObject);
    }

    private void OnApplicationQuit()
    {
        isApplicationQuitting = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public int GetCareProgress(string individualId)
    {
        if (string.IsNullOrEmpty(individualId)) return 0;
        return creatureCareProgress.TryGetValue(individualId, out int care) ? care : 0;
    }

    // Tên người chơi đặt cho từng sinh vật, theo placementId; lưu trong bản lưu đảo
    private readonly Dictionary<string, string> customNames = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Khóa dùng chung cho điểm chăm sóc và tên: placementId của thẻ sinh vật (giống HabitatResident.individualId).
    /// Chỉ khi chưa có HabitatPlacementIdentity mới dùng UniqueID của AnimalIndividual.
    /// </summary>
    public static string KeyOf(AnimalIndividual animal, HabitatPlacementIdentity identity)
    {
        if (identity == null && animal != null) identity = animal.GetComponentInParent<HabitatPlacementIdentity>();
        if (identity != null && !string.IsNullOrEmpty(identity.PlacementId)) return identity.PlacementId;
        return animal != null ? animal.UniqueID : string.Empty;
    }

    public string GetCustomName(string individualId) =>
        !string.IsNullOrEmpty(individualId) && customNames.TryGetValue(individualId, out string name) ? name : null;

    /// <summary>Đặt tên cho sinh vật và đánh dấu cần lưu đảo.</summary>
    public void SetCustomName(string individualId, string name)
    {
        if (string.IsNullOrEmpty(individualId) || WorldLaunch.IsVisit) return;
        if (string.IsNullOrWhiteSpace(name)) customNames.Remove(individualId);
        else customNames[individualId] = name;
        WorldSaveSystem.MarkDirty();
    }

    /// <summary>Nạp điểm chăm sóc và tên từ bản lưu đảo (không phát sự kiện, không ghi save).</summary>
    public void RestoreCreature(string individualId, int careProgress, string customName)
    {
        if (string.IsNullOrEmpty(individualId)) return;
        if (careProgress > 0) creatureCareProgress[individualId] = Mathf.Clamp(careProgress, 0, 100);
        else creatureCareProgress.Remove(individualId);
        if (!string.IsNullOrEmpty(customName)) customNames[individualId] = customName;
    }

    /// <summary>
    /// Chăm sóc tăng Care Progress bằng Coins; không tác động đến độ no hay kho thức ăn.
    /// </summary>
    public bool CareCreature(string individualId, out string feedback)
    {
        feedback = string.Empty;
        // Đảo đang xem của người khác: không chăm sóc, không trừ Coins
        if (WorldLaunch.IsVisit)
        {
            feedback = "Không thể chăm sóc sinh vật trên đảo người khác.";
            return false;
        }
        if (string.IsNullOrEmpty(individualId))
        {
            feedback = "Creature not found.";
            return false;
        }

        const int feedCostCoins = 2;
        if (CurrencyWallet.Instance != null && !CurrencyWallet.Instance.CanAfford(feedCostCoins))
        {
            feedback = $"Not enough Coins to care! Need {feedCostCoins} Coins.";
            return false;
        }

        int current = GetCareProgress(individualId);
        if (current >= 100)
        {
            feedback = "Care bar is full 100%! Click Rank Up.";
            return false;
        }

        // Trừ phí cho ăn; trừ không được thì không chăm (trước đây bỏ qua kết quả nên chăm miễn phí)
        if (CurrencyWallet.Instance != null && !CurrencyWallet.Instance.TrySpend(feedCostCoins, CoinReason.CardPurchase))
        {
            feedback = $"Không đủ Coins để chăm sóc! Cần {feedCostCoins} Coins.";
            return false;
        }

        int newCare = Mathf.Min(100, current + 25);
        creatureCareProgress[individualId] = newCare;
        WorldSaveSystem.MarkDirty();
        OnCareProgressChanged?.Invoke(individualId, newCare);

        feedback = $"Cared! Care: {newCare}/100 (+25)";
        return true;
    }

    /// <summary>
    /// Nâng Rank cho sinh vật (Rank I -> Rank II -> Rank III) khi Care đạt 100%
    /// </summary>
    public bool TryRankUp(AnimalIndividual animal, HabitatPlacementIdentity identity, out string resultInfo)
    {
        resultInfo = string.Empty;
        // Đảo đang xem của người khác: không nâng Rank, không trừ Coins
        if (WorldLaunch.IsVisit)
        {
            resultInfo = "Không thể nâng Rank sinh vật trên đảo người khác.";
            return false;
        }

        if (identity == null && animal != null) identity = animal.GetComponentInParent<HabitatPlacementIdentity>();
        string id = KeyOf(animal, identity);
        if (string.IsNullOrEmpty(id))
        {
            resultInfo = "Missing creature identity.";
            return false;
        }

        int currentCare = GetCareProgress(id);
        if (currentCare < 100)
        {
            resultInfo = $"Not enough Care points! Current: {currentCare}/100.";
            return false;
        }

        CreatureQualityRank currentRank = identity != null ? identity.QualityRank : CreatureQualityRank.RankI;
        if (currentRank >= CreatureQualityRank.RankIII)
        {
            resultInfo = "Creature has reached maximum Rank (Rank III)!";
            return false;
        }

        CreatureQualityRank nextRank = currentRank == CreatureQualityRank.RankI ? CreatureQualityRank.RankII : CreatureQualityRank.RankIII;
        int upgradeCost = nextRank == CreatureQualityRank.RankII ? 15 : 30;

        if (CurrencyWallet.Instance != null && !CurrencyWallet.Instance.CanAfford(upgradeCost))
        {
            resultInfo = $"Not enough Coins to rank up! Need {upgradeCost} Coins.";
            return false;
        }

        if (CurrencyWallet.Instance != null && !CurrencyWallet.Instance.TrySpend(upgradeCost, CoinReason.CardPurchase))
        {
            resultInfo = $"Không đủ Coins để nâng Rank! Cần {upgradeCost} Coins.";
            return false;
        }

        // Cập nhật Rank trên identity
        if (identity != null)
        {
            identity.TrySetQualityRank(nextRank);
            HabitatRuntimeManager.NotifyWorldChanged();
        }

        // Reset thanh Care về 0 để tiếp tục chu kỳ chăm sóc tiếp theo; rank (qualityRank) và care cùng vào bản lưu đảo
        creatureCareProgress[id] = 0;
        WorldSaveSystem.MarkDirty();
        OnCareProgressChanged?.Invoke(id, 0);
        OnRankUpgraded?.Invoke(id, nextRank);

        // Báo cáo thành tựu
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.ReportProgress("ACH_FIRST_CARE", 1);
        }

        resultInfo = $"Congratulations! Upgraded to {nextRank} (Greatly boosts production!)";
        return true;
    }
}
