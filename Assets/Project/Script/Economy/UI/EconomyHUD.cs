using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Gốc giao diện kinh tế trên màn chơi: CoinHUD ở góc trên bên phải, InventoryUI ở góc trên bên trái,
/// Bảng Đơn Hàng (OrderBoardUI, nút "Đơn hàng" ngay dưới số Coins) và Cửa hàng thẻ (CardShopUI, nút "Cửa hàng" dưới nút "Đơn hàng").
/// Tự sinh khi vào scene có bản đồ lục giác (HexWorldGenerator) và gắn vào Canvas màn hình của scene đó,
/// nên không cần sửa Scene hay tạo prefab. Muốn tự đặt thì thêm script này vào một object con của Canvas
/// trong scene; khi đó game không sinh thêm bản thứ hai.
/// CoinHUD và InventoryUI chỉ để xem: không chặn chuột, kéo thẻ hay bấm bong bóng thu hoạch phía sau.
/// Kích thước giao diện kinh tế tính bằng pixel thật. Canvas của scene MapBuilding để Scale Factor 0,5
/// (khay bài tự bù bằng scale 2), nên HUD và hai bảng được phóng lại theo Canvas để không bị thu nhỏ một nửa.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class EconomyHUD : MonoBehaviour
{
    [Tooltip("Khoảng cách từ mép màn hình tới CoinHUD và InventoryUI (pixel)")]
    [SerializeField] private Vector2 screenMargin = new Vector2(20f, 20f);

    // Sprite vẽ bằng code, tạo một lần rồi dùng chung
    private static Sprite roundedSprite;
    private static Sprite coinSprite;
    private static Sprite circleSprite;

    // Cửa sổ của Bảng Đơn Hàng và Cửa hàng thẻ, để camera và thu hoạch biết đang có bảng mở
    private static readonly List<GameObject> windows = new List<GameObject>();
    private static readonly List<RaycastResult> pointerHits = new List<RaycastResult>();

    // Gốc của HUD và hai bảng, được phóng theo Scale Factor của Canvas
    private readonly List<RectTransform> pixelRoots = new List<RectTransform>();
    private Canvas canvas;
    private float fittedCanvasScale;
    private Vector2 fittedCanvasSize;

    private void Awake()
    {
        pixelRoots.Add((RectTransform)transform);

        if (GetComponentInChildren<CoinHUD>(true) == null)
        {
            CreateCorner<CoinHUD>("CoinHUD", new Vector2(1f, 1f), new Vector2(-screenMargin.x, -screenMargin.y));
        }

        if (GetComponentInChildren<InventoryUI>(true) == null)
        {
            CreateCorner<InventoryUI>("InventoryUI", new Vector2(0f, 1f), new Vector2(screenMargin.x, -screenMargin.y));
        }

        // Bảng Đơn Hàng và Cửa hàng thẻ cần bấm được và phải phủ lên thẻ bài, nên nằm riêng ở cuối Canvas (HUD này nằm đầu)
        CreateOverlay<OrderBoardUI>("OrderBoardUI");
        CreateOverlay<CardShopUI>("CardShopUI");

        FitToCanvas();
    }

    private void LateUpdate()
    {
        // Cửa sổ Game đổi cỡ hoặc Canvas đổi Scale Factor thì phóng lại
        FitToCanvas();
    }

    private T CreateCorner<T>(string objectName, Vector2 corner, Vector2 offset) where T : Component
    {
        RectTransform rect = CreateRect(objectName, transform);
        rect.anchorMin = corner;
        rect.anchorMax = corner;
        rect.pivot = corner;
        rect.anchoredPosition = offset;
        return rect.gameObject.AddComponent<T>();
    }

    private void CreateOverlay<T>(string objectName) where T : Component
    {
        if (transform.parent == null || FindAnyObjectByType<T>(FindObjectsInactive.Include) != null) return;

        RectTransform rect = CreateRect(objectName, transform.parent);
        Stretch(rect);
        rect.SetAsLastSibling();
        rect.gameObject.AddComponent<T>();
        pixelRoots.Add(rect);
    }

    private void FitToCanvas()
    {
        if (canvas == null)
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null) return;
            canvas = parentCanvas.rootCanvas;
        }

        float canvasScale = canvas.scaleFactor;
        Vector2 canvasSize = ((RectTransform)canvas.transform).rect.size;
        if (canvasScale <= 0f || canvasSize.x <= 0f || canvasSize.y <= 0f) return;
        if (canvasScale == fittedCanvasScale && canvasSize == fittedCanvasSize) return;

        fittedCanvasScale = canvasScale;
        fittedCanvasSize = canvasSize;

        // Mỗi gốc phủ kín parent (thường là Canvas), nhưng 1 đơn vị bên trong luôn bằng 1 pixel màn hình
        foreach (RectTransform root in pixelRoots)
        {
            if (root == null) continue;

            Vector2 area = root.parent is RectTransform parent ? parent.rect.size : canvasSize;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.zero;
            root.pivot = Vector2.zero;
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = area * canvasScale;
            root.localScale = new Vector3(1f / canvasScale, 1f / canvasScale, 1f);
        }
    }

    // =========================================================
    // CHO CAMERA VÀ THU HOẠCH BIẾT KHI NÀO KHÔNG ĐƯỢC PHẢN ỨNG VỚI CHUỘT
    // =========================================================

    /// <summary>
    /// Bảng Đơn Hàng hoặc Cửa hàng thẻ đang mở.
    /// </summary>
    public static bool IsWindowOpen
    {
        get
        {
            windows.RemoveAll(window => window == null);
            foreach (GameObject window in windows)
            {
                if (window.activeInHierarchy) return true;
            }
            return false;
        }
    }

    internal static void RegisterWindow(GameObject window)
    {
        if (window != null && !windows.Contains(window)) windows.Add(window);
    }

    /// <summary>
    /// Chuột đang chỉ vào UI bấm được (nút, thẻ bài, bong bóng thu hoạch...), hoặc đang mở Bảng Đơn Hàng hay Cửa hàng.
    /// Ảnh không bấm được thì không tính (như khung trong suốt của khay bài), nên vẫn thu hoạch được ô đất nằm sau nó.
    /// </summary>
    public static bool IsPointerOverInteractiveUI(Vector2 screenPosition)
    {
        if (IsWindowOpen) return true;

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return false;

        pointerHits.Clear();
        eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screenPosition }, pointerHits);

        // Giống EventSystem: chỉ vật trên cùng nhận cú bấm, nó hoặc object cha phải có hàm xử lý bấm hay kéo
        foreach (RaycastResult hit in pointerHits)
        {
            if (hit.gameObject == null) continue;

            return ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject) != null
                || ExecuteEvents.GetEventHandler<IPointerDownHandler>(hit.gameObject) != null
                || ExecuteEvents.GetEventHandler<IBeginDragHandler>(hit.gameObject) != null;
        }
        return false;
    }

    // =========================================================
    // TỰ SINH KHI VÀO MÀN CHƠI
    // =========================================================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallAutoSpawn()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        // Scene đầu tiên đã nạp xong trước khi hàm này chạy
        SpawnIfGameplayScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SpawnIfGameplayScene();
    }

    private static void SpawnIfGameplayScene()
    {
        if (FindAnyObjectByType<EconomyHUD>(FindObjectsInactive.Include) != null) return;

        HexWorldGenerator world = FindAnyObjectByType<HexWorldGenerator>();
        if (world == null) return;

        RectTransform root = CreateRect("EconomyHUD", FindScreenCanvas(world.gameObject.scene));
        Stretch(root);

        // Đứng đầu danh sách con của Canvas để thẻ đang kéo, điểm số và thông báo luôn vẽ đè lên HUD
        root.SetAsFirstSibling();
        root.gameObject.AddComponent<EconomyHUD>();
    }

    private static Transform FindScreenCanvas(Scene scene)
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas.isRootCanvas && canvas.renderMode != RenderMode.WorldSpace && canvas.gameObject.scene == scene)
            {
                return canvas.transform;
            }
        }

        // Scene chưa có Canvas màn hình: tạo một Canvas riêng cho HUD
        GameObject canvasObject = new GameObject("EconomyHUDCanvas", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);

        Canvas ownCanvas = canvasObject.AddComponent<Canvas>();
        ownCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        ownCanvas.sortingOrder = 50;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
        return canvasObject.transform;
    }

    // =========================================================
    // DỰNG UI DÙNG CHUNG CHO CoinHUD, InventoryUI, OrderBoardUI VÀ CardShopUI
    // =========================================================

    internal static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    internal static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// Nền bo tròn tối màu, cùng tông với banner nhận thẻ và bong bóng thu hoạch.
    /// </summary>
    internal static Image CreatePanel(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = RoundedSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>
    /// Icon vuông neo ở mép trái của parent, cách mép x pixel.
    /// </summary>
    internal static Image CreateIcon(string objectName, RectTransform parent, Sprite sprite, float size, float x)
    {
        RectTransform rect = CreateRect(objectName, parent);
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = new Vector2(x, 0f);

        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    // Font lấy từ TMP Settings mặc định của project (LiberationSans SDF), giống CardRewardToast.
    // Không đặt outline ở đây: TMP chưa có material trong frame vừa tạo component.
    internal static TextMeshProUGUI CreateText(string objectName, RectTransform parent, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(objectName, parent);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }

    /// <summary>
    /// Biến rect thành nút bấm bo tròn màu color: sáng lên khi rê chuột, tối đi khi bấm, xám khi tắt (interactable = false).
    /// </summary>
    internal static Button AddButton(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = RoundedSprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.raycastTarget = true;

        // Màu thật nằm trong ColorBlock để Button tự đổi màu theo trạng thái
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.25f);
        colors.selectedColor = color;
        colors.disabledColor = new Color(0.30f, 0.32f, 0.38f, 0.75f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        // Không giữ trạng thái "đang chọn" sau khi bấm, để nút không sáng mãi và phím Enter không bấm lại
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        // Lên màu ngay, không mờ dần từ màu trắng mặc định trong frame đầu
        image.CrossFadeColor(color, 0f, true, true);
        return button;
    }

    /// <summary>
    /// Số bay (+75, -10): trôi từ from tới to, hiện lên rồi mờ dần, xong tự hủy.
    /// Tọa độ tính từ điểm giữa cạnh dưới của parent.
    /// </summary>
    internal static IEnumerator FloatText(RectTransform parent, string text, Color color, float fontSize, Vector2 from, Vector2 to, float duration)
    {
        TextMeshProUGUI label = CreateText("FloatingNumber", parent, fontSize, color, TextAlignmentOptions.Center);
        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(160f, fontSize + 10f);
        rect.anchoredPosition = from;
        label.text = text;

        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            float eased = 1f - (1f - t) * (1f - t);
            rect.anchoredPosition = Vector2.Lerp(from, to, eased);

            float fadeIn = Mathf.Clamp01(t / 0.12f);
            float fadeOut = 1f - Mathf.Clamp01((t - 0.55f) / 0.45f);
            label.color = new Color(color.r, color.g, color.b, color.a * fadeIn * fadeOut);
            yield return null;
        }

        Destroy(label.gameObject);
    }

    /// <summary>
    /// Nảy nhẹ: phóng to tới peakScale rồi thu về kích thước chuẩn.
    /// </summary>
    internal static IEnumerator Punch(Transform target, float peakScale, float duration)
    {
        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float wave = Mathf.Sin(Mathf.Clamp01(time / duration) * Mathf.PI);
            float scale = Mathf.Lerp(1f, peakScale, wave);
            target.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }

        target.localScale = Vector3.one;
    }

    /// <summary>
    /// Xóa các số bay còn dở khi HUD bị ẩn (coroutine dừng nên chúng không tự hủy được).
    /// </summary>
    internal static void ClearChildren(Transform container)
    {
        if (container == null) return;

        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Destroy(container.GetChild(i).gameObject);
        }
    }

    // =========================================================
    // SPRITE VẼ BẰNG CODE (không cần file ảnh)
    // =========================================================

    /// <summary>
    /// Hình chữ nhật bo góc dạng 9-slice, tô màu bằng Image.color.
    /// </summary>
    internal static Sprite RoundedSprite
    {
        get
        {
            if (roundedSprite == null) roundedSprite = CreateRoundedSprite(64, 24);
            return roundedSprite;
        }
    }

    /// <summary>
    /// Đồng Lumi Coin màu vàng.
    /// </summary>
    internal static Sprite CoinSprite
    {
        get
        {
            if (coinSprite == null) coinSprite = CreateCoinSprite(64);
            return coinSprite;
        }
    }

    /// <summary>
    /// Hình tròn trắng, tô màu bằng Image.color (huy hiệu, ảnh khách).
    /// </summary>
    internal static Sprite CircleSprite
    {
        get
        {
            if (circleSprite == null) circleSprite = CreateCircleSprite(64);
            return circleSprite;
        }
    }

    private static Sprite CreateRoundedSprite(int size, int radius)
    {
        Texture2D texture = NewTexture(size);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Ở 4 góc: khoảng cách tới tâm cung bo; ở phần thân thẳng: 0
                float px = x + 0.5f;
                float py = y + 0.5f;
                float dx = Mathf.Max(radius - px, px - (size - radius), 0f);
                float dy = Mathf.Max(radius - py, py - (size - radius), 0f);
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius - distance + 0.5f));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        Vector4 border = new Vector4(radius, radius, radius, radius);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    private static Sprite CreateCoinSprite(int size)
    {
        Texture2D texture = NewTexture(size);
        Color[] pixels = new Color[size * size];

        Color rim = new Color(0.80f, 0.52f, 0.10f, 1f);
        Color face = new Color(1.00f, 0.80f, 0.24f, 1f);
        Color groove = new Color(0.90f, 0.64f, 0.14f, 1f);
        Color shine = new Color(1.00f, 0.96f, 0.75f, 1f);

        float center = size * 0.5f;
        float radius = center - 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - center;
                float dy = y + 0.5f - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float r = distance / radius;

                // Vành ngoài đậm, mặt vàng và một rãnh tròn bên trong như đồng xu thật
                Color color = Color.Lerp(face, rim, Mathf.Clamp01((r - 0.80f) * 12f));
                color = Color.Lerp(color, groove, 1f - Mathf.Clamp01(Mathf.Abs(r - 0.62f) * 18f));

                // Vệt sáng nhỏ ở góc trên bên trái
                float sx = dx + radius * 0.30f;
                float sy = dy - radius * 0.30f;
                float shineAmount = 1f - Mathf.Clamp01(Mathf.Sqrt(sx * sx + sy * sy) / (radius * 0.22f));
                color = Color.Lerp(color, shine, shineAmount * 0.8f);

                color.a = Mathf.Clamp01(radius - distance + 0.5f);
                pixels[y * size + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateCircleSprite(int size)
    {
        Texture2D texture = NewTexture(size);
        Color[] pixels = new Color[size * size];

        float center = size * 0.5f;
        float radius = center - 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - center;
                float dy = y + 0.5f - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius - distance + 0.5f));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Texture2D NewTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
    }
}
