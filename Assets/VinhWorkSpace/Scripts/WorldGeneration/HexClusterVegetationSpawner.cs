using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PropArchetype = HexClusterPropResolver.PropArchetype;
using NearbyPropEntry = HexClusterPropResolver.NearbyPropEntry;

/// <summary>
/// Cấu hình tham số sinh thực vật kết nối cụm sinh thái (Seam & Corner Vegetation).
/// </summary>
[System.Serializable]
public class HexClusterVegetationConfig
{
    public bool spawnConnectorProps = true;
    public float mediumPropChance = 0.50f;
    public Vector2Int propsPerEdgeRange = new Vector2Int(2, 4);
    public float minMediumClearance = 0.24f;
    public float minSmallClearance = 0.11f;

    public bool spawnSeamMicroGrass = true;
    public GameObject seamMicroGrassPrefab;
    public Vector2Int seamMicroGrassCountRange = new Vector2Int(5, 8);
    public Vector2Int cornerMicroGrassCountRange = new Vector2Int(4, 7);
    public Vector2 seamMicroGrassScaleRange = new Vector2(1.10f, 1.80f);
    public float seamMicroGrassGroundEmbed = 0.005f;
    public float cornerFillerRadius = 0.12f;
}

/// <summary>
/// Chuyên trách sinh hoa cỏ, bụi cây, tảng đá rêu và mầm cỏ li ti dọc theo khe nối ranh giới (Seam)
/// và ngã ba (Corner Junction Hub) giữa các ô cùng loại trong LumiWorld.
/// </summary>
public static class HexClusterVegetationSpawner
{
    public static GameObject GetMicroGrassPrefab(CardData cardData, ref GameObject cachedPrefab)
    {
        if (cachedPrefab != null) return cachedPrefab;

        if (HexFilledGroundSpawner.Instance != null && HexFilledGroundSpawner.Instance.MicroGrassTuftPrefab != null)
        {
            cachedPrefab = HexFilledGroundSpawner.Instance.MicroGrassTuftPrefab;
            return cachedPrefab;
        }

        if (cardData != null && cardData.habitatProps != null)
        {
            foreach (var rule in cardData.habitatProps)
            {
                if (rule == null || rule.prefabs == null) continue;
                foreach (var p in rule.prefabs)
                {
                    if (p != null && p.name.IndexOf("MicroGrass", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        cachedPrefab = p;
                        return cachedPrefab;
                    }
                }
            }
        }

#if UNITY_EDITOR
        if (cachedPrefab == null)
        {
            cachedPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/VinhWorkSpace/Prefabs/Leafwood/05_FilledGround/MicroGrassTuft.prefab"
            );
        }
#endif

        return cachedPrefab;
    }

    public static bool ShouldSpawnSeamGrassForHabitat(CardData cardData, bool spawnSeamMicroGrass)
    {
        if (!spawnSeamMicroGrass) return false;
        if (cardData == null) return false;

        if (HexFilledGroundSpawner.Instance != null && HexFilledGroundSpawner.Instance.HasFilledGround(cardData))
            return true;

        string family = HexBiomeClusterConnector.GetHabitatFamily(cardData.cardName);
        if (string.Equals(family, "Leafwood", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(family, "Bloomfield", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (cardData.habitatProps != null)
        {
            foreach (var rule in cardData.habitatProps)
            {
                if (rule != null && !string.IsNullOrEmpty(rule.groupName))
                {
                    if (rule.groupName.IndexOf("FilledGround", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        rule.groupName.IndexOf("Grass", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public static void SpawnBorderMicroGrass(
        MonoBehaviour coroutineRunner,
        Transform container,
        Vector3 midPoint,
        Vector3 u,
        Vector3 v,
        float halfEdge,
        float surfaceY,
        CardData habitatData,
        HexClusterVegetationConfig config)
    {
        if (config == null) return;
        if (!ShouldSpawnSeamGrassForHabitat(habitatData, config.spawnSeamMicroGrass)) return;
        if (HexBiomeClusterConnector.IsPositionNearRiver(midPoint, 0.52f)) return;

        GameObject sproutPrefab = GetMicroGrassPrefab(habitatData, ref config.seamMicroGrassPrefab);
        if (sproutPrefab == null) return;

        int count = UnityEngine.Random.Range(config.seamMicroGrassCountRange.x, config.seamMicroGrassCountRange.y + 1);
        if (count <= 0) return;

        float usableSpan = halfEdge * 0.75f;
        float step = (usableSpan * 2f) / count;
        float startV = -usableSpan + step * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float vOffset = startV + i * step + UnityEngine.Random.Range(-step * 0.35f, step * 0.35f);
            float uOffset = UnityEngine.Random.Range(-0.065f, 0.065f);

            Vector3 spawnPos = midPoint + v * vOffset + u * uOffset;
            spawnPos.y = surfaceY;

            float scale = UnityEngine.Random.Range(config.seamMicroGrassScaleRange.x, config.seamMicroGrassScaleRange.y);
            float yOffset = HexHabitatSpawner.GetPrefabBottomYOffset(sproutPrefab, config.seamMicroGrassGroundEmbed) * scale;
            spawnPos.y += yOffset;

            Quaternion rot = Quaternion.Euler(
                UnityEngine.Random.Range(-3f, 3f),
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(-3f, 3f)
            ) * sproutPrefab.transform.rotation;

            GameObject sproutObj = UnityEngine.Object.Instantiate(sproutPrefab, spawnPos, rot, container);
            sproutObj.name = $"SeamSprout_{sproutPrefab.name}_{i}";
            sproutObj.transform.localScale = Vector3.zero;

            Collider[] colliders = sproutObj.GetComponentsInChildren<Collider>();
            for (int c = 0; c < colliders.Length; c++) colliders[c].enabled = false;

            Renderer[] renderers = sproutObj.GetComponentsInChildren<Renderer>();
            for (int r = 0; r < renderers.Length; r++)
            {
                renderers[r].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers[r].receiveShadows = true;
            }

            float delay = 0.04f + (float)i / count * 0.16f;
            Vector3 targetScale = sproutPrefab.transform.localScale * scale;
            if (coroutineRunner != null)
            {
                coroutineRunner.StartCoroutine(AnimatePropPopIn(sproutObj.transform, targetScale, delay));
            }
        }
    }

    public static void SpawnCornerMicroGrass(
        MonoBehaviour coroutineRunner,
        Transform container,
        Vector3 center,
        float surfaceY,
        CardData habitatData,
        HexClusterVegetationConfig config)
    {
        if (config == null) return;
        if (!ShouldSpawnSeamGrassForHabitat(habitatData, config.spawnSeamMicroGrass)) return;
        if (HexBiomeClusterConnector.IsPositionNearRiver(center, 0.60f)) return;

        GameObject sproutPrefab = GetMicroGrassPrefab(habitatData, ref config.seamMicroGrassPrefab);
        if (sproutPrefab == null) return;

        int count = UnityEngine.Random.Range(config.cornerMicroGrassCountRange.x, config.cornerMicroGrassCountRange.y + 1);
        if (count <= 0) return;

        float angleStep = 360f / count;

        for (int i = 0; i < count; i++)
        {
            float baseAngle = i * angleStep + UnityEngine.Random.Range(-15f, 15f);
            float rad = baseAngle * Mathf.Deg2Rad;
            float r = UnityEngine.Random.Range(0.04f, config.cornerFillerRadius * 1.15f);

            Vector3 spawnPos = center + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * r;
            spawnPos.y = surfaceY;

            float scale = UnityEngine.Random.Range(config.seamMicroGrassScaleRange.x, config.seamMicroGrassScaleRange.y);
            float yOffset = HexHabitatSpawner.GetPrefabBottomYOffset(sproutPrefab, config.seamMicroGrassGroundEmbed) * scale;
            spawnPos.y += yOffset;

            Quaternion rot = Quaternion.Euler(
                UnityEngine.Random.Range(-3f, 3f),
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(-3f, 3f)
            ) * sproutPrefab.transform.rotation;

            GameObject sproutObj = UnityEngine.Object.Instantiate(sproutPrefab, spawnPos, rot, container);
            sproutObj.name = $"CornerSprout_{sproutPrefab.name}_{i}";
            sproutObj.transform.localScale = Vector3.zero;

            Collider[] colliders = sproutObj.GetComponentsInChildren<Collider>();
            for (int c = 0; c < colliders.Length; c++) colliders[c].enabled = false;

            Renderer[] renderers = sproutObj.GetComponentsInChildren<Renderer>();
            for (int rend = 0; rend < renderers.Length; rend++)
            {
                renderers[rend].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers[rend].receiveShadows = true;
            }

            float delay = 0.06f + (float)i / count * 0.16f;
            Vector3 targetScale = sproutPrefab.transform.localScale * scale;
            if (coroutineRunner != null)
            {
                coroutineRunner.StartCoroutine(AnimatePropPopIn(sproutObj.transform, targetScale, delay));
            }
        }
    }

    public static void SpawnEdgeConnectorProps(
        MonoBehaviour coroutineRunner,
        Transform container,
        GameObject tileA,
        GameObject tileB,
        CardData cardData,
        HexClusterVegetationConfig config)
    {
        if (config == null || cardData == null || !cardData.HasHabitatProps()) return;

        Vector3 posA = tileA.transform.position;
        Vector3 posB = tileB.transform.position;

        Vector3 dirXZ = new Vector3(posB.x - posA.x, 0f, posB.z - posA.z);
        Vector3 u = dirXZ.normalized;
        Vector3 v = new Vector3(-u.z, 0f, u.x);
        Vector3 midPoint = (posA + posB) * 0.5f;

        var (topYA, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(tileA);
        var (topYB, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(tileB);
        if (Mathf.Abs(topYA - topYB) > 0.15f) return;

        HexTileInfo infoA = tileA.GetComponent<HexTileInfo>();
        HexTileInfo infoB = tileB.GetComponent<HexTileInfo>();
        if (infoA != null && infoB != null && RiverNetworkManager.Instance != null)
        {
            RiverTileSegment segA = RiverNetworkManager.Instance.GetRiver(infoA.coordinates);
            RiverTileSegment segB = RiverNetworkManager.Instance.GetRiver(infoB.coordinates);
            for (int d = 0; d < 6; d++)
            {
                if (segA != null && infoA.coordinates.GetNeighbor(d) == infoB.coordinates && segA.HasPort(d))
                    return;
                if (segB != null && infoB.coordinates.GetNeighbor(d) == infoA.coordinates && segB.HasPort(d))
                    return;
            }
        }

        if (HexBiomeClusterConnector.IsPositionNearRiver(midPoint, 0.65f)) return;

        float topY = (topYA + topYB) * 0.5f;

        List<NearbyPropEntry> nearbyEntries = HexClusterPropResolver.GetNearbyPropEntries(container, midPoint, 1.25f);

        bool hasSpawnedMedium = false;
        Vector3 medSpawnPos = midPoint;
        float chosenMedV = 0f;
        GameObject spawnedMedPrefab = null;
        PropArchetype spawnedMedArchetype = PropArchetype.FernBush;

        bool tryMedium = (UnityEngine.Random.value < config.mediumPropChance);
        if (tryMedium)
        {
            GameObject medPrefab = HexClusterPropResolver.GetMediumPropPrefab(cardData, nearbyEntries, midPoint, out CardData.HabitatPropRule medRule, out PropArchetype medArch);
            if (medPrefab != null)
            {
                float[] candidateVOffsets = new float[] { -0.18f, 0.18f, -0.09f, 0.09f, 0f, -0.24f, 0.24f };
                float bestDist = -1f;
                Vector3 bestPos = midPoint;
                float bestV = 0f;

                for (int i = 0; i < candidateVOffsets.Length; i++)
                {
                    float candV = candidateVOffsets[i] + UnityEngine.Random.Range(-0.03f, 0.03f);
                    float candU = UnityEngine.Random.Range(-0.06f, 0.06f);
                    Vector3 testPos = midPoint + v * candV + u * candU;
                    testPos.y = topY;

                    float minDist = float.MaxValue;
                    for (int p = 0; p < nearbyEntries.Count; p++)
                    {
                        float d = Vector2.Distance(new Vector2(testPos.x, testPos.z), new Vector2(nearbyEntries[p].position.x, nearbyEntries[p].position.z));
                        if (d < minDist) minDist = d;
                    }

                    if (minDist > bestDist)
                    {
                        bestDist = minDist;
                        bestPos = testPos;
                        bestV = candV;
                    }
                }

                if (bestDist >= config.minMediumClearance)
                {
                    SpawnConnectorPropInstance(coroutineRunner, container, medPrefab, bestPos, medRule, 0.90f, true, 0.03f, "ConnectorMedProp");
                    medSpawnPos = bestPos;
                    chosenMedV = bestV;
                    hasSpawnedMedium = true;
                    spawnedMedPrefab = medPrefab;
                    spawnedMedArchetype = medArch;
                    nearbyEntries.Add(new NearbyPropEntry { position = bestPos, name = medPrefab.name, archetype = medArch });
                }
            }
        }

        var eligibleSmall = HexClusterPropResolver.GetEligibleSmallPropsList(cardData);
        if (eligibleSmall.Count == 0) return;

        int smallCount = UnityEngine.Random.Range(config.propsPerEdgeRange.x, config.propsPerEdgeRange.y + 1);
        HashSet<GameObject> usedPrefabsOnEdge = new HashSet<GameObject>();
        HashSet<PropArchetype> usedArchetypesOnEdge = new HashSet<PropArchetype>();

        if (hasSpawnedMedium && spawnedMedPrefab != null)
        {
            usedPrefabsOnEdge.Add(spawnedMedPrefab);
            usedArchetypesOnEdge.Add(spawnedMedArchetype);
        }

        for (int i = 0; i < smallCount; i++)
        {
            var picked = HexClusterPropResolver.PickDiverseSmallProp(eligibleSmall, usedPrefabsOnEdge, usedArchetypesOnEdge, nearbyEntries);
            if (picked.prefab == null) continue;

            GameObject smallPrefab = picked.prefab;
            CardData.HabitatPropRule smallRule = picked.rule;

            usedPrefabsOnEdge.Add(picked.prefab);
            usedArchetypesOnEdge.Add(picked.arch);

            Vector3 smallPos = midPoint;
            bool foundSpot = false;

            if (hasSpawnedMedium && i == 0)
            {
                for (int t = 0; t < 12; t++)
                {
                    float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    float satDist = UnityEngine.Random.Range(0.12f, 0.20f);
                    Vector3 candSat = medSpawnPos + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * satDist;
                    candSat.y = topY;

                    float dToMed = Vector2.Distance(new Vector2(candSat.x, candSat.z), new Vector2(medSpawnPos.x, medSpawnPos.z));
                    if (dToMed >= 0.10f)
                    {
                        smallPos = candSat;
                        foundSpot = true;
                        break;
                    }
                }
            }
            else
            {
                float bestDist = -1f;
                Vector3 bestCandidate = midPoint;

                for (int t = 0; t < 10; t++)
                {
                    float offsetV = (hasSpawnedMedium)
                        ? ((chosenMedV > 0f ? -1f : 1f) * UnityEngine.Random.Range(0.10f, 0.28f))
                        : UnityEngine.Random.Range(-0.25f, 0.25f);

                    float offsetU = UnityEngine.Random.Range(-0.05f, 0.05f);
                    Vector3 cand = midPoint + v * offsetV + u * offsetU;
                    cand.y = topY;

                    float minDist = float.MaxValue;
                    for (int p = 0; p < nearbyEntries.Count; p++)
                    {
                        float d = Vector2.Distance(new Vector2(cand.x, cand.z), new Vector2(nearbyEntries[p].position.x, nearbyEntries[p].position.z));
                        if (d < minDist) minDist = d;
                    }

                    if (minDist > bestDist)
                    {
                        bestDist = minDist;
                        bestCandidate = cand;
                    }
                }

                if (bestDist >= config.minSmallClearance)
                {
                    smallPos = bestCandidate;
                    foundSpot = true;
                }
            }

            if (foundSpot)
            {
                SpawnConnectorPropInstance(coroutineRunner, container, smallPrefab, smallPos, smallRule, 0.88f, false, 0.04f * (i + 1), $"ConnectorProp_{i}");
                nearbyEntries.Add(new NearbyPropEntry { position = smallPos, name = smallPrefab.name, archetype = picked.arch });
            }
        }
    }

    public static void SpawnClusterCornerProps(
        MonoBehaviour coroutineRunner,
        Transform container,
        GameObject centerTile,
        GameObject tileB,
        GameObject tileC,
        CardData cardData,
        HexClusterVegetationConfig config)
    {
        if (config == null || cardData == null || !cardData.HasHabitatProps()) return;
        if (centerTile == null || tileB == null || tileC == null) return;

        Vector3 posA = centerTile.transform.position;
        Vector3 posB = tileB.transform.position;
        Vector3 posC = tileC.transform.position;

        var (topYA, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(centerTile);
        var (topYB, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(tileB);
        var (topYC, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(tileC);
        if (Mathf.Abs(topYA - topYB) > 0.15f || Mathf.Abs(topYA - topYC) > 0.15f) return;

        float topY = (topYA + topYB + topYC) / 3f;
        Vector3 center = (posA + posB + posC) / 3f;
        center.y = topY;

        if (HexBiomeClusterConnector.IsPositionNearRiver(center, 0.85f)) return;

        List<NearbyPropEntry> nearbyEntries = HexClusterPropResolver.GetNearbyPropEntries(container, center, 1.25f);

        GameObject centerTreePrefab = HexClusterPropResolver.GetMediumTreeOrCenterpiecePrefab(cardData, nearbyEntries, center, out CardData.HabitatPropRule centerRule, out PropArchetype centerArch);

        if (centerTreePrefab != null)
        {
            SpawnConnectorPropInstance(coroutineRunner, container, centerTreePrefab, center, centerRule, 0.95f, true, 0.03f, "ClusterCenterTree");
            nearbyEntries.Add(new NearbyPropEntry { position = center, name = centerTreePrefab.name, archetype = centerArch });
        }

        var eligibleSmall = HexClusterPropResolver.GetEligibleSmallPropsList(cardData);
        if (eligibleSmall.Count == 0) return;

        HashSet<GameObject> usedPrefabsInCluster = new HashSet<GameObject>();
        HashSet<PropArchetype> usedArchetypesInCluster = new HashSet<PropArchetype>();

        if (centerTreePrefab != null)
        {
            usedPrefabsInCluster.Add(centerTreePrefab);
            usedArchetypesInCluster.Add(centerArch);
        }

        Vector3[] edgeMidpoints = new Vector3[]
        {
            (posA + posB) * 0.5f,
            (posA + posC) * 0.5f
        };

        float delayTracker = 0.07f;

        for (int e = 0; e < edgeMidpoints.Length; e++)
        {
            Vector3 mid = edgeMidpoints[e];
            Vector3 dir = mid - center;
            dir.y = 0f;
            float edgeDist = dir.magnitude;
            if (edgeDist < 0.05f) continue;
            dir = dir.normalized;
            Vector3 perp = new Vector3(-dir.z, 0f, dir.x);

            int propsOnThisEdge = UnityEngine.Random.Range(1, 3);

            for (int pIdx = 0; pIdx < propsOnThisEdge; pIdx++)
            {
                var picked = HexClusterPropResolver.PickDiverseSmallProp(eligibleSmall, usedPrefabsInCluster, usedArchetypesInCluster, nearbyEntries);
                if (picked.prefab == null) continue;

                usedPrefabsInCluster.Add(picked.prefab);
                usedArchetypesInCluster.Add(picked.arch);

                float dFromCenter = (pIdx == 0)
                    ? UnityEngine.Random.Range(0.24f, 0.32f)
                    : UnityEngine.Random.Range(0.42f, 0.52f);

                float lateralJitter = UnityEngine.Random.Range(-0.045f, 0.045f);
                Vector3 spawnPos = center + dir * dFromCenter + perp * lateralJitter;
                spawnPos.y = topY;

                float minDist = float.MaxValue;
                for (int n = 0; n < nearbyEntries.Count; n++)
                {
                    float d = Vector2.Distance(
                        new Vector2(spawnPos.x, spawnPos.z),
                        new Vector2(nearbyEntries[n].position.x, nearbyEntries[n].position.z)
                    );
                    if (d < minDist) minDist = d;
                }

                if (minDist >= 0.12f)
                {
                    SpawnConnectorPropInstance(coroutineRunner, container, picked.prefab, spawnPos, picked.rule, 0.88f, false, delayTracker, $"ClusterEdgeProp_{e}_{pIdx}");
                    nearbyEntries.Add(new NearbyPropEntry { position = spawnPos, name = picked.prefab.name, archetype = picked.arch });
                    delayTracker += 0.04f;
                }
            }
        }
    }

    public static GameObject SpawnConnectorPropInstance(
        MonoBehaviour coroutineRunner,
        Transform container,
        GameObject prefab,
        Vector3 spawnPos,
        CardData.HabitatPropRule rule,
        float scaleMultiplier,
        bool isMedium,
        float animDelay,
        string namePrefix)
    {
        if (prefab == null) return null;

        float randomScale = (rule != null)
            ? UnityEngine.Random.Range(rule.scaleRange.x, rule.scaleRange.y) * scaleMultiplier
            : scaleMultiplier;

        float customEmbed = (rule != null) ? rule.customGroundEmbed : 0f;
        float yOffset = HexHabitatSpawner.GetPrefabBottomYOffset(prefab, customEmbed) * randomScale;
        spawnPos.y += yOffset;

        Quaternion rot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * prefab.transform.rotation;

        GameObject propObj = UnityEngine.Object.Instantiate(prefab, spawnPos, rot, container);
        propObj.name = $"{namePrefix}_{prefab.name}";
        propObj.transform.localScale = Vector3.zero;

        Collider[] colliders = propObj.GetComponentsInChildren<Collider>();
        for (int c = 0; c < colliders.Length; c++) colliders[c].enabled = false;

        Renderer[] renderers = propObj.GetComponentsInChildren<Renderer>();
        for (int r = 0; r < renderers.Length; r++)
        {
            renderers[r].shadowCastingMode = isMedium ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        Vector3 targetScale = prefab.transform.localScale * randomScale;
        if (coroutineRunner != null)
        {
            coroutineRunner.StartCoroutine(AnimatePropPopIn(propObj.transform, targetScale, animDelay));
        }
        return propObj;
    }

    public static IEnumerator AnimatePropPopIn(Transform target, Vector3 finalScale, float delay)
    {
        if (target == null) yield break;

        if (delay > 0f) yield return new WaitForSeconds(delay);

        float duration = 0.25f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (target == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float overshoot = 1f + c3 * Mathf.Pow(t - 1f, 3) + c1 * Mathf.Pow(t - 1f, 2);

            target.localScale = finalScale * overshoot;
            yield return null;
        }

        if (target != null) target.localScale = finalScale;
    }
}

