using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Giao diện UI và điều khiển trực quan cho Minigame 2 - Vũ Điệu Hòa Nhịp:
/// 1. Tích hợp Engine nhịp điệu (RhythmMinigameEngine) với phản hồi thời gian thực.
/// 2. Điều khiển chuyển động viên ngọc (Pearl Orb) và vòng định nhịp (Approach Ring).
/// 3. Phản hồi xúc giác/hoạt họa (Punch scale, shake sequence, combo counter, celebration particles).
/// 4. Quản lý luồng phiên chơi (StartSession, CancelSession, FinishSession).
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
    private Image dimOverlayImg;

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
    private LumiWorld.Acs.ResourceInventoryUISkin skin;
    private TMP_FontAsset fontAsset;

    private float displayHarmonyProgress = 0f;
    private float targetHarmonyProgress = 0f;

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

        RhythmProceduralSprites.EnsureLoaded();
        audioSource = RhythmProceduralAudio.EnsureAudioSource(gameObject, audioSource);

        var bindings = RhythmMinigameUIBuilder.BuildInterface(
            this.transform,
            currentContext,
            fontAsset,
            CancelSession,
            OnBeatButtonClicked);

        BindUIElements(bindings);

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

    private void BindUIElements(RhythmUIBindings bindings)
    {
        panelRect = bindings.PanelRect;
        dimOverlayImg = bindings.DimOverlayImg;
        harmonyFillImg = bindings.HarmonyFillImg;
        harmonyFillRect = bindings.HarmonyFillRect;
        harmonyText = bindings.HarmonyText;
        noteContainer = bindings.NoteContainer;
        railRect = bindings.RailRect;
        orbRect = bindings.OrbRect;
        orbImg = bindings.OrbImg;
        hitZoneRect = bindings.HitZoneRect;
        hitZoneImg = bindings.HitZoneImg;
        approachRingRect = bindings.ApproachRingRect;
        approachRingImg = bindings.ApproachRingImg;
        comboText = bindings.ComboText;
        feedbackGradeText = bindings.FeedbackGradeText;
        timerText = bindings.TimerText;
        beatButtonImg = bindings.BeatButtonImg;
        beatButtonText = bindings.BeatButtonText;
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

        // 4. Cập nhật đồng hồ đếm ngược định dạng chuẩn
        if (timerText != null)
        {
            timerText.text = $"<b>{engine.TimeRemaining:F1}s</b>".Replace('.', ',');
        }

        // 5. Cập nhật hiệu ứng nốt hiện tại đang chờ bấm (Pulsing highlight)
        UpdateActiveNoteHighlight();

        // 6. Smooth Harmony Progress
        UpdateHarmonySmoothFill();
    }

    private void OnBeatButtonClicked()
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
            OnBeatButtonClicked();
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

        // Đốm sao hồng tâm thở nhẹ nhàng
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
        int currentIndex = engine.CurrentSequenceIndex;
        for (int i = 0; i < activeSlotViews.Count; i++)
        {
            var slot = activeSlotViews[i];
            if (slot == null || slot.Outline == null) continue;

            if (!engine.IsSequenceCompleted && i == currentIndex)
            {
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

        // Trạng thái Ô SPACE & Nút Chạm Nhịp
        if (spaceSlotView != null && spaceSlotView.Background != null)
        {
            if (engine.IsSequenceCompleted && engine.Phase == RhythmPhase.OrbGliding)
            {
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

    private void HandlePhaseChanged(RhythmPhase phase)
    {
    }

    private void HandlePromptHint(string hint)
    {
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
            var slotView = RhythmMinigameUIBuilder.CreateNoteSlot(noteContainer, dir, i, posX, noteWidth);
            activeSlotViews.Add(slotView);
            currentX += noteWidth + spacing;
        }

        // 2. Tạo ô phím SPACE ngay cuối hàng
        float spacePosX = currentX + (spaceWidth * 0.5f);
        spaceSlotView = RhythmMinigameUIBuilder.CreateSpaceSlot(noteContainer, spacePosX, spaceWidth, fontAsset);
    }

    private void HandleNoteValidated(int index, RhythmDirection dir, bool isCorrect)
    {
        if (isCorrect)
        {
            RhythmProceduralAudio.PlayDirectionSound(audioSource, dir);

            if (index >= 0 && index < activeSlotViews.Count)
            {
                var slot = activeSlotViews[index];
                if (slot.ArrowImg != null)
                {
                    slot.ArrowImg.sprite = RhythmProceduralSprites.JellyCorrect;
                    slot.ArrowImg.color = Color.white;
                }

                StartCoroutine(PunchScaleRoutine(slot.RootObj.transform, 1.25f, 0.12f));
                SpawnHitSparkles(slot.RootObj.transform.position, 3);
            }
        }
        else
        {
            RhythmProceduralAudio.PlayWrongSound(audioSource);
            comboCount = 0;
            if (comboText != null) comboText.text = "";

            StartCoroutine(ShakeSequenceRoutine());
        }
    }

    private void HandleGradeEvaluated(RhythmHitGrade grade, float delta, string feedbackText)
    {
        RhythmProceduralAudio.PlayBeatSound(audioSource, grade);

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
                color = new Color(0.98f, 0.78f, 0.15f, 1f);
                if (spaceSlotView != null && spaceSlotView.Background != null)
                    spaceSlotView.Background.color = new Color(0.98f, 0.78f, 0.15f, 1f);
                break;
            case RhythmHitGrade.Great:
                color = new Color(0.20f, 0.85f, 0.48f, 1f);
                if (spaceSlotView != null && spaceSlotView.Background != null)
                    spaceSlotView.Background.color = new Color(0.20f, 0.85f, 0.48f, 1f);
                break;
            case RhythmHitGrade.Good:
                color = new Color(0.28f, 0.70f, 0.98f, 1f);
                if (spaceSlotView != null && spaceSlotView.Background != null)
                    spaceSlotView.Background.color = new Color(0.28f, 0.70f, 0.98f, 1f);
                break;
            case RhythmHitGrade.Miss:
                color = new Color(0.88f, 0.28f, 0.28f, 1f);
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
        if (harmonyFillImg == null) return;

        displayHarmonyProgress = Mathf.MoveTowards(
            displayHarmonyProgress,
            targetHarmonyProgress,
            Time.deltaTime * 0.85f);

        harmonyFillImg.fillAmount = displayHarmonyProgress;

        if (harmonyText != null)
        {
            int percent = Mathf.RoundToInt(displayHarmonyProgress * 100f);
            if (displayHarmonyProgress >= 0.99f)
            {
                harmonyText.text = "TIẾN TRÌNH THU PHỤC: <b>HOÀN TẤT!</b>";
            }
            else
            {
                harmonyText.text = $"TIẾN TRÌNH THU PHỤC: <b>{percent}%</b>";
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
        RhythmProceduralAudio.PlayVictoryFanfare(audioSource);

        if (harmonyFillImg != null)
        {
            harmonyFillImg.color = new Color(1f, 0.88f, 0.25f, 1f);
        }
        if (harmonyText != null)
        {
            harmonyText.text = "THU PHỤC HOÀN TẤT (100%)";
            harmonyText.color = new Color(0.48f, 0.22f, 0.05f, 1f);
        }

        // Màn hình Golden Euphoria Flash
        GameObject flashObj = new GameObject("VictoryFlash");
        flashObj.transform.SetParent(this.transform, false);
        RectTransform flashRect = flashObj.AddComponent<RectTransform>();
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.sizeDelta = Vector2.zero;
        Image flashImg = flashObj.AddComponent<Image>();
        flashImg.sprite = RhythmProceduralSprites.White;
        flashImg.color = new Color(1f, 0.94f, 0.55f, 0.55f);
        flashImg.raycastTarget = false;

        // Victory Crest
        GameObject crestObj = new GameObject("VictoryCrest");
        crestObj.transform.SetParent(panelRect, false);
        RectTransform crestRect = crestObj.AddComponent<RectTransform>();
        crestRect.sizeDelta = new Vector2(490f, 160f);
        crestRect.anchoredPosition = new Vector2(0f, 22f);
        crestRect.localScale = Vector3.zero;

        // Sunburst Rays
        GameObject sunburstObj = new GameObject("SunburstRays");
        sunburstObj.transform.SetParent(crestObj.transform, false);
        RectTransform sbRect = sunburstObj.AddComponent<RectTransform>();
        sbRect.sizeDelta = new Vector2(340f, 340f);
        sbRect.anchoredPosition = new Vector2(0f, 25f);
        Image sbImg = sunburstObj.AddComponent<Image>();
        sbImg.sprite = RhythmProceduralSprites.Sunburst;
        sbImg.color = new Color(1f, 0.90f, 0.40f, 0.45f);
        sbImg.raycastTarget = false;

        // Plaque
        GameObject plaqueObj = new GameObject("Plaque");
        plaqueObj.transform.SetParent(crestObj.transform, false);
        RectTransform plqRect = plaqueObj.AddComponent<RectTransform>();
        plqRect.sizeDelta = new Vector2(480f, 150f);
        plqRect.anchoredPosition = new Vector2(0f, 0f);
        Image plqImg = plaqueObj.AddComponent<Image>();
        plqImg.sprite = RhythmProceduralSprites.WoodBoard;
        plqImg.type = Image.Type.Sliced;
        plqImg.color = new Color(1f, 0.98f, 0.94f, 1f);

        // Heart Icon
        GameObject badgeIconObj = new GameObject("BadgeIcon");
        badgeIconObj.transform.SetParent(crestObj.transform, false);
        RectTransform biRect = badgeIconObj.AddComponent<RectTransform>();
        biRect.sizeDelta = new Vector2(64f, 64f);
        biRect.anchoredPosition = new Vector2(0f, 75f);
        Image biImg = badgeIconObj.AddComponent<Image>();
        biImg.sprite = RhythmProceduralSprites.Heart;
        biImg.color = Color.white;
        biImg.raycastTarget = false;

        // Title
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
        vtText.color = new Color(0.68f, 0.22f, 0.12f, 1f);

        var vtOutline = vTitleObj.AddComponent<Outline>();
        vtOutline.effectColor = new Color(0.45f, 0.14f, 0.08f, 0.70f);
        vtOutline.effectDistance = new Vector2(1f, -1f);

        // Subtitle
        GameObject vSubObj = new GameObject("VictorySub");
        vSubObj.transform.SetParent(plaqueObj.transform, false);
        RectTransform vsRect = vSubObj.AddComponent<RectTransform>();
        vsRect.sizeDelta = new Vector2(450f, 50f);
        vsRect.anchoredPosition = new Vector2(0f, -22f);
        var vsText = vSubObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) vsText.font = fontAsset;
        string species = !string.IsNullOrEmpty(currentContext.SpeciesName) ? currentContext.SpeciesName : "Linh Thú";
        vsText.color = new Color(0.26f, 0.16f, 0.10f, 1f);
        vsText.text = $"<b>{species}</b> đã sẵn sàng đồng hành cùng bạn!\n<size=13.5><color=#5C4032>Tâm hồn thú cưng để hòa làm một</color></size>";
        vsText.fontSize = 15f;
        vsText.alignment = TextAlignmentOptions.Center;

        List<ParticleData> particles = SpawnCelebrationParticles(panelRect, 32);

        float duration = 2.5f;
        float elapsed = 0f;
        Vector3 boardBaseScale = panelRect.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            if (sbRect != null)
            {
                sbRect.localEulerAngles = new Vector3(0f, 0f, -elapsed * 35f);
            }

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

            if (biRect != null)
            {
                float heartBeat = 1f + 0.12f * Mathf.Abs(Mathf.Sin(elapsed * 7f));
                biRect.localScale = Vector3.one * heartBeat;
            }

            if (flashImg != null)
            {
                float flashA = Mathf.Clamp01(1f - (elapsed / 0.5f)) * 0.55f;
                flashImg.color = new Color(1f, 0.94f, 0.55f, flashA);
            }

            UpdateCelebrationParticles(particles, Time.deltaTime);

            yield return null;
        }

        if (flashObj != null) Destroy(flashObj);
        foreach (var p in particles)
        {
            if (p.Obj != null) Destroy(p.Obj);
        }

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
                pImg.sprite = RhythmProceduralSprites.SparkleStar;
                pImg.color = new Color(1f, 0.92f, 0.30f, 1f);
            }
            else if (type == 1)
            {
                pImg.sprite = RhythmProceduralSprites.Heart;
                pImg.color = new Color(0.98f, 0.35f, 0.45f, 1f);
            }
            else
            {
                pImg.sprite = RhythmProceduralSprites.TargetStarburst;
                pImg.color = new Color(1f, 0.85f, 0.25f, 1f);
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
            p.Rect.anchoredPosition += p.Velocity * dt;
            p.Rect.localEulerAngles = new Vector3(0f, 0f, p.Rect.localEulerAngles.z + p.RotationSpeed * dt);

            if (p.Img != null)
            {
                Color c = p.Img.color;
                c.a = Mathf.Clamp01(c.a - dt * 0.35f);
                p.Img.color = c;
            }
        }
    }

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
                scale = Mathf.Lerp(0.78f, 1.05f, t / 0.65f);
            }
            else
            {
                scale = Mathf.Lerp(1.05f, 1.0f, (t - 0.65f) / 0.35f);
            }
            card.localScale = new Vector3(scale, scale, 1f);

            if (dim != null)
            {
                Color c = dim.color;
                c.a = Mathf.Lerp(0f, 0.52f, t);
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
            img.sprite = RhythmProceduralSprites.SparkleStar;
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

            rt.anchoredPosition = new Vector2(origPos.x, origPos.y + Mathf.Lerp(0f, 16f, t));

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

        for (int i = 0; i < activeSlotViews.Count; i++)
        {
            var slot = activeSlotViews[i];
            if (i >= engine.CurrentSequenceIndex && slot.ArrowImg != null)
            {
                slot.ArrowImg.sprite = RhythmProceduralSprites.GetJellySprite(slot.Direction);
                slot.ArrowImg.color = Color.white;
            }
            if (slot.RootObj != null) slot.RootObj.transform.localScale = Vector3.one;
        }
    }
}
