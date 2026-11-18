using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LumiWorld.Acs.Review
{
    // Approval sandbox only. No economy, save, inventory or production services are called.
    public sealed class LumiWorldUIReview : MonoBehaviour
    {
        [Serializable] public sealed class Item
        {
            public string name, category;
            public Sprite art;
            public int quantity;
        }
        public TMP_FontAsset font;
        public Sprite rounded;
        public Item[] items;
        public bool crafting;
        private string category = "Ingredients";
        private int selected, amount = 1, queued;
        private bool queueOpen, insufficient;
        private RectTransform page;
        private readonly Color ink = Hex("44372D"), muted = Hex("887E72"), graphite = Hex("494A47");
        private readonly Color paper = Hex("F7F5F1"), inset = Hex("E7E3DD"), white = Hex("FEFCF8");

        private static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var c); return c; }
        private void Start() { Build(); }
        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current == null || !UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame) return;
            var modal = transform.Find("Modal dismiss");
            if (modal != null) Destroy(modal.gameObject);
        }

        public void Build()
        {
            if (items == null || items.Length == 0) return;
            if (crafting && category == "Ingredients") category = "Food";
            if (!crafting && category == "Product") category = "Products";
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            var backdrop = Box((RectTransform)transform, "Backdrop", 0, 0, 1600, 900, Hex("739594"), false);
            Label(backdrop, "Review caption", "LUMIWORLD  /  UI REVIEW", 46, 22, 900, 30, 15, white);
            Label(backdrop, "Review data", "Sample data · no changes to your game", 46, 851, 800, 26, 15, white);
            Button(backdrop, "Reset preview", 1310, 844, 244, 38, () => { queued = 0; amount = 1; insufficient = false; Build(); }, false);
            page = Box(backdrop, "Window", 95, 70, 1410, 758, paper);
            Button(page, "Inventory", 32, 22, 240, 62, () => { crafting = false; category = "Ingredients"; selected = 0; Build(); }, !crafting);
            Button(page, "Crafting", 284, 22, 240, 62, () => { crafting = true; category = "Food"; amount = 1; Build(); }, crafting);
            Button(page, "×", 1328, 23, 52, 52, () => { page.gameObject.SetActive(false); Button(backdrop, "Open inventory", 640, 400, 320, 64, Build, true); }, false);
            Box(page, "Header divider", 32, 104, 1346, 2, inset, false);
            if (crafting) Crafting(); else Inventory();
        }

        private void Inventory()
        {
            string[] tabs = { "Ingredients", "Food", "Medicine", "Products" };
            for (int i = 0; i < tabs.Length; i++)
            {
                string tab = tabs[i];
                Button(page, tab, 32 + i * 230, 126, 218, 54, () => { category = tab; selected = 0; Build(); }, category == tab, true);
            }
            var filtered = new List<Item>();
            foreach (var item in items) if (item.category == category) filtered.Add(item);
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, filtered.Count - 1));
            for (int i = 0; i < filtered.Count; i++)
            {
                int index = i; var item = filtered[i];
                float x = 32 + i % 6 * 151, y = 202 + i / 6 * 154;
                var border = Box(page, item.name + " slot", x, y, 139, 142, i == selected ? graphite : white);
                var slot = Box(border, "Surface", 3, 3, 133, 136, Hex("EFEBE5"));
                Art(slot, item.art, 10, 5, 113, 104);
                Label(slot, "Quantity", "×" + item.quantity, 6, 105, 119, 28, 23, ink, TextAlignmentOptions.Right);
                var button = border.gameObject.AddComponent<Button>(); button.targetGraphic = border.GetComponent<Image>();
                button.onClick.AddListener(() => { selected = index; Build(); });
            }
            var detail = Box(page, "Item detail", 970, 126, 408, 600, white);
            if (filtered.Count == 0)
            {
                Label(page, "Empty", "No items in this category", 65, 315, 820, 40, 26, muted, TextAlignmentOptions.Center);
                Label(detail, "Empty detail", "Your items will appear here", 30, 260, 348, 60, 22, muted, TextAlignmentOptions.Center);
                return;
            }
            var focus = filtered[selected];
            var artArea = Box(detail, "Open recipe artwork", 24, 25, 360, 270, paper);
            Art(artArea, focus.art, 50, 15, 260, 240);
            bool hasRecipe = focus.name == "Oak Wood" || focus.category != "Ingredients";
            if (hasRecipe)
            {
                var button = artArea.gameObject.AddComponent<Button>(); button.targetGraphic = artArea.GetComponent<Image>();
                button.onClick.AddListener(() => RecipePopover(focus));
                var colors = button.colors; colors.highlightedColor = Hex("E6E7DB"); button.colors = colors;
            }
            Label(detail, "Item name", focus.name, 30, 313, 348, 90, 36, ink);
            Label(detail, "Category", focus.category, 30, 410, 348, 30, 22, muted);
            Box(detail, "Divider", 30, 462, 348, 2, inset, false);
            Label(detail, "Description", focus.name == "Oak Wood" ? "A sturdy forest material.\nUsed to make Bark Paper." : "Collected and stored for life on your island.", 30, 482, 348, 90, 22, muted);
        }

        private void Crafting()
        {
            string[] cats = { "Food", "Medicine", "Product" };
            for (int i = 0; i < cats.Length; i++)
            {
                string cat = cats[i];
                Button(page, cat, 32 + i * 222, 128, 210, 50, () => { category = cat; amount = 1; Build(); }, category == cat, true);
            }
            string target = category == "Food" ? "Roasted Acorns" : category == "Medicine" ? "Forest Herbal Syrup" : "Bark Paper";
            var product = Find(target);
            var recipe = Box(page, "Recipe row", 32, 198, 668, 108, white);
            Art(recipe, product.art, 12, 8, 92, 92);
            Label(recipe, "Recipe name", target, 119, 20, 280, 64, 25, ink);
            Box(recipe, "Selected", 0, 0, 5, 108, graphite);
            var detail = Box(page, "Recipe detail", 724, 128, 594, 594, white);
            Art(detail, product.art, 195, 8, 200, 180);
            Label(detail, "Recipe title", target, 30, 192, 534, 54, 33, ink, TextAlignmentOptions.Center);
            Label(detail, "Ingredients heading", "Ingredients", 30, 263, 534, 30, 23, muted);
            var ingredient = Find(category == "Food" ? "Acorns" : category == "Medicine" ? "Tender Leaves" : "Oak Wood");
            var row = Box(detail, "Ingredient", 30, 307, 534, 86, paper);
            var image = Art(row, ingredient.art, 8, 3, 80, 80);
            var ingredientButton = image.gameObject.AddComponent<Button>(); ingredientButton.targetGraphic = image;
            ingredientButton.onClick.AddListener(() => IngredientPopover(ingredient));
            Label(row, "Ingredient name", ingredient.name, 103, 20, 290, 45, 24, ink);
            bool enough = !insufficient && ingredient.quantity >= 2 * amount;
            Label(row, "Requirement", (insufficient ? "1" : ingredient.quantity.ToString()) + "/" + (2 * amount), 397, 20, 120, 45, 25, enough ? ink : Hex("A8473E"), TextAlignmentOptions.Right);
            Label(detail, "Time", "00:30 / item", 30, 413, 300, 32, 21, muted);
            Button(detail, "−", 30, 475, 62, 64, () => { amount = Mathf.Max(1, amount - 1); Build(); }, false);
            Label(detail, "Amount", amount.ToString(), 96, 475, 60, 64, 26, ink, TextAlignmentOptions.Center);
            Button(detail, "+", 160, 475, 62, 64, () => { amount = Mathf.Min(9, amount + 1); Build(); }, false);
            var craft = Button(detail, "Craft", 247, 475, 317, 64, () => { queued += amount; queueOpen = true; Build(); }, true);
            craft.interactable = enough;
            Button(page, "Queue (" + queued + ")", 1110, 23, 200, 52, () => { queueOpen = !queueOpen; Build(); }, false, false, 22);
            Button(page, insufficient ? "Preview: insufficient" : "Preview: available", 32, 650, 650, 50, () => { insufficient = !insufficient; Build(); }, false);
            if (queueOpen)
            {
                var drawer = Box(page, "Queue drawer", 1020, 118, 365, 610, inset);
                Label(drawer, "Queue title", "Crafting queue", 24, 20, 255, 45, 28, ink);
                Button(drawer, "×", 299, 20, 44, 44, () => { queueOpen = false; Build(); }, false);
                Label(drawer, "Queue state", queued == 0 ? "Your queue is empty" : queued + " items queued\nPreview only", 24, 100, 310, 110, 23, muted);
                if (queued > 0) Button(drawer, "Clear preview queue", 24, 507, 317, 62, () => { queued = 0; Build(); }, false);
            }
        }

        private Item Find(string name) { foreach (var item in items) if (item.name == name) return item; return items[0]; }
        private RectTransform Modal(string title)
        {
            var overlay = Box((RectTransform)transform, "Modal dismiss", 0, 0, 1600, 900, new Color(0, 0, 0, .28f), false);
            var dismiss = overlay.gameObject.AddComponent<Button>(); dismiss.targetGraphic = overlay.GetComponent<Image>();
            dismiss.onClick.AddListener(() => Destroy(overlay.gameObject));
            var panel = Box(overlay, "Popover", 520, 250, 560, 390, white);
            // Consume clicks inside without triggering the backdrop button.
            panel.gameObject.AddComponent<Button>().targetGraphic = panel.GetComponent<Image>();
            Label(panel, "Title", title, 30, 24, 450, 55, 30, ink);
            Button(panel, "×", 482, 25, 48, 48, () => Destroy(overlay.gameObject), false);
            return panel;
        }
        private void RecipePopover(Item item)
        {
            var modal = Modal(item.name);
            Art(modal, item.art, 30, 100, 150, 150);
            Label(modal, "Usage", item.name == "Oak Wood" ? "Used in\nBark Paper" : "Crafting recipe", 205, 110, 310, 100, 25, ink);
            Button(modal, "Open crafting", 30, 296, 500, 60, () => { crafting = true; category = item.name == "Oak Wood" || item.category == "Products" ? "Product" : item.category; Build(); }, true);
        }
        private void IngredientPopover(Item item)
        {
            var modal = Modal(item.name); Art(modal, item.art, 30, 100, 180, 180);
            Label(modal, "Source", "Source\nLeafwood\n\n" + item.quantity + " available", 240, 120, 280, 180, 24, ink);
        }
        private RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); return rect;
        }
        private RectTransform Box(Transform parent, string name, float x, float y, float w, float h, Color color, bool round = true)
        {
            var rect = Rect(parent, name, x, y, w, h); var image = rect.gameObject.AddComponent<Image>();
            image.color = color; if (round) { image.sprite = rounded; image.type = Image.Type.Sliced; } return rect;
        }
        private Image Art(Transform parent, Sprite sprite, float x, float y, float w, float h)
        {
            var image = Rect(parent, "Artwork", x, y, w, h).gameObject.AddComponent<Image>(); image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = true; return image;
        }
        private TextMeshProUGUI Label(Transform parent, string name, string value, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var text = Rect(parent, name, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.text = value;
            text.fontSize = size; text.color = color; text.alignment = alignment; text.fontWeight = FontWeight.SemiBold; text.raycastTarget = false; return text;
        }
        private Button Button(Transform parent, string label, float x, float y, float w, float h, UnityEngine.Events.UnityAction action, bool primary, bool tab = false, float fontSize = 26)
        {
            var rect = Box(parent, label, x, y, w, h, primary ? (tab ? white : graphite) : inset);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>(); button.onClick.AddListener(action);
            var colors = button.colors; colors.highlightedColor = Hex("E2E0D8"); colors.pressedColor = Hex("C9C5BA"); colors.disabledColor = Hex("A5A29D"); button.colors = colors;
            Label(rect, "Label", label, 8, 0, w - 16, h, fontSize, primary && !tab ? white : ink, TextAlignmentOptions.Center); return button;
        }
    }
}
