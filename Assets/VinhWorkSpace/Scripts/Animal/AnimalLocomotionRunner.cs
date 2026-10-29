using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Chuyên trách thực thi các Coroutine vận động học (Kinematic Locomotion),
/// bám địa hình, nghiêng cánh bay (banking), nhấp nhô (bobbing), trượt va chạm và chống kẹt
/// cho AI động vật LumiWorld.
/// </summary>
public static class AnimalLocomotionRunner
{
    public static void SeparateInitialPosition(AnimalMovementAI ai)
    {
        var activeAnimals = AnimalMovementAI.GetActiveAnimals();

        // 1. Tách khỏi các con thú khác nếu bị trùng tọa độ khi spawn
        for (int i = 0; i < activeAnimals.Count; i++)
        {
            AnimalMovementAI other = activeAnimals[i];
            if (other == null || other == ai) continue;

            float heightDiff = Mathf.Abs(ai.transform.position.y - other.transform.position.y);
            if (heightDiff > 0.65f) continue;

            Vector3 diff = ai.transform.position - other.transform.position;
            diff.y = 0f;
            float dist = diff.magnitude;
            float minAllowed = ai.PersonalRadius + other.PersonalRadius;

            if (dist < minAllowed)
            {
                int myIdx = activeAnimals.IndexOf(ai);
                float angle = (myIdx * (360f / Mathf.Max(activeAnimals.Count, 3))) * Mathf.Deg2Rad;
                Vector3 scatter = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (ai.PersonalRadius * 0.8f);
                Vector3 scatteredPos = ClampToValidHabitat(ai, ai.transform.position + scatter, ai.transform.position);
                float surfaceY = GetSurfaceYAt(ai, scatteredPos);
                ai.currentWorldY = surfaceY + ai.currentAltitudeOffset;
                ai.transform.position = new Vector3(scatteredPos.x, ai.currentWorldY, scatteredPos.z);
                break;
            }
        }

        // 2. Tách khỏi gốc cây, tảng đá nếu spawn đè lên prop
        if (ai.LocomotionType != AnimalLocomotionType.Flying || !ai.IsAirborne)
        {
            Vector3 center = HexMetrics.HexToWorldPosition(ai.currentHex, ai.transform.position.y);
            for (int pass = 0; pass < 3; pass++)
            {
                AnimalObstacleScanner.GetNearbyObstacles(ai.worldGen, ai.transform.position, 1.0f, ai.tempNearbyObstacles);
                bool adjusted = false;
                for (int o = 0; o < ai.tempNearbyObstacles.Count; o++)
                {
                    AnimalMovementAI.TileObstacle obs = ai.tempNearbyObstacles[o];
                    Vector3 diff = ai.transform.position - obs.worldPos;
                    diff.y = 0f;
                    float dist = diff.magnitude;
                    float minSafe = obs.radius + AnimalMovementAI.PropObstacleBodyRadius;
                    if (dist < minSafe)
                    {
                        Vector3 pushNorm = (dist > 0.001f) ? (diff / dist) : (ai.transform.position - center).normalized;
                        if (pushNorm.sqrMagnitude < 0.001f) pushNorm = Vector3.forward;
                        Vector3 safePos = obs.worldPos + pushNorm * (minSafe + 0.04f);
                        Vector3 clamped = ClampToValidHabitat(ai, safePos, ai.transform.position);
                        float surfaceY = GetSurfaceYAt(ai, clamped);
                        ai.currentWorldY = surfaceY + ai.currentAltitudeOffset;
                        ai.transform.position = new Vector3(clamped.x, ai.currentWorldY, clamped.z);
                        adjusted = true;
                        break;
                    }
                }
                if (!adjusted) break;
            }
        }
    }

    public static void ResolveOverlaps(AnimalMovementAI ai)
    {
        var activeAnimals = AnimalMovementAI.GetActiveAnimals();
        if (activeAnimals.Count <= 1) return;

        Vector3 myPos = ai.transform.position;
        Vector3 totalPush = Vector3.zero;
        int overlapCount = 0;

        for (int i = 0; i < activeAnimals.Count; i++)
        {
            AnimalMovementAI other = activeAnimals[i];
            if (other == null || other == ai) continue;

            float heightDiff = Mathf.Abs(myPos.y - other.transform.position.y);
            if (heightDiff > 0.65f) continue;

            Vector3 otherPos = other.transform.position;
            Vector3 diff = myPos - otherPos;
            diff.y = 0f;

            float dist = diff.magnitude;
            float minAllowedDist = ai.PersonalRadius + other.PersonalRadius;

            if (dist < minAllowedDist)
            {
                Vector3 pushDir;
                if (dist < 0.001f)
                {
                    int idx = activeAnimals.IndexOf(ai);
                    float angle = (idx * (360f / Mathf.Max(activeAnimals.Count, 3))) * Mathf.Deg2Rad;
                    pushDir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)).normalized;
                    dist = 0.001f;
                }
                else
                {
                    pushDir = diff / dist;
                }

                float overlap = minAllowedDist - dist;
                if (overlap > 0.015f)
                {
                    totalPush += pushDir * overlap;
                    overlapCount++;
                }
            }
        }

        if (overlapCount > 0)
        {
            Vector3 avgPush = totalPush / overlapCount;
            Vector3 targetPos = myPos + avgPush;
            Vector3 pushedPos = Vector3.MoveTowards(myPos, targetPos, ai.SeparationSpeed * Time.deltaTime);
            Vector3 newPos = ClampToValidHabitat(ai, pushedPos, myPos);

            UpdateElevationAndBanking(ai, newPos, Vector3.zero);
            ai.UpdateCurrentHexFromPosition();
        }
    }

    public static void UpdateElevationAndBanking(AnimalMovementAI ai, Vector3 flatPos, Vector3 moveDir)
    {
        float surfaceY = GetSurfaceYAt(ai, flatPos);
        ai.currentAltitudeOffset = Mathf.MoveTowards(ai.currentAltitudeOffset, ai.targetAltitudeOffset, ai.AltitudeSmoothSpeed * Time.deltaTime);

        float targetWorldY = surfaceY + ai.currentAltitudeOffset;
        ai.currentWorldY = Mathf.MoveTowards(ai.currentWorldY, targetWorldY, ai.AltitudeSmoothSpeed * 1.5f * Time.deltaTime);

        float bobbing = (ai.LocomotionType == AnimalLocomotionType.Flying && ai.IsAirborne)
            ? Mathf.Sin(Time.time * ai.FlightBobbingFrequency) * ai.FlightBobbingAmplitude
            : 0f;

        ai.transform.position = new Vector3(flatPos.x, ai.currentWorldY + bobbing, flatPos.z);

        if (ai.LocomotionType == AnimalLocomotionType.Flying && ai.IsAirborne && moveDir.sqrMagnitude > 0.001f)
        {
            float turnAngle = Vector3.SignedAngle(ai.transform.forward, moveDir, Vector3.up);
            float targetBank = Mathf.Clamp(-turnAngle * 0.45f, -ai.MaxBankAngle, ai.MaxBankAngle);
            ai.currentBankAngle = Mathf.MoveTowards(ai.currentBankAngle, targetBank, ai.BankSpeed * 25f * Time.deltaTime);
        }
        else
        {
            ai.currentBankAngle = Mathf.MoveTowards(ai.currentBankAngle, 0f, ai.BankSpeed * 20f * Time.deltaTime);
            if (ai.LocomotionType == AnimalLocomotionType.Flying && ai.IsAirborne && !ai.IsMoving)
            {
                Vector3 rot = ai.transform.eulerAngles;
                if (Mathf.Abs(Mathf.DeltaAngle(rot.z, ai.currentBankAngle)) > 0.01f)
                {
                    ai.transform.rotation = Quaternion.Euler(rot.x, rot.y, Mathf.MoveTowardsAngle(rot.z, ai.currentBankAngle, ai.BankSpeed * 20f * Time.deltaTime));
                }
            }
        }
    }

    public static Vector3 ClampToValidHabitat(AnimalMovementAI ai, Vector3 candidatePos, Vector3 fallbackPos)
    {
        HexCoordinates candHex = HexMetrics.WorldToHex(candidatePos);

        if (ai.CanMoveToHex(candHex))
        {
            Vector3 hexCenter = HexMetrics.HexToWorldPosition(candHex, candidatePos.y);
            Vector3 fromCenter = candidatePos - hexCenter;
            fromCenter.y = 0f;

            if (fromCenter.magnitude > ai.SafeHexRadius)
            {
                HexCoordinates furtherHex = HexMetrics.WorldToHex(hexCenter + fromCenter.normalized * 1.45f);
                if (!ai.CanMoveToHex(furtherHex))
                {
                    fromCenter = fromCenter.normalized * ai.SafeHexRadius;
                    return new Vector3(hexCenter.x + fromCenter.x, candidatePos.y, hexCenter.z + fromCenter.z);
                }
            }

            return candidatePos;
        }

        Vector3 curCenter = HexMetrics.HexToWorldPosition(ai.currentHex, fallbackPos.y);
        Vector3 curOffset = fallbackPos - curCenter;
        curOffset.y = 0f;

        if (curOffset.magnitude > ai.SafeHexRadius)
        {
            curOffset = curOffset.normalized * ai.SafeHexRadius;
        }

        return new Vector3(curCenter.x + curOffset.x, fallbackPos.y, curCenter.z + curOffset.z);
    }

    public static float GetSurfaceYAt(AnimalMovementAI ai, Vector3 worldPos)
    {
        HexCoordinates hex = HexMetrics.WorldToHex(worldPos);
        if (ai.worldGen != null && ai.worldGen.MapTiles.TryGetValue(hex, out GameObject tileObj) && tileObj != null)
        {
            var (topY, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(tileObj);
            return topY;
        }
        return HexMetrics.TileHeight;
    }

    public static Vector3 PickDestination(AnimalMovementAI ai)
    {
        HexCoordinates targetHex = ai.currentHex;

        if (ai.worldGen != null && ai.worldGen.MapTiles != null)
        {
            List<HexCoordinates> matchingNeighbors = new List<HexCoordinates>();
            for (int dir = 0; dir < 6; dir++)
            {
                HexCoordinates nHex = ai.currentHex.GetNeighbor(dir);
                if (ai.CanMoveToHex(nHex))
                {
                    matchingNeighbors.Add(nHex);
                }
            }

            if (matchingNeighbors.Count > 0 && Random.value > ai.StayInCurrentHexChance)
            {
                targetHex = matchingNeighbors[Random.Range(0, matchingNeighbors.Count)];
            }
        }

        float targetSurfaceY = HexMetrics.TileHeight;
        if (ai.worldGen != null && ai.worldGen.MapTiles.TryGetValue(targetHex, out GameObject tTileObj) && tTileObj != null)
        {
            var (topY, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(tTileObj);
            targetSurfaceY = topY;
        }

        Vector3 hexCenter = HexMetrics.HexToWorldPosition(targetHex, targetSurfaceY);

        if (ai.LocomotionType == AnimalLocomotionType.Flying && ai.IsAirborne)
        {
            Vector2 randomOffset = Random.insideUnitCircle * ai.MaxWanderRadius;
            Vector3 airCandidate = new Vector3(hexCenter.x + randomOffset.x, hexCenter.y, hexCenter.z + randomOffset.y);
            HexCoordinates candHex = HexMetrics.WorldToHex(airCandidate);
            if (ai.CanMoveToHex(candHex))
            {
                ai.lastTargetDestination = airCandidate;
                return airCandidate;
            }
            ai.lastTargetDestination = hexCenter;
            return hexCenter;
        }

        ai.lastTargetDestination = AnimalSectorNavigator.PickGroundDestination(
            ai.transform.position, ai.currentHex, targetHex, hexCenter,
            ai.MaxWanderRadius, ai.SafeHexRadius, ai.PreferCameraFacingGlade,
            ai.transform.forward, ai.PersonalRadius, ai, AnimalMovementAI.GetActiveAnimals(),
            ai.tempNearbyObstacles, ai.worldGen, ai.CanMoveToHex);

        return ai.lastTargetDestination;
    }

    public static IEnumerator MoveToTargetRoutine(AnimalMovementAI ai, Vector3 targetWorldPos, float speed, string moveClip)
    {
        ai.isMoving = true;

        if (Vector3.Distance(ai.transform.position, targetWorldPos) < 0.08f)
        {
            ai.StopMoveAnimation(moveClip);
            ai.isMoving = false;
            yield break;
        }

        float maxDuration = 5.0f;
        float elapsed = 0f;
        float blockedTimer = 0f;
        float stuckCheckTimer = 0f;
        int consecutiveStuckCount = 0;
        Vector3 windowStartPos = ai.transform.position;
        Vector3 smoothedAvoidance = Vector3.zero;

        bool shouldTurnInPlace = ai.TurnBeforeWalking && (!ai.IsAirborne || ai.LocomotionType != AnimalLocomotionType.Flying);
        if (shouldTurnInPlace)
        {
            Vector3 initialDir = targetWorldPos - ai.transform.position;
            initialDir.y = 0f;

            if (initialDir.sqrMagnitude > 0.001f)
            {
                ai.PlayIdleAnimation();

                Quaternion faceRot = Quaternion.LookRotation(initialDir);
                while (Quaternion.Angle(ai.transform.rotation, faceRot) > ai.TurnThresholdAngle && elapsed < 0.8f)
                {
                    if (ai.FarmAI != null && ai.FarmAI.currentState != AnimalState.WanderRandom)
                    {
                        ai.StopMoveAnimation(moveClip);
                        ai.isMoving = false;
                        yield break;
                    }
                    elapsed += Time.deltaTime;
                    ai.transform.rotation = Quaternion.RotateTowards(ai.transform.rotation, faceRot, ai.RotationSpeed * Time.deltaTime);
                    UpdateElevationAndBanking(ai, ai.transform.position, Vector3.zero);
                    yield return null;
                }
            }
        }

        ai.PlayMoveAnimation(moveClip);

        while (elapsed < maxDuration)
        {
            if (ai.FarmAI != null && ai.FarmAI.currentState != AnimalState.WanderRandom)
            {
                ai.StopMoveAnimation(moveClip);
                ai.isMoving = false;
                yield break;
            }
            elapsed += Time.deltaTime;

            Vector3 currentPos = ai.transform.position;
            Vector3 diff = targetWorldPos - currentPos;
            Vector3 flatDir = new Vector3(diff.x, 0f, diff.z);

            if (flatDir.sqrMagnitude < 0.0036f)
            {
                break;
            }

            Vector3 avoidance = AnimalSectorNavigator.ComputeAvoidanceVector(
                currentPos, flatDir.normalized, ai, AnimalMovementAI.GetActiveAnimals(),
                ai.LocomotionType, ai.IsAirborne, ai.PersonalRadius, ai.tempNearbyObstacles,
                ai.worldGen, ai.currentHex, ai.SafeHexRadius, ai.CanMoveToHex, out bool isBlockedDirectly);

            if (isBlockedDirectly)
            {
                ai.StopMoveAnimation(moveClip);

                blockedTimer += Time.deltaTime;
                if (blockedTimer > 0.30f)
                {
                    break;
                }

                UpdateElevationAndBanking(ai, currentPos, Vector3.zero);
                yield return null;
                continue;
            }
            else
            {
                blockedTimer = 0f;
                ai.PlayMoveAnimation(moveClip);
            }

            smoothedAvoidance = Vector3.MoveTowards(smoothedAvoidance, avoidance, 6.0f * Time.deltaTime);

            Vector3 moveDir = flatDir.normalized;
            if (smoothedAvoidance.sqrMagnitude > 0.001f)
            {
                moveDir = (flatDir.normalized + smoothedAvoidance * ai.AvoidanceWeight).normalized;
            }

            if (moveDir.sqrMagnitude > 0.001f)
            {
                Quaternion desiredRot = Quaternion.LookRotation(moveDir);
                if (ai.LocomotionType == AnimalLocomotionType.Flying && ai.IsAirborne)
                {
                    ai.transform.rotation = Quaternion.RotateTowards(ai.transform.rotation, desiredRot * Quaternion.Euler(0f, 0f, ai.currentBankAngle), ai.RotationSpeed * Time.deltaTime);
                }
                else
                {
                    ai.transform.rotation = Quaternion.RotateTowards(ai.transform.rotation, desiredRot, ai.RotationSpeed * Time.deltaTime);
                }
            }

            Vector3 step = moveDir * (speed * Time.deltaTime);
            if (step.sqrMagnitude > flatDir.sqrMagnitude)
            {
                step = flatDir;
            }

            Vector3 candidatePos = currentPos + step;

            if (ai.LocomotionType != AnimalLocomotionType.Flying || !ai.IsAirborne)
            {
                AnimalObstacleScanner.GetNearbyObstacles(ai.worldGen, candidatePos, 0.7f, ai.tempNearbyObstacles);
                for (int pass = 0; pass < 2; pass++)
                {
                    bool hadCollision = false;
                    for (int o = 0; o < ai.tempNearbyObstacles.Count; o++)
                    {
                        AnimalMovementAI.TileObstacle obs = ai.tempNearbyObstacles[o];
                        Vector3 obsDiff = candidatePos - obs.worldPos;
                        obsDiff.y = 0f;
                        float dist = obsDiff.magnitude;
                        float minSafeDist = obs.radius + AnimalMovementAI.PropObstacleBodyRadius;

                        if (dist < minSafeDist)
                        {
                            hadCollision = true;
                            Vector3 pushNorm;
                            if (dist < 0.001f)
                            {
                                Vector3 fromOld = currentPos - obs.worldPos;
                                fromOld.y = 0f;
                                pushNorm = fromOld.sqrMagnitude > 0.001f ? fromOld.normalized : -moveDir;
                            }
                            else
                            {
                                pushNorm = obsDiff / dist;
                            }

                            candidatePos = new Vector3(obs.worldPos.x + pushNorm.x * minSafeDist, candidatePos.y, obs.worldPos.z + pushNorm.z * minSafeDist);

                            float intoNormal = Vector3.Dot(step, -pushNorm);
                            if (intoNormal > 0f)
                            {
                                step += pushNorm * intoNormal;
                            }
                        }
                    }
                    if (!hadCollision) break;
                }
            }

            Vector3 nextPos = ClampToValidHabitat(ai, candidatePos, currentPos);

            float stepDistSqr = (nextPos - currentPos).sqrMagnitude;
            if (stepDistSqr < 0.000025f)
            {
                Vector3 hexCenter = HexMetrics.HexToWorldPosition(ai.currentHex, currentPos.y);
                Vector3 radial = currentPos - hexCenter;
                radial.y = 0f;
                bool borderSlid = false;
                if (radial.sqrMagnitude > 0.01f)
                {
                    Vector3 tangent = Vector3.Cross(Vector3.up, radial).normalized;
                    Vector3 tangentStep = Vector3.Project(step, tangent);
                    Vector3 slidePos = ClampToValidHabitat(ai, currentPos + tangentStep, currentPos);
                    if ((slidePos - currentPos).sqrMagnitude > 0.000025f)
                    {
                        nextPos = slidePos;
                        borderSlid = true;
                    }
                }

                if (!borderSlid)
                {
                    ai.StopMoveAnimation(moveClip);
                    blockedTimer += Time.deltaTime;
                    if (blockedTimer > 0.20f)
                    {
                        break;
                    }
                    UpdateElevationAndBanking(ai, currentPos, Vector3.zero);
                    yield return null;
                    continue;
                }
            }

            UpdateElevationAndBanking(ai, nextPos, moveDir);
            ai.UpdateCurrentHexFromPosition();

            stuckCheckTimer += Time.deltaTime;
            if (stuckCheckTimer >= 0.25f)
            {
                float netMoved = Vector3.Distance(ai.transform.position, windowStartPos);
                if (netMoved < 0.03f)
                {
                    consecutiveStuckCount++;
                    if (consecutiveStuckCount >= 2)
                    {
                        break;
                    }
                }
                else
                {
                    consecutiveStuckCount = 0;
                }

                stuckCheckTimer = 0f;
                windowStartPos = ai.transform.position;
            }

            yield return null;
        }

        ai.StopMoveAnimation(moveClip);
        ai.UpdateCurrentHexFromPosition();
        ai.isMoving = false;
    }

    public static IEnumerator LandRoutine(AnimalMovementAI ai)
    {
        if (!ai.IsAirborne || !AnimalAnimationClassifier.HasGroundCapabilities(ai.idleClips)) yield break;

        ai.targetAltitudeOffset = ai.YOffset;

        string descentClip = AnimalAnimationClassifier.GetFlightClip(ai.moveClips, preferHover: true);
        if (ai.anim != null && !string.IsNullOrEmpty(descentClip) && ai.anim.GetClip(descentClip) != null)
        {
            ai.anim.CrossFade(descentClip, 0.2f);
        }

        float landTimer = 0f;
        while (Mathf.Abs(ai.currentAltitudeOffset - ai.YOffset) > 0.06f && landTimer < 2.5f)
        {
            if (ai.FarmAI != null && ai.FarmAI.currentState != AnimalState.WanderRandom) yield break;
            landTimer += Time.deltaTime;
            UpdateElevationAndBanking(ai, ai.transform.position, Vector3.zero);
            yield return null;
        }

        ai.isAirborne = false;
        ai.currentAltitudeOffset = ai.YOffset;

        string groundIdle = AnimalAnimationClassifier.GetGroundIdleClip(ai.idleClips, ai.moveClips);
        if (ai.anim != null && !string.IsNullOrEmpty(groundIdle) && ai.anim.GetClip(groundIdle) != null)
        {
            ai.anim.CrossFade(groundIdle, 0.3f);
        }
    }

    public static IEnumerator TakeoffRoutine(AnimalMovementAI ai)
    {
        if (ai.IsAirborne) yield break;

        string takeoffClip = AnimalAnimationClassifier.GetFlightClip(ai.moveClips, preferHover: true);
        if (ai.anim != null && !string.IsNullOrEmpty(takeoffClip) && ai.anim.GetClip(takeoffClip) != null)
        {
            ai.anim.CrossFade(takeoffClip, 0.2f);
        }

        yield return new WaitForSeconds(0.25f);

        ai.isAirborne = true;
        ai.targetAltitudeOffset = ai.FlightAltitude;

        float takeoffTimer = 0f;
        float targetY = ai.FlightAltitude - 0.15f;
        while (ai.currentAltitudeOffset < targetY && takeoffTimer < 2.0f)
        {
            if (ai.FarmAI != null && ai.FarmAI.currentState != AnimalState.WanderRandom) yield break;
            takeoffTimer += Time.deltaTime;
            UpdateElevationAndBanking(ai, ai.transform.position, Vector3.zero);
            yield return null;
        }
    }

    public static IEnumerator IdleLookAroundRoutine(AnimalMovementAI ai)
    {
        if (ai.IsAirborne) yield break;

        float turnAngle = Random.Range(-20f, 20f);
        Quaternion targetRot = ai.transform.rotation * Quaternion.Euler(0f, turnAngle, 0f);

        float duration = 1.6f;
        float elapsed = 0f;
        Quaternion startRot = ai.transform.rotation;

        while (elapsed < duration && !ai.IsMoving)
        {
            if (ai.FarmAI != null && ai.FarmAI.currentState != AnimalState.WanderRandom) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            ai.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            UpdateElevationAndBanking(ai, ai.transform.position, Vector3.zero);
            yield return null;
        }
    }

    public static IEnumerator FlyingBehaviorRoutine(AnimalMovementAI ai)
    {
        if (ai.IsAirborne)
        {
            bool canLand = AnimalAnimationClassifier.HasGroundCapabilities(ai.idleClips);
            float rand = Random.value;

            if (canLand && rand < ai.LandChance)
            {
                yield return ai.StartCoroutine(LandRoutine(ai));

                float groundWait = Random.Range(ai.MinIdleTime, ai.MaxIdleTime);
                float elapsed = 0f;
                while (elapsed < groundWait && !ai.IsMoving)
                {
                    if (ai.FarmAI != null && ai.FarmAI.currentState != AnimalState.WanderRandom) yield break;
                    elapsed += Time.deltaTime;
                    UpdateElevationAndBanking(ai, ai.transform.position, Vector3.zero);
                    yield return null;
                }
            }
            else if (rand < 0.85f && ai.moveClips.Count > 0)
            {
                Vector3 targetWorldPos = PickDestination(ai);
                string flyClip = AnimalAnimationClassifier.GetFlightClip(ai.moveClips, preferHover: false);
                float speed = ai.FlySpeed;
                yield return ai.StartCoroutine(MoveToTargetRoutine(ai, targetWorldPos, speed, flyClip));
            }
            else
            {
                ai.PlayStationaryAnimation();

                float hoverTime = Random.Range(1.8f, 3.5f);
                float elapsed = 0f;
                while (elapsed < hoverTime && !ai.IsMoving)
                {
                    if (ai.FarmAI != null && ai.FarmAI.currentState != AnimalState.WanderRandom) yield break;
                    elapsed += Time.deltaTime;
                    UpdateElevationAndBanking(ai, ai.transform.position, Vector3.zero);
                    yield return null;
                }
            }
        }
        else
        {
            float rand = Random.value;

            if (rand < 0.40f && AnimalAnimationClassifier.HasGroundWalk(ai.moveClips))
            {
                Vector3 targetWorldPos = PickDestination(ai);
                string walkClip = AnimalAnimationClassifier.GetGroundMoveClip(ai.moveClips, ai.idleClips, isLongDistance: false);
                yield return ai.StartCoroutine(MoveToTargetRoutine(ai, targetWorldPos, ai.WalkSpeed, walkClip));
            }
            else if (rand < 0.65f)
            {
                ai.StopMoveAnimation();
                if (Random.value < ai.IdleTurnChance)
                {
                    yield return ai.StartCoroutine(IdleLookAroundRoutine(ai));
                }

                float waitTime = Random.Range(ai.MinIdleTime, ai.MaxIdleTime);
                float elapsed = 0f;
                while (elapsed < waitTime && !ai.IsMoving)
                {
                    if (ai.FarmAI != null && ai.FarmAI.currentState != AnimalState.WanderRandom) yield break;
                    elapsed += Time.deltaTime;
                    UpdateElevationAndBanking(ai, ai.transform.position, Vector3.zero);
                    yield return null;
                }
            }
            else
            {
                yield return ai.StartCoroutine(TakeoffRoutine(ai));
            }
        }
    }

    public static IEnumerator GroundBehaviorRoutine(AnimalMovementAI ai)
    {
        bool willMove = (ai.moveClips.Count > 0) && (Random.value > 0.15f);

        if (willMove)
        {
            Vector3 targetWorldPos = PickDestination(ai);
            float moveDistance = Vector3.Distance(ai.transform.position, targetWorldPos);

            if (moveDistance >= 0.08f)
            {
                bool isLongDistance = moveDistance > 0.85f;
                string moveClip = ai.PickMoveClip(isLongDistance);
                float speed = AnimalAnimationClassifier.GetSpeedForClip(
                    moveClip, ai.LocomotionType, ai.IsAirborne, ai.WalkSpeed, ai.RunSpeed, ai.SwimSpeed, ai.FlySpeed);

                yield return ai.StartCoroutine(MoveToTargetRoutine(ai, targetWorldPos, speed, moveClip));
            }

            ai.StopMoveAnimation();

            float waitTime = Random.Range(ai.MinIdleTime, ai.MaxIdleTime);

            if (Random.value < ai.IdleTurnChance)
            {
                yield return ai.StartCoroutine(IdleLookAroundRoutine(ai));
            }

            float idleElapsed = 0f;
            while (idleElapsed < waitTime && !ai.IsMoving)
            {
                if (ai.FarmAI != null && ai.FarmAI.currentState != AnimalState.WanderRandom) yield break;
                idleElapsed += Time.deltaTime;
                UpdateElevationAndBanking(ai, ai.transform.position, Vector3.zero);
                yield return null;
            }
        }
        else
        {
            ai.StopMoveAnimation();

            float waitTime = Random.Range(1.0f, 1.8f);

            if (Random.value < ai.IdleTurnChance)
            {
                yield return ai.StartCoroutine(IdleLookAroundRoutine(ai));
            }

            float idleElapsed = 0f;
            while (idleElapsed < waitTime && !ai.IsMoving)
            {
                if (ai.FarmAI != null && ai.FarmAI.currentState != AnimalState.WanderRandom) yield break;
                idleElapsed += Time.deltaTime;
                UpdateElevationAndBanking(ai, ai.transform.position, Vector3.zero);
                yield return null;
            }
        }
    }

    public static IEnumerator BehaviorLoopRoutine(AnimalMovementAI ai)
    {
        ai.PlayStationaryAnimation();
        yield return new WaitForSeconds(0.25f);

        bool wasInterruptedByFarmAI = false;

        while (true)
        {
            if (ai.anim == null) yield break;

            if (ai.FarmAI != null && ai.FarmAI.currentState != AnimalState.WanderRandom)
            {
                wasInterruptedByFarmAI = true;
                yield return new WaitForSeconds(0.25f);
                continue;
            }

            if (wasInterruptedByFarmAI)
            {
                wasInterruptedByFarmAI = false;
                ai.UpdateCurrentHexFromPosition();
                ai.currentWorldY = ai.transform.position.y;
                if (ai.LocomotionType == AnimalLocomotionType.Flying)
                {
                    ai.isAirborne = true;
                    ai.targetAltitudeOffset = ai.FlightAltitude;
                    ai.currentAltitudeOffset = ai.FlightAltitude;
                }
                ai.PlayStationaryAnimation();
            }

            if (ai.LocomotionType == AnimalLocomotionType.Flying)
            {
                yield return ai.StartCoroutine(FlyingBehaviorRoutine(ai));
            }
            else
            {
                yield return ai.StartCoroutine(GroundBehaviorRoutine(ai));
            }
        }
    }
}

