using System;
using System.Collections.Generic;
using UnityEngine;
using LumiWorld.Acs;

/// <summary>
/// Resolves canonical creature names, 16 official front-view portraits (Resources/UI/Production/Creatures),
/// 3D model prefabs (Resources/CreatureModels), and clean biome display names for P-05 / P-06.
/// </summary>
public static class HabitatCreaturePanelStyle
{
    private static readonly Dictionary<string, Sprite> creatureAvatarCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, (string canonicalName, string englishName, string prefabName)> speciesMap =
        new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase)
    {
        { "caterpillar", ("Petaluff", "Petaluff", "saubuom_anm") },
        { "saubuom", ("Petaluff", "Petaluff", "saubuom_anm") },
        { "petaluff", ("Petaluff", "Petaluff", "saubuom_anm") },

        { "deer", ("Bramblet", "Bramblet", "nai_anm") },
        { "nai", ("Bramblet", "Bramblet", "nai_anm") },
        { "bramblet", ("Bramblet", "Bramblet", "nai_anm") },

        { "flyingsquirrel", ("Spriglide", "Spriglide", "socbay_anm") },
        { "socbay", ("Spriglide", "Spriglide", "socbay_anm") },
        { "spriglide", ("Spriglide", "Spriglide", "socbay_anm") },

        { "grasshopper", ("Frondle", "Frondle", "chauchau_anm") },
        { "chauchau", ("Frondle", "Frondle", "chauchau_anm") },
        { "frondle", ("Frondle", "Frondle", "chauchau_anm") },

        { "flowerdog", ("Dandrift", "Dandrift", "chohoa_anm") },
        { "chohoa", ("Dandrift", "Dandrift", "chohoa_anm") },
        { "dandrift", ("Dandrift", "Dandrift", "chohoa_anm") },

        { "peacock", ("Galume", "Galume", "chimcong_anm") },
        { "chimcong", ("Galume", "Galume", "chimcong_anm") },
        { "galume", ("Galume", "Galume", "chimcong_anm") },

        { "highlandcow", ("Heathorn", "Heathorn", "bolongdai_anm") },
        { "bolongdai", ("Heathorn", "Heathorn", "bolongdai_anm") },
        { "heathorn", ("Heathorn", "Heathorn", "bolongdai_anm") },

        { "dragonfly", ("Iriwing", "Iriwing", "chuongchuong_anm") },
        { "chuongchuong", ("Iriwing", "Iriwing", "chuongchuong_anm") },
        { "iriwing", ("Iriwing", "Iriwing", "chuongchuong_anm") },

        { "otter", ("Rivlet", "Rivlet", "raica_anm") },
        { "raica", ("Rivlet", "Rivlet", "raica_anm") },
        { "rivlet", ("Rivlet", "Rivlet", "raica_anm") },

        { "capybara", ("Pebbara", "Pebbara", "capybara_anm") },
        { "pebbara", ("Pebbara", "Pebbara", "capybara_anm") },

        { "grassfox", ("Fernkin", "Fernkin", "caoco_anm") },
        { "caoco", ("Fernkin", "Fernkin", "caoco_anm") },
        { "fernkin", ("Fernkin", "Fernkin", "caoco_anm") },

        { "blossomfrog", ("Puddleaf", "Puddleaf", "echhoa_anm") },
        { "echhoa", ("Puddleaf", "Puddleaf", "echhoa_anm") },
        { "puddleaf", ("Puddleaf", "Puddleaf", "echhoa_anm") },

        { "parrot", ("Heliko", "Heliko", "chimvet7mau_anm") },
        { "chimvet7mau", ("Heliko", "Heliko", "chimvet7mau_anm") },
        { "heliko", ("Heliko", "Heliko", "chimvet7mau_anm") },

        { "cow", ("Heathorn", "Heathorn", "bo_anm") },
        { "bo", ("Heathorn", "Heathorn", "bo_anm") },

        { "fox", ("Fernkin", "Fernkin", "cao_anm") },
        { "cao", ("Fernkin", "Fernkin", "cao_anm") },

        { "reedle", ("Reedle", "Reedle", "thiennga_anm") },
        { "craggle", ("Craggle", "Craggle", "canoc_anm") },
        { "lotelle", ("Lotelle", "Lotelle", "echduoica_anm") },
        { "braluff", ("Braluff", "Braluff", "Braluff") },
        { "creature_braluff", ("Braluff", "Braluff", "Braluff") }
    };

    public static void ClearCache()
    {
        creatureAvatarCache.Clear();
    }

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
            dispName = mapped.englishName;
            pName = mapped.prefabName;
        }

        GameObject prefab = Resources.Load<GameObject>("CreatureModels/" + pName);
        Sprite cardSprite = null;

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
                    else if (!string.IsNullOrEmpty(cImgName) && !speciesMap.ContainsKey(clean))
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
        string cacheKey = "portrait_" + canonicalName;

        if (creatureAvatarCache.TryGetValue(cacheKey, out var cached) && cached != null)
        {
            return cached;
        }

<<<<<<< HEAD
        Texture2D rawTex = null;
        string filePath = Path.Combine(Application.dataPath, "AVT_ANM", canonicalName + ".png");
        if (File.Exists(filePath))
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                rawTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!rawTex.LoadImage(bytes)) rawTex = null;
            }
            catch (Exception) { rawTex = null; }
        }
        if (rawTex == null)
        {
            rawTex = Resources.Load<Texture2D>("CreatureAvatars/" + canonicalName);
        }
        if (rawTex != null && rawTex.isReadable)
        {
            try
            {
                if (rawTex != null)
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
=======
        // Official close-up Non-background portrait for P-05 Resident cards
        Sprite officialPortrait = LumiWorldUITheme.CreaturePortrait(canonicalName);
        if (officialPortrait != null)
        {
            creatureAvatarCache[cacheKey] = officialPortrait;
            return officialPortrait;
>>>>>>> Acs
        }

        if (resolved.cardSprite != null)
        {
            creatureAvatarCache[cacheKey] = resolved.cardSprite;
            return resolved.cardSprite;
        }

        return null;
    }

    public static Sprite GetCreatureDetailSprite(string speciesId)
    {
        var resolved = ResolveCreatureData(speciesId);
        string canonicalName = resolved.canonicalAvt;
        string cacheKey = "detail_" + canonicalName;

        if (creatureAvatarCache.TryGetValue(cacheKey, out var cached) && cached != null)
        {
            return cached;
        }

        // Full Non-background official creature artwork for P-06 Detail preview
        Sprite detailArt = LumiWorldUITheme.CreatureDetailArt(canonicalName) ?? LumiWorldUITheme.CreaturePortrait(canonicalName);
        if (detailArt != null)
        {
            creatureAvatarCache[cacheKey] = detailArt;
            return detailArt;
        }

        if (resolved.cardSprite != null)
        {
            creatureAvatarCache[cacheKey] = resolved.cardSprite;
            return resolved.cardSprite;
        }

        return null;
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

    /// <summary>
    /// Returns the clean canonical biome name (without "Habitat", "Forest", "Plains", etc.) per P-05 spec.
    /// </summary>
    public static string GetAffinityDisplayName(BiomeAffinity affinity)
    {
        switch (affinity)
        {
            case BiomeAffinity.Leafwood: return "Leafwood";
            case BiomeAffinity.Bloomfield: return "Bloomfield";
            case BiomeAffinity.Windheath: return "Windheath";
            case BiomeAffinity.Reedmarsh: return "Reedmarsh";
            case BiomeAffinity.Lilywater: return "Lilywater";
            case BiomeAffinity.Streamstone: return "Streamstone";
            case BiomeAffinity.Rainleaf: return "Rainleaf";
            default: return "Leafwood";
        }
    }

    public static Sprite GetResourceDetailCardBackground(BiomeAffinity affinity)
    {
        return LumiWorldUITheme.SurfaceSprite;
    }
}
