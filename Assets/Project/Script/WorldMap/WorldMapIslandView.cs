using TMPro;
using UnityEngine;

/// <summary>
/// Một hòn đảo trên WorldMap: hình tượng trưng, bảng tên và điểm bên dưới, vòng sáng khi là đảo của mình
/// hoặc đang được chọn. Đảo hoang (ô trống) chỉ có hình mờ, không có collider nên không bấm được.
/// </summary>
public class WorldMapIslandView : MonoBehaviour
{
    private static readonly Color OwnRingColor = new Color(1f, 0.85f, 0.3f, 0.95f);
    private static readonly Color SelectedRingColor = new Color(1f, 1f, 1f, 0.95f);
    private static readonly Color LabelColor = new Color(0.25f, 0.17f, 0.08f, 1f);
    private static readonly Color PlateColor = new Color(0.98f, 0.93f, 0.8f, 0.95f);

    public WorldMapIsland Island { get; private set; }
    public bool IsOwn { get; private set; }

    private SpriteRenderer ringRenderer;
    private bool selected;

    public static WorldMapIslandView CreatePlayer(Transform parent, WorldMapIsland island, IslandArtSet art, bool own)
    {
        int tier = art != null ? art.TierOf(island.score) : DefaultTier(island.score);
        Sprite sprite = IslandSprites.ForPlayer(art, island.playerId, tier);

        float scale = IslandSprites.ScaleOfTier(tier);
        var view = Create(parent, "Island " + island.x + ":" + island.y, island.x, island.y, sprite, scale, 1f);
        view.Island = island;
        view.IsOwn = own;

        // Collider theo cỡ hình để bấm trúng phần đảo, không trúng biển xung quanh
        var collider = view.gameObject.AddComponent<CircleCollider2D>();
        collider.radius = sprite.bounds.extents.x * scale * 0.8f;

        view.ringRenderer = AddRenderer(view.transform, "Ring", IslandSprites.Ring(art), 0);
        view.ringRenderer.transform.localScale = Vector3.one * scale * 1.15f;
        view.AddLabel(island.displayName + "  ·  " + island.score + " pts", -sprite.bounds.extents.y * scale * 0.75f);
        view.RefreshRing();
        return view;
    }

    public static WorldMapIslandView CreateWild(Transform parent, int x, int y, IslandArtSet art)
    {
        return Create(parent, "Wild " + x + ":" + y, x, y, IslandSprites.ForWild(art, x, y), 0.6f, 0.55f);
    }

    public void SetSelected(bool value)
    {
        selected = value;
        RefreshRing();
    }

    private void RefreshRing()
    {
        if (ringRenderer == null) return;
        ringRenderer.enabled = selected || IsOwn;
        ringRenderer.color = selected ? SelectedRingColor : OwnRingColor;
    }

    private static WorldMapIslandView Create(Transform parent, string name, int x, int y, Sprite sprite, float scale, float alpha)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = WorldMapLayout.SlotPosition(x, y);

        var body = AddRenderer(go.transform, "Body", sprite, 1);
        body.transform.localScale = Vector3.one * scale;
        body.color = new Color(1f, 1f, 1f, alpha);

        return go.AddComponent<WorldMapIslandView>();
    }

    private static SpriteRenderer AddRenderer(Transform parent, string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
    }

    // Bảng tên kiểu Ikariam: nền giấy nhạt, chữ nâu, nằm dưới đảo.
    // TextMeshPro 3D đo cỡ chữ theo đơn vị thế giới (fontSize 10 cao khoảng 1 đơn vị), nên dùng cỡ lớn hơn UI.
    private void AddLabel(string text, float offsetY)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, offsetY - 0.3f, 0f);

        var label = go.AddComponent<TextMeshPro>();
        label.text = text;
        label.fontSize = 6f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = LabelColor;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.rectTransform.sizeDelta = new Vector2(12f, 1f);
        label.sortingOrder = 4;

        // Nền giấy vừa khít chữ, nằm ngay dưới lớp chữ
        Vector2 size = label.GetPreferredValues(text);
        var plate = AddRenderer(go.transform, "Plate", PlateSprite(), 3);
        plate.color = PlateColor;
        plate.transform.localScale = new Vector3(size.x + 0.4f, size.y + 0.12f, 1f);
    }

    private static Sprite plateSprite;

    // Sprite trắng 1x1 đơn vị, kéo giãn theo cỡ chữ
    private static Sprite PlateSprite()
    {
        if (plateSprite != null) return plateSprite;
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "IslandPlate", filterMode = FilterMode.Point };
        var pixels = new Color32[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        texture.SetPixels32(pixels);
        texture.Apply();
        plateSprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        return plateSprite;
    }

    private static int DefaultTier(int score)
    {
        if (score < 50) return 0;
        if (score < 200) return 1;
        if (score < 600) return 2;
        return 3;
    }
}
