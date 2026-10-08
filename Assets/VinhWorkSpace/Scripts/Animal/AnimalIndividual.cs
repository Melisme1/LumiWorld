using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Quản lý thông số cá thể riêng biệt của từng con thú được đặt xuống thế giới:
/// - Cấp sao (1★ đến 5★) tương ứng với Phẩm cấp (Common -> Legendary).
/// - Thiên phú / Tính cách độc nhất (Trait / Personality).
/// - Định danh riêng biệt (Unique ID & Display Name).
/// - Năng suất sản xuất tài nguyên thực tế dựa trên Star Level và Trait.
/// - Nhận diện Vùng đất sinh sống (Terrain - ví dụ Leafwood) để khai thác đúng loại tài nguyên của vùng đất đó (ví dụ Gỗ).
/// - Đồng bộ trực tiếp với AnimalMovementAI (tốc độ) và AnimalVisualEnhancer (hào quang theo cấp sao).
/// </summary>
[DisallowMultipleComponent]
public class AnimalIndividual : MonoBehaviour
{
    [Header("1. Dữ liệu loài (Species Blueprint)")]
    [SerializeField] private AnimalSpeciesData speciesData;

    [Header("2. Dữ liệu cá thể riêng biệt (Individual Stats)")]
    [SerializeField] private string uniqueID;
    [SerializeField] private string individualName;
    [Range(1, 5)]
    [SerializeField] private int starLevel = 1;
    [SerializeField] private AnimalRank rank = AnimalRank.Common;
    [SerializeField] private AnimalTrait trait = AnimalTrait.Friendly;

    [Header("3. Chỉ số năng suất thực tế sau khi tính nhân phẩm")]
    [SerializeField] private float actualProductionInterval;
    [SerializeField] private int actualResourceAmount;
    [SerializeField] private float actualWalkSpeed;
    [SerializeField] private float productionProgress; // 0.0 -> 1.0 (tiến độ chu kỳ)

    [Header("4. Thống kê sản lượng")]
    [SerializeField] private int totalProducedCount = 0;

    [Header("5. Vùng đất cư trú & Tài nguyên khai thác")]
    [SerializeField] private CardData habitatTerrain;
    [SerializeField] private ResourceData activeResource;
    [SerializeField] private string activeResourceName;

    public AnimalSpeciesData SpeciesData => speciesData;
    public string UniqueID => uniqueID;
    public string IndividualName => individualName;
    public int StarLevel => starLevel;
    public AnimalRank Rank => rank;
    public AnimalTrait Trait => trait;
    public float ProductionProgress => productionProgress;
    public int TotalProducedCount => totalProducedCount;
    public CardData HabitatTerrain => habitatTerrain;
    public ResourceData ActiveResource => activeResource;
    public string ActiveResourceName => activeResourceName;

    // Sự kiện khi sinh tài nguyên thành công: (con thú này, tên tài nguyên, số lượng)
    public event Action<AnimalIndividual, string, int> OnResourceProduced;
    // Sự kiện nâng cao cho hệ sinh thái / kho đồ quản lý theo ScriptableObject
    public event Action<AnimalIndividual, ResourceData, int> OnResourceDataProduced;

    private float timer = 0f;
    private bool isInitialized = false;

    private void Awake()
    {
        // Nếu đã có sẵn speciesData trên prefab hoặc Inspector thì tự khởi tạo
        if (!isInitialized && speciesData != null)
        {
            Initialize(speciesData);
        }
    }

    private void Start()
    {
        // Đảm bảo con thú liên kết với vùng đất sau khi toàn bộ hierarchy đã ổn định
        if (habitatTerrain == null)
        {
            BindToHabitat(null);
        }
    }

    /// <summary>
    /// Khởi tạo thông số độc nhất cho con thú khi đặt xuống thế giới
    /// </summary>
    public void Initialize(AnimalSpeciesData data, GameObject tileObj = null, int forcedStar = 0, AnimalTrait? forcedTrait = null)
    {
        if (data == null) return;
        this.speciesData = data;
        this.isInitialized = true;

        if (string.IsNullOrEmpty(uniqueID))
        {
            uniqueID = Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
        }

        // 1. Xác định cấp sao (1★ đến 5★)
        if (forcedStar >= 1 && forcedStar <= 5)
        {
            this.starLevel = forcedStar;
        }
        else
        {
            this.starLevel = RollStarLevel(speciesData);
        }
        this.rank = (AnimalRank)this.starLevel;

        // 2. Xác định Thiên phú / Tính cách (Trait)
        if (forcedTrait.HasValue)
        {
            this.trait = forcedTrait.Value;
        }
        else if (speciesData.traitPool != null && speciesData.traitPool.Count > 0)
        {
            this.trait = speciesData.traitPool[Random.Range(0, speciesData.traitPool.Count)];
        }

        // 3. Đặt tên riêng hiển thị theo phẩm cấp
        string starIcons = new string('★', starLevel);
        string rankTitle = GetRankTitle(rank);
        string traitTitle = GetTraitName(trait);
        this.individualName = $"[{starIcons} {rankTitle}] {speciesData.speciesName} ({traitTitle}) #{uniqueID}";
        gameObject.name = $"{speciesData.speciesName}_{starLevel}Star_{uniqueID}";

        // 4. Tính toán thông số năng suất thực tế
        ComputeEffectiveStats();

        // 5. Liên kết với vùng đất ô lục giác (Leafwood, Bloomfield...) để chọn loại tài nguyên khai thác
        BindToHabitat(tileObj);

        // 6. Áp dụng thông số vào các component vận hành
        ApplyToMovementAI();
        ApplyToVisualEnhancer();

        string resourceLabel = activeResource != null ? activeResource.GetColoredName() : "<color=grey>(Vùng đất này không có tài nguyên)</color>";
        string terrainLabel = habitatTerrain != null ? $"tại vùng đất <b>{habitatTerrain.cardName}</b>" : "";

        string productionLabel = LumiWorld.Acs.ResourceProductionRuntime.ClaimsWorld ?
            "→ Production GDD: chọn tài nguyên trong HabitatSystem; rate theo Rank GDD và Settings trong Hierarchy." :
            $"→ Khai thác: {resourceLabel} | Năng suất: {actualResourceAmount} mỗi {actualProductionInterval:F1}s";
        Debug.Log($"<color={GetRankHexColor(rank)}><b>[LumiWorld Động Vật]</b> Bạn vừa triệu hồi: {individualName} {terrainLabel}!</color>\n" +
                  productionLabel + $" | Tốc độ: {actualWalkSpeed:F2}m/s");
    }

    /// <summary>
    /// Gán hoặc cập nhật vùng đất cư trú cho con thú (Leafwood, Bloomfield, v.v.)
    /// và xác định loại tài nguyên mà vùng đất đó cung cấp để bắt đầu chu kỳ sản xuất.
    /// </summary>
    public void BindToHabitat(GameObject tileObj)
    {
        if (tileObj == null)
        {
            // 1. Thử lấy GameObject cha (Hex tile gốc)
            if (transform.parent != null)
            {
                tileObj = transform.parent.gameObject;
            }
        }

        if (tileObj != null)
        {
            // Tìm PlacedCard loại Terrain trên ô này (ví dụ Leafwood)
            PlacedCard[] placedCards = tileObj.GetComponentsInChildren<PlacedCard>();
            foreach (var pc in placedCards)
            {
                if (pc != null && pc.cardData != null && pc.cardData.cardType == CardType.Terrain)
                {
                    habitatTerrain = pc.cardData;
                    break;
                }
            }
        }

        // Nếu vẫn chưa tìm thấy habitatTerrain, thử tìm theo tọa độ ô lục giác thông qua HexWorldGenerator
        if (habitatTerrain == null)
        {
            HexWorldGenerator worldGen = FindAnyObjectByType<HexWorldGenerator>();
            if (worldGen != null && worldGen.MapTiles != null)
            {
                HexCoordinates hex = HexMetrics.WorldToHex(transform.position);
                if (worldGen.MapTiles.TryGetValue(hex, out GameObject mapTile) && mapTile != null)
                {
                    PlacedCard[] placedCards = mapTile.GetComponentsInChildren<PlacedCard>();
                    foreach (var pc in placedCards)
                    {
                        if (pc != null && pc.cardData != null && pc.cardData.cardType == CardType.Terrain)
                        {
                            habitatTerrain = pc.cardData;
                            break;
                        }
                    }
                }
            }
        }

        // Tài nguyên khai thác HOÀN TOÀN do VÙNG ĐẤT quyết định (ví dụ Leafwood -> Gỗ)
        if (habitatTerrain != null && habitatTerrain.producedResource != null)
        {
            activeResource = habitatTerrain.producedResource;
            activeResourceName = activeResource.resourceName;
        }
        else
        {
            // Vùng đất không có tài nguyên hoặc con thú chưa ở trên đất có tài nguyên -> Không sản xuất gì
            activeResource = null;
            activeResourceName = "";
        }
    }

    [Header("6. Chế độ sản xuất theo Cụm Biome")]
    [Tooltip("Nếu bật: Sản xuất tài nguyên được gom chung và quản lý bởi Cụm Biome (tiết kiệm UI và gom mẻ lớn)")]
    [SerializeField] private bool useBiomeClusterProduction = true;

    public bool UseBiomeClusterProduction => useBiomeClusterProduction;
    public int ActualResourceAmount => actualResourceAmount;

    /// <summary>
    /// Tính toán sản lượng thu hoạch mà cá thể thú này đóng góp cho mẻ thu hoạch của Cụm Biome
    /// </summary>
    public int CalculateHarvestYield()
    {
        int amount = actualResourceAmount;

        // Kích hoạt hiệu ứng trait Ngôi sao may mắn (Lucky Star)
        if (trait == AnimalTrait.LuckyStar && Random.value < 0.15f)
        {
            amount *= 2;
            string resLabel = activeResource != null ? activeResource.GetColoredName() : "Tài nguyên";
            Debug.Log($"<color=yellow>✨ [May mắn!] {individualName} kích hoạt nhân đôi sản lượng đóng góp: +{amount} {resLabel}!</color>");
        }

        totalProducedCount += amount;
        return amount;
    }

    private void Update()
    {
        if (!isInitialized || speciesData == null) return;

        if (LumiWorld.Acs.ResourceProductionRuntime.ClaimsWorld) return;

        // Nếu đang dùng hệ thống gom cụm Biome thì BiomeHarvestManager sẽ quản lý chu kỳ tập trung
        if (useBiomeClusterProduction) return;

        // CHỈ sản xuất khi con thú đang ở trên vùng đất có tài nguyên (ví dụ Leafwood sinh Gỗ)
        if (activeResource == null) return;

        timer += Time.deltaTime;
        productionProgress = Mathf.Clamp01(timer / actualProductionInterval);

        if (timer >= actualProductionInterval)
        {
            timer = 0f;
            ProduceResource();
        }
    }

    private void ProduceResource()
    {
        if (activeResource == null) return;

        int amount = actualResourceAmount;

        // Thưởng thêm sản lượng nếu vùng đất có bonus
        if (habitatTerrain != null && habitatTerrain.landResourceBonus > 0)
        {
            amount += habitatTerrain.landResourceBonus;
        }

        // Kích hoạt hiệu ứng trait Ngôi sao may mắn (Lucky Star)
        if (trait == AnimalTrait.LuckyStar && Random.value < 0.15f)
        {
            amount *= 2;
            Debug.Log($"<color=yellow>✨ [May mắn!] {individualName} kích hoạt nhân đôi sản lượng: +{amount} {activeResource.GetColoredName()}!</color>");
        }

        totalProducedCount += amount;

        // Bắn sự kiện
        OnResourceProduced?.Invoke(this, activeResource.resourceName, amount);
        OnResourceDataProduced?.Invoke(this, activeResource, amount);

        string terrainLabel = habitatTerrain != null ? $"[Vùng đất {habitatTerrain.cardName}]" : "";
        Debug.Log($"🌾 {terrainLabel} <b>{individualName}</b> vừa thu hoạch +{amount} {activeResource.GetColoredName()}! (Tổng tích lũy: {totalProducedCount})");
    }

    private void ComputeEffectiveStats()
    {
        // 1. Hệ số nhân theo cấp sao (1★ -> 5★)
        // 1★: 1.0x
        // 2★: 1.25x
        // 3★: 1.6x
        // 4★: 2.1x
        // 5★: 3.0x
        float starProductionMultiplier = starLevel switch
        {
            1 => 1.0f,
            2 => 1.25f,
            3 => 1.6f,
            4 => 2.1f,
            5 => 3.0f,
            _ => 1.0f
        };

        // 2. Ảnh hưởng của Trait (Tính cách)
        float traitIntervalFactor = 1.0f;
        int traitBonusAmount = 0;
        float speedFactor = 1.0f;

        switch (trait)
        {
            case AnimalTrait.Energetic: // Năng động
                speedFactor = 1.25f;
                traitIntervalFactor = 0.85f; // Nhanh hơn 15%
                break;
            case AnimalTrait.Lazy: // Lười biếng
                speedFactor = 0.80f;
                traitBonusAmount = 1; // Mỗi lần cho nhiều hơn
                break;
            case AnimalTrait.ResourceHoarder: // Thần tài
                traitBonusAmount = 1;
                break;
            case AnimalTrait.NatureLover: // Yêu thiên nhiên
                traitIntervalFactor = 0.80f;
                break;
            case AnimalTrait.Friendly: // Hòa đồng
                traitIntervalFactor = 0.90f;
                break;
        }

        // Tính kết quả
        actualProductionInterval = Mathf.Max(5.0f, (speciesData.baseProductionInterval / starProductionMultiplier) * traitIntervalFactor);
        actualResourceAmount = Mathf.Max(1, Mathf.RoundToInt(speciesData.baseResourceAmount * (1f + (starLevel - 1) * 0.4f)) + traitBonusAmount);
        actualWalkSpeed = speciesData.baseWalkSpeed * speedFactor;
    }

    private void ApplyToMovementAI()
    {
        AnimalMovementAI moveAI = GetComponent<AnimalMovementAI>();
        if (moveAI == null)
        {
            moveAI = gameObject.AddComponent<AnimalMovementAI>();
        }
        if (moveAI != null)
        {
            moveAI.SetSpeeds(actualWalkSpeed, actualWalkSpeed * 1.8f);
            if (speciesData != null)
            {
                moveAI.SetLocomotion(speciesData.locomotionType, speciesData.flightAltitude);
                moveAI.SetYOffset(speciesData.yOffset);
            }
        }
    }

    private void ApplyToVisualEnhancer()
    {
        AnimalVisualEnhancer visual = GetComponent<AnimalVisualEnhancer>();
        if (visual == null)
        {
            visual = gameObject.AddComponent<AnimalVisualEnhancer>();
        }
        if (visual != null)
        {
            Color rankColor = GetRankColor(rank);
            visual.ApplyRankVisuals(rank, starLevel, rankColor);
        }
    }

    private int RollStarLevel(AnimalSpeciesData data)
    {
        float totalWeight = data.weight1Star + data.weight2Star + data.weight3Star + data.weight4Star + data.weight5Star;
        if (totalWeight <= 0f) totalWeight = 100f;

        float roll = Random.Range(0f, totalWeight);
        if (roll < data.weight1Star) return 1;
        roll -= data.weight1Star;

        if (roll < data.weight2Star) return 2;
        roll -= data.weight2Star;

        if (roll < data.weight3Star) return 3;
        roll -= data.weight3Star;

        if (roll < data.weight4Star) return 4;

        return 5; // 5★ Huyền thoại
    }

    public static string GetRankTitle(AnimalRank r) => r switch
    {
        AnimalRank.Common => "Phổ thông",
        AnimalRank.Uncommon => "Hảo hạng",
        AnimalRank.Rare => "Quý hiếm",
        AnimalRank.Epic => "Sử thi",
        AnimalRank.Legendary => "Huyền thoại",
        _ => "Thường"
    };

    public static string GetTraitName(AnimalTrait t) => t switch
    {
        AnimalTrait.Energetic => "Năng động",
        AnimalTrait.Lazy => "Lười biếng",
        AnimalTrait.ResourceHoarder => "Thần tài",
        AnimalTrait.NatureLover => "Yêu thiên nhiên",
        AnimalTrait.Friendly => "Hòa đồng",
        AnimalTrait.LuckyStar => "Ngôi sao may mắn",
        _ => "Bình thường"
    };

    public static Color GetRankColor(AnimalRank r) => r switch
    {
        AnimalRank.Common => new Color(1.8f, 1.8f, 1.8f, 1.0f),         // 1★: Bạc ngọc trai (Silver Pearl)
        AnimalRank.Uncommon => new Color(0.8f, 2.3f, 1.1f, 1.0f),       // 2★: Xanh ngọc lục bảo (Emerald)
        AnimalRank.Rare => new Color(0.7f, 1.7f, 2.8f, 1.0f),           // 3★: Xanh lam đại dương (Sapphire Azure)
        AnimalRank.Epic => new Color(2.4f, 0.9f, 2.7f, 1.0f),           // 4★: Tím thạch anh sử thi (Amethyst)
        AnimalRank.Legendary => new Color(3.0f, 2.2f, 0.5f, 1.0f),      // 5★: Vàng thái dương huyền thoại (Solar Gold)
        _ => Color.white
    };

    public static string GetRankHexColor(AnimalRank r) => r switch
    {
        AnimalRank.Common => "#D1D5DB",
        AnimalRank.Uncommon => "#34D399",
        AnimalRank.Rare => "#60A5FA",
        AnimalRank.Epic => "#C084FC",
        AnimalRank.Legendary => "#FBBF24",
        _ => "#FFFFFF"
    };
}
