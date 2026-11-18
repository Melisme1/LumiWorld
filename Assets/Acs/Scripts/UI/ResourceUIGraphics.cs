using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LumiWorld.Acs
{
    public static class ResourceUIGraphics
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
        }

        public static Image Image(RectTransform rect, Sprite sprite, bool sliced = false, float ppu = 1, bool blocking = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite ?? (sliced ? LumiWorldUITheme.SurfaceSprite : null);
            image.color = sliced ? LumiWorldUITheme.Surface : Color.white;
            image.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.pixelsPerUnitMultiplier = ppu;
            image.preserveAspect = !sliced;
            image.raycastTarget = blocking;
            if (blocking) rect.gameObject.AddComponent<ResourceUIInputBlocker>();
            return image;
        }

        public static Image Icon(Transform parent, string name, Sprite sprite, float x, float y, float size)
        {
            var rect = Rect(name, parent);
            Place(rect, x, y, size, size);
            return Image(rect, sprite);
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string value, ResourceInventoryUISkin skin,
            float x, float y, float w, float h, float size = 16, Color? color = null, bool bold = false)
        {
            var rect = Rect(name, parent);
            Place(rect, x, y, w, h);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            var font = bold ? LumiWorldUITheme.Heading : LumiWorldUITheme.Body;
            label.font = font != null ? font : (skin != null ? skin.font : null);
            label.text = value;
            label.fontSize = size;
            label.color = color ?? LumiWorldUITheme.Text;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }

        public static Button Button(Transform parent, string name, string label, ResourceInventoryUISkin skin,
            float x, float y, float w, float h, UnityAction action, bool primary = false)
        {
            var rect = Rect(name, parent);
            Place(rect, x, y, w, h);
            var bgSprite = skin != null ? (primary ? skin.buttonPrimary : skin.buttonSecondary) : null;
            var image = Image(rect, bgSprite ?? LumiWorldUITheme.SurfaceSprite, true, skin != null ? skin.buttonPPU : 1f, true);
            image.color = primary ? LumiWorldUITheme.Primary : LumiWorldUITheme.Inset;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.94f, 0.94f, 0.94f, 1f);
            colors.pressedColor = new Color(0.84f, 0.84f, 0.84f, 1f);
            colors.disabledColor = new Color(0.72f, 0.72f, 0.72f, 0.65f);
            button.colors = colors;
            if (action != null) button.onClick.AddListener(action);
            var text = Text(rect, "Label", label, skin, 10, 0, w - 20, h, 16, primary ? Color.white : LumiWorldUITheme.Text, true);
            text.alignment = TextAlignmentOptions.Center;
            return button;
        }

        public static Button Round(Transform parent, string name, Sprite icon, ResourceInventoryUISkin skin,
            float x, float y, float size, UnityAction action)
        {
            var rect = Rect(name, parent);
            Place(rect, x, y, size, size);
            var bgSprite = skin != null && skin.buttonIcon != null ? skin.buttonIcon : LumiWorldUITheme.SurfaceSprite;
            var image = Image(rect, bgSprite, true, 1f, true);
            image.color = LumiWorldUITheme.Inset;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.93f, 0.92f, 0.90f, 1f);
            colors.pressedColor = new Color(0.84f, 0.82f, 0.79f, 1f);
            button.colors = colors;
            if (action != null) button.onClick.AddListener(action);
            var ic = Icon(rect, "Icon", icon ?? LumiWorldUITheme.Glyph("Close"), size * 0.22f, size * 0.22f, size * 0.56f);
            ic.color = LumiWorldUITheme.Text;
            ic.raycastTarget = false;
            return button;
        }

        public static RectTransform Scroll(Transform parent, string name, float x, float y, float w, float h)
        {
            var area = Rect(name, parent);
            Place(area, x, y, w, h);
            var scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;
            var viewport = Rect("Viewport", area);
            Stretch(viewport);
            viewport.offsetMax = new Vector2(-10, 0);
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            viewport.gameObject.AddComponent<ResourceUIInputBlocker>();
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport);
            Place(content, 0, 0, w - 10, h);
            scroll.viewport = viewport;
            scroll.content = content;
            var track = Rect("Scrollbar", area);
            Place(track, w - 7, 0, 5, h);
            var background = track.gameObject.AddComponent<Image>();
            background.color = new Color(0.46f, 0.42f, 0.38f, 0.14f);
            var handle = Rect("Handle", track);
            Stretch(handle);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(0.46f, 0.42f, 0.38f, 0.48f);
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle;
            bar.targetGraphic = handleImage;
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        public static void Clear(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(child);
                else UnityEngine.Object.DestroyImmediate(child);
            }
        }
    }
}
