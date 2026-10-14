using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using LumiWorld.Acs;

/// <summary>
/// Quản lý việc nạp ảnh từ thư mục Assets (DS_pannel_thu, chitietthu_pannel, AVT_ANM),
/// chuẩn hóa định danh loài sinh vật sang tên tệp chuẩn (caterpillar -> Petaluff, deer -> Bramblet,...),
/// tự động nạp 3D Prefabs từ Resources/CreatureModels, và cung cấp các Sprite game chuyên nghiệp
/// (Thanh năng lượng, Nút bấm 3D Game, Chip thông tin kính mờ).
/// </summary>
public static class HabitatCreaturePanelStyle
{
    private static Sprite panelBackgroundSprite;
    private static Sprite hexButtonFrameSprite;
    private static Sprite energyTrackSprite;
    private static Sprite energyFillSprite;
    private static Sprite rectBannerSprite;
    private static Sprite infoChipSprite;
    private static Sprite gameButtonSprite;

    private static Sprite buttonChoAnSprite;
    private static Sprite buttonTriLieuSprite;
    private static Sprite buttonNangSprite;
    private static Sprite oChiTietThuSprite;
    private static Sprite oChiTietNhoThuSprite;
    private static Sprite buttonExitSprite;

    private static Sprite resourceCardNormalSprite;
    private static Sprite resourceCardProducingSprite;
    private static Sprite resourceCardLockedSprite;
    private static Sprite techPillCyanSprite;
    private static Sprite techPillEmeraldSprite;
    private static Sprite techArtBoxSprite;
    private static Sprite pencilIconSprite;

    private static readonly Dictionary<string, Sprite> detailBgCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Sprite> creatureAvatarCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

    // Bảng quy đổi toàn diện: Canonical Name, Tên tiếng Việt, Prefab 3D tương ứng
    private static readonly Dictionary<string, (string canonicalName, string vietnameseName, string prefabName)> speciesMap =
        new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase)
    {
        { "caterpillar", ("Petaluff", "Sâu Bướm", "saubuom_anm") },
        { "saubuom", ("Petaluff", "Sâu Bướm", "saubuom_anm") },
        { "petaluff", ("Petaluff", "Sâu Bướm", "saubuom_anm") },

        { "deer", ("Bramblet", "Nai Rừng", "nai_anm") },
        { "nai", ("Bramblet", "Nai Rừng", "nai_anm") },
        { "bramblet", ("Bramblet", "Nai Rừng", "nai_anm") },

        { "flyingsquirrel", ("Spriglide", "Sóc Bay", "socbay_anm") },
        { "socbay", ("Spriglide", "Sóc Bay", "socbay_anm") },
        { "spriglide", ("Spriglide", "Sóc Bay", "socbay_anm") },

        { "grasshopper", ("Frondle", "Châu Chấu", "chauchau_anm") },
        { "chauchau", ("Frondle", "Châu Chấu", "chauchau_anm") },
        { "frondle", ("Frondle", "Châu Chấu", "chauchau_anm") },

        { "flowerdog", ("Dandrift", "Chó Hoa", "chohoa_anm") },
        { "chohoa", ("Dandrift", "Chó Hoa", "chohoa_anm") },
        { "dandrift", ("Dandrift", "Chó Hoa", "chohoa_anm") },

        { "peacock", ("Galume", "Chim Công", "chimcong_anm") },
        { "chimcong", ("Galume", "Chim Công", "chimcong_anm") },
        { "galume", ("Galume", "Chim Công", "chimcong_anm") },

        { "highlandcow", ("Heathorn", "Bò Lông Dài", "bolongdai_anm") },
        { "bolongdai", ("Heathorn", "Bò Lông Dài", "bolongdai_anm") },
        { "heathorn", ("Heathorn", "Bò Lông Dài", "bolongdai_anm") },

        { "dragonfly", ("Iriwing", "Chuồn Chuồn", "chuongchuong_anm") },
        { "chuongchuong", ("Iriwing", "Chuồn Chuồn", "chuongchuong_anm") },
        { "iriwing", ("Iriwing", "Chuồn Chuồn", "chuongchuong_anm") },

        { "otter", ("Rivlet", "Rái Cá", "raica_anm") },
        { "raica", ("Rivlet", "Rái Cá", "raica_anm") },
        { "rivlet", ("Rivlet", "Rái Cá", "raica_anm") },

        { "capybara", ("Pebbara", "Capybara", "capybara_anm") },
        { "pebbara", ("Pebbara", "Capybara", "capybara_anm") },

        { "grassfox", ("Fernkin", "Cáo Cỏ", "caoco_anm") },
        { "caoco", ("Fernkin", "Cáo Cỏ", "caoco_anm") },
        { "fernkin", ("Fernkin", "Cáo Cỏ", "caoco_anm") },

        { "blossomfrog", ("Puddleaf", "Ếch Hoa", "echhoa_anm") },
        { "echhoa", ("Puddleaf", "Ếch Hoa", "echhoa_anm") },
        { "puddleaf", ("Puddleaf", "Ếch Hoa", "echhoa_anm") },

        { "parrot", ("Heliko", "Vẹt 7 Màu", "chimvet7mau_anm") },
        { "chimvet7mau", ("Heliko", "Vẹt 7 Màu", "chimvet7mau_anm") },
        { "heliko", ("Heliko", "Vẹt 7 Màu", "chimvet7mau_anm") },

        { "cow", ("Heathorn", "Bò Sữa", "bo_anm") },
        { "bo", ("Heathorn", "Bò Sữa", "bo_anm") },

        { "fox", ("Fernkin", "Cáo Lửa", "cao_anm") },
        { "cao", ("Fernkin", "Cáo Lửa", "cao_anm") },

        { "reedle", ("Reedle", "Chim Cò Lau", "thiennga_anm") },
        { "craggle", ("Craggle", "Rùa Đá", "canoc_anm") },
        { "lotelle", ("Lotelle", "Rùa Sen", "echduoica_anm") }
    };

    public static Sprite PanelBackground
    {
        get
        {
            if (panelBackgroundSprite == null)
            {
                panelBackgroundSprite = LoadSpriteFromDisk("Assets/DS_pannel_thu/pannel_avt.png");
            }
            return panelBackgroundSprite;
        }
    }

    public static Sprite HexButtonFrame
    {
        get
        {
            if (hexButtonFrameSprite == null)
            {
                hexButtonFrameSprite = LoadSpriteFromDisk("Assets/DS_pannel_thu/Button_avt_clean.png");
                if (hexButtonFrameSprite == null)
                {
                    hexButtonFrameSprite = LoadSpriteFromDisk("Assets/DS_pannel_thu/Button_avt.png");
                }
            }
            return hexButtonFrameSprite;
        }
    }

    /// <summary>
    /// Khung hộp công nghệ cho Thanh Năng Lượng (Energy Bar Casing)
    /// </summary>
    public static Sprite EnergyTrackSprite
    {
        get
        {
            if (energyTrackSprite == null)
            {
                energyTrackSprite = CreateTechBorderBox(96, 24, 8, new Color(0.04f, 0.07f, 0.12f, 0.92f), new Color(0.20f, 0.70f, 0.95f, 0.85f));
            }
            return energyTrackSprite;
        }
    }

    /// <summary>
    /// Lõi năng lượng phát sáng gradient (Cyan/Emerald Chill Tech Core)
    /// </summary>
    public static Sprite EnergyFillSprite
    {
        get
        {
            if (energyFillSprite == null)
            {
                energyFillSprite = CreateTechEnergyFill(96, 20, new Color(0.12f, 0.88f, 0.98f, 1f), new Color(0.25f, 0.98f, 0.70f, 1f));
            }
            return energyFillSprite;
        }
    }

    /// <summary>
    /// Thanh chữ nhật nằm ngang phong cách phù hợp môi trường (Bottom Summary Banner từ pannel_footer_banner.png)
    /// </summary>
    public static Sprite RectBannerSprite
    {
        get
        {
            if (rectBannerSprite == null)
            {
                rectBannerSprite = LoadSpriteFromDisk("Assets/DS_pannel_thu/pannel_footer_banner.png");
                if (rectBannerSprite == null)
                {
                    rectBannerSprite = CreateTechBorderBox(64, 28, 4, new Color(0.08f, 0.12f, 0.16f, 0.85f), new Color(0.35f, 0.50f, 0.65f, 0.60f));
                }
            }
            return rectBannerSprite;
        }
    }

    /// <summary>
    /// Thẻ chip / nút thông tin phong cách Dark Glass với viền mảnh thanh lịch
    /// </summary>
    public static Sprite InfoChipSprite
    {
        get
        {
            if (infoChipSprite == null)
            {
                infoChipSprite = CreateTechBorderBox(64, 32, 5, new Color(0.06f, 0.09f, 0.14f, 0.82f), new Color(0.30f, 0.45f, 0.60f, 0.55f));
            }
            return infoChipSprite;
        }
    }

    /// <summary>
    /// Nút bấm game 3D có độ sâu (Bevel & Shadow)
    /// </summary>
    public static Sprite GameButtonSprite
    {
        get
        {
            if (gameButtonSprite == null)
            {
                gameButtonSprite = Create3DGameButton(64, 36, 6);
            }
            return gameButtonSprite;
        }
    }

    /// <summary>
    /// Nút Thoát ("X") hình tròn viền công nghệ tinh tế
    /// </summary>
    public static Sprite ButtonExit
    {
        get
        {
            if (buttonExitSprite == null)
            {
                buttonExitSprite = LoadSpriteFromDisk("Assets/chitietthu_pannel/button_exit.png");
            }
            return buttonExitSprite;
        }
    }

    /// <summary>
    /// Biểu tượng cây bút chì đổi tên (Pencil Edit Icon)
    /// </summary>
    public static Sprite PencilIconSprite
    {
        get
        {
            if (pencilIconSprite == null)
            {
                pencilIconSprite = LoadSpriteFromDisk("Assets/DS_pannel_thu/pencil_icon.png");
            }
            return pencilIconSprite;
        }
    }

    /// <summary>
    /// Nút Cho Ăn xanh lá chuyên nghiệp từ Assets/chitietthu_pannel/button_choan.png (tỉ lệ chuẩn pill không méo mó)
    /// </summary>
    public static Sprite ButtonChoAn
    {
        get
        {
            if (buttonChoAnSprite == null)
            {
                buttonChoAnSprite = LoadSpriteWithBorders("Assets/chitietthu_pannel/button_choan.png", new Vector4(100, 50, 100, 50), 555.5f);
            }
            return buttonChoAnSprite;
        }
    }

    /// <summary>
    /// Nút Trị Liệu xanh dương chuyên nghiệp từ Assets/chitietthu_pannel/button_trilieu.png (tỉ lệ chuẩn pill không méo mó)
    /// </summary>
    public static Sprite ButtonTriLieu
    {
        get
        {
            if (buttonTriLieuSprite == null)
            {
                buttonTriLieuSprite = LoadSpriteWithBorders("Assets/chitietthu_pannel/button_trilieu.png", new Vector4(100, 50, 100, 50), 555.5f);
            }
            return buttonTriLieuSprite;
        }
    }

    /// <summary>
    /// Nút Nâng Cấp vàng kim lấp lánh từ Assets/chitietthu_pannel/button_nang.png (tỉ lệ chuẩn pill không méo mó)
    /// </summary>
    public static Sprite ButtonNang
    {
        get
        {
            if (buttonNangSprite == null)
            {
                buttonNangSprite = LoadSpriteWithBorders("Assets/chitietthu_pannel/button_nang.png", new Vector4(100, 50, 100, 50), 555.5f);
            }
            return buttonNangSprite;
        }
    }

    /// <summary>
    /// Ô trường thông tin dài (Bụng đói, no, tình trạng, thức ăn ưa thích) từ Assets/chitietthu_pannel/ochitiet_thu.png
    /// </summary>
    public static Sprite OChiTietThu
    {
        get
        {
            if (oChiTietThuSprite == null)
            {
                oChiTietThuSprite = LoadSpriteWithBorders("Assets/chitietthu_pannel/ochitiet_thu.png", new Vector4(45, 30, 45, 30), 412.5f);
            }
            return oChiTietThuSprite;
        }
    }

    /// <summary>
    /// Ô trường thông tin nhỏ (Nai rừng, rank, sao, trait) từ Assets/chitietthu_pannel/ochitietnho_thu.png
    /// </summary>
    public static Sprite OChiTietNhoThu
    {
        get
        {
            if (oChiTietNhoThuSprite == null)
            {
                oChiTietNhoThuSprite = LoadSpriteWithBorders("Assets/chitietthu_pannel/ochitietnho_thu.png", new Vector4(100, 50, 100, 50), 714.3f);
            }
            return oChiTietNhoThuSprite;
        }
    }

    public static Sprite ResourceCardNormalSprite
    {
        get
        {
            if (resourceCardNormalSprite == null)
                resourceCardNormalSprite = CreateTechBorderBox(64, 48, 6, new Color(0.08f, 0.15f, 0.22f, 0.94f), new Color(0.18f, 0.40f, 0.58f, 0.75f));
            return resourceCardNormalSprite;
        }
    }

    public static Sprite ResourceCardProducingSprite
    {
        get
        {
            if (resourceCardProducingSprite == null)
                resourceCardProducingSprite = CreateTechBorderBox(64, 48, 6, new Color(0.08f, 0.16f, 0.24f, 0.94f), new Color(0f, 0.70f, 0.88f, 0.92f));
            return resourceCardProducingSprite;
        }
    }

    public static Sprite ResourceCardLockedSprite
    {
        get
        {
            if (resourceCardLockedSprite == null)
                resourceCardLockedSprite = CreateTechBorderBox(64, 48, 6, new Color(0.06f, 0.10f, 0.15f, 0.80f), new Color(0.15f, 0.22f, 0.30f, 0.55f));
            return resourceCardLockedSprite;
        }
    }

    public static Sprite TechPillCyanSprite
    {
        get
        {
            if (techPillCyanSprite == null)
                techPillCyanSprite = CreateTechBorderBox(64, 32, 6, new Color(0f, 0.68f, 0.86f, 1f), new Color(0.35f, 0.85f, 1f, 1f));
            return techPillCyanSprite;
        }
    }

    public static Sprite TechPillEmeraldSprite
    {
        get
        {
            if (techPillEmeraldSprite == null)
                techPillEmeraldSprite = CreateTechBorderBox(64, 32, 6, new Color(0.02f, 0.58f, 0.38f, 1f), new Color(0.20f, 0.82f, 0.55f, 1f));
            return techPillEmeraldSprite;
        }
    }

    public static Sprite TechArtBoxSprite
    {
        get
        {
            if (techArtBoxSprite == null)
                techArtBoxSprite = CreateTechBorderBox(48, 48, 6, new Color(0.04f, 0.08f, 0.14f, 0.80f), new Color(0.20f, 0.42f, 0.58f, 0.70f));
            return techArtBoxSprite;
        }
    }

    public static void ClearCache()
    {
        buttonChoAnSprite = null;
        buttonTriLieuSprite = null;
        buttonNangSprite = null;
        oChiTietThuSprite = null;
        oChiTietNhoThuSprite = null;
        buttonExitSprite = null;
        panelBackgroundSprite = null;
        hexButtonFrameSprite = null;
        rectBannerSprite = null;
        energyTrackSprite = null;
        energyFillSprite = null;
        resourceCardNormalSprite = null;
        resourceCardProducingSprite = null;
        resourceCardLockedSprite = null;
        techPillCyanSprite = null;
        techPillEmeraldSprite = null;
        techArtBoxSprite = null;
        pencilIconSprite = null;
        detailBgCache.Clear();
        creatureAvatarCache.Clear();
    }

    public static Sprite GetDetailBackground(BiomeAffinity affinity)
    {
        string filename = GetDetailBgFileName(affinity);
        if (detailBgCache.TryGetValue(filename, out var cached) && cached != null)
        {
            return cached;
        }

        string fullPath = "Assets/chitietthu_pannel/" + filename;
        Sprite sp = LoadSpriteFromDisk(fullPath);
        if (sp != null)
        {
            detailBgCache[filename] = sp;
        }
        return sp;
    }

    private static string GetDetailBgFileName(BiomeAffinity affinity)
    {
        switch (affinity)
        {
            case BiomeAffinity.Leafwood:
            case BiomeAffinity.Rainleaf:
                return "chitietxanhla.png";

            case BiomeAffinity.Reedmarsh:
            case BiomeAffinity.Lilywater:
                return "chitietxanhduong.png";

            case BiomeAffinity.Bloomfield:
                return "chitiethong.png";

            case BiomeAffinity.Windheath:
            case BiomeAffinity.Streamstone:
            default:
                return "chitietxam.png";
        }
    }

    /// <summary>
    /// Tra cứu toàn diện: Tên tiếng Việt, Tên tệp Avatar chuẩn, Prefab 3D từ Resources và Card Sprite fallback
    /// </summary>
    public static (string displayName, string canonicalAvt, GameObject modelPrefab, Sprite cardSprite) ResolveCreatureData(string speciesId)
    {
        string raw = !string.IsNullOrEmpty(speciesId) ? speciesId.Trim() : "Unknown";
        string clean = NormalizeSpeciesName(raw);

        string canonical = clean;
        string dispName = clean;
        string pName = "saubuom_anm";

        if (speciesMap.TryGetValue(clean, out var mapped))
        {
            canonical = mapped.canonicalName;
            dispName = mapped.vietnameseName;
            pName = mapped.prefabName;
        }

        // 1. Tải 3D Prefab từ Resources/CreatureModels/
        GameObject prefab = Resources.Load<GameObject>("CreatureModels/" + pName);

        Sprite cardSprite = null;

        // 2. Tra cứu CardData trong bộ nhớ
        CardData[] allCards = Resources.FindObjectsOfTypeAll<CardData>();
        foreach (var c in allCards)
        {
            if (c == null) continue;
            bool match = string.Equals(c.cardID, raw, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(c.cardName, raw, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(c.cardID, "creature_" + clean, StringComparison.OrdinalIgnoreCase) ||
                         (c.animalSpeciesData != null && (
                             string.Equals(c.animalSpeciesData.speciesID, raw, StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(c.animalSpeciesData.speciesName, raw, StringComparison.OrdinalIgnoreCase)));

            if (match)
            {
                if (!string.IsNullOrEmpty(c.cardName)) dispName = c.cardName;
                if (prefab == null && c.prefabToPlace != null) prefab = c.prefabToPlace;
                if (c.cardImage != null)
                {
                    cardSprite = c.cardImage;
                    string cImgName = NormalizeSpeciesName(c.cardImage.name);
                    if (speciesMap.TryGetValue(cImgName, out var fromCardImg))
                    {
                        canonical = fromCardImg.canonicalName;
                    }
                    else if (!string.IsNullOrEmpty(cImgName))
                    {
                        canonical = cImgName;
                    }
                }
                break;
            }
        }

        return (dispName, canonical, prefab, cardSprite);
    }

    public static Sprite GetCreatureAvatar(string speciesId, CreatureQualityRank rank)
    {
        var resolved = ResolveCreatureData(speciesId);
        string canonicalName = resolved.canonicalAvt;

        string cacheKey = $"{canonicalName}_{(int)rank}";
        if (creatureAvatarCache.TryGetValue(cacheKey, out var cached) && cached != null)
        {
            return cached;
        }

        string filePath = Path.Combine(Application.dataPath, "AVT_ANM", canonicalName + ".png");
        if (File.Exists(filePath))
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                Texture2D rawTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (rawTex.LoadImage(bytes))
                {
                    Texture2D slicedTex = SliceAndCutoutFrame(rawTex, rank, canonicalName);
                    if (slicedTex != null)
                    {
                        Sprite sp = Sprite.Create(slicedTex, new Rect(0, 0, slicedTex.width, slicedTex.height), new Vector2(0.5f, 0.5f), 100f);
                        sp.name = cacheKey;
                        creatureAvatarCache[cacheKey] = sp;
                        return sp;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HabitatCreaturePanelStyle] Lỗi nạp avatar từ AVT_ANM cho {canonicalName}: {ex.Message}");
            }
        }

        var allAvts = Resources.LoadAll<Sprite>("CreatureAvatars");
        foreach (var avt in allAvts)
        {
            if (avt != null && avt.name.IndexOf(canonicalName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                creatureAvatarCache[cacheKey] = avt;
                return avt;
            }
        }

        if (resolved.cardSprite != null)
        {
            creatureAvatarCache[cacheKey] = resolved.cardSprite;
            return resolved.cardSprite;
        }

        return null;
    }

    private static Texture2D SliceAndCutoutFrame(Texture2D rawTex, CreatureQualityRank rank, string canonicalName = "")
    {
        int rawW = rawTex.width;
        int rawH = rawTex.height;

        // Xử lý đặc biệt cho Bramblet (Nai) - hình 3 pose (3099x1560):
        // Frame 0 chính là góc nhìn chính diện Front-view sạch sẽ (x = 210..890), tách biệt hoàn toàn góc nghiêng 3/4 (x = 1210..1970)
        bool isBramblet = string.Equals(canonicalName, "Bramblet", StringComparison.OrdinalIgnoreCase) ||
                          (!string.IsNullOrEmpty(rawTex.name) && rawTex.name.IndexOf("Bramblet", StringComparison.OrdinalIgnoreCase) >= 0) ||
                          (rawW >= 3000 && rawW <= 3200 && rawH >= 1500 && rawH <= 1650);

        int frameCount = 1;
        int frameIdx = 0;

        if (isBramblet)
        {
            frameCount = 3;
            frameIdx = 0; // Luôn lấy frame chính diện đẹp nhất cho Nai rừng
        }
        else
        {
            frameCount = Mathf.Clamp(Mathf.RoundToInt((float)rawW / rawH), 1, 3);
            if (frameCount == 3)
            {
                frameIdx = rank == CreatureQualityRank.RankI ? 0 : (rank == CreatureQualityRank.RankII ? 1 : 2);
            }
            else if (frameCount == 2)
            {
                frameIdx = rank == CreatureQualityRank.RankI ? 0 : 1;
            }
        }

        int frameW = rawW / frameCount;
        int frameH = rawH;

        Color[] sourcePixels = rawTex.GetPixels(frameIdx * frameW, 0, frameW, frameH);

        int targetW = Mathf.Min(frameW, 400);
        int targetH = Mathf.Min(frameH, 400);

        Texture2D workTex = new Texture2D(targetW, targetH, TextureFormat.RGBA32, false);
        workTex.filterMode = FilterMode.Bilinear;
        workTex.wrapMode = TextureWrapMode.Clamp;

        Color[] workPixels = new Color[targetW * targetH];
        for (int y = 0; y < targetH; y++)
        {
            float v = (float)y / (targetH - 1);
            int sy = Mathf.Clamp(Mathf.RoundToInt(v * (frameH - 1)), 0, frameH - 1);
            for (int x = 0; x < targetW; x++)
            {
                float u = (float)x / (targetW - 1);
                int sx = Mathf.Clamp(Mathf.RoundToInt(u * (frameW - 1)), 0, frameW - 1);
                workPixels[y * targetW + x] = sourcePixels[sy * frameW + sx];
            }
        }

        bool[] visited = new bool[targetW * targetH];
        Queue<int> queue = new Queue<int>();

        bool IsBgWhite(Color c) => c.r >= 0.88f && c.g >= 0.88f && c.b >= 0.88f;

        for (int x = 0; x < targetW; x++)
        {
            int idxTop = (targetH - 1) * targetW + x;
            int idxBot = x;
            if (IsBgWhite(workPixels[idxTop])) { queue.Enqueue(idxTop); visited[idxTop] = true; }
            if (IsBgWhite(workPixels[idxBot])) { queue.Enqueue(idxBot); visited[idxBot] = true; }
        }

        for (int y = 0; y < targetH; y++)
        {
            int idxL = y * targetW;
            int idxR = y * targetW + (targetW - 1);
            if (!visited[idxL] && IsBgWhite(workPixels[idxL])) { queue.Enqueue(idxL); visited[idxL] = true; }
            if (!visited[idxR] && IsBgWhite(workPixels[idxR])) { queue.Enqueue(idxR); visited[idxR] = true; }
        }

        while (queue.Count > 0)
        {
            int curr = queue.Dequeue();
            workPixels[curr] = Color.clear;

            int cx = curr % targetW;
            int cy = curr / targetW;

            int[] dx = { -1, 1, 0, 0 };
            int[] dy = { 0, 0, -1, 1 };

            for (int i = 0; i < 4; i++)
            {
                int nx = cx + dx[i];
                int ny = cy + dy[i];

                if (nx >= 0 && nx < targetW && ny >= 0 && ny < targetH)
                {
                    int nIdx = ny * targetW + nx;
                    if (!visited[nIdx] && IsBgWhite(workPixels[nIdx]))
                    {
                        visited[nIdx] = true;
                        queue.Enqueue(nIdx);
                    }
                }
            }
        }

        // Tự động căn giữa và crop ôm sát nhân vật
        int minX = targetW, maxX = 0, minY = targetH, maxY = 0;
        bool hasOpaque = false;
        for (int y = 0; y < targetH; y++)
        {
            for (int x = 0; x < targetW; x++)
            {
                if (workPixels[y * targetW + x].a > 0.05f)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                    hasOpaque = true;
                }
            }
        }

        if (hasOpaque && (maxX > minX) && (maxY > minY))
        {
            int cropW = (maxX - minX) + 1;
            int cropH = (maxY - minY) + 1;
            int squareSize = Mathf.Max(cropW, cropH) + 16;

            Texture2D squareTex = new Texture2D(squareSize, squareSize, TextureFormat.RGBA32, false);
            squareTex.filterMode = FilterMode.Bilinear;
            squareTex.wrapMode = TextureWrapMode.Clamp;

            Color[] sqPixels = new Color[squareSize * squareSize];
            for (int i = 0; i < sqPixels.Length; i++) sqPixels[i] = Color.clear;

            int offsetX = (squareSize - cropW) / 2;
            int offsetY = (squareSize - cropH) / 2;

            for (int cy = 0; cy < cropH; cy++)
            {
                for (int cx = 0; cx < cropW; cx++)
                {
                    Color p = workPixels[(minY + cy) * targetW + (minX + cx)];
                    sqPixels[(offsetY + cy) * squareSize + (offsetX + cx)] = p;
                }
            }

            squareTex.SetPixels(sqPixels);
            squareTex.Apply();
            return squareTex;
        }

        workTex.SetPixels(workPixels);
        workTex.Apply();
        return workTex;
    }

    public static string NormalizeSpeciesName(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        string s = name.Trim();
        int under = s.IndexOf('_');
        if (under >= 0 && under < s.Length - 1)
        {
            s = s.Substring(under + 1);
        }
        return s;
    }

    public static string GetAffinityDisplayName(BiomeAffinity affinity)
    {
        switch (affinity)
        {
            case BiomeAffinity.Leafwood: return "Rừng Leafwood";
            case BiomeAffinity.Bloomfield: return "Đồng Hoa Bloomfield";
            case BiomeAffinity.Windheath: return "Thảo Nguyên Windheath";
            case BiomeAffinity.Reedmarsh: return "Đầm Lau Reedmarsh";
            case BiomeAffinity.Lilywater: return "Hồ Sen Lilywater";
            case BiomeAffinity.Streamstone: return "Suối Đá Streamstone";
            case BiomeAffinity.Rainleaf: return "Rừng Mưa Rainleaf";
            default: return "Vùng Đất Tự Nhiên";
        }
    }

    public static string GetFavoriteFoodInfo(BiomeAffinity affinity)
    {
        switch (affinity)
        {
            case BiomeAffinity.Leafwood:
            case BiomeAffinity.Rainleaf:
                return "Lá mầm ngọt & Quả rừng tươi";

            case BiomeAffinity.Bloomfield:
                return "Mật hoa tinh khiết & Cánh hoa thơm";

            case BiomeAffinity.Windheath:
            case BiomeAffinity.Streamstone:
                return "Khoáng thạch & Hạt thông";

            case BiomeAffinity.Reedmarsh:
            case BiomeAffinity.Lilywater:
                return "Rong biển ngọc & Tảo non";

            default:
                return "Thảo mộc tự nhiên & Trái cây";
        }
    }

    private static Sprite LoadSpriteFromDisk(string relativePath)
    {
        string fullPath = Path.Combine(Application.dataPath, relativePath.Replace("Assets/", ""));
        if (!File.Exists(fullPath)) fullPath = Path.Combine(Application.dataPath, relativePath);

        if (File.Exists(fullPath))
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(fullPath);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes))
                {
                    tex.filterMode = FilterMode.Bilinear;
                    tex.wrapMode = TextureWrapMode.Clamp;
                    return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HabitatCreaturePanelStyle] Không thể nạp ảnh {relativePath}: {ex.Message}");
            }
        }
        return null;
    }

    private static Sprite LoadSpriteWithBorders(string relativePath, Vector4 border, float ppu = 100f)
    {
        string fullPath = Path.Combine(Application.dataPath, relativePath.Replace("Assets/", ""));
        if (!File.Exists(fullPath)) fullPath = Path.Combine(Application.dataPath, relativePath);

        if (File.Exists(fullPath))
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(fullPath);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes))
                {
                    tex.filterMode = FilterMode.Bilinear;
                    tex.wrapMode = TextureWrapMode.Clamp;
                    return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect, border);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HabitatCreaturePanelStyle] Không thể nạp ảnh 9-slice {relativePath}: {ex.Message}");
            }
        }
        return null;
    }

    /// <summary>
    /// Tạo khung viền chữ nhật công nghệ cao (9-slice chuẩn không biến dạng)
    /// </summary>
    private static Sprite CreateTechBorderBox(int w, int h, int cornerRadius, Color fillColor, Color borderColor)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] px = new Color[w * h];
        float r = cornerRadius;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = (x < r) ? r - x : (x > w - 1 - r ? x - (w - 1 - r) : 0f);
                float dy = (y < r) ? r - y : (y > h - 1 - r ? y - (h - 1 - r) : 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist > r)
                {
                    px[y * w + x] = Color.clear;
                }
                else if (dist >= r - 1.2f || x == 0 || x == w - 1 || y == 0 || y == h - 1)
                {
                    px[y * w + x] = borderColor;
                }
                else
                {
                    px[y * w + x] = fillColor;
                }
            }
        }

        tex.SetPixels(px);
        tex.Apply();

        Vector4 border = new Vector4(cornerRadius + 2, cornerRadius + 2, cornerRadius + 2, cornerRadius + 2);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    /// <summary>
    /// Tạo thanh năng lượng gradient công nghệ cao
    /// </summary>
    private static Sprite CreateTechEnergyFill(int w, int h, Color leftCol, Color rightCol)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float t = (float)x / (w - 1);
                Color c = Color.Lerp(leftCol, rightCol, t);
                // Highlight viền trên làm sáng bóng
                if (y >= h - 2) c = Color.Lerp(c, Color.white, 0.45f);
                px[y * w + x] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(3, 3, 3, 3));
    }

    /// <summary>
    /// Nút bấm Game 3D có viền vát nổi và đổ bóng chân nút
    /// </summary>
    private static Sprite Create3DGameButton(int w, int h, int r)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = (x < r) ? r - x : (x > w - 1 - r ? x - (w - 1 - r) : 0f);
                float dy = (y < r) ? r - y : (y > h - 1 - r ? y - (h - 1 - r) : 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist > r)
                {
                    px[y * w + x] = Color.clear;
                }
                else if (y < 4) // Đổ bóng chân nút 3D
                {
                    px[y * w + x] = new Color(0f, 0f, 0f, 0.45f);
                }
                else if (y >= h - 2) // Viền sáng mép trên
                {
                    px[y * w + x] = new Color(1f, 1f, 1f, 0.35f);
                }
                else
                {
                    px[y * w + x] = Color.white;
                }
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r + 2, 6, r + 2, 6));
    }
}
