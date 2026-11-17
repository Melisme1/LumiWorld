using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Giao diện UI 2D và điều khiển cho Minigame Chạy / Bay Vượt Chướng Ngại Vật (ObstacleRunner):
/// - Dùng Avatar 2D dạng Token + hoạt họa Squash/Stretch, nghiêng góc và bóng đổ dưới đất.
/// - Tự động chọn môi trường Ground hoặc Sky theo locomotionType của loài.
/// - Thanh tiến trình theo thời gian với tốc độ tăng dần về cuối.
/// </summary>
public class RunnerMinigameView : MonoBehaviour, IMinigameSession
{
    public MinigameType Type => MinigameType.ObstacleRunner;
    public bool IsActive => isSessionActive;

    private bool isSessionActive = false;
    private bool isFinishing = false;
    private MinigameContext currentContext;
    private Action<MinigameResult> completionCallback;
    private RunnerMinigameEngine engine;

    // UI References
    private RectTransform panelRect;
    private Image progressFillImg;
    private RectTransform progressMarkerRect;
    private TextMeshProUGUI progressText;
    private TextMeshProUGUI speedText;
    private readonly List<Image> heartIcons = new List<Image>();

    private RectTransform viewportRect;
    private RectTransform playerTokenRect;
    private RectTransform playerShadowRect;
    private Image playerShadowImg;
    private CanvasGroup playerCanvasGroup;
    private readonly Dictionary<int, RectTransform> obstacleViews = new Dictionary<int, RectTransform>();
    private readonly List<RectTransform> decorClouds = new List<RectTransform>();
    private readonly List<RectTransform> groundStripes = new List<RectTransform>();

    private TextMeshProUGUI actionHintText;
    private AudioSource audioSource;
    private TMP_FontAsset fontAsset;

    // Animation state (Approach A)
    private float animClock = 0f;
    private Vector3 currentSquashScale = Vector3.one;
    private readonly float groundFloorHeight = 48f;
    private bool pointerHeld = false;

    // Cached procedural 2D sprites
    private static Sprite cloudSprite;
    private static Sprite rockSprite;
    private static Sprite bushSprite;
    private static Sprite logSprite;
    private static Sprite stormCloudSprite;
    private static Sprite branchSprite;
    private static Sprite roundedRectSprite;

    public void StartSession(MinigameContext context, Action<MinigameResult> onComplete)
    {
        currentContext = context ?? new MinigameContext();
        completionCallback = onComplete;
        isSessionActive = true;
        isFinishing = false;
        pointerHeld = false;
        animClock = 0f;
        currentSquashScale = Vector3.one;

        var skin = Resources.Load<LumiWorld.Acs.ResourceInventoryUISkin>("UI/ResourceInventoryUISkin");
        fontAsset = skin != null && skin.font != null ? skin.font : null;

        RhythmProceduralSprites.EnsureLoaded();
        EnsureRunnerSprites();
        audioSource = RhythmProceduralAudio.EnsureAudioSource(gameObject, audioSource);

        RunnerEnvironmentMode mode = ResolveEnvironmentMode(currentContext);
        BuildUI(mode);

        engine = new RunnerMinigameEngine();
        engine.OnPlayerJumped += HandlePlayerJumped;
        engine.OnPlayerLanded += HandlePlayerLanded;
        engine.OnObstacleSpawned += HandleObstacleSpawned;
        engine.OnObstacleDespawned += HandleObstacleDespawned;
        engine.OnLivesChanged += HandleLivesChanged;
        engine.OnProgressChanged += HandleProgressChanged;
        engine.OnSessionFinished += HandleSessionFinished;

        engine.Initialize(mode, duration: 18f, maxLives: 3);
        StartCoroutine(AnimateIntroRoutine());
    }

    public void CancelSession()
    {
        if (!isSessionActive) return;
        isSessionActive = false;
        completionCallback?.Invoke(new MinigameResult
        {
            IsSuccess = false,
            Type = MinigameType.ObstacleRunner,
            Message = "Challenge cancelled."
        });
    }

    private void Update()
    {
        if (!isSessionActive || isFinishing || engine == null) return;

        bool keyHeld = false;
        if (Keyboard.current != null)
        {
            keyHeld = Keyboard.current.spaceKey.isPressed
                   || Keyboard.current.upArrowKey.isPressed
                   || Keyboard.current.wKey.isPressed;
        }

        engine.SetThrust(keyHeld || pointerHeld);
        engine.Tick(Time.deltaTime);

        UpdatePlayerVisuals(Time.deltaTime);
        UpdateObstaclesVisuals();
        UpdateParallaxDecor(Time.deltaTime);
    }

    private static RunnerEnvironmentMode ResolveEnvironmentMode(MinigameContext context)
    {
        if (context?.CreatureCard?.animalSpeciesData != null &&
            context.CreatureCard.animalSpeciesData.locomotionType == AnimalLocomotionType.Flying)
        {
            return RunnerEnvironmentMode.Sky;
        }
        return RunnerEnvironmentMode.Ground;
    }

    #region Engine Event Handlers

    private void HandlePlayerJumped()
    {
        currentSquashScale = new Vector3(0.84f, 1.22f, 1f);
        RhythmProceduralAudio.PlayDirectionSound(audioSource, RhythmDirection.Up);
    }

    private void HandlePlayerLanded()
    {
        currentSquashScale = new Vector3(1.20f, 0.80f, 1f);
    }

    private float GetBaseOffset() => engine != null && engine.Mode == RunnerEnvironmentMode.Ground
        ? groundFloorHeight - 7f
        : 14f;

    private void HandleObstacleSpawned(RunnerObstacleState obs)
    {
        if (viewportRect == null) return;

        GameObject obsObj = new GameObject($"Obstacle_{obs.Id}_{obs.Kind}");
        obsObj.transform.SetParent(viewportRect, false);
        RectTransform rt = obsObj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(obs.Width, obs.Height);

        Image img = obsObj.AddComponent<Image>();
        img.sprite = GetObstacleSprite(obs.Kind);
        img.color = Color.white;

        rt.anchoredPosition = new Vector2(obs.X, GetBaseOffset() + obs.Y);
        obstacleViews[obs.Id] = rt;
    }

    private void HandleObstacleDespawned(int obstacleId)
    {
        if (obstacleViews.TryGetValue(obstacleId, out RectTransform rt))
        {
            if (rt != null) Destroy(rt.gameObject);
            obstacleViews.Remove(obstacleId);
        }
    }

    private void HandleLivesChanged(int remainingLives)
    {
        for (int i = 0; i < heartIcons.Count; i++)
        {
            if (heartIcons[i] != null)
            {
                bool alive = i < remainingLives;
                heartIcons[i].color = alive ? new Color(0.95f, 0.28f, 0.34f, 1f) : new Color(0.35f, 0.35f, 0.38f, 0.38f);
            }
        }

        if (remainingLives < engine.MaxLives && remainingLives > 0)
        {
            RhythmProceduralAudio.PlayBeatSound(audioSource, RhythmHitGrade.Miss);
        }
    }

    private void HandleProgressChanged(float progress, float speedMult)
    {
        if (progressFillImg != null)
        {
            progressFillImg.fillAmount = progress;
        }

        if (progressMarkerRect != null && progressFillImg != null)
        {
            float barWidth = ((RectTransform)progressFillImg.transform.parent).rect.width;
            progressMarkerRect.anchoredPosition = new Vector2(barWidth * progress, 0f);
        }

        if (progressText != null)
        {
            progressText.text = $"{Mathf.RoundToInt(progress * 100f)}%";
        }

        if (speedText != null)
        {
            speedText.text = speedMult.ToString("0.0", CultureInfo.InvariantCulture) + "x";
        }
    }

    private void HandleSessionFinished(bool isSuccess, string message)
    {
        if (!isSessionActive || isFinishing) return;
        isFinishing = true;

        if (isSuccess) RhythmProceduralAudio.PlayVictoryFanfare(audioSource);
        else RhythmProceduralAudio.PlayBeatSound(audioSource, RhythmHitGrade.Miss);

        StartCoroutine(FinishDelayRoutine(isSuccess, message));
    }

    private IEnumerator FinishDelayRoutine(bool isSuccess, string message)
    {
        if (actionHintText != null)
        {
            actionHintText.text = isSuccess ? "TAMED! WELL DONE!" : "ESCAPED! TRY AGAIN NEXT TIME";
            actionHintText.color = isSuccess ? new Color(0.16f, 0.65f, 0.30f, 1f) : new Color(0.86f, 0.24f, 0.24f, 1f);
        }

        yield return new WaitForSecondsRealtime(0.85f);

        isSessionActive = false;
        completionCallback?.Invoke(new MinigameResult
        {
            IsSuccess = isSuccess,
            Type = MinigameType.ObstacleRunner,
            Score = engine.ObstaclesCleared,
            Accuracy = engine.Progress,
            Message = message
        });
    }

    #endregion

    #region Visual & Procedural Animation Updates

    private void UpdatePlayerVisuals(float dt)
    {
        if (playerTokenRect == null || engine == null) return;

        animClock += dt * engine.SpeedMultiplier;
        float baseOffset = GetBaseOffset();

        float runBob = 0f;
        float tiltDeg = 0f;
        if (engine.Mode == RunnerEnvironmentMode.Ground)
        {
            if (engine.IsGrounded)
            {
                runBob = Mathf.Abs(Mathf.Sin(animClock * 13f)) * 5f;
                tiltDeg = Mathf.Sin(animClock * 13f) * 4.5f - 3f;
            }
            else
            {
                tiltDeg = Mathf.Clamp(engine.VelocityY * 0.024f, -16f, 16f);
            }
        }
        else
        {
            runBob = Mathf.Sin(animClock * 6f) * 3f;
            tiltDeg = Mathf.Clamp(engine.VelocityY * 0.055f, -22f, 22f);
        }

        playerTokenRect.anchoredPosition = new Vector2(RunnerMinigameEngine.PlayerX, baseOffset + engine.PlayerY + runBob);
        playerTokenRect.localRotation = Quaternion.Euler(0f, 0f, tiltDeg);

        currentSquashScale = Vector3.Lerp(currentSquashScale, Vector3.one, dt * 14f);
        playerTokenRect.localScale = currentSquashScale;

        // Bóng đổ trên mặt đất co giãn theo độ cao nhảy
        if (playerShadowRect != null && playerShadowImg != null)
        {
            float heightRatio = Mathf.Clamp01(engine.PlayerY / 160f);
            float shadowScale = Mathf.Lerp(1.0f, 0.42f, heightRatio);
            playerShadowRect.localScale = new Vector3(shadowScale, 1f, 1f);
            playerShadowImg.color = new Color(0.10f, 0.18f, 0.10f, Mathf.Lerp(0.32f, 0.08f, heightRatio));
        }

        if (playerCanvasGroup != null)
        {
            playerCanvasGroup.alpha = engine.IsInvulnerable
                ? (Mathf.Sin(Time.unscaledTime * 28f) > 0f ? 0.35f : 1f)
                : 1f;
        }
    }

    private void UpdateObstaclesVisuals()
    {
        float baseOffset = GetBaseOffset();
        var list = engine.Obstacles;
        for (int i = 0; i < list.Count; i++)
        {
            RunnerObstacleState obs = list[i];
            if (obstacleViews.TryGetValue(obs.Id, out RectTransform rt) && rt != null)
            {
                rt.anchoredPosition = new Vector2(obs.X, baseOffset + obs.Y);
            }
        }
    }

    private void UpdateParallaxDecor(float dt)
    {
        float scroll = engine.CurrentScrollSpeed * dt;

        for (int i = 0; i < decorClouds.Count; i++)
        {
            RectTransform c = decorClouds[i];
            if (c == null) continue;
            Vector2 pos = c.anchoredPosition;
            pos.x -= scroll * 0.26f;
            if (pos.x < -140f) pos.x = RunnerMinigameEngine.TrackWidth + 100f;
            c.anchoredPosition = pos;
        }

        for (int i = 0; i < groundStripes.Count; i++)
        {
            RectTransform g = groundStripes[i];
            if (g == null) continue;
            Vector2 pos = g.anchoredPosition;
            pos.x -= scroll;
            if (pos.x < -120f) pos.x += 120f * groundStripes.Count;
            g.anchoredPosition = pos;
        }
    }

    #endregion

    #region UI Construction

    private void BuildUI(RunnerEnvironmentMode mode)
    {
        GameObject panelObj = new GameObject("RunnerMinigamePanel");
        panelObj.transform.SetParent(transform, false);
        panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(1020f, 580f);
        panelRect.anchoredPosition = Vector2.zero;

        Image panelImg = panelObj.AddComponent<Image>();
        panelImg.sprite = RhythmProceduralSprites.WoodBoard;
        panelImg.type = Image.Type.Sliced;
        panelImg.color = Color.white;

        BuildHeader(panelObj.transform, mode);
        BuildProgressBar(panelObj.transform);
        BuildPlayfield(panelObj.transform, mode);
        BuildFooterControls(panelObj.transform, mode);
    }

    private void BuildHeader(Transform parent, RunnerEnvironmentMode mode)
    {
        string species = !string.IsNullOrEmpty(currentContext.SpeciesName) ? currentContext.SpeciesName : "Wild Creature";
        string envLabel = mode == RunnerEnvironmentMode.Sky ? "SKY" : "GROUND";

        CreateText(parent, "TitleText", $"{species} — Wild Chase", 26, TextAlignmentOptions.Left,
            new Vector2(44f, -30f), new Vector2(560f, 38f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Color(0.24f, 0.16f, 0.10f, 1f), FontStyles.Bold);

        // Badge môi trường gọn gàng
        GameObject badgeObj = new GameObject("EnvBadge");
        badgeObj.transform.SetParent(parent, false);
        RectTransform badgeRt = badgeObj.AddComponent<RectTransform>();
        badgeRt.anchorMin = new Vector2(0f, 1f);
        badgeRt.anchorMax = new Vector2(0f, 1f);
        badgeRt.pivot = new Vector2(0f, 1f);
        badgeRt.anchoredPosition = new Vector2(44f, -68f);
        badgeRt.sizeDelta = new Vector2(106f, 26f);

        Image badgeImg = badgeObj.AddComponent<Image>();
        badgeImg.sprite = roundedRectSprite;
        badgeImg.type = Image.Type.Sliced;
        badgeImg.color = mode == RunnerEnvironmentMode.Sky
            ? new Color(0.24f, 0.60f, 0.88f, 1f)
            : new Color(0.28f, 0.66f, 0.32f, 1f);

        CreateText(badgeObj.transform, "EnvLabel", envLabel, 13, TextAlignmentOptions.Center,
            Vector2.zero, new Vector2(106f, 26f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Color.white, FontStyles.Bold);

        // 3 Trái tim HP
        heartIcons.Clear();
        for (int i = 0; i < 3; i++)
        {
            GameObject hObj = new GameObject($"Heart_{i}");
            hObj.transform.SetParent(parent, false);
            RectTransform hRt = hObj.AddComponent<RectTransform>();
            hRt.anchorMin = new Vector2(1f, 1f);
            hRt.anchorMax = new Vector2(1f, 1f);
            hRt.pivot = new Vector2(1f, 1f);
            hRt.anchoredPosition = new Vector2(-100f - i * 38f, -32f);
            hRt.sizeDelta = new Vector2(30f, 30f);

            Image hImg = hObj.AddComponent<Image>();
            hImg.sprite = RhythmProceduralSprites.Heart;
            hImg.color = new Color(0.95f, 0.28f, 0.34f, 1f);
            heartIcons.Add(hImg);
        }

        // Nút Đóng
        GameObject closeObj = new GameObject("CloseButton");
        closeObj.transform.SetParent(parent, false);
        RectTransform closeRt = closeObj.AddComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1f, 1f);
        closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.pivot = new Vector2(1f, 1f);
        closeRt.anchoredPosition = new Vector2(-32f, -26f);
        closeRt.sizeDelta = new Vector2(40f, 40f);

        Image closeImg = closeObj.AddComponent<Image>();
        closeImg.sprite = RhythmProceduralSprites.RedCloseBtn;
        Button closeBtn = closeObj.AddComponent<Button>();
        closeBtn.onClick.AddListener(CancelSession);
    }

    private void BuildProgressBar(Transform parent)
    {
        // Đặt tách hẳn phía trên Viewport (không bị che)
        GameObject barBg = new GameObject("ProgressBarBg");
        barBg.transform.SetParent(parent, false);
        RectTransform barBgRt = barBg.AddComponent<RectTransform>();
        barBgRt.anchorMin = new Vector2(0.5f, 1f);
        barBgRt.anchorMax = new Vector2(0.5f, 1f);
        barBgRt.pivot = new Vector2(0.5f, 1f);
        barBgRt.anchoredPosition = new Vector2(0f, -104f);
        barBgRt.sizeDelta = new Vector2(932f, 22f);

        Image bgImg = barBg.AddComponent<Image>();
        bgImg.sprite = roundedRectSprite;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = new Color(0.24f, 0.18f, 0.13f, 0.85f);

        GameObject fillObj = new GameObject("ProgressFill");
        fillObj.transform.SetParent(barBg.transform, false);
        RectTransform fillRt = fillObj.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(3f, 3f);
        fillRt.offsetMax = new Vector2(-3f, -3f);

        progressFillImg = fillObj.AddComponent<Image>();
        progressFillImg.sprite = RhythmProceduralSprites.White;
        progressFillImg.type = Image.Type.Filled;
        progressFillImg.fillMethod = Image.FillMethod.Horizontal;
        progressFillImg.fillAmount = 0f;
        progressFillImg.color = new Color(0.32f, 0.82f, 0.44f, 1f);

        // Ngôi sao đánh dấu vị trí tiến trình
        GameObject markerObj = new GameObject("ProgressMarker");
        markerObj.transform.SetParent(fillObj.transform, false);
        progressMarkerRect = markerObj.AddComponent<RectTransform>();
        progressMarkerRect.anchorMin = new Vector2(0f, 0.5f);
        progressMarkerRect.anchorMax = new Vector2(0f, 0.5f);
        progressMarkerRect.sizeDelta = new Vector2(20f, 20f);
        Image markerImg = markerObj.AddComponent<Image>();
        markerImg.sprite = RhythmProceduralSprites.SparkleStar;
        markerImg.color = new Color(1f, 0.92f, 0.40f, 1f);

        // Biểu tượng đích (trái tim thu phục) ở cuối thanh tiến trình
        GameObject goalObj = new GameObject("ProgressGoal");
        goalObj.transform.SetParent(barBg.transform, false);
        RectTransform goalRt = goalObj.AddComponent<RectTransform>();
        goalRt.anchorMin = new Vector2(1f, 0.5f);
        goalRt.anchorMax = new Vector2(1f, 0.5f);
        goalRt.anchoredPosition = new Vector2(-4f, 0f);
        goalRt.sizeDelta = new Vector2(22f, 22f);
        Image goalImg = goalObj.AddComponent<Image>();
        goalImg.sprite = RhythmProceduralSprites.Heart;
        goalImg.color = new Color(0.95f, 0.34f, 0.42f, 1f);

        progressText = CreateText(parent, "ProgressPct", "0%", 17, TextAlignmentOptions.Left,
            new Vector2(166f, -72f), new Vector2(100f, 26f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Color(0.24f, 0.16f, 0.10f, 1f), FontStyles.Bold);

        speedText = CreateText(parent, "SpeedLabel", "1.0x", 17, TextAlignmentOptions.Right,
            new Vector2(-46f, -72f), new Vector2(120f, 26f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Color(0.76f, 0.34f, 0.14f, 1f), FontStyles.Bold);
    }

    private void BuildPlayfield(Transform parent, RunnerEnvironmentMode mode)
    {
        // Khung viền nâu ấm bao quanh Viewport
        GameObject frameObj = new GameObject("ViewportFrame");
        frameObj.transform.SetParent(parent, false);
        RectTransform frameRt = frameObj.AddComponent<RectTransform>();
        frameRt.anchorMin = new Vector2(0.5f, 0.5f);
        frameRt.anchorMax = new Vector2(0.5f, 0.5f);
        frameRt.anchoredPosition = new Vector2(0f, -22f);
        frameRt.sizeDelta = new Vector2(RunnerMinigameEngine.TrackWidth + 12f, RunnerMinigameEngine.TrackHeight + groundFloorHeight + 12f);
        Image frameImg = frameObj.AddComponent<Image>();
        frameImg.sprite = roundedRectSprite;
        frameImg.type = Image.Type.Sliced;
        frameImg.color = new Color(0.36f, 0.25f, 0.16f, 0.90f);

        GameObject vpObj = new GameObject("RunnerViewport");
        vpObj.transform.SetParent(frameObj.transform, false);
        viewportRect = vpObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.5f, 0.5f);
        viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
        viewportRect.anchoredPosition = Vector2.zero;
        viewportRect.sizeDelta = new Vector2(RunnerMinigameEngine.TrackWidth, RunnerMinigameEngine.TrackHeight + groundFloorHeight);

        vpObj.AddComponent<RectMask2D>();
        Image vpBg = vpObj.AddComponent<Image>();
        vpBg.color = mode == RunnerEnvironmentMode.Sky
            ? new Color(0.54f, 0.81f, 0.97f, 1f)
            : new Color(0.82f, 0.94f, 0.90f, 1f);

        AddPointerHoldEvents(vpObj);

        // Mây trắng mềm mại trôi parallax
        decorClouds.Clear();
        for (int i = 0; i < 4; i++)
        {
            GameObject cloudObj = new GameObject($"Cloud_{i}");
            cloudObj.transform.SetParent(vpObj.transform, false);
            RectTransform cRt = cloudObj.AddComponent<RectTransform>();
            cRt.anchorMin = Vector2.zero;
            cRt.anchorMax = Vector2.zero;
            cRt.sizeDelta = new Vector2(118f + (i % 2) * 26f, 48f);
            cRt.anchoredPosition = new Vector2(140f + i * 240f, 220f + (i % 2) * 44f);

            Image cImg = cloudObj.AddComponent<Image>();
            cImg.sprite = cloudSprite;
            cImg.color = new Color(1f, 1f, 1f, mode == RunnerEnvironmentMode.Sky ? 0.78f : 0.85f);
            decorClouds.Add(cRt);
        }

        groundStripes.Clear();
        if (mode == RunnerEnvironmentMode.Ground)
        {
            // Đường chân trời sáng dịu
            GameObject horizonObj = new GameObject("HorizonGlow");
            horizonObj.transform.SetParent(vpObj.transform, false);
            RectTransform hzRt = horizonObj.AddComponent<RectTransform>();
            hzRt.anchorMin = new Vector2(0f, 0f);
            hzRt.anchorMax = new Vector2(1f, 0f);
            hzRt.pivot = new Vector2(0.5f, 0f);
            hzRt.anchoredPosition = new Vector2(0f, groundFloorHeight);
            hzRt.sizeDelta = new Vector2(0f, 84f);
            Image hzImg = horizonObj.AddComponent<Image>();
            hzImg.color = new Color(0.90f, 0.97f, 0.92f, 0.75f);

            // Đồi xa màu xanh dịu
            GameObject hillObj = new GameObject("DistantHills");
            hillObj.transform.SetParent(vpObj.transform, false);
            RectTransform hRt = hillObj.AddComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 0f);
            hRt.anchorMax = new Vector2(1f, 0f);
            hRt.pivot = new Vector2(0.5f, 0f);
            hRt.anchoredPosition = new Vector2(0f, groundFloorHeight);
            hRt.sizeDelta = new Vector2(0f, 34f);
            Image hImg = hillObj.AddComponent<Image>();
            hImg.color = new Color(0.64f, 0.83f, 0.68f, 0.85f);

            // Mặt đất xanh cỏ
            GameObject groundObj = new GameObject("GroundFloor");
            groundObj.transform.SetParent(vpObj.transform, false);
            RectTransform gRt = groundObj.AddComponent<RectTransform>();
            gRt.anchorMin = new Vector2(0f, 0f);
            gRt.anchorMax = new Vector2(1f, 0f);
            gRt.pivot = new Vector2(0.5f, 0f);
            gRt.sizeDelta = new Vector2(0f, groundFloorHeight);
            Image gImg = groundObj.AddComponent<Image>();
            gImg.color = new Color(0.42f, 0.66f, 0.30f, 1f);

            // Viền cỏ sáng phía trên mặt đất
            GameObject topGrass = new GameObject("GrassRim");
            topGrass.transform.SetParent(groundObj.transform, false);
            RectTransform tgRt = topGrass.AddComponent<RectTransform>();
            tgRt.anchorMin = new Vector2(0f, 1f);
            tgRt.anchorMax = new Vector2(1f, 1f);
            tgRt.pivot = new Vector2(0.5f, 1f);
            tgRt.sizeDelta = new Vector2(0f, 6f);
            Image tgImg = topGrass.AddComponent<Image>();
            tgImg.color = new Color(0.56f, 0.80f, 0.38f, 1f);

            for (int i = 0; i < 10; i++)
            {
                GameObject stripe = new GameObject($"GroundStripe_{i}");
                stripe.transform.SetParent(groundObj.transform, false);
                RectTransform sRt = stripe.AddComponent<RectTransform>();
                sRt.anchorMin = new Vector2(0f, 0.5f);
                sRt.anchorMax = new Vector2(0f, 0.5f);
                sRt.pivot = new Vector2(0f, 0.5f);
                sRt.sizeDelta = new Vector2(48f, 6f);
                sRt.anchoredPosition = new Vector2(i * 120f, -4f);
                Image sImg = stripe.AddComponent<Image>();
                sImg.color = new Color(0.32f, 0.54f, 0.22f, 0.65f);
                groundStripes.Add(sRt);
            }

            // Bóng đổ dưới chân nhân vật (nằm ngay trên thảm cỏ)
            GameObject shadowObj = new GameObject("PlayerShadow");
            shadowObj.transform.SetParent(vpObj.transform, false);
            playerShadowRect = shadowObj.AddComponent<RectTransform>();
            playerShadowRect.anchorMin = Vector2.zero;
            playerShadowRect.anchorMax = Vector2.zero;
            playerShadowRect.pivot = new Vector2(0.5f, 0.5f);
            playerShadowRect.sizeDelta = new Vector2(50f, 11f);
            playerShadowRect.anchoredPosition = new Vector2(RunnerMinigameEngine.PlayerX + RunnerMinigameEngine.PlayerSize * 0.5f, groundFloorHeight - 4f);
            playerShadowImg = shadowObj.AddComponent<Image>();
            playerShadowImg.sprite = RhythmProceduralSprites.PearlOrb;
            playerShadowImg.color = new Color(0.10f, 0.18f, 0.10f, 0.32f);
        }

        BuildPlayerToken(vpObj.transform, mode);
    }

    private void BuildPlayerToken(Transform parent, RunnerEnvironmentMode mode)
    {
        GameObject tokenObj = new GameObject("PlayerCreatureToken");
        tokenObj.transform.SetParent(parent, false);
        playerTokenRect = tokenObj.AddComponent<RectTransform>();
        playerTokenRect.anchorMin = Vector2.zero;
        playerTokenRect.anchorMax = Vector2.zero;
        playerTokenRect.pivot = new Vector2(0f, 0f);
        playerTokenRect.sizeDelta = new Vector2(RunnerMinigameEngine.PlayerSize, RunnerMinigameEngine.PlayerSize);

        float baseOffset = mode == RunnerEnvironmentMode.Ground ? groundFloorHeight - 7f : 14f;
        playerTokenRect.anchoredPosition = new Vector2(RunnerMinigameEngine.PlayerX, baseOffset);
        playerCanvasGroup = tokenObj.AddComponent<CanvasGroup>();

        Image bgImg = tokenObj.AddComponent<Image>();
        bgImg.sprite = RhythmProceduralSprites.PearlOrb;
        bgImg.color = new Color(1f, 0.97f, 0.90f, 1f);

        GameObject ringObj = new GameObject("WildBorderRing");
        ringObj.transform.SetParent(tokenObj.transform, false);
        RectTransform ringRt = ringObj.AddComponent<RectTransform>();
        ringRt.anchorMin = Vector2.zero;
        ringRt.anchorMax = Vector2.one;
        ringRt.sizeDelta = new Vector2(8f, 8f);
        Image ringImg = ringObj.AddComponent<Image>();
        ringImg.sprite = RhythmProceduralSprites.Ring;
        ringImg.color = new Color(0.92f, 0.24f, 0.24f, 0.95f);

        GameObject avtObj = new GameObject("CreatureAvatar");
        avtObj.transform.SetParent(tokenObj.transform, false);
        RectTransform avtRt = avtObj.AddComponent<RectTransform>();
        avtRt.anchorMin = new Vector2(0.08f, 0.08f);
        avtRt.anchorMax = new Vector2(0.92f, 0.92f);
        avtRt.sizeDelta = Vector2.zero;
        Image avtImg = avtObj.AddComponent<Image>();
        avtImg.preserveAspect = true;
        avtImg.sprite = ResolveCreatureSprite();
    }

    private Sprite ResolveCreatureSprite()
    {
        string speciesKey = currentContext?.CreatureCard?.animalSpeciesData != null
            ? currentContext.CreatureCard.animalSpeciesData.speciesID
            : currentContext?.SpeciesName;

        if (!string.IsNullOrEmpty(speciesKey))
        {
            Sprite avt = HabitatCreaturePanelStyle.GetCreatureAvatar(speciesKey, LumiWorld.Acs.CreatureQualityRank.RankI);
            if (avt != null) return avt;
        }

        if (currentContext?.CreatureCard != null && currentContext.CreatureCard.cardImage != null)
        {
            return currentContext.CreatureCard.cardImage;
        }

        return RhythmProceduralSprites.PearlOrb;
    }

    private void BuildFooterControls(Transform parent, RunnerEnvironmentMode mode)
    {
        string hint = mode == RunnerEnvironmentMode.Sky
            ? "HOLD SPACE / CLICK to fly up — release to glide"
            : "PRESS SPACE / CLICK to jump over obstacles";

        actionHintText = CreateText(parent, "HintText", hint, 18, TextAlignmentOptions.Left,
            new Vector2(46f, 28f), new Vector2(620f, 36f), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Color(0.28f, 0.20f, 0.14f, 1f), FontStyles.Bold);

        GameObject btnObj = new GameObject("JumpActionButton");
        btnObj.transform.SetParent(parent, false);
        RectTransform btnRt = btnObj.AddComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(1f, 0f);
        btnRt.anchorMax = new Vector2(1f, 0f);
        btnRt.pivot = new Vector2(1f, 0f);
        btnRt.anchoredPosition = new Vector2(-46f, 20f);
        btnRt.sizeDelta = new Vector2(190f, 50f);

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.sprite = roundedRectSprite;
        btnImg.type = Image.Type.Sliced;
        btnImg.color = new Color(0.22f, 0.68f, 0.36f, 1f);

        AddPointerHoldEvents(btnObj);

        CreateText(btnObj.transform, "BtnLabel", mode == RunnerEnvironmentMode.Sky ? "FLY UP" : "JUMP!", 20,
            TextAlignmentOptions.Center, Vector2.zero, new Vector2(190f, 50f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Color.white, FontStyles.Bold);
    }

    private void AddPointerHoldEvents(GameObject target)
    {
        EventTrigger trigger = target.GetComponent<EventTrigger>() ?? target.AddComponent<EventTrigger>();

        var downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        downEntry.callback.AddListener(_ => pointerHeld = true);
        trigger.triggers.Add(downEntry);

        var upEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        upEntry.callback.AddListener(_ => pointerHeld = false);
        trigger.triggers.Add(upEntry);

        var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener(_ => pointerHeld = false);
        trigger.triggers.Add(exitEntry);
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, string content, float fontSize,
        TextAlignmentOptions align, Vector2 pos, Vector2 size, Vector2 anchorMin, Vector2 anchorMax,
        Color color, FontStyles style)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = anchorMin;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) tmp.font = fontAsset;
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = color;
        tmp.fontStyle = style;
        return tmp;
    }

    private IEnumerator AnimateIntroRoutine()
    {
        if (panelRect == null) yield break;
        float t = 0f;
        while (t < 0.18f)
        {
            t += Time.unscaledDeltaTime;
            float s = Mathf.SmoothStep(0.88f, 1f, t / 0.18f);
            panelRect.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        panelRect.localScale = Vector3.one;
    }

    #endregion

    #region Procedural 2D Sprites (Runner)

    private static void EnsureRunnerSprites()
    {
        roundedRectSprite ??= CreateRoundedRectSprite(32, 8);
        cloudSprite ??= CreateCloudSprite(128, 56);
        rockSprite ??= CreateRockSprite(64, 64);
        bushSprite ??= CreateBushSprite(64, 64);
        logSprite ??= CreateLogSprite(80, 48);
        stormCloudSprite ??= CreateStormCloudSprite(96, 72);
        branchSprite ??= CreateBranchSprite(72, 88);
    }

    private static Sprite GetObstacleSprite(RunnerObstacleKind kind) => kind switch
    {
        RunnerObstacleKind.Rock => rockSprite,
        RunnerObstacleKind.ThornBush => bushSprite,
        RunnerObstacleKind.FallenLog => logSprite,
        RunnerObstacleKind.StormCloud => stormCloudSprite,
        RunnerObstacleKind.HighBranch => branchSprite,
        _ => rockSprite
    };

    private static Sprite CreateRoundedRectSprite(int size, int radius)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius));
                float dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius));
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius - dist + 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    private static Sprite CreateCloudSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        Color[] pixels = new Color[w * h];
        Vector3[] puffs =
        {
            new Vector3(w * 0.28f, h * 0.42f, h * 0.30f),
            new Vector3(w * 0.48f, h * 0.56f, h * 0.38f),
            new Vector3(w * 0.68f, h * 0.44f, h * 0.30f),
            new Vector3(w * 0.50f, h * 0.34f, h * 0.28f)
        };
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float maxA = 0f;
                for (int i = 0; i < puffs.Length; i++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(puffs[i].x, puffs[i].y));
                    maxA = Mathf.Max(maxA, Mathf.Clamp01(puffs[i].z - d + 1f));
                }
                if (y < h * 0.20f) maxA = 0f;
                pixels[y * w + x] = new Color(1f, 1f, 1f, maxA);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateRockSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        Color[] pixels = new Color[w * h];
        Color body = new Color(0.54f, 0.52f, 0.50f, 1f);
        Color hi = new Color(0.74f, 0.72f, 0.68f, 1f);
        Color dark = new Color(0.32f, 0.30f, 0.29f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // Đáy phẳng chạm sát y = 0, vòm đá thuôn lên trên
                float nx = (x - w * 0.5f) / (w * 0.44f);
                float ny = (float)y / (h * 0.88f);
                float d = nx * nx + ny * ny * 0.85f;
                Color c = Color.clear;
                if (d <= 1f)
                {
                    float a = Mathf.Clamp01((1f - d) * 7f);
                    c = ny > 0.55f ? hi : (ny < 0.16f ? dark : body);
                    c.a = a;
                }
                pixels[y * w + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateBushSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        Color[] pixels = new Color[w * h];
        Color leafDark = new Color(0.14f, 0.36f, 0.18f, 1f);
        Color leafLight = new Color(0.26f, 0.58f, 0.26f, 1f);
        Color berry = new Color(0.92f, 0.24f, 0.28f, 1f);

        Vector3[] tufts =
        {
            new Vector3(w * 0.26f, h * 0.26f, w * 0.24f),
            new Vector3(w * 0.50f, h * 0.48f, w * 0.30f),
            new Vector3(w * 0.74f, h * 0.26f, w * 0.24f)
        };
        Vector2[] berries = { new Vector2(w * 0.34f, h * 0.36f), new Vector2(w * 0.55f, h * 0.56f), new Vector2(w * 0.66f, h * 0.30f) };

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float maxA = 0f;
                for (int i = 0; i < tufts.Length; i++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(tufts[i].x, tufts[i].y));
                    maxA = Mathf.Max(maxA, Mathf.Clamp01(tufts[i].z - d + 1f));
                }
                Color c = Color.clear;
                if (maxA > 0f)
                {
                    c = y > h * 0.42f ? leafLight : leafDark;
                    for (int b = 0; b < berries.Length; b++)
                    {
                        if (Vector2.Distance(new Vector2(x, y), berries[b]) < 3.6f) c = berry;
                    }
                    c.a = maxA;
                }
                pixels[y * w + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateLogSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        Color[] pixels = new Color[w * h];
        Color bark = new Color(0.46f, 0.28f, 0.14f, 1f);
        Color barkDark = new Color(0.32f, 0.18f, 0.09f, 1f);
        Color barkHi = new Color(0.60f, 0.40f, 0.22f, 1f);
        Color cut = new Color(0.86f, 0.68f, 0.44f, 1f);
        Color cutRing = new Color(0.68f, 0.48f, 0.26f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = Color.clear;
                if (x >= 3 && x <= w - 3 && y <= h - 4)
                {
                    c = y > h * 0.68f ? barkHi : (y < h * 0.20f ? barkDark : bark);
                    float cutDist = Vector2.Distance(new Vector2(x, y), new Vector2(w - 14f, h * 0.46f));
                    if (cutDist < h * 0.38f)
                    {
                        c = (cutDist > h * 0.16f && cutDist < h * 0.22f) ? cutRing : cut;
                    }
                }
                pixels[y * w + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateStormCloudSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        Color[] pixels = new Color[w * h];
        Color darkCloud = new Color(0.30f, 0.35f, 0.45f, 1f);
        Color topCloud = new Color(0.44f, 0.50f, 0.62f, 1f);
        Vector3[] puffs =
        {
            new Vector3(w * 0.28f, h * 0.48f, h * 0.28f),
            new Vector3(w * 0.50f, h * 0.60f, h * 0.34f),
            new Vector3(w * 0.72f, h * 0.48f, h * 0.28f)
        };
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float maxA = 0f;
                for (int i = 0; i < puffs.Length; i++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(puffs[i].x, puffs[i].y));
                    maxA = Mathf.Max(maxA, Mathf.Clamp01(puffs[i].z - d + 1f));
                }
                Color c = y > h * 0.55f ? topCloud : darkCloud;
                c.a = maxA;
                pixels[y * w + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateBranchSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        Color[] pixels = new Color[w * h];
        Color wood = new Color(0.44f, 0.26f, 0.14f, 1f);
        Color leaf = new Color(0.24f, 0.54f, 0.24f, 1f);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = Color.clear;
                if (x >= w * 0.38f && x <= w * 0.62f && y >= h * 0.25f)
                {
                    c = wood;
                }
                if (Vector2.Distance(new Vector2(x, y), new Vector2(w * 0.5f, h * 0.30f)) < w * 0.34f)
                {
                    c = leaf;
                }
                pixels[y * w + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    #endregion
}
