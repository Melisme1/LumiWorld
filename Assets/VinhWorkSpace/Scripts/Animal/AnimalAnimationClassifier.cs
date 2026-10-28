using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Quản lý việc quét, phân loại animation clip (di chuyển, đứng yên, bay, hạ cánh, đi bộ)
/// và ánh xạ tốc độ tương ứng cho sinh vật trong LumiWorld.
/// </summary>
public static class AnimalAnimationClassifier
{
    /// <summary>
    /// Tự động quét và phân loại animation thành 2 nhóm: Di chuyển (moveClips) & Đứng yên (idleClips)
    /// </summary>
    public static void ClassifyAvailableAnimations(
        Animation anim,
        List<string> moveClips,
        List<string> idleClips,
        ref AnimalLocomotionType locomotionType,
        ref bool isAirborne,
        ref float targetAltitudeOffset,
        ref float currentAltitudeOffset,
        float yOffset,
        float flightAltitude,
        bool autoDetectFlying)
    {
        moveClips.Clear();
        idleClips.Clear();

        if (anim == null) return;

        foreach (AnimationState state in anim)
        {
            string cName = state.name;

            // BỎ QUA HOÀN TOÀN Rest_Pose / BindPose (vì đây là T-pose tĩnh của exporter 3D, không phải animation diễn hoạt)
            // BỎ QUA Death / Die để thú không tự nhiên gục chết khi đứng nghỉ
            if (cName.IndexOf("Rest_Pose", StringComparison.OrdinalIgnoreCase) >= 0 ||
                cName.IndexOf("RestPose", StringComparison.OrdinalIgnoreCase) >= 0 ||
                cName.IndexOf("BindPose", StringComparison.OrdinalIgnoreCase) >= 0 ||
                cName.IndexOf("Death", StringComparison.OrdinalIgnoreCase) >= 0 ||
                cName.IndexOf("Die", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            if (IsMovementAnimation(cName))
            {
                moveClips.Add(cName);
            }
            else
            {
                idleClips.Add(cName);
            }
        }

        // KIỂM TRA ĐIỀU KIỆN BAY: Chỉ loài nào có animation clip "Flap" (đập cánh) mới là sinh vật bay (Flying).
        // Bất kỳ loài nào không có animation này đều được ép buộc di chuyển trên mặt đất bình thường (Ground).
        bool hasFlapClip = HasFlapAnimation(anim);

        if (!hasFlapClip)
        {
            locomotionType = AnimalLocomotionType.Ground;
            isAirborne = false;
            targetAltitudeOffset = yOffset;
            currentAltitudeOffset = yOffset;
        }
        else if (autoDetectFlying || locomotionType == AnimalLocomotionType.Flying)
        {
            locomotionType = AnimalLocomotionType.Flying;
            isAirborne = true;
            targetAltitudeOffset = flightAltitude;
            currentAltitudeOffset = flightAltitude;
        }

        // Fallback an toàn nếu không phân loại được
        if (moveClips.Count == 0 && idleClips.Count == 0)
        {
            foreach (AnimationState state in anim)
            {
                if (state.name.IndexOf("Rest", StringComparison.OrdinalIgnoreCase) < 0 &&
                    state.name.IndexOf("Death", StringComparison.OrdinalIgnoreCase) < 0 &&
                    state.name.IndexOf("Die", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    idleClips.Add(state.name);
                }
            }
        }
    }

    /// <summary>
    /// Kiểm tra xem mô hình động vật có animation clip "Flap" (đập cánh) hay không.
    /// </summary>
    public static bool HasFlapAnimation(Animation anim)
    {
        if (anim == null) return false;

        foreach (AnimationState state in anim)
        {
            if (state != null && !string.IsNullOrEmpty(state.name))
            {
                if (state.name.IndexOf("Flap", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Kiểm tra xem tên animation có phải thuộc nhóm di chuyển hay không.
    /// </summary>
    public static bool IsMovementAnimation(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        return name.IndexOf("Walk", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Run", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Trot", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Swim", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Fly", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Glide", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Flap", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Fetch", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Fall", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Hop", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Bite", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Tìm clip bay phù hợp nhất (Flap / Glide / Fly)
    /// </summary>
    public static string GetFlightClip(List<string> moveClips, bool preferHover = false)
    {
        if (moveClips == null || moveClips.Count == 0) return "";

        if (preferHover)
        {
            string flapClip = moveClips.Find(c => c.IndexOf("Flap", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrEmpty(flapClip)) return flapClip;
        }

        string flyClip = moveClips.Find(c =>
            c.IndexOf("Fly", StringComparison.OrdinalIgnoreCase) >= 0 ||
            c.IndexOf("Glide", StringComparison.OrdinalIgnoreCase) >= 0);
        if (!string.IsNullOrEmpty(flyClip)) return flyClip;

        string anyFlight = moveClips.Find(c => c.IndexOf("Flap", StringComparison.OrdinalIgnoreCase) >= 0);
        if (!string.IsNullOrEmpty(anyFlight)) return anyFlight;

        return moveClips[0];
    }

    /// <summary>
    /// Kiểm tra loài này có clip nghỉ ngơi hợp lệ dưới mặt đất không
    /// </summary>
    public static bool HasGroundCapabilities(List<string> idleClips)
    {
        return idleClips != null && idleClips.Count > 0;
    }

    /// <summary>
    /// Kiểm tra loài này có clip đi lại dưới mặt cỏ không (Walk, Trot, Hop, Fetch, Bite)
    /// </summary>
    public static bool HasGroundWalk(List<string> moveClips)
    {
        if (moveClips == null) return false;
        return moveClips.Exists(c =>
            c.IndexOf("Walk", StringComparison.OrdinalIgnoreCase) >= 0 ||
            c.IndexOf("Hop", StringComparison.OrdinalIgnoreCase) >= 0 ||
            c.IndexOf("Trot", StringComparison.OrdinalIgnoreCase) >= 0 ||
            c.IndexOf("Fetch", StringComparison.OrdinalIgnoreCase) >= 0 ||
            c.IndexOf("Bite", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    /// <summary>
    /// Lấy clip nghỉ ngơi hợp lệ dưới đất
    /// </summary>
    public static string GetGroundIdleClip(List<string> idleClips, List<string> moveClips)
    {
        if (idleClips == null || idleClips.Count == 0) return GetFlightClip(moveClips, preferHover: true);

        string baseIdle = idleClips.Find(c =>
            c.Equals("Idle", StringComparison.OrdinalIgnoreCase) ||
            c.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0);

        string eatOrSit = idleClips.Find(c =>
            c.IndexOf("Eat", StringComparison.OrdinalIgnoreCase) >= 0 ||
            c.IndexOf("Graze", StringComparison.OrdinalIgnoreCase) >= 0 ||
            c.IndexOf("Sit", StringComparison.OrdinalIgnoreCase) >= 0);

        float roll = Random.value;
        if (roll < 0.88f && !string.IsNullOrEmpty(baseIdle))
        {
            return baseIdle;
        }
        if (roll < 0.96f && !string.IsNullOrEmpty(eatOrSit))
        {
            return eatOrSit;
        }

        return idleClips[Random.Range(0, idleClips.Count)];
    }

    /// <summary>
    /// Lấy clip đi lại dưới mặt đất
    /// </summary>
    public static string GetGroundMoveClip(List<string> moveClips, List<string> idleClips, bool isLongDistance = false)
    {
        if (moveClips == null || moveClips.Count == 0)
        {
            if (idleClips != null && idleClips.Count > 0) return idleClips[0];
            return GetFlightClip(moveClips, false);
        }

        string walkClip = moveClips.Find(c => c.IndexOf("Walk", StringComparison.OrdinalIgnoreCase) >= 0);
        string trotClip = moveClips.Find(c => c.IndexOf("Trot", StringComparison.OrdinalIgnoreCase) >= 0);
        string runClip = moveClips.Find(c => c.IndexOf("Run", StringComparison.OrdinalIgnoreCase) >= 0);
        string hopClip = moveClips.Find(c => c.IndexOf("Fetch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                             c.IndexOf("Hop", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                             c.IndexOf("Bite", StringComparison.OrdinalIgnoreCase) >= 0);
        string glideClip = moveClips.Find(c => c.IndexOf("Fall", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                              c.IndexOf("Glide", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                              c.IndexOf("Fly", StringComparison.OrdinalIgnoreCase) >= 0);

        if (!isLongDistance)
        {
            if (!string.IsNullOrEmpty(walkClip)) return walkClip;
            if (!string.IsNullOrEmpty(trotClip)) return trotClip;
            if (!string.IsNullOrEmpty(hopClip)) return hopClip;
        }
        else
        {
            if (Random.value < 0.50f && !string.IsNullOrEmpty(runClip))
            {
                return runClip;
            }
            if (Random.value < 0.50f && !string.IsNullOrEmpty(glideClip))
            {
                return glideClip;
            }
            if (!string.IsNullOrEmpty(walkClip)) return walkClip;
            if (!string.IsNullOrEmpty(trotClip)) return trotClip;
            if (!string.IsNullOrEmpty(hopClip)) return hopClip;
        }

        if (moveClips.Count > 0) return moveClips[0];
        if (idleClips != null && idleClips.Count > 0) return idleClips[0];
        return GetFlightClip(moveClips, false);
    }

    /// <summary>
    /// Xác định tốc độ di chuyển tương ứng với tên animation clip
    /// </summary>
    public static float GetSpeedForClip(
        string clipName,
        AnimalLocomotionType locomotionType,
        bool isAirborne,
        float walkSpeed,
        float runSpeed,
        float swimSpeed,
        float flySpeed)
    {
        if (string.IsNullOrEmpty(clipName)) return (locomotionType == AnimalLocomotionType.Flying) ? flySpeed : walkSpeed;

        if (clipName.IndexOf("Run", StringComparison.OrdinalIgnoreCase) >= 0) return runSpeed;
        if (clipName.IndexOf("Swim", StringComparison.OrdinalIgnoreCase) >= 0) return swimSpeed;
        if (clipName.IndexOf("Fly", StringComparison.OrdinalIgnoreCase) >= 0 ||
            clipName.IndexOf("Glide", StringComparison.OrdinalIgnoreCase) >= 0 ||
            clipName.IndexOf("Flap", StringComparison.OrdinalIgnoreCase) >= 0) return flySpeed;
        if (clipName.IndexOf("Fetch", StringComparison.OrdinalIgnoreCase) >= 0 ||
            clipName.IndexOf("Hop", StringComparison.OrdinalIgnoreCase) >= 0 ||
            clipName.IndexOf("Bite", StringComparison.OrdinalIgnoreCase) >= 0) return walkSpeed * 1.15f;

        if (locomotionType == AnimalLocomotionType.Flying && isAirborne) return flySpeed;

        return walkSpeed;
    }
}

