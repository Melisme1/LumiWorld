using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Quản lý việc tính toán không gian 3D, quét 16 sector tìm hành lang di chuyển thông thoáng (Sector Scan),
/// cơ chế tự giải thoát góc kẹt (Unstuck / Backtrack) và tính toán vector né tránh tiếp tuyến (Tangential Steering)
/// cho động vật trong LumiWorld.
/// </summary>
public static class AnimalSectorNavigator
{
    private const int NumSectors = 16;
    private const float OpenThreshold = 0.20f;

    /// <summary>
    /// Tìm điểm đến tự nhiên trên mặt đất bằng phương pháp quét 360 độ (16 Sector Scan):
    /// - Đo khoảng cách thông thoáng thực tế theo từng hướng (né tránh cây, đá, gốc cây, mép ô và thú khác).
    /// - Nhận biết các khe hở / hành lang di chuyển thoáng đãng (Clear Corridors).
    /// - Tự động phát hiện góc kẹt và kích hoạt cơ chế Backtrack / Escape ra bãi cỏ mở phía sau.
    /// </summary>
    public static Vector3 PickGroundDestination(
        Vector3 currentPos,
        HexCoordinates currentHex,
        HexCoordinates targetHex,
        Vector3 hexCenter,
        float maxWanderRadius,
        float safeHexRadius,
        bool preferCameraFacingGlade,
        Vector3 currentForward,
        float personalRadius,
        AnimalMovementAI currentAnimal,
        List<AnimalMovementAI> activeAnimals,
        List<AnimalMovementAI.TileObstacle> tempNearbyObstacles,
        HexWorldGenerator worldGen,
        Func<HexCoordinates, bool> canMoveToHex)
    {
        float maxSearchRange = (targetHex != currentHex) ? 1.75f : maxWanderRadius;

        // Lấy danh sách vật cản trong bán kính lân cận
        AnimalObstacleScanner.GetNearbyObstacles(worldGen, currentPos, 1.8f, tempNearbyObstacles);

        // Hướng mong muốn chính (Primary Bias Direction)
        Vector3 biasDir;
        if (targetHex != currentHex)
        {
            biasDir = hexCenter - currentPos;
            biasDir.y = 0f;
            biasDir = biasDir.sqrMagnitude > 0.001f ? biasDir.normalized : currentForward;
        }
        else
        {
            biasDir = preferCameraFacingGlade ? (new Vector3(0f, 0f, -1f) + currentForward * 0.4f).normalized : currentForward;
        }

        float bestClearDist = -1f;
        int bestClearIdx = -1;

        Vector3[] sectorDirs = new Vector3[NumSectors];
        float[] clearDists = new float[NumSectors];

        for (int i = 0; i < NumSectors; i++)
        {
            float angleDeg = i * (360f / NumSectors);
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            sectorDirs[i] = dir;

            float clearDist = maxSearchRange;

            // 1. Kiểm tra va chạm với vật cản tĩnh (Thân cây, gốc cây, tảng đá, khúc gỗ)
            for (int o = 0; o < tempNearbyObstacles.Count; o++)
            {
                AnimalMovementAI.TileObstacle obs = tempNearbyObstacles[o];
                Vector3 toObs = obs.worldPos - currentPos;
                toObs.y = 0f;

                float proj = Vector3.Dot(toObs, dir);
                if (proj > 0f) // Vật cản nằm ở bán cầu phía trước tia quét
                {
                    float perpSqr = toObs.sqrMagnitude - proj * proj;
                    float safeClearance = obs.radius + AnimalObstacleScanner.PropObstacleBodyRadius + 0.02f;

                    if (perpSqr < safeClearance * safeClearance)
                    {
                        // Tia quét cắt qua vành bảo vệ của vật thể
                        float halfChord = Mathf.Sqrt(Mathf.Max(0f, safeClearance * safeClearance - perpSqr));
                        float hitDist = Mathf.Max(0f, proj - halfChord);
                        if (hitDist < clearDist)
                        {
                            clearDist = hitDist;
                        }
                    }
                }
            }

            // 2. Kiểm tra biên giới Habitat hợp lệ và mép an toàn ô
            float stepSize = 0.12f;
            for (float d = stepSize; d <= clearDist; d += stepSize)
            {
                Vector3 samplePos = currentPos + dir * d;
                HexCoordinates sampleHex = HexMetrics.WorldToHex(samplePos);
                if (!canMoveToHex(sampleHex))
                {
                    clearDist = Mathf.Max(0f, d - stepSize);
                    break;
                }

                // Không vượt quá mép an toàn safeHexRadius của ô đích TRỪ KHI hướng đó dẫn sang ô láng giềng hợp lệ
                Vector3 hCenter = HexMetrics.HexToWorldPosition(sampleHex, samplePos.y);
                Vector3 fromHCenter = samplePos - hCenter;
                fromHCenter.y = 0f;
                if (fromHCenter.magnitude > safeHexRadius)
                {
                    HexCoordinates furtherHex = HexMetrics.WorldToHex(hCenter + fromHCenter.normalized * 1.45f);
                    if (!canMoveToHex(furtherHex))
                    {
                        clearDist = Mathf.Max(0f, d - stepSize);
                        break;
                    }
                }
            }

            // 3. Kiểm tra các con thú khác (tránh chọn hướng đi đâm vào đồng loại)
            for (int a = 0; a < activeAnimals.Count; a++)
            {
                AnimalMovementAI other = activeAnimals[a];
                if (other == null || other == currentAnimal) continue;
                if (Mathf.Abs(currentPos.y - other.transform.position.y) > 0.65f) continue;

                Vector3 toOther = other.transform.position - currentPos;
                toOther.y = 0f;
                float proj = Vector3.Dot(toOther, dir);
                if (proj > 0f)
                {
                    float perpSqr = toOther.sqrMagnitude - proj * proj;
                    float safeSep = personalRadius + other.GetPersonalRadius() + 0.05f;
                    if (perpSqr < safeSep * safeSep)
                    {
                        float halfChord = Mathf.Sqrt(Mathf.Max(0f, safeSep * safeSep - perpSqr));
                        float hitDist = Mathf.Max(0f, proj - halfChord);
                        if (hitDist < clearDist)
                        {
                            clearDist = hitDist;
                        }
                    }
                }
            }

            clearDists[i] = clearDist;

            if (clearDist > bestClearDist)
            {
                bestClearDist = clearDist;
                bestClearIdx = i;
            }
        }

        // KIỂM TRA TÌNH TRẠNG KẸT VÀ LỰA CHỌN HÀNH LANG DI CHUYỂN
        List<int> openSectors = new List<int>();
        for (int i = 0; i < NumSectors; i++)
        {
            if (clearDists[i] >= OpenThreshold)
            {
                openSectors.Add(i);
            }
        }

        // TRƯỜNG HỢP 1: BỊ KẸT TRONG GÓC CỤT -> BACKTRACK / ESCAPE
        if (openSectors.Count == 0 || bestClearDist < OpenThreshold)
        {
            int escapeIdx = (bestClearIdx >= 0) ? bestClearIdx : 0;
            Vector3 escapeDir = sectorDirs[escapeIdx];
            float escapeDist = Mathf.Clamp(clearDists[escapeIdx] * 0.85f, 0.15f, 0.35f);
            return currentPos + escapeDir * escapeDist;
        }

        // TRƯỜNG HỢP 2: CÓ CÁC HÀNH LANG THOÁNG ĐÃNG (Clear Corridors)
        int chosenIdx = openSectors[0];
        float bestScore = -float.MaxValue;

        for (int s = 0; s < openSectors.Count; s++)
        {
            int idx = openSectors[s];
            Vector3 dir = sectorDirs[idx];
            float dist = clearDists[idx];

            float biasDot = Vector3.Dot(dir, biasDir);
            float fwdDot = Vector3.Dot(dir, currentForward);

            float score = (dist / maxSearchRange) * 2.0f
                        + (biasDot + 1f) * 1.2f
                        + (fwdDot + 1f) * 0.6f
                        + Random.Range(0f, 0.5f);

            if (score > bestScore)
            {
                bestScore = score;
                chosenIdx = idx;
            }
        }

        Vector3 chosenDir = sectorDirs[chosenIdx];
        float chosenClearDist = clearDists[chosenIdx];

        float minRatio = (targetHex != currentHex) ? 0.70f : 0.45f;
        float maxRatio = (targetHex != currentHex) ? 0.95f : 0.75f;
        float minCap = (targetHex != currentHex) ? 0.80f : 0.20f;
        float stepLength = Random.Range(Mathf.Min(minCap, chosenClearDist * minRatio), chosenClearDist * maxRatio);
        return currentPos + chosenDir * stepLength;
    }

    /// <summary>
    /// Tính toán vector né tránh: Né các con thú khác (Tangential Steering) + Đẩy nhẹ vào trong khi áp sát mép viền (Soft Boundary Steering)
    /// </summary>
    public static Vector3 ComputeAvoidanceVector(
        Vector3 currentPos,
        Vector3 forwardDir,
        AnimalMovementAI currentAnimal,
        List<AnimalMovementAI> activeAnimals,
        AnimalLocomotionType locomotionType,
        bool isAirborne,
        float personalRadius,
        List<AnimalMovementAI.TileObstacle> tempNearbyObstacles,
        HexWorldGenerator worldGen,
        HexCoordinates currentHex,
        float safeHexRadius,
        Func<HexCoordinates, bool> canMoveToHex,
        out bool isBlockedDirectly)
    {
        isBlockedDirectly = false;
        Vector3 avoidance = Vector3.zero;

        // 1. NÉ TRÁNH CÁC CON THÚ KHÁC
        for (int i = 0; i < activeAnimals.Count; i++)
        {
            AnimalMovementAI other = activeAnimals[i];
            if (other == null || other == currentAnimal) continue;

            float heightDiff = Mathf.Abs(currentAnimal.transform.position.y - other.transform.position.y);
            if (heightDiff > 0.65f) continue;

            Vector3 otherPos = other.transform.position;
            Vector3 toOther = otherPos - currentPos;
            toOther.y = 0f;

            float dist = toOther.magnitude;
            float combinedRadius = personalRadius + other.GetPersonalRadius();
            float detectionRange = combinedRadius * 1.6f;

            if (dist < detectionRange && dist > 0.001f)
            {
                Vector3 toOtherNorm = toOther / dist;
                float forwardDot = Vector3.Dot(forwardDir, toOtherNorm);

                if (forwardDot > 0.05f)
                {
                    Vector3 sideDir = Vector3.Cross(Vector3.up, forwardDir).normalized;
                    float sideDot = Vector3.Dot(toOther, sideDir);
                    Vector3 steerSide = (sideDot >= 0f) ? -sideDir : sideDir;

                    float steerUrgency = Mathf.Clamp01((detectionRange - dist) / detectionRange);
                    avoidance += steerSide * (steerUrgency * 1.8f);

                    if (dist < combinedRadius * 1.02f && forwardDot > 0.60f)
                    {
                        isBlockedDirectly = true;
                    }
                }
                else
                {
                    float repStrength = Mathf.Clamp01((combinedRadius - dist) / combinedRadius);
                    avoidance += (-toOtherNorm) * repStrength;
                }
            }
        }

        // 2. NÉ TRÁNH CHƯỚNG NGẠI VẬT TRÊN Ô (Thân cây, gốc cây nhỏ, tảng đá, khúc gỗ...)
        if (locomotionType != AnimalLocomotionType.Flying || !isAirborne)
        {
            AnimalObstacleScanner.GetNearbyObstacles(worldGen, currentPos, 1.0f, tempNearbyObstacles);
            for (int o = 0; o < tempNearbyObstacles.Count; o++)
            {
                AnimalMovementAI.TileObstacle obs = tempNearbyObstacles[o];
                Vector3 toObs = obs.worldPos - currentPos;
                toObs.y = 0f;
                float dist = toObs.magnitude;
                float minSafe = obs.radius + AnimalObstacleScanner.PropObstacleBodyRadius;
                float detectionDist = minSafe + 0.35f;

                if (dist < detectionDist && dist > 0.001f)
                {
                    Vector3 toObsNorm = toObs / dist;
                    float forwardDot = Vector3.Dot(forwardDir, toObsNorm);

                    if (forwardDot > 0.05f)
                    {
                        Vector3 sideDir = Vector3.Cross(Vector3.up, forwardDir).normalized;
                        float sideDot = Vector3.Dot(toObs, sideDir);

                        Vector3 steerSide;
                        if (Mathf.Abs(sideDot) < 0.03f)
                        {
                            steerSide = (currentAnimal.GetHashCode() % 2 == 0) ? -sideDir : sideDir;
                        }
                        else
                        {
                            steerSide = (sideDot >= 0f) ? -sideDir : sideDir;
                        }

                        float urgency = Mathf.Clamp01((detectionDist - dist) / (detectionDist - minSafe));
                        avoidance += steerSide * (urgency * 1.2f);

                        if (dist < minSafe + 0.02f && forwardDot > 0.65f)
                        {
                            isBlockedDirectly = true;
                        }
                    }
                    else
                    {
                        float rep = Mathf.Clamp01((minSafe - dist) / minSafe);
                        avoidance += (-toObsNorm) * (rep * 0.8f);
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
            HexCoordinates aheadHex = HexMetrics.WorldToHex(currentPos + forwardDir * 0.6f);
            if (!canMoveToHex(aheadHex))
            {
                Vector3 toCenterDir = -fromCenter.normalized;
                float boundaryFactor = Mathf.Clamp01((distFromCenter - boundaryThreshold) / (safeHexRadius - boundaryThreshold));
                avoidance += toCenterDir * (boundaryFactor * 2.0f);
            }
        }

        return avoidance;
    }
}

