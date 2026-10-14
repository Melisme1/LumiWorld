using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Giao diện UI và điều khiển trực quan cho Minigame 2 - Vũ Điệu Hòa Nhịp:
/// 1. Đồ họa Sprite Vector Procedural sắc nét 100%:
///    - Icon mũi tên dày dặn, mập mạp, viền trắng 3D, mã màu trực quan (◀ Xanh biển, ▲ Xanh lá, ▼ Vàng nghệ, ▶ Đỏ thắm).
///    - Viên ngọc phát sáng hào quang (Luminous Glow Orb).
///    - Vùng hồng tâm đồng tâm (Target Reticle) to rõ.
/// 2. Logic Phím SPACE trực quan, hiển thị Ô SPACE ngay cuối chuỗi nốt:
///    - Ô [ SPACE ] to gấp rưỡi nằm cạnh các mũi tên.
///    - Bước 1 (Gõ mũi tên): Ô Space mờ báo hiệu "Chờ xong mũi tên".
///    - Bước 2 (Ngọc bay): Ô Space BỪNG SÁNG VÀNG RỰC, nhấp nháy liên tục: "BẤM SPACE NGAY!".
/// 3. Cơ chế Approach Ring (Vòng thu nhỏ định nhịp):
///    - Vòng hào quang bên ngoài co dần về hồng tâm khi ngọc lướt tới.
///    - Vòng co khít vào tâm ➔ BẤM SPACE để ăn trọn PERFECT!
/// 4. Hỗ trợ song song cả Bàn phím (WASD / Mũi tên / Space) lẫn Chuột / Cảm ứng.
/// </summary>
public class RhythmMinigameView : MonoBehaviour, IMinigameSession
{
    public MinigameType Type => MinigameType.RhythmResonance;
    public bool IsActive => isSessionActive;

    private bool isSessionActive = false;
    private MinigameContext currentContext;
    private Action<MinigameResult> completionCallback;

    private RhythmMinigameEngine engine;

    // UI References
    private RectTransform panelRect;

    private Image harmonyFillImg;
    private RectTransform harmonyFillRect;
    private TextMeshProUGUI harmonyText;

    private RectTransform noteContainer;
    private readonly List<NoteSlotView> activeSlotViews = new List<NoteSlotView>();
    private SpaceSlotView spaceSlotView;

    private RectTransform railRect;
    private RectTransform orbRect;
    private Image orbImg;
    private RectTransform hitZoneRect;
    private Image hitZoneImg;
    private RectTransform approachRingRect;
    private Image approachRingImg;

    private TextMeshProUGUI comboText;
    private int comboCount = 0;
    private TextMeshProUGUI feedbackGradeText;
    private Coroutine feedbackAnimCoroutine;
    private TextMeshProUGUI timerText;

    private Image beatButtonImg;
    private TextMeshProUGUI beatButtonText;

    private AudioSource audioSource;
    private static AudioClip noteLeftClip;
    private static AudioClip noteUpClip;
    private static AudioClip noteDownClip;
    private static AudioClip noteRightClip;
    private static AudioClip beatPerfectClip;
    private static AudioClip beatGreatClip;
    private static AudioClip beatMissClip;
    private static AudioClip wrongKeyClip;
    private static AudioClip victoryFanfareClip;

    private LumiWorld.Acs.ResourceInventoryUISkin skin;
    private TMP_FontAsset fontAsset;

    // Static Procedural Sprites Cache (Cozy 3D Style)
    private static Sprite woodBoardSpriteCache;
    private static Sprite jellyLeftSpriteCache;
    private static Sprite jellyUpSpriteCache;
    private static Sprite jellyDownSpriteCache;
    private static Sprite jellyRightSpriteCache;
    private static Sprite jellyCorrectSpriteCache;
    private static Sprite stoneSpaceSpriteCache;
    private static Sprite capsuleBarSpriteCache;
    private static Sprite glossyFillSpriteCache;
    private static Sprite pearlOrbSpriteCache;
    private static Sprite targetStarburstSpriteCache;
    private static Sprite bigPillBtnSpriteCache;
    private static Sprite greenBadgeSpriteCache;
    private static Sprite redCloseBtnSpriteCache;
    private static Sprite cornerFloraSpriteCache;
    private static Sprite ringSpriteCache;
    private static Sprite whiteSpriteCache;
    private static Sprite sparkleStarSpriteCache;
    private static Sprite heartSpriteCache;
    private static Sprite sunburstSpriteCache;
    private static Sprite ivyGarlandSpriteCache;
    private static Sprite sideIvyLeftSpriteCache;
    private static Sprite sideIvyRightSpriteCache;
    private static Sprite floraLeftSpriteCache;
    private static Sprite floraRightSpriteCache;
    private static Sprite barWingSpriteCache;
    private static Sprite goldBeadSpriteCache;
    private static Sprite clockIconSpriteCache;

    private float displayHarmonyProgress = 0f;
    private float targetHarmonyProgress = 0f;
    private Image dimOverlayImg;

    private class NoteSlotView
    {
        public GameObject RootObj;
        public Image Background;
        public Outline Outline;
        public Image ArrowImg;
        public RhythmDirection Direction;
        public Color BaseColor;
    }

    private class SpaceSlotView
    {
        public GameObject RootObj;
        public Image Background;
        public Outline Outline;
        public TextMeshProUGUI MainLabel;
    }

    public void StartSession(MinigameContext context, Action<MinigameResult> onComplete)
    {
        this.currentContext = context;
        this.completionCallback = onComplete;
        this.isSessionActive = true;
        this.comboCount = 0;
        this.displayHarmonyProgress = 0f;
        this.targetHarmonyProgress = 0f;

        skin = Resources.Load<LumiWorld.Acs.ResourceInventoryUISkin>("UI/ResourceInventoryUISkin");
        fontAsset = skin != null && skin.font != null ? skin.font : null;

        EnsureProceduralSprites();
        EnsureAudioSource();
        BuildInterface();

        StartCoroutine(AnimateModalIntroRoutine(panelRect, dimOverlayImg));

        engine = new RhythmMinigameEngine();
        engine.OnPhaseChanged += HandlePhaseChanged;
        engine.OnNewSequence += HandleNewSequence;
        engine.OnNoteStepValidated += HandleNoteValidated;
        engine.OnBeatGradeEvaluated += HandleGradeEvaluated;
        engine.OnPromptHint += HandlePromptHint;
        engine.OnHarmonyChanged += HandleHarmonyChanged;
        engine.OnSessionFinished += HandleSessionFinished;

        engine.Initialize(context.Rank, context.TimeLimitSeconds);
        engine.StartPlaying();
    }

    public void CancelSession()
    {
        if (!isSessionActive) return;
        isSessionActive = false;
        completionCallback?.Invoke(new MinigameResult
        {
            IsSuccess = false,
            Type = MinigameType.RhythmResonance,
            Message = "Người chơi đã hủy phiên thử thách."
        });
    }

    private void Update()
    {
        if (!isSessionActive || engine == null) return;

        // 1. Cập nhật nhịp đồng hồ cho Engine
        engine.Tick(Time.deltaTime);

        // 2. Bắt input bàn phím
        HandleKeyboardInput();

        // 3. Cập nhật vị trí viên ngọc & vòng Approach Ring
        UpdateOrbAndApproachRing();

        // 4. Cập nhật đồng hồ đếm ngược định dạng chuẩn ấm áp (ví dụ 13,9s như mẫu mockup)
        if (timerText != null)
        {
            timerText.text = $"<b>{engine.TimeRemaining:F1}s</b>".Replace('.', ',');
        }

        // 5. Cập nhật hiệu ứng nốt hiện tại đang chờ bấm (Pulsing highlight)
        UpdateActiveNoteHighlight();

        // 6. Smooth Harmony Progress (Lấp đầy thanh tiến trình mềm mại như rót mật)
        UpdateHarmonySmoothFill();

        // 7. Nhịp thở thư thái (Idle Breathing) cho nút Chạm Nhịp
        if (beatButtonImg != null && !engine.IsSequenceCompleted)
        {
            float breath = 1.0f + 0.02f * Mathf.Sin(Time.time * 3.5f);
            beatButtonImg.transform.localScale = Vector3.one * breath;
        }
    }

    private void HandleKeyboardInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
        {
            engine.InputDirection(RhythmDirection.Left);
        }
        else if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
        {
            engine.InputDirection(RhythmDirection.Up);
        }
        else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
        {
            engine.InputDirection(RhythmDirection.Down);
        }
        else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
        {
            engine.InputDirection(RhythmDirection.Right);
        }

        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            if (beatButtonImg != null)
            {
                StartCoroutine(PunchScaleRoutine(beatButtonImg.transform, 1.08f, 0.12f));
            }

            var grade = engine.InputBeatStrike();
            if (grade == RhythmHitGrade.None && !engine.IsSequenceCompleted)
            {
                if (spaceSlotView != null && spaceSlotView.RootObj != null)
                {
                    StartCoroutine(PunchScaleRoutine(spaceSlotView.RootObj.transform, 1.15f, 0.15f));
                }
            }
        }
    }

    private void UpdateOrbAndApproachRing()
    {
        if (railRect == null || orbRect == null) return;

        float railWidth = railRect.rect.width - 56f;
        float progress = engine.OrbNormalizedProgress;
        float posX = (progress * railWidth) - (railWidth * 0.5f);

        orbRect.anchoredPosition = new Vector2(posX, 0f);

        float targetPos = RhythmMinigameEngine.TargetBeatNormalizedPos;
        float distToTarget = Mathf.Abs(progress - targetPos);

        // Approach Ring (Vòng định tâm co lại theo hạt ngọc)
        if (approachRingRect != null && approachRingImg != null)
        {
            if (engine.Phase == RhythmPhase.OrbGliding)
            {
                approachRingRect.gameObject.SetActive(true);
                // Tỉ lệ co từ 2.4x về 1.0x khi hạt ngọc tiến về hồng tâm
                float timeNorm = Mathf.Clamp01(progress / targetPos);
                float scale = Mathf.Lerp(2.4f, 1.0f, timeNorm);
                approachRingRect.localScale = new Vector3(scale, scale, 1f);

                bool isInsideTarget = distToTarget <= RhythmMinigameEngine.PerfectPosThreshold;
                approachRingImg.color = isInsideTarget
                    ? new Color(1f, 0.98f, 0.60f, 0.95f)
                    : new Color(1f, 0.85f, 0.35f, 0.70f);
            }
            else
            {
                approachRingRect.gameObject.SetActive(false);
            }
        }

        // Đốm sao hồng tâm thở nhẹ nhàng (Subtle ambient pulse)
        if (hitZoneRect != null)
        {
            float starPulse = 1.0f + 0.06f * Mathf.Sin(Time.time * 6f);
            hitZoneRect.localScale = new Vector3(starPulse, starPulse, 1f);
        }

        // Hiệu ứng phát sáng khi hạt ngọc vào vùng Hit Zone
        if (engine.Phase == RhythmPhase.OrbGliding)
        {
            bool isInsideHitZone = distToTarget <= RhythmMinigameEngine.GoodPosThreshold;

            if (orbImg != null)
            {
                orbImg.color = isInsideHitZone
                    ? new Color(1f, 1f, 0.75f, 1f)
                    : new Color(1f, 0.95f, 0.60f, 1f);
            }

            if (hitZoneImg != null)
            {
                hitZoneImg.color = isInsideHitZone
                    ? new Color(1f, 0.98f, 0.70f, 1f)
                    : Color.white;
            }
        }
    }

    private void UpdateActiveNoteHighlight()
    {
        // 1. Highlight nốt mũi tên đang cần gõ
        int currentIndex = engine.CurrentSequenceIndex;
        for (int i = 0; i < activeSlotViews.Count; i++)
        {
            var slot = activeSlotViews[i];
            if (slot == null || slot.Outline == null) continue;

            if (!engine.IsSequenceCompleted && i == currentIndex)
            {
                // Nốt hiện tại đang cần bấm: Viền vàng hoàng kim nhấp nháy êm ái
                float pulse = 1.0f + 0.12f * Mathf.Sin(Time.time * 8f);
                slot.Outline.effectColor = new Color(0.95f, 0.78f, 0.20f, 1f);
                slot.Outline.effectDistance = new Vector2(2.5f * pulse, -2.5f * pulse);
                slot.RootObj.transform.localScale = Vector3.one * (1.0f + 0.04f * Mathf.Sin(Time.time * 8f));
            }
            else if (i > currentIndex)
            {
                slot.Outline.effectColor = new Color(0.72f, 0.66f, 0.55f, 0.6f);
                slot.Outline.effectDistance = new Vector2(1.5f, -1.5f);
                slot.RootObj.transform.localScale = Vector3.one;
            }
        }

        // 2. Trạng thái Ô SPACE & Nút Chạm Nhịp
        if (spaceSlotView != null && spaceSlotView.Background != null)
        {
            if (engine.IsSequenceCompleted && engine.Phase == RhythmPhase.OrbGliding)
            {
                // ĐÃ XONG MŨI TÊN: Ô SPACE bừng sáng vàng hoàng kim rực rỡ và đập nhịp!
                float pulse = 1.0f + 0.10f * Mathf.Sin(Time.time * 12f);
                spaceSlotView.Background.color = new Color(0.98f, 0.72f, 0.18f, 1f);
                if (spaceSlotView.Outline != null)
                {
                    spaceSlotView.Outline.effectColor = new Color(1f, 0.95f, 0.55f, 1f);
                    spaceSlotView.Outline.effectDistance = new Vector2(3f * pulse, -3f * pulse);
                }
                spaceSlotView.MainLabel.text = "SPACE!";
                spaceSlotView.MainLabel.color = Color.white;
                spaceSlotView.RootObj.transform.localScale = Vector3.one * (1.0f + 0.05f * Mathf.Sin(Time.time * 12f));

                if (beatButtonImg != null && beatButtonText != null)
                {
                    beatButtonImg.color = new Color(0.98f, 0.65f, 0.16f, 1f);
                    beatButtonText.text = "CHẠM NHỊP (SPACE)";
                }
            }
            else
            {
                // Đang gõ mũi tên: Ô Space hiển thị dịu dàng, chờ hoàn thành nốt
                spaceSlotView.Background.color = Color.white;
                if (spaceSlotView.Outline != null)
                {
                    spaceSlotView.Outline.effectColor = new Color(0.68f, 0.60f, 0.48f, 0.7f);
                    spaceSlotView.Outline.effectDistance = new Vector2(1.5f, -1.5f);
                }
                spaceSlotView.MainLabel.text = "SPACE";
                spaceSlotView.MainLabel.color = new Color(0.36f, 0.32f, 0.26f, 0.95f);
                spaceSlotView.RootObj.transform.localScale = Vector3.one;

                if (beatButtonImg != null && beatButtonText != null)
                {
                    beatButtonImg.color = Color.white;
                    beatButtonText.text = "CHẠM NHỊP (SPACE)";
                }
            }
        }
    }

    // ==========================================
    // XỬ LÝ SỰ KIỆN TỪ ENGINE
    // ==========================================

    private void HandlePhaseChanged(RhythmPhase phase)
    {
        // Trạng thái nút bấm được quản lý linh hoạt trong UpdateActiveNoteHighlight()
    }

    private void HandlePromptHint(string hint)
    {
        // Đã gỡ bỏ PromptBanner theo yêu cầu thiết kế giao diện cozy tối giản
    }

    private void HandleNewSequence(IReadOnlyList<RhythmDirection> sequence)
    {
        foreach (var slot in activeSlotViews)
        {
            if (slot.RootObj != null) Destroy(slot.RootObj);
        }
        activeSlotViews.Clear();

        if (spaceSlotView != null && spaceSlotView.RootObj != null)
        {
            Destroy(spaceSlotView.RootObj);
            spaceSlotView = null;
        }

        if (noteContainer == null) return;

        // Tính toán tọa độ ngang chính xác bằng công thức toán học tuyệt đối
        int count = sequence.Count;
        float noteWidth = 72f;
        float spaceWidth = 114f;
        float spacing = 12f;
        float totalWidth = (count * noteWidth) + spaceWidth + (count * spacing);
        float currentX = -totalWidth * 0.5f;

        // 1. Tạo các ô mũi tên theo thứ tự
        for (int i = 0; i < count; i++)
        {
            RhythmDirection dir = sequence[i];
            float posX = currentX + (noteWidth * 0.5f);
            var slotView = CreateNoteSlot(noteContainer, dir, i, posX, noteWidth);
            activeSlotViews.Add(slotView);
            currentX += noteWidth + spacing;
        }

        // 2. Tạo ô phím SPACE ngay cuối hàng
        float spacePosX = currentX + (spaceWidth * 0.5f);
        spaceSlotView = CreateSpaceSlot(noteContainer, spacePosX, spaceWidth);
    }

    private void HandleNoteValidated(int index, RhythmDirection dir, bool isCorrect)
    {
        if (isCorrect)
        {
            PlayDirectionSound(dir);

            if (index >= 0 && index < activeSlotViews.Count)
            {
                var slot = activeSlotViews[index];
                // Nốt đã bấm đúng: Đổi sang sprite kẹo thạch xanh lục bảo có dấu V trắng 3D!
                if (slot.ArrowImg != null)
                {
                    slot.ArrowImg.sprite = jellyCorrectSpriteCache;
                    slot.ArrowImg.color = Color.white;
                }

                StartCoroutine(PunchScaleRoutine(slot.RootObj.transform, 1.25f, 0.12f));
                SpawnHitSparkles(slot.RootObj.transform.position, 3);
            }
        }
        else
        {
            PlayWrongSound();
            comboCount = 0;
            if (comboText != null) comboText.text = "";

            // Báo nhắc và rung lắc nhẹ mà không làm mất mũi tên
            StartCoroutine(ShakeSequenceRoutine());
        }
    }

    private void HandleGradeEvaluated(RhythmHitGrade grade, float delta, string feedbackText)
    {
        PlayBeatSound(grade);

        Color color = Color.white;
        if (grade == RhythmHitGrade.Perfect || grade == RhythmHitGrade.Great || grade == RhythmHitGrade.Good)
        {
            comboCount++;
            if (comboText != null)
            {
                if (comboCount >= 2)
                {
                    comboText.text = $"<color=#F59E0B><b>COMBO x{comboCount}</b></color>";
                    StartCoroutine(PunchScaleRoutine(comboText.transform, 1.25f, 0.16f));
                }
                else
                {
                    comboText.text = "";
                }
            }

            if (hitZoneRect != null)
            {
                StartCoroutine(PunchScaleRoutine(hitZoneRect, 1.35f, 0.16f));
                SpawnHitSparkles(hitZoneRect.position, 6);
            }

            // Phản hồi lực nảy bảng gỗ (Micro-recoil) tạo cảm giác đánh trúng đầm tay, đã mắt
            if (panelRect != null && (grade == RhythmHitGrade.Perfect || grade == RhythmHitGrade.Great))
            {
                StartCoroutine(PunchScaleRoutine(panelRect, 1.018f, 0.10f));
            }
        }
        else
        {
            comboCount = 0;
            if (comboText != null) comboText.text = "";
        }

        switch (grade)
        {
            case RhythmHitGrade.Perfect:
                color = new Color(0.98f, 0.78f, 0.15f, 1f); // Hoàng kim
                if (spaceSlotView != null && spaceSlotView.Background != null)
                    spaceSlotView.Background.color = new Color(0.98f, 0.78f, 0.15f, 1f);
                break;
            case RhythmHitGrade.Great:
                color = new Color(0.20f, 0.85f, 0.48f, 1f); // Lục bảo
                if (spaceSlotView != null && spaceSlotView.Background != null)
                    spaceSlotView.Background.color = new Color(0.20f, 0.85f, 0.48f, 1f);
                break;
            case RhythmHitGrade.Good:
                color = new Color(0.28f, 0.70f, 0.98f, 1f); // Lam ngọc
                if (spaceSlotView != null && spaceSlotView.Background != null)
                    spaceSlotView.Background.color = new Color(0.28f, 0.70f, 0.98f, 1f);
                break;
            case RhythmHitGrade.Miss:
                color = new Color(0.88f, 0.28f, 0.28f, 1f); // Đỏ san hô
                if (spaceSlotView != null && spaceSlotView.Background != null)
                    spaceSlotView.Background.color = new Color(0.88f, 0.28f, 0.28f, 1f);
                break;
        }

        if (feedbackGradeText != null)
        {
            if (feedbackAnimCoroutine != null) StopCoroutine(feedbackAnimCoroutine);
            feedbackAnimCoroutine = StartCoroutine(ShowFeedbackRoutine(feedbackText, color));
        }
    }

    private void HandleHarmonyChanged(float progress)
    {
        targetHarmonyProgress = Mathf.Clamp01(progress);
    }

    private void UpdateHarmonySmoothFill()
    {
        if (Mathf.Abs(displayHarmonyProgress - targetHarmonyProgress) > 0.0005f)
        {
            displayHarmonyProgress = Mathf.MoveTowards(displayHarmonyProgress, targetHarmonyProgress, Time.deltaTime * 1.5f);

            if (harmonyFillImg != null)
            {
                harmonyFillImg.fillAmount = displayHarmonyProgress;
            }
            if (harmonyFillRect != null)
            {
                harmonyFillRect.anchorMin = new Vector2(0f, 0f);
                harmonyFillRect.anchorMax = new Vector2(displayHarmonyProgress, 1f);
                harmonyFillRect.sizeDelta = Vector2.zero;
            }
            if (harmonyText != null)
            {
                int percent = Mathf.RoundToInt(displayHarmonyProgress * 100f);
                if (percent >= 100)
                {
                    harmonyText.text = "THU PHỤC HOÀN TẤT (100%)!";
                    harmonyText.color = new Color(0.48f, 0.22f, 0.05f, 1f);
                }
                else
                {
                    harmonyText.text = $"TIẾN TRÌNH THU PHỤC: <b>{percent}%</b>";
                    harmonyText.color = new Color(0.48f, 0.22f, 0.05f, 1f);
                }
            }
        }
    }

    private void HandleSessionFinished(bool isSuccess, string message)
    {
        isSessionActive = false;
        StartCoroutine(SessionFinishedRoutine(isSuccess, message));
    }

    private class ParticleData
    {
        public GameObject Obj;
        public RectTransform Rect;
        public Image Img;
        public Vector2 Velocity;
        public float RotationSpeed;
        public float Gravity;
    }

    private IEnumerator SessionFinishedRoutine(bool isSuccess, string message)
    {
        if (isSuccess)
        {
            // Chạy chuỗi hiệu ứng chiến thắng khải hoàn tạo cảm giác thỏa mãn cực độ!
            yield return StartCoroutine(PlayVictoryEuphoriaRoutine(message));
        }
        else
        {
            if (feedbackGradeText != null)
            {
                feedbackGradeText.text = "HẾT THỜI GIAN!";
                feedbackGradeText.color = new Color(0.88f, 0.30f, 0.25f, 1f);
                feedbackGradeText.fontSize = 24;
            }

            yield return new WaitForSeconds(1.15f);

            completionCallback?.Invoke(new MinigameResult
            {
                IsSuccess = false,
                Type = MinigameType.RhythmResonance,
                Score = Mathf.RoundToInt(engine.HarmonyProgress * 1000f),
                Accuracy = engine.HarmonyProgress,
                Message = message
            });
        }
    }

    private IEnumerator PlayVictoryEuphoriaRoutine(string message)
    {
        EnsureAudioSource();
        if (audioSource != null && victoryFanfareClip != null)
        {
            audioSource.PlayOneShot(victoryFanfareClip);
        }

        // 1. Đổi thanh tiến độ bừng sáng vàng hoàng kim hổ phách
        if (harmonyFillImg != null)
        {
            harmonyFillImg.color = new Color(1f, 0.88f, 0.25f, 1f);
        }
        if (harmonyText != null)
        {
            harmonyText.text = "THU PHỤC HOÀN TẤT (100%)";
            harmonyText.color = new Color(0.48f, 0.22f, 0.05f, 1f);
        }

        // 2. Màn hình Golden Euphoria Flash (Chớp sáng nhẹ nhàng)
        GameObject flashObj = new GameObject("VictoryFlash");
        flashObj.transform.SetParent(this.transform, false);
        RectTransform flashRect = flashObj.AddComponent<RectTransform>();
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.sizeDelta = Vector2.zero;
        Image flashImg = flashObj.AddComponent<Image>();
        flashImg.sprite = GetWhiteSprite();
        flashImg.color = new Color(1f, 0.94f, 0.55f, 0.55f);
        flashImg.raycastTarget = false;

        // 3. Tạo Huy Hiệu Chiến Thắng Khải Hoàn (Victory Celebration Crest) ở giữa bảng như ảnh thiết kế
        GameObject crestObj = new GameObject("VictoryCrest");
        crestObj.transform.SetParent(panelRect, false);
        RectTransform crestRect = crestObj.AddComponent<RectTransform>();
        crestRect.sizeDelta = new Vector2(490f, 160f);
        crestRect.anchoredPosition = new Vector2(0f, 22f);
        crestRect.localScale = Vector3.zero;

        // Hào quang Sunburst Rays xoay tròn phía sau
        GameObject sunburstObj = new GameObject("SunburstRays");
        sunburstObj.transform.SetParent(crestObj.transform, false);
        RectTransform sbRect = sunburstObj.AddComponent<RectTransform>();
        sbRect.sizeDelta = new Vector2(340f, 340f);
        sbRect.anchoredPosition = new Vector2(0f, 25f);
        Image sbImg = sunburstObj.AddComponent<Image>();
        sbImg.sprite = sunburstSpriteCache;
        sbImg.color = new Color(1f, 0.90f, 0.40f, 0.45f);
        sbImg.raycastTarget = false;

        // Banner thẻ gỗ viền caramel & lòng kem ấm (Plaque)
        GameObject plaqueObj = new GameObject("Plaque");
        plaqueObj.transform.SetParent(crestObj.transform, false);
        RectTransform plqRect = plaqueObj.AddComponent<RectTransform>();
        plqRect.sizeDelta = new Vector2(480f, 150f);
        plqRect.anchoredPosition = new Vector2(0f, 0f);
        Image plqImg = plaqueObj.AddComponent<Image>();
        plqImg.sprite = woodBoardSpriteCache;
        plqImg.type = Image.Type.Sliced;
        plqImg.color = new Color(1f, 0.98f, 0.94f, 1f);

        // Biểu tượng trái tim 3D bừng sáng trên đỉnh banner
        GameObject badgeIconObj = new GameObject("BadgeIcon");
        badgeIconObj.transform.SetParent(crestObj.transform, false);
        RectTransform biRect = badgeIconObj.AddComponent<RectTransform>();
        biRect.sizeDelta = new Vector2(64f, 64f);
        biRect.anchoredPosition = new Vector2(0f, 75f);
        Image biImg = badgeIconObj.AddComponent<Image>();
        biImg.sprite = heartSpriteCache;
        biImg.color = Color.white;
        biImg.raycastTarget = false;

        // Text tiêu đề chiến thắng
        GameObject vTitleObj = new GameObject("VictoryTitle");
        vTitleObj.transform.SetParent(plaqueObj.transform, false);
        RectTransform vtRect = vTitleObj.AddComponent<RectTransform>();
        vtRect.sizeDelta = new Vector2(450f, 36f);
        vtRect.anchoredPosition = new Vector2(0f, 24f);
        var vtText = vTitleObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) vtText.font = fontAsset;
        vtText.text = "THU PHỤC THÀNH CÔNG !";
        vtText.fontSize = 25f;
        vtText.fontStyle = FontStyles.Bold;
        vtText.alignment = TextAlignmentOptions.Center;
        vtText.color = new Color(0.68f, 0.22f, 0.12f, 1f); // Nâu đỏ ấm áp

        var vtOutline = vTitleObj.AddComponent<Outline>();
        vtOutline.effectColor = new Color(0.45f, 0.14f, 0.08f, 0.70f);
        vtOutline.effectDistance = new Vector2(1f, -1f);

        // Text tên thú & gắn kết tình bạn (màu sắc sắc nét, tương phản tối đa trên nền giấy kem)
        GameObject vSubObj = new GameObject("VictorySub");
        vSubObj.transform.SetParent(plaqueObj.transform, false);
        RectTransform vsRect = vSubObj.AddComponent<RectTransform>();
        vsRect.sizeDelta = new Vector2(450f, 50f);
        vsRect.anchoredPosition = new Vector2(0f, -22f);
        var vsText = vSubObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) vsText.font = fontAsset;
        string species = !string.IsNullOrEmpty(currentContext.SpeciesName) ? currentContext.SpeciesName : "Linh Thú";
        vsText.color = new Color(0.26f, 0.16f, 0.10f, 1f); // Nâu chocolate đậm sắc nét, tương phản hoàn hảo trên nền giấy kem
        vsText.text = $"<b>{species}</b> đã sẵn sàng đồng hành cùng bạn!\n<size=13.5><color=#5C4032>Tâm hồn thú cưng để hòa làm một</color></size>";
        vsText.fontSize = 15f;
        vsText.alignment = TextAlignmentOptions.Center;

        // 4. Bắn chùm pháo hoa hạt ánh sáng tinh linh (32 hạt)
        List<ParticleData> particles = SpawnCelebrationParticles(panelRect, 32);

        // 5. Vòng lặp animation thư thái kéo dài 2.5 giây
        float duration = 2.5f;
        float elapsed = 0f;
        Vector3 boardBaseScale = panelRect.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            // Xoay Sunburst Rays
            if (sbRect != null)
            {
                sbRect.localEulerAngles = new Vector3(0f, 0f, -elapsed * 35f);
            }

            // Đập nảy Crest với Overshoot trong 0.5s đầu
            if (crestRect != null)
            {
                if (elapsed < 0.5f)
                {
                    float p = elapsed / 0.5f;
                    float scale = Mathf.Sin(p * Mathf.PI * 0.5f);
                    float overshoot = 1f + 0.18f * Mathf.Sin(p * Mathf.PI);
                    crestRect.localScale = Vector3.one * (scale * overshoot);
                }
                else
                {
                    float breath = 1f + 0.03f * Mathf.Sin((elapsed - 0.5f) * 6f);
                    crestRect.localScale = Vector3.one * breath;
                }
            }

            // Nảy bảng gỗ đàn hồi
            if (panelRect != null)
            {
                if (elapsed < 0.45f)
                {
                    float bump = 1f + 0.06f * Mathf.Sin(elapsed / 0.45f * Mathf.PI);
                    panelRect.localScale = boardBaseScale * bump;
                }
                else
                {
                    panelRect.localScale = boardBaseScale;
                }
            }

            // Icon trái tim trên banner đập nhịp vui vẻ
            if (biRect != null)
            {
                float heartBeat = 1f + 0.12f * Mathf.Abs(Mathf.Sin(elapsed * 7f));
                biRect.localScale = Vector3.one * heartBeat;
            }

            // Mờ dần Flash
            if (flashImg != null)
            {
                float flashA = Mathf.Clamp01(1f - (elapsed / 0.5f)) * 0.55f;
                flashImg.color = new Color(1f, 0.94f, 0.55f, flashA);
            }

            // Cập nhật vị trí hạt pháo hoa
            UpdateCelebrationParticles(particles, Time.deltaTime);

            yield return null;
        }

        // 6. Dọn dẹp flash và particles
        if (flashObj != null) Destroy(flashObj);
        foreach (var p in particles)
        {
            if (p.Obj != null) Destroy(p.Obj);
        }

        // 7. Gọi callback hoàn tất để chuyển giao kết quả
        completionCallback?.Invoke(new MinigameResult
        {
            IsSuccess = true,
            Type = MinigameType.RhythmResonance,
            Score = Mathf.RoundToInt(engine.HarmonyProgress * 1000f),
            Accuracy = engine.HarmonyProgress,
            Message = message
        });
    }

    private List<ParticleData> SpawnCelebrationParticles(Transform parent, int count)
    {
        var list = new List<ParticleData>();
        for (int i = 0; i < count; i++)
        {
            GameObject pObj = new GameObject($"VictoryPart_{i}");
            pObj.transform.SetParent(parent, false);
            RectTransform pRect = pObj.AddComponent<RectTransform>();
            float size = UnityEngine.Random.Range(22f, 36f);
            pRect.sizeDelta = new Vector2(size, size);
            pRect.anchoredPosition = new Vector2(UnityEngine.Random.Range(-30f, 30f), UnityEngine.Random.Range(-10f, 40f));

            Image pImg = pObj.AddComponent<Image>();
            pImg.raycastTarget = false;

            int type = i % 3;
            if (type == 0)
            {
                pImg.sprite = sparkleStarSpriteCache;
                pImg.color = new Color(1f, 0.92f, 0.30f, 1f); // Sao vàng
            }
            else if (type == 1)
            {
                pImg.sprite = heartSpriteCache;
                pImg.color = new Color(0.98f, 0.35f, 0.45f, 1f); // Tim hồng
            }
            else
            {
                pImg.sprite = targetStarburstSpriteCache;
                pImg.color = new Color(1f, 0.85f, 0.25f, 1f); // Sao/hoa vàng
            }

            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = UnityEngine.Random.Range(240f, 520f);
            Vector2 vel = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed * 0.85f + 60f);

            list.Add(new ParticleData
            {
                Obj = pObj,
                Rect = pRect,
                Img = pImg,
                Velocity = vel,
                RotationSpeed = UnityEngine.Random.Range(-180f, 180f),
                Gravity = UnityEngine.Random.Range(220f, 380f)
            });
        }
        return list;
    }

    private void UpdateCelebrationParticles(List<ParticleData> particles, float dt)
    {
        for (int i = 0; i < particles.Count; i++)
        {
            var p = particles[i];
            if (p.Obj == null || p.Rect == null) continue;

            p.Velocity.y -= p.Gravity * dt;
            p.Velocity.x *= Mathf.Pow(0.92f, dt * 60f); // Ma sát không khí
            p.Rect.anchoredPosition += p.Velocity * dt;
            p.Rect.localEulerAngles = new Vector3(0f, 0f, p.Rect.localEulerAngles.z + p.RotationSpeed * dt);

            if (p.Img != null)
            {
                Color c = p.Img.color;
                c.a = Mathf.Clamp01(c.a - dt * 0.42f);
                p.Img.color = c;
            }
        }
    }

    // ==========================================
    // TẠO GIAO DIỆN ĐỒ HỌA (COZY 3D UI CREATION)
    // ==========================================

    private void BuildInterface()
    {
        // 0. Nền Dim đen toàn màn hình (Focus mờ nền, loại bỏ cảm giác popup thô sơ / AI)
        GameObject dimObj = new GameObject("DimOverlay");
        dimObj.transform.SetParent(this.transform, false);
        RectTransform dimRect = dimObj.AddComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.sizeDelta = Vector2.zero;
        dimOverlayImg = dimObj.AddComponent<Image>();
        dimOverlayImg.color = new Color(0f, 0f, 0f, 0.52f);
        dimOverlayImg.raycastTarget = true; // Chặn bấm xuyên ra cảnh phía sau

        // 1. Khung Card Bảng Gỗ Cozy 3D (Kích thước 760x560 chuẩn theo mockup)
        GameObject cardObj = new GameObject("RhythmWoodBoard");
        cardObj.transform.SetParent(this.transform, false);
        panelRect = cardObj.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(760f, 560f);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;

        Image panelImg = cardObj.AddComponent<Image>();
        panelImg.sprite = woodBoardSpriteCache;
        panelImg.type = Image.Type.Sliced;
        panelImg.color = Color.white;

        // Dây leo Ivy chạy dọc viền trên, trái và phải bảng gỗ như mẫu thiết kế chuẩn
        CreateTopIvyGarland(cardObj.transform, new Vector2(0f, 268f));
        CreateSideIvyGarland(cardObj.transform, false); // Viền bên trái
        CreateSideIvyGarland(cardObj.transform, true);  // Viền bên phải

        // Cụm hoa & lá xanh ở 2 góc dưới (hoa vàng/cam góc trái, quả mọng/hoa tím góc phải)
        CreateFloraBouquet(cardObj.transform, new Vector2(-344f, -244f), false);
        CreateFloraBouquet(cardObj.transform, new Vector2(344f, -244f), true);

        // Trang trí lá cây nhẹ ở 2 góc trên
        CreateCornerDecoration(cardObj.transform, new Vector2(-336f, 240f), 0f);
        CreateCornerDecoration(cardObj.transform, new Vector2(336f, 240f), 180f);

        // 2. Header: Nút Đóng & Đồng Hồ Thời Gian
        CreateCloseButton(cardObj.transform, new Vector2(335f, 226f), CancelSession);
        CreateTopTimer(cardObj.transform, new Vector2(-280f, 226f));

        // Tiêu đề chính to rõ: VŨ ĐIỆU HÒA NHỊP (Bubbly vàng cam có viền nâu ấm, không biểu tượng thừa)
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(cardObj.transform, false);
        RectTransform tRect = titleObj.AddComponent<RectTransform>();
        tRect.sizeDelta = new Vector2(580f, 38f);
        tRect.anchoredPosition = new Vector2(0f, 228f);
        var titleText = titleObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) titleText.font = fontAsset;
        titleText.text = "VŨ ĐIỆU HÒA NHỊP";
        titleText.fontSize = 28f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(0.98f, 0.65f, 0.12f, 1f); // Vàng cam mật ong ấm áp

        var titleOutline = titleObj.AddComponent<Outline>();
        titleOutline.effectColor = new Color(0.38f, 0.14f, 0.04f, 0.95f); // Viền sô-cô-la dày dặn
        titleOutline.effectDistance = new Vector2(1.8f, -1.8f);

        GameObject subObj = new GameObject("SubTitle");
        subObj.transform.SetParent(cardObj.transform, false);
        RectTransform sRect = subObj.AddComponent<RectTransform>();
        sRect.sizeDelta = new Vector2(580f, 22f);
        sRect.anchoredPosition = new Vector2(0f, 196f);
        var subTitleText = subObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) subTitleText.font = fontAsset;
        subTitleText.text = $"{currentContext.SpeciesName} • {GetRankText(currentContext.Rank)}";
        subTitleText.fontSize = 14f;
        subTitleText.fontStyle = FontStyles.Bold;
        subTitleText.alignment = TextAlignmentOptions.Center;
        subTitleText.color = new Color(0.32f, 0.22f, 0.18f, 1f); // Nâu hạt dẻ đậm nét

        // 3. Thanh Tiến Độ Thu Phục (Ống Capsule Vàng Hoàng Kim Dây Leo)
        CreateHarmonyBar(cardObj.transform);

        // 4. Khay Nền & Khung Hiển Thị Chuỗi Mũi Tên Kẹo Thạch 3D (Chiều cao 96px gọn gàng, sạch sẽ)
        GameObject seqBackplateObj = new GameObject("SequenceBackplate");
        seqBackplateObj.transform.SetParent(cardObj.transform, false);
        RectTransform sbpRect = seqBackplateObj.AddComponent<RectTransform>();
        sbpRect.sizeDelta = new Vector2(640f, 96f);
        sbpRect.anchoredPosition = new Vector2(0f, 50f);
        Image sbpImg = seqBackplateObj.AddComponent<Image>();
        sbpImg.sprite = capsuleBarSpriteCache;
        sbpImg.type = Image.Type.Sliced;
        sbpImg.color = new Color(0.95f, 0.92f, 0.86f, 0.92f);

        GameObject seqContainerObj = new GameObject("SequenceContainer");
        seqContainerObj.transform.SetParent(cardObj.transform, false);
        noteContainer = seqContainerObj.AddComponent<RectTransform>();
        noteContainer.sizeDelta = new Vector2(640f, 96f);
        noteContainer.anchoredPosition = new Vector2(0f, 50f);

        // 5. Thanh Nhịp Điệu Gỗ Trầm & Hồng Tâm Luminous Starburst
        CreateRhythmRail(cardObj.transform);

        // 6. Phản Hồi Đánh Giá & Nút Bấm Chạm Nhịp
        CreateBottomControls(cardObj.transform);
    }

    private NoteSlotView CreateNoteSlot(Transform parent, RhythmDirection dir, int index, float posX, float width)
    {
        GameObject slotObj = new GameObject($"NoteSlot_{index}");
        slotObj.transform.SetParent(parent, false);
        RectTransform slotRect = slotObj.AddComponent<RectTransform>();
        slotRect.anchorMin = new Vector2(0.5f, 0.5f);
        slotRect.anchorMax = new Vector2(0.5f, 0.5f);
        slotRect.pivot = new Vector2(0.5f, 0.5f);
        slotRect.sizeDelta = new Vector2(width, 76f);
        slotRect.anchoredPosition = new Vector2(posX, 0f);

        // Nút Kẹo Thạch 3D bóng bẩy có sẵn icon mũi tên dày dặn (căn chính giữa khay theo mockup)
        GameObject arrowImgObj = new GameObject("ArrowIcon");
        arrowImgObj.transform.SetParent(slotObj.transform, false);
        RectTransform arrRect = arrowImgObj.AddComponent<RectTransform>();
        arrRect.sizeDelta = new Vector2(66f, 66f);
        arrRect.anchoredPosition = Vector2.zero;

        Image arrowImg = arrowImgObj.AddComponent<Image>();
        arrowImg.sprite = GetJellySprite(dir);
        arrowImg.color = Color.white;
        arrowImg.raycastTarget = false;

        var outline = arrowImgObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.72f, 0.65f, 0.52f, 0.50f);
        outline.effectDistance = new Vector2(1f, -1f);

        return new NoteSlotView
        {
            RootObj = slotObj,
            Background = arrowImg,
            Outline = outline,
            ArrowImg = arrowImg,
            Direction = dir,
            BaseColor = GetDirectionColor(dir)
        };
    }

    private SpaceSlotView CreateSpaceSlot(Transform parent, float posX, float width)
    {
        GameObject spaceObj = new GameObject("SpaceSlot");
        spaceObj.transform.SetParent(parent, false);
        RectTransform rect = spaceObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, 76f);
        rect.anchoredPosition = new Vector2(posX, 0f);

        GameObject spaceBtnObj = new GameObject("SpaceBtn");
        spaceBtnObj.transform.SetParent(spaceObj.transform, false);
        RectTransform btnRect = spaceBtnObj.AddComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(width - 4f, 66f);
        btnRect.anchoredPosition = Vector2.zero;

        Image bg = spaceBtnObj.AddComponent<Image>();
        bg.sprite = stoneSpaceSpriteCache;
        bg.type = Image.Type.Sliced;
        bg.color = Color.white;
        bg.raycastTarget = false;

        var outline = spaceBtnObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.68f, 0.60f, 0.48f, 0.70f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // Nhãn chính SPACE to rõ nằm gọn gàng bên trong nút
        GameObject mainObj = new GameObject("MainLabel");
        mainObj.transform.SetParent(spaceBtnObj.transform, false);
        RectTransform mr = mainObj.AddComponent<RectTransform>();
        mr.anchorMin = Vector2.zero;
        mr.anchorMax = Vector2.one;
        mr.sizeDelta = Vector2.zero;
        var mainTmp = mainObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) mainTmp.font = fontAsset;
        mainTmp.text = "SPACE";
        mainTmp.fontSize = 15.5f;
        mainTmp.fontStyle = FontStyles.Bold;
        mainTmp.alignment = TextAlignmentOptions.Center;
        mainTmp.color = new Color(0.36f, 0.32f, 0.26f, 0.95f);
        mainTmp.raycastTarget = false;

        return new SpaceSlotView
        {
            RootObj = spaceObj,
            Background = bg,
            Outline = outline,
            MainLabel = mainTmp
        };
    }

    private void CreateHarmonyBar(Transform parent)
    {
        // Ống Capsule bo tròn mềm mại
        GameObject bgObj = new GameObject("HarmonyBg");
        bgObj.transform.SetParent(parent, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(640f, 38f);
        bgRect.anchoredPosition = new Vector2(0f, 150f);

        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.sprite = capsuleBarSpriteCache;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = new Color(0.92f, 0.88f, 0.80f, 1f); // Lòng rãnh kem ấm

        var bgOutline = bgObj.AddComponent<Outline>();
        bgOutline.effectColor = new Color(0.78f, 0.62f, 0.35f, 0.85f); // Viền vàng kim tinh tế
        bgOutline.effectDistance = new Vector2(1.2f, -1.2f);

        // Đôi cánh dây leo lá xanh ôm 2 đầu thanh tiến trình (như ảnh thiết kế)
        CreateBarWing(bgObj.transform, new Vector2(-326f, 0f), 0f);
        CreateBarWing(bgObj.transform, new Vector2(326f, 0f), 180f);

        // Thanh Fill ngọc vàng hoàng kim bóng bẩy
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(bgObj.transform, false);
        harmonyFillRect = fillObj.AddComponent<RectTransform>();
        harmonyFillRect.anchorMin = new Vector2(0f, 0f);
        harmonyFillRect.anchorMax = new Vector2(0f, 1f);
        harmonyFillRect.sizeDelta = Vector2.zero;
        harmonyFillRect.pivot = new Vector2(0f, 0.5f);

        harmonyFillImg = fillObj.AddComponent<Image>();
        harmonyFillImg.sprite = glossyFillSpriteCache;
        harmonyFillImg.type = Image.Type.Filled;
        harmonyFillImg.fillMethod = Image.FillMethod.Horizontal;
        harmonyFillImg.fillAmount = 0f;
        harmonyFillImg.color = new Color(0.98f, 0.74f, 0.16f, 1f); // Vàng hổ phách rực rỡ như mẫu

        // Nhãn hiển thị tiến độ súc tích, chữ to rõ
        GameObject tObj = new GameObject("Label");
        tObj.transform.SetParent(bgObj.transform, false);
        RectTransform tlRect = tObj.AddComponent<RectTransform>();
        tlRect.anchorMin = Vector2.zero;
        tlRect.anchorMax = Vector2.one;
        tlRect.sizeDelta = Vector2.zero;
        harmonyText = tObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) harmonyText.font = fontAsset;
        harmonyText.text = "TIẾN TRÌNH THU PHỤC: <b>0%</b>";
        harmonyText.fontSize = 15.5f;
        harmonyText.fontStyle = FontStyles.Bold;
        harmonyText.alignment = TextAlignmentOptions.Center;
        harmonyText.color = new Color(0.48f, 0.22f, 0.05f, 1f); // Nâu vàng trầm ấm tương phản cao

        var tOutline = tObj.AddComponent<Outline>();
        tOutline.effectColor = new Color(1f, 0.95f, 0.75f, 0.65f);
        tOutline.effectDistance = new Vector2(0.8f, -0.8f);
    }

    private void CreateRhythmRail(Transform parent)
    {
        GameObject railObj = new GameObject("RhythmRail");
        railObj.transform.SetParent(parent, false);
        railRect = railObj.AddComponent<RectTransform>();
        railRect.sizeDelta = new Vector2(640f, 48f);
        railRect.anchoredPosition = new Vector2(0f, -46f);

        // Nền rãnh gỗ ấm màu caramel óc chó trầm
        Image railImg = railObj.AddComponent<Image>();
        railImg.sprite = capsuleBarSpriteCache;
        railImg.type = Image.Type.Sliced;
        railImg.color = new Color(0.24f, 0.16f, 0.12f, 0.98f);

        var railOutline = railObj.AddComponent<Outline>();
        railOutline.effectColor = new Color(0.62f, 0.42f, 0.20f, 0.85f); // Viền đồng vàng ấm áp
        railOutline.effectDistance = new Vector2(1f, -1f);

        // Rãnh trượt kem mềm mịn ở giữa
        GameObject guideLineObj = new GameObject("GuideLine");
        guideLineObj.transform.SetParent(railObj.transform, false);
        RectTransform glRect = guideLineObj.AddComponent<RectTransform>();
        glRect.anchorMin = new Vector2(0.04f, 0.5f);
        glRect.anchorMax = new Vector2(0.96f, 0.5f);
        glRect.sizeDelta = new Vector2(0f, 5f);
        Image glImg = guideLineObj.AddComponent<Image>();
        glImg.sprite = whiteSpriteCache;
        glImg.color = new Color(0.88f, 0.72f, 0.48f, 0.45f);

        float railWidth = 640f - 60f;

        // Các hạt ngọc vàng định nhịp (Rhythmic Golden Beads) đặt dọc theo rãnh gỗ
        float[] beadPercents = new float[] { 0.16f, 0.32f, 0.48f, 0.64f, 0.80f };
        foreach (var bp in beadPercents)
        {
            GameObject beadObj = new GameObject($"Bead_{bp}");
            beadObj.transform.SetParent(railObj.transform, false);
            RectTransform br = beadObj.AddComponent<RectTransform>();
            br.sizeDelta = new Vector2(13f, 13f);
            float bx = (bp * railWidth) - (railWidth * 0.5f);
            br.anchoredPosition = new Vector2(bx, 0f);
            Image bImg = beadObj.AddComponent<Image>();
            bImg.sprite = goldBeadSpriteCache;
            bImg.color = Color.white;
            bImg.raycastTarget = false;
        }

        float targetPosX = (RhythmMinigameEngine.TargetBeatNormalizedPos * railWidth) - (railWidth * 0.5f);

        // Đóa Sao Vàng Hào Quang Rực Rỡ (Luminous Golden Starburst Target)
        GameObject hitZoneObj = new GameObject("HitZone");
        hitZoneObj.transform.SetParent(railObj.transform, false);
        hitZoneRect = hitZoneObj.AddComponent<RectTransform>();
        hitZoneRect.sizeDelta = new Vector2(62f, 62f);
        hitZoneRect.anchoredPosition = new Vector2(targetPosX, 0f);

        hitZoneImg = hitZoneObj.AddComponent<Image>();
        hitZoneImg.sprite = targetStarburstSpriteCache;
        hitZoneImg.color = Color.white;

        // Approach Ring (Vòng hào quang thu nhỏ đón đích)
        GameObject ringObj = new GameObject("ApproachRing");
        ringObj.transform.SetParent(railObj.transform, false);
        approachRingRect = ringObj.AddComponent<RectTransform>();
        approachRingRect.sizeDelta = new Vector2(62f, 62f);
        approachRingRect.anchoredPosition = new Vector2(targetPosX, 0f);

        approachRingImg = ringObj.AddComponent<Image>();
        approachRingImg.sprite = ringSpriteCache;
        approachRingImg.color = new Color(1f, 0.90f, 0.45f, 0.85f);
        ringObj.SetActive(false);

        // Hạt Ngọc Trai 3D Ánh Sáng (Glossy Pearl Orb)
        GameObject orbObj = new GameObject("LuminousOrb");
        orbObj.transform.SetParent(railObj.transform, false);
        orbRect = orbObj.AddComponent<RectTransform>();
        orbRect.sizeDelta = new Vector2(40f, 40f);
        orbRect.anchoredPosition = new Vector2(-railWidth * 0.5f, 0f);

        orbImg = orbObj.AddComponent<Image>();
        orbImg.sprite = pearlOrbSpriteCache;
        orbImg.color = Color.white;
    }

    private void CreateTopTimer(Transform parent, Vector2 pos)
    {
        GameObject timerObj = new GameObject("TopTimerBadge");
        timerObj.transform.SetParent(parent, false);
        RectTransform tRect = timerObj.AddComponent<RectTransform>();
        tRect.sizeDelta = new Vector2(114f, 34f);
        tRect.anchoredPosition = pos;

        Image bg = timerObj.AddComponent<Image>();
        bg.sprite = greenBadgeSpriteCache;
        bg.type = Image.Type.Sliced;
        bg.color = Color.white;

        var outline = timerObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.18f, 0.46f, 0.18f, 0.9f);
        outline.effectDistance = new Vector2(1f, -1f);

        // Icon đồng hồ vẽ procedural sắc nét (loại bỏ hoàn toàn ký hiệu unicode để không bị lỗi ô vuông □)
        GameObject clockIconObj = new GameObject("ClockIcon");
        clockIconObj.transform.SetParent(timerObj.transform, false);
        RectTransform cir = clockIconObj.AddComponent<RectTransform>();
        cir.sizeDelta = new Vector2(18f, 18f);
        cir.anchoredPosition = new Vector2(-32f, 0f);
        Image cImg = clockIconObj.AddComponent<Image>();
        cImg.sprite = clockIconSpriteCache;
        cImg.color = Color.white;
        cImg.raycastTarget = false;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(timerObj.transform, false);
        RectTransform tr = textObj.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.sizeDelta = Vector2.zero;
        tr.anchoredPosition = new Vector2(10f, 0f); // Dời sang phải nhường chỗ cho icon đồng hồ

        timerText = textObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) timerText.font = fontAsset;
        timerText.fontSize = 14.5f;
        timerText.fontStyle = FontStyles.Bold;
        timerText.alignment = TextAlignmentOptions.Center;
        timerText.color = Color.white;
        timerText.text = "13,9s";
    }

    private void CreateBottomControls(Transform parent)
    {
        // 1. Text Combo ngắn gọn
        GameObject comboObj = new GameObject("ComboFeedback");
        comboObj.transform.SetParent(parent, false);
        RectTransform comboRect = comboObj.AddComponent<RectTransform>();
        comboRect.sizeDelta = new Vector2(500f, 22f);
        comboRect.anchoredPosition = new Vector2(0f, -98f);
        comboText = comboObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) comboText.font = fontAsset;
        comboText.fontSize = 14f;
        comboText.fontStyle = FontStyles.Bold;
        comboText.alignment = TextAlignmentOptions.Center;
        comboText.color = new Color(0.48f, 0.38f, 0.32f, 1f);
        comboText.text = "";

        // 2. Text Phản Hồi Đánh Giá súc tích (✦ PERFECT! (+35%) ✦ / GREAT / GOOD / MISS)
        GameObject gradeObj = new GameObject("GradeFeedback");
        gradeObj.transform.SetParent(parent, false);
        RectTransform gradeRect = gradeObj.AddComponent<RectTransform>();
        gradeRect.sizeDelta = new Vector2(500f, 34f);
        gradeRect.anchoredPosition = new Vector2(0f, -126f);
        feedbackGradeText = gradeObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) feedbackGradeText.font = fontAsset;
        feedbackGradeText.fontSize = 24f;
        feedbackGradeText.fontStyle = FontStyles.Bold;
        feedbackGradeText.alignment = TextAlignmentOptions.Center;
        feedbackGradeText.textWrappingMode = TextWrappingModes.NoWrap;
        feedbackGradeText.text = "";

        var gOutline = gradeObj.AddComponent<Outline>();
        gOutline.effectColor = new Color(0.42f, 0.18f, 0.05f, 0.85f);
        gOutline.effectDistance = new Vector2(1.2f, -1.2f);

        // 3. Nút Chạm Nhịp Viên Thuốc 3D Màu Xanh Bơ
        GameObject btnObj = new GameObject("Btn_Beat");
        btnObj.transform.SetParent(parent, false);
        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(320f, 54f);
        rect.anchoredPosition = new Vector2(0f, -172f);

        beatButtonImg = btnObj.AddComponent<Image>();
        beatButtonImg.sprite = bigPillBtnSpriteCache;
        beatButtonImg.type = Image.Type.Sliced;
        beatButtonImg.color = Color.white;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = beatButtonImg;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 0.95f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.78f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(() =>
        {
            if (beatButtonImg != null)
            {
                StartCoroutine(PunchScaleRoutine(beatButtonImg.transform, 1.08f, 0.12f));
            }

            var grade = engine.InputBeatStrike();
            if (grade == RhythmHitGrade.None && !engine.IsSequenceCompleted)
            {
                if (spaceSlotView != null && spaceSlotView.RootObj != null)
                {
                    StartCoroutine(PunchScaleRoutine(spaceSlotView.RootObj.transform, 1.15f, 0.15f));
                }
            }
        });

        GameObject tObj = new GameObject("Text");
        tObj.transform.SetParent(btnObj.transform, false);
        RectTransform tr = tObj.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.sizeDelta = Vector2.zero;
        beatButtonText = tObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) beatButtonText.font = fontAsset;
        beatButtonText.text = "CHẠM NHỊP (SPACE)";
        beatButtonText.fontSize = 17f;
        beatButtonText.fontStyle = FontStyles.Bold;
        beatButtonText.alignment = TextAlignmentOptions.Center;
        beatButtonText.color = Color.white;
        beatButtonText.raycastTarget = false;

        var btnOutline = tObj.AddComponent<Outline>();
        btnOutline.effectColor = new Color(0.12f, 0.44f, 0.18f, 0.90f);
        btnOutline.effectDistance = new Vector2(1f, -1f);

        // 4. Dòng chú thích phím tối giản, cô đọng nhất
        GameObject legendObj = new GameObject("HotkeyLegend");
        legendObj.transform.SetParent(parent, false);
        RectTransform legRect = legendObj.AddComponent<RectTransform>();
        legRect.sizeDelta = new Vector2(580f, 20f);
        legRect.anchoredPosition = new Vector2(0f, -222f);
        var legTmp = legendObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) legTmp.font = fontAsset;
        legTmp.text = "<color=#6E523D>[A, W, S, D] hoặc [Mũi tên] • [SPACE] Chạm nhịp</color>";
        legTmp.fontSize = 12.5f;
        legTmp.alignment = TextAlignmentOptions.Center;
        legTmp.raycastTarget = false;
    }

    private void CreateCloseButton(Transform parent, Vector2 pos, UnityEngine.Events.UnityAction action)
    {
        GameObject btnObj = new GameObject("CloseBtn");
        btnObj.transform.SetParent(parent, false);
        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(38f, 38f);
        rect.anchoredPosition = pos;

        Image img = btnObj.AddComponent<Image>();
        img.sprite = redCloseBtnSpriteCache;
        img.color = Color.white;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.8f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(action);
    }

    private void CreateTopIvyGarland(Transform parent, Vector2 pos)
    {
        GameObject garlandObj = new GameObject("TopIvyGarland");
        garlandObj.transform.SetParent(parent, false);
        RectTransform gr = garlandObj.AddComponent<RectTransform>();
        gr.sizeDelta = new Vector2(560f, 48f);
        gr.anchoredPosition = pos;

        Image img = garlandObj.AddComponent<Image>();
        img.sprite = ivyGarlandSpriteCache;
        img.color = Color.white;
        img.raycastTarget = false;
    }

    private void CreateSideIvyGarland(Transform parent, bool isRight)
    {
        GameObject ivyObj = new GameObject(isRight ? "SideIvyRight" : "SideIvyLeft");
        ivyObj.transform.SetParent(parent, false);
        RectTransform r = ivyObj.AddComponent<RectTransform>();
        r.sizeDelta = new Vector2(46f, 480f);
        r.anchoredPosition = new Vector2(isRight ? 368f : -368f, 0f);

        Image img = ivyObj.AddComponent<Image>();
        img.sprite = isRight ? sideIvyRightSpriteCache : sideIvyLeftSpriteCache;
        img.color = Color.white;
        img.raycastTarget = false;
    }

    private void CreateFloraBouquet(Transform parent, Vector2 pos, bool isRight)
    {
        GameObject floraObj = new GameObject(isRight ? "FloraBouquetRight" : "FloraBouquetLeft");
        floraObj.transform.SetParent(parent, false);
        RectTransform fr = floraObj.AddComponent<RectTransform>();
        fr.sizeDelta = new Vector2(110f, 110f);
        fr.anchoredPosition = pos;
        fr.localEulerAngles = isRight ? new Vector3(0f, 180f, 0f) : Vector3.zero;

        Image img = floraObj.AddComponent<Image>();
        img.sprite = isRight ? floraRightSpriteCache : floraLeftSpriteCache;
        img.color = Color.white;
        img.raycastTarget = false;
    }

    private void CreateBarWing(Transform parent, Vector2 pos, float rotY)
    {
        GameObject wingObj = new GameObject("BarWing");
        wingObj.transform.SetParent(parent, false);
        RectTransform wr = wingObj.AddComponent<RectTransform>();
        wr.sizeDelta = new Vector2(48f, 48f);
        wr.anchoredPosition = pos;
        wr.localEulerAngles = new Vector3(0f, rotY, 0f);

        Image img = wingObj.AddComponent<Image>();
        img.sprite = barWingSpriteCache;
        img.color = Color.white;
        img.raycastTarget = false;
    }

    private void CreateCornerDecoration(Transform parent, Vector2 pos, float rotZ)
    {
        GameObject decoObj = new GameObject("CornerFlora");
        decoObj.transform.SetParent(parent, false);
        RectTransform dr = decoObj.AddComponent<RectTransform>();
        dr.sizeDelta = new Vector2(90f, 90f);
        dr.anchoredPosition = pos;
        dr.localEulerAngles = new Vector3(0f, 0f, rotZ);

        Image img = decoObj.AddComponent<Image>();
        img.sprite = cornerFloraSpriteCache;
        img.color = Color.white;
        img.raycastTarget = false;
    }

    private static Sprite GetJellySprite(RhythmDirection dir)
    {
        return dir switch
        {
            RhythmDirection.Left => jellyLeftSpriteCache,
            RhythmDirection.Up => jellyUpSpriteCache,
            RhythmDirection.Down => jellyDownSpriteCache,
            RhythmDirection.Right => jellyRightSpriteCache,
            _ => jellyLeftSpriteCache
        };
    }

    // ==========================================
    // PROCEDURAL SPRITES GENERATION (COZY 3D STYLE)
    // ==========================================

    private static void EnsureProceduralSprites()
    {
        if (woodBoardSpriteCache == null) woodBoardSpriteCache = CreateWoodBoardSprite();
        if (jellyLeftSpriteCache == null) jellyLeftSpriteCache = CreateJellyButtonSprite(new Color(0.95f, 0.35f, 0.40f, 1f), new Color(0.68f, 0.18f, 0.22f, 1f), RhythmDirection.Left, false);
        if (jellyUpSpriteCache == null) jellyUpSpriteCache = CreateJellyButtonSprite(new Color(0.98f, 0.80f, 0.20f, 1f), new Color(0.72f, 0.52f, 0.10f, 1f), RhythmDirection.Up, false);
        if (jellyDownSpriteCache == null) jellyDownSpriteCache = CreateJellyButtonSprite(new Color(0.96f, 0.56f, 0.22f, 1f), new Color(0.72f, 0.34f, 0.12f, 1f), RhythmDirection.Down, false);
        if (jellyRightSpriteCache == null) jellyRightSpriteCache = CreateJellyButtonSprite(new Color(0.24f, 0.72f, 0.90f, 1f), new Color(0.12f, 0.46f, 0.65f, 1f), RhythmDirection.Right, false);
        if (jellyCorrectSpriteCache == null) jellyCorrectSpriteCache = CreateJellyButtonSprite(new Color(0.28f, 0.78f, 0.38f, 1f), new Color(0.14f, 0.48f, 0.20f, 1f), null, true);
        if (stoneSpaceSpriteCache == null) stoneSpaceSpriteCache = CreateStoneSpaceSprite();
        if (capsuleBarSpriteCache == null) capsuleBarSpriteCache = CreateCapsuleBarSprite();
        if (glossyFillSpriteCache == null) glossyFillSpriteCache = CreateGlossyFillSprite();
        if (pearlOrbSpriteCache == null) pearlOrbSpriteCache = CreatePearlOrbSprite();
        if (targetStarburstSpriteCache == null) targetStarburstSpriteCache = CreateLuminousHitTargetSprite();
        if (bigPillBtnSpriteCache == null) bigPillBtnSpriteCache = CreateBigPillButtonSprite();
        if (greenBadgeSpriteCache == null) greenBadgeSpriteCache = CreateGreenBadgeSprite();
        if (redCloseBtnSpriteCache == null) redCloseBtnSpriteCache = CreateRedCloseButtonSprite();
        if (cornerFloraSpriteCache == null) cornerFloraSpriteCache = CreateCornerFloraSprite();
        if (ringSpriteCache == null) ringSpriteCache = CreateRingSprite();
        if (whiteSpriteCache == null) whiteSpriteCache = GetWhiteSprite();
        if (sparkleStarSpriteCache == null) sparkleStarSpriteCache = CreateSparkleStarSprite();
        if (heartSpriteCache == null) heartSpriteCache = CreateHeartSprite();
        if (sunburstSpriteCache == null) sunburstSpriteCache = CreateSunburstSprite();
        if (ivyGarlandSpriteCache == null) ivyGarlandSpriteCache = CreateIvyGarlandSprite();
        if (sideIvyLeftSpriteCache == null) sideIvyLeftSpriteCache = CreateSideIvySprite(false);
        if (sideIvyRightSpriteCache == null) sideIvyRightSpriteCache = CreateSideIvySprite(true);
        if (floraLeftSpriteCache == null) floraLeftSpriteCache = CreateFloraBouquetSprite(false);
        if (floraRightSpriteCache == null) floraRightSpriteCache = CreateFloraBouquetSprite(true);
        if (barWingSpriteCache == null) barWingSpriteCache = CreateBarVineWingSprite();
        if (goldBeadSpriteCache == null) goldBeadSpriteCache = CreateGoldBeadSprite();
        if (clockIconSpriteCache == null) clockIconSpriteCache = CreateClockIconSprite();
    }

    private static Sprite CreateSparkleStarSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];
        float center = 64f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Abs(x - center);
                float py = Mathf.Abs(y - center);
                float dist = Mathf.Sqrt(px * px + py * py);

                // Đường cong sao 4 cánh
                float starCurve = Mathf.Pow(px / 52f, 0.55f) + Mathf.Pow(py / 52f, 0.55f);
                if (starCurve <= 1.0f)
                {
                    Color c;
                    if (dist <= 12f)
                    {
                        c = Color.white; // Lõi tâm sáng chói
                    }
                    else
                    {
                        float t = (starCurve - 0.2f) / 0.8f;
                        c = Color.Lerp(new Color(1f, 0.96f, 0.65f, 1f), new Color(0.98f, 0.78f, 0.15f, 1f), t);
                    }
                    cols[y * size + x] = c;
                }
                else
                {
                    cols[y * size + x] = Color.clear;
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateHeartSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x - 64f) / 38f;
                float v = (y - 52f) / 38f;

                float eq = (u * u + v * v - 1f);
                float val = eq * eq * eq - (u * u * v * v * v);

                if (val <= 0f)
                {
                    Color c;
                    if (v < -0.1f)
                    {
                        c = new Color(0.75f, 0.16f, 0.25f, 1f);
                    }
                    else
                    {
                        c = new Color(0.98f, 0.32f, 0.44f, 1f);
                        float elX = (u + 0.32f) / 0.24f;
                        float elY = (v - 0.38f) / 0.20f;
                        if (elX * elX + elY * elY <= 1.0f)
                        {
                            c = Color.Lerp(c, Color.white, 0.45f);
                        }
                    }
                    cols[y * size + x] = c;
                }
                else
                {
                    cols[y * size + x] = Color.clear;
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateSunburstSprite()
    {
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];
        float center = 128f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x - center;
                float py = y - center;
                float dist = Mathf.Sqrt(px * px + py * py);

                if (dist <= 124f)
                {
                    float angle = Mathf.Atan2(py, px);
                    float rays = Mathf.Cos(12f * angle);
                    float rayFactor = Mathf.Clamp01(rays * 0.5f + 0.5f);

                    float fade = 1.0f - (dist / 124f);
                    float alpha = fade * (0.25f + 0.75f * rayFactor);
                    if (dist <= 32f)
                    {
                        alpha = Mathf.Max(alpha, 0.85f * (1.0f - dist / 32f));
                    }

                    cols[y * size + x] = new Color(1f, 0.90f, 0.40f, alpha * 0.85f);
                }
                else
                {
                    cols[y * size + x] = Color.clear;
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSpriteCache == null)
        {
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] cols = new Color[] { Color.white, Color.white, Color.white, Color.white };
            tex.SetPixels(cols);
            tex.Apply();
            whiteSpriteCache = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        }
        return whiteSpriteCache;
    }

    private static Sprite CreateWoodBoardSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];

        float r = 28f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = x < 32 ? 32f : (x > size - 32 ? size - 32 : x);
                float cy = y < 32 ? 32f : (y > size - 32 ? size - 32 : y);
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

                if (dist > r)
                {
                    cols[y * size + x] = Color.clear;
                    continue;
                }

                // Đáy đổ bóng 3D của bảng gỗ
                if (y < 12)
                {
                    cols[y * size + x] = new Color(0.38f, 0.22f, 0.12f, 1f);
                }
                // Khung viền gỗ caramel ấm
                else if (x <= 14 || x >= size - 15 || y <= 16 || y >= size - 15 || dist >= r - 12f)
                {
                    if (y >= size - 14)
                        cols[y * size + x] = new Color(0.82f, 0.58f, 0.34f, 1f);
                    else
                        cols[y * size + x] = new Color(0.68f, 0.44f, 0.24f, 1f);
                }
                // Chỉ rãnh trong viền gỗ
                else if (x <= 17 || x >= size - 18 || y <= 19 || y >= size - 18 || dist >= r - 15f)
                {
                    cols[y * size + x] = new Color(0.50f, 0.30f, 0.15f, 1f);
                }
                // Lòng bảng ngà sáng tự nhiên (Parchment Ivory)
                else
                {
                    cols[y * size + x] = new Color(0.97f, 0.94f, 0.88f, 1f);
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(38f, 38f, 38f, 38f));
    }

    private static Sprite CreateJellyButtonSprite(Color baseCol, Color bevelCol, RhythmDirection? dir, bool isCheck)
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];

        float cornerR = 26f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (x < 10 || x > 118 || y < 10 || y > 118)
                {
                    cols[y * size + x] = Color.clear;
                    continue;
                }

                float cx = x < 36 ? 36f : (x > 92 ? 92f : x);
                float cy = y < 36 ? 36f : (y > 92 ? 92f : y);
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

                if (dist > cornerR)
                {
                    cols[y * size + x] = Color.clear;
                    continue;
                }

                Color c;
                if (y < 24)
                {
                    c = bevelCol;
                }
                else
                {
                    c = baseCol;
                    // Vệt cong elip highlight bóng bẩy ở nửa trên
                    float elX = (x - 64f) / 38f;
                    float elY = (y - 88f) / 18f;
                    float elDist = elX * elX + elY * elY;
                    if (elDist <= 1.0f)
                    {
                        float gloss = (1.0f - elDist) * 0.42f;
                        c = Color.Lerp(c, Color.white, gloss);
                    }
                    if (y >= 106 && dist >= cornerR - 3f)
                    {
                        c = Color.Lerp(c, Color.white, 0.45f);
                    }
                }

                // Vẽ icon dày dặn với 2x2 supersampling
                float iconHit = 0f;
                for (int sy = 0; sy < 2; sy++)
                {
                    for (int sx = 0; sx < 2; sx++)
                    {
                        float px = x + (sx + 0.5f) * 0.5f;
                        float py = y + (sy + 0.5f) * 0.5f;

                        bool inIcon = false;
                        if (isCheck)
                        {
                            float d1 = DistToSegment(px, py, 44f, 66f, 58f, 50f);
                            float d2 = DistToSegment(px, py, 58f, 50f, 84f, 84f);
                            if (d1 <= 6f || d2 <= 6f) inIcon = true;
                        }
                        else if (dir == RhythmDirection.Left)
                        {
                            if (px >= 38f && px <= 64f && Mathf.Abs(py - 66f) <= (px - 38f) * 0.85f) inIcon = true;
                            if (px >= 64f && px <= 88f && Mathf.Abs(py - 66f) <= 10f) inIcon = true;
                        }
                        else if (dir == RhythmDirection.Right)
                        {
                            if (px >= 64f && px <= 90f && Mathf.Abs(py - 66f) <= (90f - px) * 0.85f) inIcon = true;
                            if (px >= 40f && px <= 64f && Mathf.Abs(py - 66f) <= 10f) inIcon = true;
                        }
                        else if (dir == RhythmDirection.Up)
                        {
                            if (py >= 64f && py <= 90f && Mathf.Abs(px - 64f) <= (90f - py) * 0.85f) inIcon = true;
                            if (py >= 40f && py <= 64f && Mathf.Abs(px - 64f) <= 10f) inIcon = true;
                        }
                        else if (dir == RhythmDirection.Down)
                        {
                            if (py >= 38f && py <= 64f && Mathf.Abs(px - 64f) <= (py - 38f) * 0.85f) inIcon = true;
                            if (py >= 64f && py <= 88f && Mathf.Abs(px - 64f) <= 10f) inIcon = true;
                        }

                        if (inIcon) iconHit += 1f;
                    }
                }

                if (iconHit > 0f)
                {
                    float alpha = iconHit / 4f;
                    c = Color.Lerp(c, Color.white, alpha);
                }

                cols[y * size + x] = c;
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static float DistToSegment(float px, float py, float x1, float y1, float x2, float y2)
    {
        float l2 = (x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1);
        if (l2 == 0f) return Vector2.Distance(new Vector2(px, py), new Vector2(x1, y1));
        float t = Mathf.Clamp01(((px - x1) * (x2 - x1) + (py - y1) * (y2 - y1)) / l2);
        Vector2 proj = new Vector2(x1 + t * (x2 - x1), y1 + t * (y2 - y1));
        return Vector2.Distance(new Vector2(px, py), proj);
    }

    private static Sprite CreateStoneSpaceSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];

        float cornerR = 24f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (x < 10 || x > 118 || y < 10 || y > 118)
                {
                    cols[y * size + x] = Color.clear;
                    continue;
                }

                float cx = x < 34 ? 34f : (x > 94 ? 94f : x);
                float cy = y < 34 ? 34f : (y > 94 ? 94f : y);
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

                if (dist > cornerR)
                {
                    cols[y * size + x] = Color.clear;
                    continue;
                }

                if (y < 22)
                {
                    cols[y * size + x] = new Color(0.60f, 0.54f, 0.44f, 1f);
                }
                else
                {
                    Color c = new Color(0.92f, 0.88f, 0.81f, 1f);
                    float elX = (x - 64f) / 42f;
                    float elY = (y - 86f) / 18f;
                    float elDist = elX * elX + elY * elY;
                    if (elDist <= 1.0f)
                    {
                        c = Color.Lerp(c, Color.white, (1.0f - elDist) * 0.35f);
                    }
                    cols[y * size + x] = c;
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(34f, 34f, 34f, 34f));
    }

    private static Sprite CreateCapsuleBarSprite()
    {
        int width = 128;
        int height = 64;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[width * height];

        float r = 26f;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float cx = x < 32 ? 32f : (x > width - 32 ? width - 32 : x);
                float cy = 32f;
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

                if (dist > r)
                {
                    cols[y * width + x] = Color.clear;
                    continue;
                }

                if (dist >= r - 4f)
                {
                    cols[y * width + x] = new Color(0.68f, 0.46f, 0.28f, 1f);
                }
                else
                {
                    float depth = (r - dist) / r;
                    cols[y * width + x] = Color.Lerp(new Color(0.82f, 0.78f, 0.70f, 1f), new Color(0.92f, 0.89f, 0.83f, 1f), depth);
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(32f, 26f, 32f, 26f));
    }

    private static Sprite CreateGlossyFillSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Color c = Color.white;
                if (y >= 32 && y <= 58)
                {
                    float norm = 1.0f - Mathf.Abs(y - 45f) / 13f;
                    c = Color.Lerp(new Color(0.92f, 0.92f, 0.92f, 1f), Color.white, norm);
                }
                else
                {
                    c = new Color(0.85f, 0.85f, 0.85f, 1f);
                }
                cols[y * size + x] = c;
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(16f, 16f, 16f, 16f));
    }

    private static Sprite CreatePearlOrbSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];
        float center = 64f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist <= 44f)
                {
                    float norm = dist / 44f;
                    float lightDist = Vector2.Distance(new Vector2(x, y), new Vector2(46f, 82f));
                    Color pearl = Color.Lerp(new Color(1f, 1f, 0.96f, 1f), new Color(0.92f, 0.84f, 0.65f, 1f), norm);

                    if (lightDist <= 12f)
                    {
                        float spec = 1.0f - (lightDist / 12f);
                        pearl = Color.Lerp(pearl, Color.white, spec * spec);
                    }
                    cols[y * size + x] = pearl;
                }
                else if (dist <= 58f)
                {
                    float glow = 1.0f - ((dist - 44f) / 14f);
                    cols[y * size + x] = new Color(1f, 0.95f, 0.65f, glow * glow * 0.70f);
                }
                else
                {
                    cols[y * size + x] = Color.clear;
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateLuminousHitTargetSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];
        float center = 64f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Abs(x - center);
                float py = Mathf.Abs(y - center);
                float dist = Mathf.Sqrt(px * px + py * py);

                Color c = Color.clear;

                // 1. Quầng hào quang vàng tỏa sáng mềm mại bên ngoài
                if (dist <= 58f)
                {
                    float glow = 1.0f - (dist / 58f);
                    c = new Color(1f, 0.88f, 0.35f, glow * glow * 0.45f);
                }

                // 2. Vành tròn vàng hổ phách định vị
                if (dist >= 30f && dist <= 38f)
                {
                    float ringNorm = 1.0f - Mathf.Abs(dist - 34f) / 4f;
                    Color ringCol = new Color(1f, 0.90f, 0.40f, ringNorm * 0.85f);
                    c = Color.Lerp(c, ringCol, ringCol.a);
                }

                // 3. Nhân ngọc phát sáng ở tâm
                if (dist <= 20f)
                {
                    float coreNorm = 1.0f - (dist / 20f);
                    Color coreCol = Color.Lerp(new Color(1f, 0.82f, 0.20f, 1f), Color.white, coreNorm * coreNorm);
                    c = Color.Lerp(c, coreCol, 0.90f);
                }

                // 4. Tia sáng 4 cánh (Lens Flare Cross & Star Curve)
                float starCurve = Mathf.Pow(px / 46f, 0.52f) + Mathf.Pow(py / 46f, 0.52f);
                if (starCurve <= 1.0f)
                {
                    float flareT = 1.0f - starCurve;
                    Color flareCol = Color.Lerp(new Color(1f, 0.92f, 0.50f, 1f), Color.white, flareT);
                    c = Color.Lerp(c, flareCol, Mathf.Clamp01(flareT * 1.5f));
                }

                cols[y * size + x] = c;
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateBigPillButtonSprite()
    {
        int width = 256;
        int height = 96;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[width * height];

        float r = 38f;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float cx = x < 48 ? 48f : (x > width - 48 ? width - 48 : x);
                float cy = 48f;
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

                if (dist > r)
                {
                    cols[y * width + x] = Color.clear;
                    continue;
                }

                Color c;
                if (y < 22)
                {
                    c = new Color(0.14f, 0.44f, 0.18f, 1f); // Bóng đổ đáy 3D
                }
                else
                {
                    c = new Color(0.28f, 0.76f, 0.26f, 1f); // Xanh bơ / chanh tươi tắn
                    // Vệt bóng gương cong ở nửa trên
                    if (y >= 50 && y <= 86)
                    {
                        float norm = 1.0f - Mathf.Abs(y - 68f) / 18f;
                        c = Color.Lerp(c, Color.white, norm * 0.42f);
                    }
                    if (y >= height - 14 && dist <= r - 2f)
                    {
                        c = Color.Lerp(c, Color.white, 0.35f);
                    }
                }
                cols[y * width + x] = c;
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(46f, 28f, 46f, 28f));
    }

    private static Sprite CreateGreenBadgeSprite()
    {
        int width = 128;
        int height = 64;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[width * height];

        float r = 26f;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float cx = x < 32 ? 32f : (x > width - 32 ? width - 32 : x);
                float cy = 32f;
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

                if (dist > r)
                {
                    cols[y * width + x] = Color.clear;
                    continue;
                }

                Color c;
                if (y < 16)
                {
                    c = new Color(0.22f, 0.54f, 0.16f, 1f); // Bóng đổ 3D đáy
                }
                else
                {
                    c = new Color(0.33f, 0.68f, 0.20f, 1f); // Xanh lá táo tươi tắn
                    if (y >= height - 12 && dist <= r - 1f)
                    {
                        c = Color.Lerp(c, Color.white, 0.35f); // Viền sáng trên đỉnh
                    }
                }

                cols[y * width + x] = c;
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(32f, 26f, 32f, 26f));
    }

    private static Sprite CreateRedCloseButtonSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];
        float center = 64f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist > 52f)
                {
                    cols[y * size + x] = Color.clear;
                    continue;
                }

                Color c;
                if (dist >= 46f)
                {
                    c = new Color(1f, 0.92f, 0.88f, 1f); // Viền kem bọc ngoài
                }
                else if (y < 24)
                {
                    c = new Color(0.60f, 0.12f, 0.12f, 1f); // Bóng đổ đỏ trầm đáy 3D
                }
                else
                {
                    c = new Color(0.92f, 0.26f, 0.24f, 1f); // Đỏ kẹo ngọt
                    float elDist = Vector2.Distance(new Vector2(x, y), new Vector2(center, 78f));
                    if (elDist <= 24f)
                    {
                        c = Color.Lerp(c, Color.white, (1.0f - elDist / 24f) * 0.40f);
                    }
                }

                // Dấu X trắng dày dặn sắc nét
                float px = x - center;
                float py = y - (center + 2f);
                float d1 = Mathf.Abs(px - py);
                float d2 = Mathf.Abs(px + py);
                if ((d1 <= 6f || d2 <= 6f) && Mathf.Abs(px) <= 18f && Mathf.Abs(py) <= 18f)
                {
                    c = Color.white;
                }

                cols[y * size + x] = c;
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateCornerFloraSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];
        Array.Clear(cols, 0, cols.Length);

        // Nhánh dây leo ôm cong góc bảng
        DrawPointedLeaf(cols, size, size, new Vector2(36f, 94f), -35f, 28f, 16f, new Color(0.20f, 0.50f, 0.16f, 1f), new Color(0.38f, 0.76f, 0.26f, 1f));
        DrawPointedLeaf(cols, size, size, new Vector2(72f, 68f), -65f, 26f, 15f, new Color(0.22f, 0.54f, 0.18f, 1f), new Color(0.42f, 0.80f, 0.30f, 1f));
        DrawSmallBlossom(cols, size, size, new Vector2(28f, 102f), 12f);

        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateRingSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];
        float center = 64f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist >= 42f && dist <= 56f)
                {
                    float t = 1.0f - Mathf.Abs(dist - 49f) / 7f;
                    cols[y * size + x] = new Color(1f, 1f, 1f, t * t);
                }
                else
                {
                    cols[y * size + x] = Color.clear;
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static void DrawPointedLeaf(Color[] cols, int texW, int texH, Vector2 basePos, float angleDeg, float length, float width, Color baseCol, Color tipCol)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        float r = length + width;
        int minX = Mathf.Clamp(Mathf.FloorToInt(basePos.x - r), 0, texW - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(basePos.x + r), 0, texW - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(basePos.y - r), 0, texH - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(basePos.y + r), 0, texH - 1);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x - basePos.x;
                float dy = y - basePos.y;

                float forward = dx * cos + dy * sin;
                float lateral = -dx * sin + dy * cos;

                if (forward >= 0f && forward <= length)
                {
                    float t = forward / length;
                    float halfWidth = (width * 0.5f) * Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.65f));
                    float latAbs = Mathf.Abs(lateral);

                    if (latAbs <= halfWidth)
                    {
                        float edgeDist = halfWidth - latAbs;
                        float aa = Mathf.Clamp01(edgeDist / 1.0f);

                        Color col = Color.Lerp(baseCol, tipCol, t * 0.5f);

                        if (latAbs <= 1.0f)
                        {
                            col = Color.Lerp(col, new Color(0.72f, 0.94f, 0.48f, 1f), (1.0f - latAbs) * 0.65f);
                        }
                        else if (lateral > 0f)
                        {
                            col = Color.Lerp(col, Color.white, 0.15f * (1.0f - latAbs / halfWidth));
                        }
                        else
                        {
                            col = Color.Lerp(col, new Color(0.12f, 0.32f, 0.10f, 1f), 0.25f);
                        }

                        int idx = y * texW + x;
                        cols[idx] = Color.Lerp(cols[idx], col, aa * col.a);
                    }
                }
            }
        }
    }

    private static void DrawSmallBlossom(Color[] cols, int texW, int texH, Vector2 pos, float radius)
    {
        int minX = Mathf.Clamp(Mathf.FloorToInt(pos.x - radius), 0, texW - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(pos.x + radius), 0, texW - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(pos.y - radius), 0, texH - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(pos.y + radius), 0, texH - 1);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x - pos.x;
                float dy = y - pos.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                float petalR = radius * (0.65f + 0.35f * Mathf.Cos(5f * angle));
                if (dist <= petalR)
                {
                    Color col = dist <= radius * 0.35f
                        ? new Color(0.98f, 0.54f, 0.12f, 1f)
                        : new Color(1f, 0.90f, 0.35f, 1f);
                    int idx = y * texW + x;
                    cols[idx] = Color.Lerp(cols[idx], col, col.a);
                }
            }
        }
    }

    private static void DrawBerry(Color[] cols, int texW, int texH, Vector2 pos, float radius, Color color)
    {
        int minX = Mathf.Clamp(Mathf.FloorToInt(pos.x - radius), 0, texW - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(pos.x + radius), 0, texW - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(pos.y - radius), 0, texH - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(pos.y + radius), 0, texH - 1);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), pos);
                if (dist <= radius)
                {
                    float t = dist / radius;
                    float specDist = Vector2.Distance(new Vector2(x, y), pos + new Vector2(-radius * 0.3f, radius * 0.3f));
                    Color c = Color.Lerp(color, color * 0.70f, t);
                    if (specDist <= radius * 0.4f)
                    {
                        c = Color.Lerp(c, Color.white, (1.0f - specDist / (radius * 0.4f)) * 0.75f);
                    }
                    int idx = y * texW + x;
                    cols[idx] = Color.Lerp(cols[idx], c, c.a);
                }
            }
        }
    }

    private static Sprite CreateIvyGarlandSprite()
    {
        int width = 512;
        int height = 64;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[width * height];
        Array.Clear(cols, 0, cols.Length);

        // 1. Thân dây leo uốn lượn tự nhiên
        for (int x = 0; x < width; x++)
        {
            float normX = (float)x / width;
            float stemY = 40f + 6f * Mathf.Sin(normX * 18f) + 4f * Mathf.Cos(normX * 32f);
            int minY = Mathf.Clamp(Mathf.FloorToInt(stemY - 3f), 0, height - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(stemY + 3f), 0, height - 1);
            for (int y = minY; y <= maxY; y++)
            {
                float dy = Mathf.Abs(y - stemY);
                if (dy <= 2.8f)
                {
                    float aa = Mathf.Clamp01((2.8f - dy) / 1.0f);
                    Color stemCol = new Color(0.18f, 0.36f, 0.12f, 1f);
                    int idx = y * width + x;
                    cols[idx] = Color.Lerp(cols[idx], stemCol, aa);
                }
            }
        }

        // 2. Các nhánh lá hình quả hạnh có chóp nhọn (Botanical Almond Leaves)
        int leafCount = 20;
        for (int i = 0; i < leafCount; i++)
        {
            float norm = (i + 0.5f) / leafCount;
            float lx = norm * (width - 40f) + 20f + 4f * Mathf.Sin(i * 1.7f);
            float ly = 40f + 6f * Mathf.Sin((lx / width) * 18f) + 4f * Mathf.Cos((lx / width) * 32f);

            bool pointDown = (i % 2 == 0);
            float angle = pointDown ? (-40f + 10f * Mathf.Sin(i * 2.3f)) : (35f + 10f * Mathf.Cos(i * 2.3f));
            float len = pointDown ? 22f : 18f;
            float w = pointDown ? 13f : 11f;

            DrawPointedLeaf(cols, width, height, new Vector2(lx, ly), angle, len, w,
                new Color(0.24f, 0.54f, 0.18f, 1f),
                new Color(0.42f, 0.82f, 0.28f, 1f));
        }

        // 3. Điểm các bông hoa vàng nhỏ nở dọc theo dây leo
        DrawSmallBlossom(cols, width, height, new Vector2(90f, 44f), 9f);
        DrawSmallBlossom(cols, width, height, new Vector2(256f, 46f), 10f);
        DrawSmallBlossom(cols, width, height, new Vector2(420f, 42f), 9f);

        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateSideIvySprite(bool isRight)
    {
        int width = 64;
        int height = 480;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[width * height];
        Array.Clear(cols, 0, cols.Length);

        // 1. Thân dây leo uốn lượn dọc
        for (int y = 0; y < height; y++)
        {
            float normY = (float)y / height;
            float stemX = 32f + 5f * Mathf.Sin(normY * 16f) + 3f * Mathf.Cos(normY * 26f);
            int minX = Mathf.Clamp(Mathf.FloorToInt(stemX - 3f), 0, width - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(stemX + 3f), 0, width - 1);
            for (int x = minX; x <= maxX; x++)
            {
                float dx = Mathf.Abs(x - stemX);
                if (dx <= 2.6f)
                {
                    float aa = Mathf.Clamp01((2.6f - dx) / 1.0f);
                    Color stemCol = new Color(0.18f, 0.36f, 0.12f, 1f);
                    int idx = y * width + x;
                    cols[idx] = Color.Lerp(cols[idx], stemCol, aa);
                }
            }
        }

        // 2. Lá cây mọc so le hai bên
        int leafCount = 18;
        for (int i = 0; i < leafCount; i++)
        {
            float norm = (i + 0.5f) / leafCount;
            float ly = norm * (height - 40f) + 20f;
            float lx = 32f + 5f * Mathf.Sin((ly / height) * 16f) + 3f * Mathf.Cos((ly / height) * 26f);

            bool pointIn = (i % 2 == 0);
            float angle = pointIn ? (130f + 15f * Mathf.Sin(i * 1.5f)) : (50f + 15f * Mathf.Cos(i * 1.5f));
            float len = 19f;
            float w = 11f;

            DrawPointedLeaf(cols, width, height, new Vector2(lx, ly), angle, len, w,
                new Color(0.24f, 0.54f, 0.18f, 1f),
                new Color(0.42f, 0.82f, 0.28f, 1f));
        }

        // 3. Nụ hoa / quả nhỏ
        DrawSmallBlossom(cols, width, height, new Vector2(30f, 140f), 8f);
        DrawSmallBlossom(cols, width, height, new Vector2(34f, 320f), 8f);

        // Lật ảnh nếu là bên phải
        if (isRight)
        {
            Color[] flipped = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    flipped[y * width + x] = cols[y * width + (width - 1 - x)];
                }
            }
            cols = flipped;
        }

        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateBarVineWingSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];
        Array.Clear(cols, 0, cols.Length);

        // Cụm lá ôm đuôi thanh tiến trình
        DrawPointedLeaf(cols, size, size, new Vector2(26f, 32f), 30f, 22f, 12f, new Color(0.22f, 0.56f, 0.24f, 1f), new Color(0.42f, 0.80f, 0.32f, 1f));
        DrawPointedLeaf(cols, size, size, new Vector2(26f, 32f), 110f, 18f, 10f, new Color(0.20f, 0.50f, 0.20f, 1f), new Color(0.38f, 0.74f, 0.28f, 1f));
        DrawSmallBlossom(cols, size, size, new Vector2(22f, 30f), 6f);

        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateFloraBouquetSprite(bool rightSide)
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];
        Array.Clear(cols, 0, cols.Length);

        // 1. Chùm lá xanh tỏa đều
        DrawPointedLeaf(cols, size, size, new Vector2(50f, 48f), 45f, 32f, 18f, new Color(0.20f, 0.50f, 0.16f, 1f), new Color(0.38f, 0.76f, 0.26f, 1f));
        DrawPointedLeaf(cols, size, size, new Vector2(50f, 48f), 115f, 34f, 19f, new Color(0.22f, 0.54f, 0.18f, 1f), new Color(0.42f, 0.80f, 0.30f, 1f));
        DrawPointedLeaf(cols, size, size, new Vector2(50f, 48f), 165f, 30f, 17f, new Color(0.18f, 0.46f, 0.14f, 1f), new Color(0.35f, 0.72f, 0.24f, 1f));
        DrawPointedLeaf(cols, size, size, new Vector2(50f, 48f), -20f, 28f, 16f, new Color(0.24f, 0.56f, 0.19f, 1f), new Color(0.44f, 0.84f, 0.32f, 1f));

        if (!rightSide)
        {
            // Bên trái: Hoa vàng 5 cánh nở xòe rực rỡ với nhụy cam tươi
            DrawSmallBlossom(cols, size, size, new Vector2(54f, 50f), 20f);
            DrawSmallBlossom(cols, size, size, new Vector2(86f, 36f), 11f);
        }
        else
        {
            // Bên phải: Chùm quả mọng ngọt ngào (Đỏ, Tím, Lam, Cam)
            DrawBerry(cols, size, size, new Vector2(54f, 50f), 11f, new Color(0.92f, 0.26f, 0.26f, 1f)); // Đỏ
            DrawBerry(cols, size, size, new Vector2(40f, 66f), 9.5f, new Color(0.68f, 0.32f, 0.88f, 1f)); // Tím
            DrawBerry(cols, size, size, new Vector2(72f, 42f), 9f, new Color(0.16f, 0.68f, 0.88f, 1f)); // Lam ngọc
            DrawBerry(cols, size, size, new Vector2(34f, 42f), 8f, new Color(0.98f, 0.62f, 0.16f, 1f)); // Cam hổ phách
        }

        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateGoldBeadSprite()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];
        float center = 16f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist <= 12f)
                {
                    float norm = dist / 12f;
                    Color gold = Color.Lerp(new Color(1f, 0.90f, 0.45f, 1f), new Color(0.85f, 0.55f, 0.12f, 1f), norm);
                    float specDist = Vector2.Distance(new Vector2(x, y), new Vector2(13f, 20f));
                    if (specDist <= 4f)
                    {
                        gold = Color.Lerp(gold, Color.white, 1.0f - specDist / 4f);
                    }
                    cols[y * size + x] = gold;
                }
                else
                {
                    cols[y * size + x] = Color.clear;
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateClockIconSprite()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];
        float center = 16f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist <= 13f)
                {
                    if (dist >= 10.5f)
                    {
                        cols[y * size + x] = Color.white;
                    }
                    else
                    {
                        Color c = new Color(1f, 1f, 1f, 0.15f);

                        float px = x - center;
                        float py = y - center;
                        bool minHand = px >= 0f && px <= 6.5f && Mathf.Abs(py - px) <= 1.2f;
                        bool hourHand = px <= 0f && px >= -5f && Mathf.Abs(py - (-px * 1.4f)) <= 1.2f;
                        bool centerPin = dist <= 2.2f;

                        if (minHand || hourHand || centerPin)
                        {
                            c = Color.white;
                        }
                        cols[y * size + x] = c;
                    }
                }
                else
                {
                    cols[y * size + x] = Color.clear;
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // ==========================================
    // TIỆN ÍCH HOẠT HỌA & MÀU SẮC
    // ==========================================

    private IEnumerator AnimateModalIntroRoutine(RectTransform card, Image dim)
    {
        if (card == null) yield break;

        card.localScale = new Vector3(0.78f, 0.78f, 1f);
        if (dim != null)
        {
            Color c = dim.color;
            c.a = 0f;
            dim.color = c;
        }

        float dur = 0.32f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;

            float scale;
            if (t < 0.65f)
            {
                float subT = t / 0.65f;
                scale = Mathf.Lerp(0.78f, 1.035f, Mathf.Sin(subT * Mathf.PI * 0.5f));
            }
            else
            {
                float subT = (t - 0.65f) / 0.35f;
                scale = Mathf.Lerp(1.035f, 1.0f, Mathf.Sin(subT * Mathf.PI * 0.5f));
            }
            card.localScale = new Vector3(scale, scale, 1f);

            if (dim != null)
            {
                Color c = dim.color;
                c.a = Mathf.Lerp(0f, 0.52f, Mathf.Clamp01(t * 1.5f));
                dim.color = c;
            }

            yield return null;
        }

        card.localScale = Vector3.one;
        if (dim != null)
        {
            dim.color = new Color(0f, 0f, 0f, 0.52f);
        }
    }

    private void SpawnHitSparkles(Vector3 worldPos, int count)
    {
        if (panelRect == null) return;
        StartCoroutine(HitSparklesRoutine(worldPos, count));
    }

    private IEnumerator HitSparklesRoutine(Vector3 worldPos, int count)
    {
        List<ParticleData> sparks = new List<ParticleData>();
        for (int i = 0; i < count; i++)
        {
            GameObject spObj = new GameObject("HitSpark");
            spObj.transform.SetParent(panelRect, false);
            spObj.transform.position = worldPos;
            RectTransform spRect = spObj.AddComponent<RectTransform>();
            float s = UnityEngine.Random.Range(16f, 26f);
            spRect.sizeDelta = new Vector2(s, s);

            Image img = spObj.AddComponent<Image>();
            img.sprite = sparkleStarSpriteCache;
            img.color = new Color(1f, 0.94f, 0.35f, 1f);
            img.raycastTarget = false;

            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = UnityEngine.Random.Range(120f, 260f);
            sparks.Add(new ParticleData
            {
                Obj = spObj,
                Rect = spRect,
                Img = img,
                Velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed),
                RotationSpeed = UnityEngine.Random.Range(-180f, 180f),
                Gravity = 100f
            });
        }

        float dur = 0.32f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            for (int i = 0; i < sparks.Count; i++)
            {
                var sp = sparks[i];
                if (sp.Obj == null) continue;
                sp.Velocity.y -= sp.Gravity * Time.deltaTime;
                sp.Rect.anchoredPosition += sp.Velocity * Time.deltaTime;
                sp.Rect.localEulerAngles = new Vector3(0f, 0f, sp.Rect.localEulerAngles.z + sp.RotationSpeed * Time.deltaTime);
                if (sp.Img != null)
                {
                    Color c = sp.Img.color;
                    c.a = Mathf.Clamp01(1.0f - (elapsed / dur));
                    sp.Img.color = c;
                }
            }
            yield return null;
        }

        for (int i = 0; i < sparks.Count; i++)
        {
            if (sparks[i].Obj != null) Destroy(sparks[i].Obj);
        }
    }

    private IEnumerator ShowFeedbackRoutine(string text, Color color)
    {
        if (feedbackGradeText == null) yield break;

        feedbackGradeText.text = text;
        feedbackGradeText.color = color;
        RectTransform rt = feedbackGradeText.rectTransform;
        Vector2 origPos = new Vector2(0f, -126f);
        rt.anchoredPosition = origPos;
        rt.localScale = Vector3.one * 0.60f;

        float dur = 0.75f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;

            // Pop scale 0.6 -> 1.30 -> 1.0
            float s;
            if (t < 0.15f)
            {
                s = Mathf.Lerp(0.60f, 1.30f, t / 0.15f);
            }
            else if (t < 0.35f)
            {
                s = Mathf.Lerp(1.30f, 1.0f, (t - 0.15f) / 0.20f);
            }
            else
            {
                s = 1.0f;
            }
            rt.localScale = Vector3.one * s;

            // Trôi nhẹ lên trên
            rt.anchoredPosition = new Vector2(origPos.x, origPos.y + Mathf.Lerp(0f, 16f, t));

            // Mờ dần ở 40% thời gian cuối
            if (t >= 0.60f)
            {
                float alpha = 1.0f - ((t - 0.60f) / 0.40f);
                Color c = color;
                c.a = alpha;
                feedbackGradeText.color = c;
            }
            yield return null;
        }

        feedbackGradeText.text = "";
        rt.anchoredPosition = origPos;
        rt.localScale = Vector3.one;
    }

    private IEnumerator PunchScaleRoutine(Transform target, float maxScale, float duration)
    {
        if (target == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float s = t < 0.5f ? Mathf.Lerp(1.0f, maxScale, t * 2f) : Mathf.Lerp(maxScale, 1.0f, (t - 0.5f) * 2f);
            target.localScale = Vector3.one * s;
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    private IEnumerator ShakeSequenceRoutine()
    {
        if (noteContainer == null) yield break;

        Vector2 origPos = noteContainer.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < 0.22f)
        {
            elapsed += Time.deltaTime;
            float xOffset = Mathf.Sin(elapsed * 55f) * 8f;
            noteContainer.anchoredPosition = new Vector2(origPos.x + xOffset, origPos.y);
            yield return null;
        }
        noteContainer.anchoredPosition = origPos;

        // Khôi phục sprite kẹo thạch cho các nốt chưa bấm
        for (int i = 0; i < activeSlotViews.Count; i++)
        {
            var slot = activeSlotViews[i];
            if (i >= engine.CurrentSequenceIndex && slot.ArrowImg != null)
            {
                slot.ArrowImg.sprite = GetJellySprite(slot.Direction);
                slot.ArrowImg.color = Color.white;
            }
            if (slot.RootObj != null) slot.RootObj.transform.localScale = Vector3.one;
        }
    }

    // ==========================================
    // ÂM THANH PROCEDURAL (PROCEDURAL AUDIO SYNTHESIS)
    // ==========================================

    private void EnsureAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D Stereo UI Sound
        }

        if (noteLeftClip == null)
        {
            noteLeftClip = CreateToneClip("Note_C4", 261.63f, 0.14f, 0.35f, ToneType.Pluck);
            noteUpClip = CreateToneClip("Note_E4", 329.63f, 0.14f, 0.35f, ToneType.Pluck);
            noteDownClip = CreateToneClip("Note_G4", 392.00f, 0.14f, 0.35f, ToneType.Pluck);
            noteRightClip = CreateToneClip("Note_B4", 493.88f, 0.14f, 0.35f, ToneType.Pluck);
            beatPerfectClip = CreateToneClip("Beat_Perfect", 523.25f, 0.28f, 0.50f, ToneType.Chime);
            beatGreatClip = CreateToneClip("Beat_Great", 440.00f, 0.22f, 0.45f, ToneType.Chime);
            beatMissClip = CreateToneClip("Beat_Miss", 180.00f, 0.25f, 0.30f, ToneType.Thud);
            wrongKeyClip = CreateToneClip("Wrong_Key", 140.00f, 0.18f, 0.35f, ToneType.Thud);
            victoryFanfareClip = CreateVictoryFanfareClip();
        }
    }

    private static AudioClip CreateVictoryFanfareClip()
    {
        int sampleRate = 44100;
        float duration = 2.4f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        // Chuỗi arpeggio Đô Trưởng: C5, E5, G5, B5, C6 (chuông gió tinh linh)
        float[] notes = new float[] { 523.25f, 659.25f, 783.99f, 987.77f, 1046.50f };
        float[] startTimes = new float[] { 0.0f, 0.12f, 0.24f, 0.36f, 0.50f };

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float totalSample = 0f;

            // 1. Các nốt chuông arpeggio
            for (int n = 0; n < notes.Length; n++)
            {
                if (t >= startTimes[n])
                {
                    float noteTime = t - startTimes[n];
                    float noteDur = n == notes.Length - 1 ? 1.8f : 0.65f;
                    if (noteTime < noteDur)
                    {
                        float env = Mathf.Exp(-4.0f * (noteTime / noteDur));
                        float freq = notes[n];
                        // Âm chuông sin + hài âm lấp lánh
                        float s = Mathf.Sin(2f * Mathf.PI * freq * noteTime) * 0.45f;
                        s += Mathf.Sin(2f * Mathf.PI * freq * 2f * noteTime) * 0.22f;
                        s += Mathf.Sin(2f * Mathf.PI * freq * 3f * noteTime) * 0.12f;
                        totalSample += s * env;
                    }
                }
            }

            // 2. Hợp âm đệm ngân vang (C Major warm chord) từ t = 0.5s đến 2.4s
            if (t >= 0.50f)
            {
                float chordTime = t - 0.50f;
                float chordDur = 1.9f;
                float chordEnv = Mathf.Exp(-2.2f * (chordTime / chordDur));
                float chord = (Mathf.Sin(2f * Mathf.PI * 261.63f * chordTime) * 0.20f
                             + Mathf.Sin(2f * Mathf.PI * 329.63f * chordTime) * 0.18f
                             + Mathf.Sin(2f * Mathf.PI * 392.00f * chordTime) * 0.18f
                             + Mathf.Sin(2f * Mathf.PI * 523.25f * chordTime) * 0.15f);
                totalSample += chord * chordEnv;
            }

            samples[i] = Mathf.Clamp(totalSample * 0.55f, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("Victory_Fanfare", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void PlayDirectionSound(RhythmDirection dir)
    {
        EnsureAudioSource();
        AudioClip clip = dir switch
        {
            RhythmDirection.Left => noteLeftClip,
            RhythmDirection.Up => noteUpClip,
            RhythmDirection.Down => noteDownClip,
            RhythmDirection.Right => noteRightClip,
            _ => noteLeftClip
        };
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    private void PlayBeatSound(RhythmHitGrade grade)
    {
        EnsureAudioSource();
        AudioClip clip = grade switch
        {
            RhythmHitGrade.Perfect => beatPerfectClip,
            RhythmHitGrade.Great => beatGreatClip,
            RhythmHitGrade.Good => beatGreatClip,
            _ => beatMissClip
        };
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    private void PlayWrongSound()
    {
        EnsureAudioSource();
        if (audioSource != null && wrongKeyClip != null) audioSource.PlayOneShot(wrongKeyClip);
    }

    private enum ToneType { Pluck, Chime, Thud }

    private static AudioClip CreateToneClip(string name, float freq, float duration, float volume, ToneType type)
    {
        int sampleRate = 44100;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float progress = (float)i / sampleCount;

            // Envelope (ADSR)
            float env = 1f;
            if (type == ToneType.Pluck)
            {
                // Tiếng phím gõ đàn kalimba ngân vang êm tai
                env = Mathf.Exp(-6.5f * progress);
            }
            else if (type == ToneType.Chime)
            {
                // Chuông gió tinh linh ngân vang lấp lánh khi trúng nhịp Space
                env = Mathf.Exp(-3.2f * progress);
            }
            else // Thud
            {
                // Tiếng gõ gỗ trầm báo sai nhẹ nhàng không chát chúa
                env = Mathf.Exp(-12.0f * progress);
            }

            // Waveform
            float sin = Mathf.Sin(2f * Mathf.PI * freq * t);
            float harmonic = Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.35f;
            if (type == ToneType.Chime)
            {
                harmonic += Mathf.Sin(2f * Mathf.PI * freq * 3f * t) * 0.20f;
            }

            samples[i] = (sin + harmonic) * env * volume;
        }

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static Color GetDirectionColor(RhythmDirection dir) => dir switch
    {
        RhythmDirection.Left => new Color(0.20f, 0.50f, 0.80f, 1f),  // Lam Ngọc Sapphire
        RhythmDirection.Up => new Color(0.22f, 0.62f, 0.38f, 1f),    // Lục Bảo Emerald
        RhythmDirection.Down => new Color(0.85f, 0.52f, 0.15f, 1f),  // Hổ Phách Amber
        RhythmDirection.Right => new Color(0.82f, 0.28f, 0.30f, 1f), // Hồng San Hô Ruby
        _ => new Color(0.4f, 0.4f, 0.4f, 1f)
    };

    private static float GetDirectionRotationAngle(RhythmDirection dir) => dir switch
    {
        RhythmDirection.Up => 0f,
        RhythmDirection.Right => -90f,
        RhythmDirection.Down => 180f,
        RhythmDirection.Left => 90f,
        _ => 0f
    };

    private static string GetDirectionKeyHint(RhythmDirection dir) => dir switch
    {
        RhythmDirection.Left => "[ A ]",
        RhythmDirection.Up => "[ W ]",
        RhythmDirection.Down => "[ S ]",
        RhythmDirection.Right => "[ D ]",
        _ => ""
    };

    private static string GetRankText(AnimalRank rank) => rank switch
    {
        AnimalRank.Common => "Cấp 1 - Phổ thông",
        AnimalRank.Uncommon => "Cấp 2 - Hảo hạng",
        AnimalRank.Rare => "Cấp 3 - Quý hiếm",
        AnimalRank.Epic => "Cấp 4 - Sử thi",
        AnimalRank.Legendary => "Cấp 5 - Huyền thoại",
        _ => $"Cấp {(int)rank}"
    };
}
