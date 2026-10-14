using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Quản lý tập trung toàn bộ hệ thống Minigame trong LumiWorld (Facade & Strategy Pattern):
/// 1. Tách biệt hoàn toàn việc kích hoạt Minigame khỏi các logic gameplay cụ thể (WildAlphaInteractable, NPC...).
/// 2. Quản lý vòng đời Canvas Overlay, Backdrop chặn click xuyên thế giới 3D, bắt phím ESC.
/// 3. Điều phối phiên chơi đến đúng Minigame Engine/View tương ứng qua Interface IMinigameSession.
/// 4. Đảm bảo nguyên lý Open-Closed: Thêm minigame mới chỉ cần đăng ký loại session mới.
/// </summary>
public class MinigameManager : MonoBehaviour
{
    private static MinigameManager _instance;
    public static MinigameManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<MinigameManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("MinigameManager");
                    _instance = go.AddComponent<MinigameManager>();
                }
            }
            return _instance;
        }
    }

    private GameObject activeModalCanvasObj;
    private IMinigameSession activeSession;

    public bool IsMinigameActive => activeSession != null && activeSession.IsActive;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        // Nhấn phím ESC để đóng Minigame nếu đang mở
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (IsMinigameActive)
            {
                CloseMinigame();
            }
        }
    }

    /// <summary>
    /// Kích hoạt một phiên Minigame bất kỳ thông qua ngữ cảnh (Context) và Callback nhận kết quả
    /// </summary>
    public void StartMinigame(MinigameType type, MinigameContext context, Action<MinigameResult> onComplete)
    {
        // Đóng minigame cũ nếu có
        CloseMinigame();

        // 1. Tạo Canvas Overlay độc lập
        activeModalCanvasObj = new GameObject("Minigame_ModalCanvas");
        Canvas canvas = activeModalCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1005; // Đè lên trên các UI khác

        CanvasScaler scaler = activeModalCanvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        activeModalCanvasObj.AddComponent<GraphicRaycaster>();

        // Đăng ký với EconomyHUD để chặn kéo rê camera thế giới 3D phía sau
        EconomyHUD.RegisterWindow(activeModalCanvasObj, CloseMinigame);

        // 2. Lớp nền mờ (Backdrop)
        GameObject backdrop = new GameObject("Backdrop");
        backdrop.transform.SetParent(activeModalCanvasObj.transform, false);
        Image bgImg = backdrop.AddComponent<Image>();
        bgImg.color = new Color(0.10f, 0.14f, 0.12f, 0.65f);
        RectTransform bgRect = backdrop.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        Button bgBtn = backdrop.AddComponent<Button>();
        bgBtn.onClick.AddListener(CloseMinigame);

        // 3. Khởi tạo Component Minigame tương ứng theo Strategy Pattern
        switch (type)
        {
            case MinigameType.RhythmResonance:
            default:
                var rhythmView = activeModalCanvasObj.AddComponent<RhythmMinigameView>();
                activeSession = rhythmView;
                break;

            case MinigameType.RockPaperScissors:
                // Sẵn sàng gắn RockPaperScissorsView khi triển khai Minigame 1
                Debug.LogWarning("[MinigameManager] Minigame Kéo Búa Bao đang được phát triển, tạm thời dùng Vũ Điệu Hòa Nhịp.");
                var fallbackView = activeModalCanvasObj.AddComponent<RhythmMinigameView>();
                activeSession = fallbackView;
                break;
        }

        // Bắt đầu phiên chơi
        activeSession.StartSession(context, (result) =>
        {
            CloseMinigame();
            onComplete?.Invoke(result);
        });
    }

    /// <summary>
    /// Đóng và dọn dẹp minigame an toàn
    /// </summary>
    public void CloseMinigame()
    {
        if (activeSession != null)
        {
            activeSession.CancelSession();
            activeSession = null;
        }

        if (activeModalCanvasObj != null)
        {
            EconomyHUD.UnregisterWindow(activeModalCanvasObj);
            EconomyHUD.MarkWindowClosed();
            Destroy(activeModalCanvasObj);
            activeModalCanvasObj = null;
        }
    }

    private void OnDestroy()
    {
        CloseMinigame();
        if (_instance == this) _instance = null;
    }
}
