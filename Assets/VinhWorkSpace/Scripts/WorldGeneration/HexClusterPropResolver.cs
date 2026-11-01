using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chuyên trách phân tích sinh thái, lọc và lựa chọn thảm thực vật / props trang trí
/// cho các đường nối và ngã ba giữa các ô lục giác trong cụm Biome.
/// Áp dụng thuật toán đa dạng hóa nghịch đảo tần suất (Dynamic Inverse-Frequency Scoring)
/// để tạo ra cảnh quan tự nhiên phong phú, tránh lặp lại hoặc chụm cục các loại prop.
/// </summary>
public static class HexClusterPropResolver
{
    public enum PropArchetype
    {
        FernBush,    // Dương xỉ, bụi lá, bụi oải hương, thạch nam
        Flower,      // Hoa chuông, hoa cúc, hoa dại
        Mushroom,    // Nấm rừng đốm đỏ
        RockAccent,  // Tảng đá rêu, phiến đá, sỏi hoa
        WoodAccent,  // Khúc gỗ mục, gốc cây rêu
        Tree,        // Cây thân gỗ phụ (birch, willow)
        Grass        // Cỏ khóm, thảm cỏ
    }

    public struct NearbyPropEntry
    {
        public Vector3 position;
        public string name;
        public PropArchetype archetype;
    }

    public static PropArchetype GetArchetypeFromName(string name)
    {
        if (string.IsNullOrEmpty(name)) return PropArchetype.FernBush;

        if (name.IndexOf("Log", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Stump", StringComparison.OrdinalIgnoreCase) >= 0)
            return PropArchetype.WoodAccent;

        if (name.IndexOf("Toadstool", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Mushroom", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Fungi", StringComparison.OrdinalIgnoreCase) >= 0)
            return PropArchetype.Mushroom;

        if (name.IndexOf("Bluebell", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Bellflower", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Flower", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Daisy", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Blossom", StringComparison.OrdinalIgnoreCase) >= 0)
            return PropArchetype.Flower;

        if (name.IndexOf("Boulder", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Slab", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Rock", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Pebble", StringComparison.OrdinalIgnoreCase) >= 0)
            return PropArchetype.RockAccent;

        if (name.IndexOf("Grass", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Tussock", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Carpet", StringComparison.OrdinalIgnoreCase) >= 0)
            return PropArchetype.Grass;

        if (name.IndexOf("Tree", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Birch", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Willow", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Oak", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Pine", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Sentinel", StringComparison.OrdinalIgnoreCase) >= 0)
            return PropArchetype.Tree;

        if (name.IndexOf("Fern", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Bush", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Hazel", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Shrub", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Lavender", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Heather", StringComparison.OrdinalIgnoreCase) >= 0)
            return PropArchetype.FernBush;

        return PropArchetype.FernBush;
    }

    public static PropArchetype GetPropArchetype(GameObject prefab, CardData.HabitatPropRule rule)
    {
        string combined = (prefab != null ? prefab.name : "") + " " + (rule != null ? rule.groupName : "");
        return GetArchetypeFromName(combined);
    }

    public static int CountArchetypeNearby(List<NearbyPropEntry> entries, PropArchetype arch)
    {
        if (entries == null) return 0;
        int count = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].archetype == arch) count++;
        }
        return count;
    }

    public static List<NearbyPropEntry> GetNearbyPropEntries(Transform connectionsContainer, Vector3 checkCenter, float radius)
    {
        List<NearbyPropEntry> entries = new List<NearbyPropEntry>();
        float sqrRadius = radius * radius;

        if (connectionsContainer != null)
        {
            for (int i = 0; i < connectionsContainer.childCount; i++)
            {
                Transform child = connectionsContainer.GetChild(i);
                if (child.name.StartsWith("Connector") || child.name.StartsWith("Corner"))
                {
                    Vector3 p = child.position;
                    float d2 = (p.x - checkCenter.x) * (p.x - checkCenter.x) + (p.z - checkCenter.z) * (p.z - checkCenter.z);
                    if (d2 <= sqrRadius)
                    {
                        entries.Add(new NearbyPropEntry
                        {
                            position = p,
                            name = child.name,
                            archetype = GetArchetypeFromName(child.name)
                        });
                    }
                }
            }
        }

        HexWorldGenerator worldGen = UnityEngine.Object.FindAnyObjectByType<HexWorldGenerator>();
        if (worldGen != null && worldGen.MapTiles != null)
        {
            foreach (var pair in worldGen.MapTiles)
            {
                GameObject tile = pair.Value;
                if (tile == null) continue;

                Vector3 tPos = tile.transform.position;
                float tileDist2 = (tPos.x - checkCenter.x) * (tPos.x - checkCenter.x) + (tPos.z - checkCenter.z) * (tPos.z - checkCenter.z);
                if (tileDist2 > 4.5f) continue;

                for (int i = 0; i < tile.transform.childCount; i++)
                {
                    Transform child = tile.transform.GetChild(i);
                    if (child.name.StartsWith("Habitat_") || child.name.StartsWith("PlacedCard_"))
                    {
                        for (int j = 0; j < child.childCount; j++)
                        {
                            Transform propTransform = child.GetChild(j);
                            Vector3 propPos = propTransform.position;
                            float d2 = (propPos.x - checkCenter.x) * (propPos.x - checkCenter.x) + (propPos.z - checkCenter.z) * (propPos.z - checkCenter.z);
                            if (d2 <= sqrRadius)
                            {
                                entries.Add(new NearbyPropEntry
                                {
                                    position = propPos,
                                    name = propTransform.name,
                                    archetype = GetArchetypeFromName(propTransform.name)
                                });
                            }
                        }
                    }
                }
            }
        }

        return entries;
    }

    public static List<(GameObject prefab, CardData.HabitatPropRule rule, PropArchetype archetype)> GetEligibleSmallPropsList(CardData cardData)
    {
        List<(GameObject prefab, CardData.HabitatPropRule rule, PropArchetype archetype)> list =
            new List<(GameObject, CardData.HabitatPropRule, PropArchetype)>();

        if (cardData == null || cardData.habitatProps == null) return list;

        foreach (var rule in cardData.habitatProps)
        {
            if (rule == null || rule.prefabs == null || rule.prefabs.Count == 0) continue;
            string gName = rule.groupName ?? "";

            if (gName.IndexOf("Anchor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Hero", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Peak", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Colossus", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            foreach (var p in rule.prefabs)
            {
                if (p == null) continue;
                string pName = p.name;

                if (pName.IndexOf("Tree", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Oak", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Pine", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Birch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Willow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Peak", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Anchor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Hero", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Sentinel", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                PropArchetype arch = GetPropArchetype(p, rule);
                if (arch == PropArchetype.FernBush ||
                    arch == PropArchetype.Flower ||
                    arch == PropArchetype.Mushroom ||
                    arch == PropArchetype.RockAccent ||
                    arch == PropArchetype.WoodAccent ||
                    arch == PropArchetype.Grass)
                {
                    list.Add((p, rule, arch));
                }
            }
        }

        return list;
    }

    public static GameObject GetSmallPropPrefab(CardData cardData, out CardData.HabitatPropRule matchedRule)
    {
        matchedRule = null;
        var list = GetEligibleSmallPropsList(cardData);
        if (list.Count > 0)
        {
            var item = list[UnityEngine.Random.Range(0, list.Count)];
            matchedRule = item.rule;
            return item.prefab;
        }
        return null;
    }

    public static (GameObject prefab, CardData.HabitatPropRule rule, PropArchetype arch) PickDiverseSmallProp(
        List<(GameObject prefab, CardData.HabitatPropRule rule, PropArchetype archetype)> eligibleSmall,
        HashSet<GameObject> usedPrefabs,
        HashSet<PropArchetype> usedArchetypes,
        List<NearbyPropEntry> nearbyEntries)
    {
        if (eligibleSmall == null || eligibleSmall.Count == 0)
            return (null, null, PropArchetype.FernBush);

        List<(GameObject prefab, CardData.HabitatPropRule rule, PropArchetype arch, float score)> scored =
            new List<(GameObject, CardData.HabitatPropRule, PropArchetype, float)>();

        for (int c = 0; c < eligibleSmall.Count; c++)
        {
            var item = eligibleSmall[c];
            if (usedPrefabs != null && usedPrefabs.Contains(item.prefab)) continue;

            float baseScore;
            switch (item.archetype)
            {
                case PropArchetype.Flower: baseScore = 28f; break;
                case PropArchetype.Mushroom: baseScore = 26f; break;
                case PropArchetype.FernBush: baseScore = 24f; break;
                case PropArchetype.RockAccent: baseScore = 22f; break;
                case PropArchetype.Grass: baseScore = 20f; break;
                case PropArchetype.WoodAccent: baseScore = 14f; break;
                default: baseScore = 20f; break;
            }

            if (usedArchetypes != null && usedArchetypes.Contains(item.archetype))
            {
                baseScore *= 0.08f;
            }

            int archCount = CountArchetypeNearby(nearbyEntries, item.archetype);
            float diversityMult = 1.0f / (1.0f + archCount * 1.25f);
            if (archCount == 0) diversityMult *= 1.35f;

            scored.Add((item.prefab, item.rule, item.archetype, baseScore * diversityMult));
        }

        if (scored.Count == 0)
        {
            for (int c = 0; c < eligibleSmall.Count; c++)
            {
                var item = eligibleSmall[c];
                scored.Add((item.prefab, item.rule, item.archetype, 10f));
            }
        }

        float totalScore = 0f;
        for (int s = 0; s < scored.Count; s++) totalScore += scored[s].score;

        float rVal = UnityEngine.Random.Range(0f, totalScore);
        float acc = 0f;
        var picked = scored[scored.Count - 1];
        for (int s = 0; s < scored.Count; s++)
        {
            acc += scored[s].score;
            if (rVal <= acc)
            {
                picked = scored[s];
                break;
            }
        }

        return (picked.prefab, picked.rule, picked.arch);
    }

    public static GameObject GetMediumPropPrefab(
        CardData cardData,
        List<NearbyPropEntry> nearbyEntries,
        Vector3 spawnTarget,
        out CardData.HabitatPropRule matchedRule,
        out PropArchetype chosenArchetype)
    {
        matchedRule = null;
        chosenArchetype = PropArchetype.FernBush;
        if (cardData == null || cardData.habitatProps == null || cardData.habitatProps.Count == 0)
            return null;

        List<(GameObject prefab, CardData.HabitatPropRule rule, PropArchetype arch, float weight)> candidates =
            new List<(GameObject, CardData.HabitatPropRule, PropArchetype, float)>();

        foreach (var rule in cardData.habitatProps)
        {
            if (rule == null || rule.prefabs == null || rule.prefabs.Count == 0) continue;
            string gName = rule.groupName ?? "";

            if (gName.IndexOf("Anchor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Hero", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Peak", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Colossus", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            foreach (var p in rule.prefabs)
            {
                if (p == null) continue;
                string pName = p.name;
                if (pName.IndexOf("Anchor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Hero", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Peak", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                if (pName.IndexOf("Micro", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Toadstool", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Bluebell", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Daisy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Pebble", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                PropArchetype arch = GetPropArchetype(p, rule);

                float baseWeight;
                switch (arch)
                {
                    case PropArchetype.FernBush: baseWeight = 30f; break;
                    case PropArchetype.RockAccent: baseWeight = 26f; break;
                    case PropArchetype.Flower: baseWeight = 22f; break;
                    case PropArchetype.Grass: baseWeight = 18f; break;
                    case PropArchetype.WoodAccent: baseWeight = 15f; break;
                    case PropArchetype.Tree: baseWeight = 12f; break;
                    default: baseWeight = 15f; break;
                }

                bool duplicateTooClose = false;
                if (nearbyEntries != null)
                {
                    for (int e = 0; e < nearbyEntries.Count; e++)
                    {
                        float d = Vector2.Distance(
                            new Vector2(spawnTarget.x, spawnTarget.z),
                            new Vector2(nearbyEntries[e].position.x, nearbyEntries[e].position.z)
                        );

                        if (d < 0.50f && nearbyEntries[e].name.IndexOf(p.name, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            duplicateTooClose = true;
                            break;
                        }

                        if (d < 0.55f && (arch == PropArchetype.WoodAccent || arch == PropArchetype.Tree))
                        {
                            if (nearbyEntries[e].archetype == PropArchetype.WoodAccent || nearbyEntries[e].archetype == PropArchetype.Tree)
                            {
                                duplicateTooClose = true;
                                break;
                            }
                        }
                    }
                }

                if (duplicateTooClose) continue;

                int archetypeCount = CountArchetypeNearby(nearbyEntries, arch);
                float diversityFactor = 1.0f / (1.0f + archetypeCount * 1.35f);

                if (archetypeCount == 0)
                {
                    diversityFactor *= 1.30f;
                }

                float finalWeight = baseWeight * diversityFactor;
                if (finalWeight > 0.01f)
                {
                    candidates.Add((p, rule, arch, finalWeight));
                }
            }
        }

        if (candidates.Count > 0)
        {
            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++) totalWeight += candidates[i].weight;

            float r = UnityEngine.Random.Range(0f, totalWeight);
            float accum = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                accum += candidates[i].weight;
                if (r <= accum)
                {
                    matchedRule = candidates[i].rule;
                    chosenArchetype = candidates[i].arch;
                    return candidates[i].prefab;
                }
            }

            matchedRule = candidates[candidates.Count - 1].rule;
            chosenArchetype = candidates[candidates.Count - 1].arch;
            return candidates[candidates.Count - 1].prefab;
        }

        return null;
    }

    public static GameObject GetMediumTreeOrCenterpiecePrefab(
        CardData cardData,
        List<NearbyPropEntry> nearbyEntries,
        Vector3 spawnTarget,
        out CardData.HabitatPropRule matchedRule,
        out PropArchetype chosenArchetype)
    {
        matchedRule = null;
        chosenArchetype = PropArchetype.Tree;

        if (cardData == null || cardData.habitatProps == null || cardData.habitatProps.Count == 0)
            return null;

        List<(GameObject prefab, CardData.HabitatPropRule rule, PropArchetype arch)> treeCandidates =
            new List<(GameObject, CardData.HabitatPropRule, PropArchetype)>();

        foreach (var rule in cardData.habitatProps)
        {
            if (rule == null || rule.prefabs == null || rule.prefabs.Count == 0) continue;
            string gName = rule.groupName ?? "";

            if (gName.IndexOf("Anchor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Hero", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Peak", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Colossus", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            foreach (var p in rule.prefabs)
            {
                if (p == null) continue;
                string pName = p.name;

                if (pName.IndexOf("Anchor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Hero", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Peak", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Micro", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                PropArchetype arch = GetPropArchetype(p, rule);
                if (arch == PropArchetype.Tree ||
                    gName.IndexOf("Tree", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Tree", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Oak", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Pine", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Birch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Willow", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    treeCandidates.Add((p, rule, PropArchetype.Tree));
                }
            }
        }

        if (treeCandidates.Count > 0)
        {
            var bestCandidate = treeCandidates[0];
            float bestScore = -999f;

            for (int i = 0; i < treeCandidates.Count; i++)
            {
                var cand = treeCandidates[i];
                float score = 10f;
                if (nearbyEntries != null)
                {
                    for (int e = 0; e < nearbyEntries.Count; e++)
                    {
                        if (nearbyEntries[e].name.IndexOf(cand.prefab.name, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            score -= 6f;
                        }
                    }
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCandidate = cand;
                }
            }

            matchedRule = bestCandidate.rule;
            chosenArchetype = PropArchetype.Tree;
            return bestCandidate.prefab;
        }

        GameObject medPrefab = GetMediumPropPrefab(cardData, nearbyEntries, spawnTarget, out matchedRule, out chosenArchetype);
        if (medPrefab != null)
        {
            return medPrefab;
        }

        return GetSmallPropPrefab(cardData, out matchedRule);
    }
}

