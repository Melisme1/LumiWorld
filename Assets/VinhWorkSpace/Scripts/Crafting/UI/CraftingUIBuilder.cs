using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LumiWorld.Acs;

namespace LumiWorld.Economy.Crafting
{
    public class RecipeCardView
    {
        public CraftingRecipeData recipe;
        public RectTransform root;
        public Image background;
        public Image selectedBorder;
        public Image icon;
        public TextMeshProUGUI name;
        public TextMeshProUGUI status;
        public Image statusIcon;
        public Button button;
    }

    public class QueueCardView
    {
        public ActiveCraftingJob job;
        public RectTransform root;
        public Image icon;
        public TextMeshProUGUI title;
        public Image statePill;
        public Image readyIcon;
        public TextMeshProUGUI statePillText;
        public TextMeshProUGUI status;
        public GameObject progressBarTrack;
        public RectTransform progressBarFill;
        public Button claimButton;
        public TextMeshProUGUI claimLabel;
    }

    public struct CraftingUIStyleConfig
    {
        public Vector2 buttonCorner;
        public Vector2 buttonSize;
        public Color hudColor;
        public Color backdropColor;
        public Color panelColor;
        public Color cardColor;
        public Color selectedCardColor;
        public Color sectionColor;
        public Color slotColor;
        public Color textColor;
        public Color mutedColor;
        public Color enoughColor;
        public Color missingColor;
        public Color accentColor;
        public Color secondaryColor;
        public Color tabActiveColor;
        public Color tabInactiveColor;
    }

    public class CraftingUIBindings
    {
        public GameObject window;
        public RectTransform panel;

        public RectTransform leftColumn;
        public RectTransform tabBar;
        public RectTransform rightInspector;
        public RectTransform queueDrawer;
        public RectTransform ingredientPopover;

        public readonly List<(CraftingUI.TabFilter tab, Button btn, Image bg, Image border, Image icon, TextMeshProUGUI label)> tabButtons =
            new List<(CraftingUI.TabFilter, Button, Image, Image, Image, TextMeshProUGUI)>();

        public ScrollRect recipeScrollRect;
        public RectTransform recipeListContent;
        public readonly List<RecipeCardView> recipeCards = new List<RecipeCardView>();

        public Image inspectorIcon;
        public TextMeshProUGUI inspectorTitle;
        public TextMeshProUGUI inspectorOutputText;
        public RectTransform ingredientsContainer;
        public TextMeshProUGUI durationText;
        public TextMeshProUGUI quantityText;
        public Button btnDecrease;
        public Button btnIncrease;
        public Button btnStartCraft;
        public Image btnStartCraftBg;
        public Image btnStartCraftIcon;
        public TextMeshProUGUI btnStartCraftLabel;

        public Button queueToggleButton;
        public Image queueToggleBg;
        public Image queueToggleIcon;
        public TextMeshProUGUI queueToggleLabel;

        public ScrollRect queueScrollRect;
        public RectTransform queueListContent;
        public Button btnClaimAll;
        public TextMeshProUGUI btnClaimAllLabel;
    }

    /// <summary>
    /// Builds the P-01 / P-02 / P-15 Crafting UI window (warm neutral P-00 visual master).
    /// </summary>
    public static class CraftingUIBuilder
    {
        public const float PanelWidth = 1080f;
        public const float PanelHeight = 640f;

        public static CraftingUIBindings BuildAll(
            Transform parentTransform,
            CraftingUIStyleConfig style,
            Action onSwitchToInventory,
            Action onClose,
            Action onToggleQueue,
            Action<CraftingUI.TabFilter> onTabSelected,
            Action<CraftingRecipeData> onSelectRecipe,
            Action<int> onAdjustQty,
            Action onStartCraft,
            Action onClaimAll)
        {
            var b = new CraftingUIBindings();
            BuildWindow(
                parentTransform,
                style,
                onSwitchToInventory,
                onClose,
                onToggleQueue,
                onTabSelected,
                onSelectRecipe,
                onAdjustQty,
                onStartCraft,
                onClaimAll,
                b);
            return b;
        }

        private static void BuildWindow(
            Transform parent,
            CraftingUIStyleConfig style,
            Action onSwitchToInventory,
            Action onClose,
            Action onToggleQueue,
            Action<CraftingUI.TabFilter> onTabSelected,
            Action<CraftingRecipeData> onSelectRecipe,
            Action<int> onAdjustQty,
            Action onStartCraft,
            Action onClaimAll,
            CraftingUIBindings b)
        {
            RectTransform windowRect = EconomyHUD.CreateRect("Window", parent);
            EconomyHUD.Stretch(windowRect);
            b.window = windowRect.gameObject;
            EconomyHUD.RegisterWindow(b.window, onClose);

            // Backdrop
            RectTransform backdropRect = EconomyHUD.CreateRect("Backdrop", windowRect);
            EconomyHUD.Stretch(backdropRect);
            Image backdrop = backdropRect.gameObject.AddComponent<Image>();
            backdrop.color = LumiWorldUITheme.Backdrop;
            Button backdropBtn = backdropRect.gameObject.AddComponent<Button>();
            backdropBtn.transition = Selectable.Transition.None;
            backdropBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            backdropBtn.onClick.AddListener(() => onClose());

            // Main Window Panel
            b.panel = EconomyHUD.CreateRect("Panel", windowRect);
            b.panel.anchorMin = b.panel.anchorMax = b.panel.pivot = new Vector2(0.5f, 0.5f);
            b.panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            Image panelBg = EconomyHUD.CreatePanel(b.panel, LumiWorldUITheme.Window);
            panelBg.raycastTarget = true;
            LumiWorldUITheme.AddSoftElevation(b.panel.gameObject, 6f, 0.22f);
            EconomyHUD.AddBlueBorder(b.panel, LumiWorldUITheme.Edge);

            BuildHeader(b.panel, onSwitchToInventory, onClose, onToggleQueue, b);
            BuildLeftColumn(b.panel, style, onTabSelected, onSelectRecipe, b);
            BuildRightInspector(b.panel, style, onAdjustQty, onStartCraft, b);
            BuildQueueDrawer(b.panel, style, onToggleQueue, onClaimAll, b);

            ApplyQueueLayout(b, false);
            b.window.SetActive(false);
        }

        public static void ApplyQueueLayout(CraftingUIBindings b, bool queueOpen)
        {
            if (b == null) return;
            if (b.queueDrawer != null)
                b.queueDrawer.gameObject.SetActive(queueOpen);

            if (b.leftColumn != null)
            {
                b.leftColumn.anchoredPosition = new Vector2(24f, -86f);
                b.leftColumn.sizeDelta = new Vector2(queueOpen ? 344f : 428f, 528f);
            }

            if (b.rightInspector != null)
            {
                b.rightInspector.anchoredPosition = new Vector2(queueOpen ? 380f : 466f, -86f);
                b.rightInspector.sizeDelta = new Vector2(queueOpen ? 344f : 590f, 528f);
            }

            if (b.queueDrawer != null)
            {
                b.queueDrawer.anchoredPosition = new Vector2(736f, -86f);
                b.queueDrawer.sizeDelta = new Vector2(320f, 528f);
            }

            if (b.queueToggleBg != null)
                b.queueToggleBg.color = queueOpen ? LumiWorldUITheme.Primary : LumiWorldUITheme.Inset;
            if (b.queueToggleLabel != null)
                b.queueToggleLabel.color = queueOpen ? Color.white : LumiWorldUITheme.Text;
            if (b.queueToggleIcon != null)
                b.queueToggleIcon.color = queueOpen ? Color.white : LumiWorldUITheme.Text;
        }

        private static void BuildHeader(
            RectTransform panel,
            Action onSwitchToInventory,
            Action onClose,
            Action onToggleQueue,
            CraftingUIBindings b)
        {
            // Top Header Pill: [G-02 Crafting] only (synchronized with Inventory P-00 header)
            BuildTopModePill(panel, "HeaderCraftingBtn", "Crafting", LumiWorldUITheme.Glyph("G-02"), 28f, -14f, 216f, 50f, true, null);

            // Queue Toggle Button immediately left of [×] (taller, squarer, soft elevated)
            RectTransform queueRect = EconomyHUD.CreateRect("QueueToggle", panel);
            queueRect.anchorMin = queueRect.anchorMax = new Vector2(0f, 1f);
            queueRect.pivot = new Vector2(0f, 1f);
            queueRect.anchoredPosition = new Vector2(PanelWidth - 264f, -16f);
            queueRect.sizeDelta = new Vector2(180f, 46f);

            b.queueToggleBg = EconomyHUD.CreatePanel(queueRect, LumiWorldUITheme.Inset);
            b.queueToggleBg.raycastTarget = true;
            LumiWorldUITheme.AddSoftElevation(queueRect.gameObject, 2.5f, 0.14f);

            b.queueToggleButton = queueRect.gameObject.AddComponent<Button>();
            b.queueToggleButton.targetGraphic = b.queueToggleBg;
            b.queueToggleButton.navigation = new Navigation { mode = Navigation.Mode.None };
            b.queueToggleButton.onClick.AddListener(() => onToggleQueue?.Invoke());

            b.queueToggleIcon = EconomyHUD.CreateIcon("Checklist", queueRect, LumiWorldUITheme.Glyph("G-21"), 26f, 16f);
            b.queueToggleIcon.color = LumiWorldUITheme.Text;

            b.queueToggleLabel = EconomyHUD.CreateText("Count", queueRect, 16.5f, LumiWorldUITheme.Text, TextAlignmentOptions.MidlineLeft);
            ApplyHeadingFont(b.queueToggleLabel);
            EconomyHUD.Stretch(b.queueToggleLabel.rectTransform);
            b.queueToggleLabel.rectTransform.offsetMin = new Vector2(50f, 0f);
            b.queueToggleLabel.rectTransform.offsetMax = new Vector2(-10f, 0f);
            b.queueToggleLabel.text = "Queue (0)";

            // Close [×] Button
            RectTransform closeRect = EconomyHUD.CreateRect("CloseButton", panel);
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(0f, 1f);
            closeRect.pivot = new Vector2(0f, 1f);
            closeRect.anchoredPosition = new Vector2(PanelWidth - 72f, -16f);
            closeRect.sizeDelta = new Vector2(46f, 46f);
            Image closeBg = EconomyHUD.CreatePanel(closeRect, LumiWorldUITheme.Inset);
            closeBg.raycastTarget = true;
            LumiWorldUITheme.AddSoftElevation(closeRect.gameObject, 2.5f, 0.14f);
            Button closeBtn = closeRect.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            closeBtn.onClick.AddListener(() => onClose?.Invoke());

            Sprite closeGlyph = LumiWorldUITheme.Glyph("Close");
            if (closeGlyph != null)
            {
                Image closeIc = EconomyHUD.CreateIcon("CloseIcon", closeRect, closeGlyph, 22f, 12f);
                closeIc.color = LumiWorldUITheme.Text;
            }
            else
            {
                TextMeshProUGUI closeLbl = EconomyHUD.CreateText("Lbl", closeRect, 18f, LumiWorldUITheme.Text, TextAlignmentOptions.Center);
                EconomyHUD.Stretch(closeLbl.rectTransform);
                closeLbl.text = "×";
            }

            // Header Divider
            RectTransform divider = EconomyHUD.CreateRect("HeaderDivider", panel);
            divider.anchorMin = divider.anchorMax = new Vector2(0f, 1f);
            divider.pivot = new Vector2(0f, 1f);
            divider.anchoredPosition = new Vector2(28f, -74f);
            divider.sizeDelta = new Vector2(PanelWidth - 56f, 2f);
            divider.gameObject.AddComponent<Image>().color = LumiWorldUITheme.Inset;
        }

        private static void BuildTopModePill(
            RectTransform parent,
            string name,
            string label,
            Sprite glyph,
            float x,
            float y,
            float w,
            float h,
            bool active,
            Action onClick)
        {
            RectTransform rect = EconomyHUD.CreateRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(w, h);

            Image bg = EconomyHUD.CreatePanel(rect, active ? LumiWorldUITheme.Primary : LumiWorldUITheme.Inset);
            bg.raycastTarget = true;
            LumiWorldUITheme.AddSoftElevation(rect.gameObject, 3f, 0.16f);

            Button btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            if (glyph != null)
            {
                Image ic = EconomyHUD.CreateIcon("Glyph", rect, glyph, 28f, 18f);
                ic.color = active ? Color.white : LumiWorldUITheme.Text;
            }

            TextMeshProUGUI txt = EconomyHUD.CreateText("Label", rect, 20f, active ? Color.white : LumiWorldUITheme.Text, TextAlignmentOptions.MidlineLeft);
            ApplyHeadingFont(txt);
            EconomyHUD.Stretch(txt.rectTransform);
            txt.rectTransform.offsetMin = new Vector2(glyph != null ? 54f : 14f, 0f);
            txt.rectTransform.offsetMax = new Vector2(-12f, 0f);
            txt.text = label;
        }

        private static void BuildLeftColumn(
            RectTransform panel,
            CraftingUIStyleConfig style,
            Action<CraftingUI.TabFilter> onTabSelected,
            Action<CraftingRecipeData> onSelectRecipe,
            CraftingUIBindings b)
        {
            b.leftColumn = EconomyHUD.CreateRect("LeftCol", panel);
            b.leftColumn.anchorMin = b.leftColumn.anchorMax = new Vector2(0f, 1f);
            b.leftColumn.pivot = new Vector2(0f, 1f);
            b.leftColumn.anchoredPosition = new Vector2(24f, -86f);
            b.leftColumn.sizeDelta = new Vector2(428f, 528f);

            // Taller, squarer Horizontal Categories directly above recipe listings (P-01: Food / Medicine / Product)
            b.tabBar = EconomyHUD.CreateRect("TabBar", b.leftColumn);
            b.tabBar.anchorMin = new Vector2(0f, 1f);
            b.tabBar.anchorMax = new Vector2(1f, 1f);
            b.tabBar.pivot = new Vector2(0f, 1f);
            b.tabBar.anchoredPosition = Vector2.zero;
            b.tabBar.sizeDelta = new Vector2(0f, 48f);

            HorizontalLayoutGroup tabLayout = b.tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 8f;
            tabLayout.childControlWidth = true;
            tabLayout.childControlHeight = true;
            tabLayout.childForceExpandWidth = true;
            tabLayout.childForceExpandHeight = true;

            var tabs = new (CraftingUI.TabFilter filter, string label, string glyph)[]
            {
                (CraftingUI.TabFilter.Food, "Food", "G-16"),
                (CraftingUI.TabFilter.Herbal, "Medicine", "G-17"),
                (CraftingUI.TabFilter.Paper, "Product", "G-18")
            };

            foreach (var t in tabs)
            {
                RectTransform tRect = EconomyHUD.CreateRect("Tab_" + t.label, b.tabBar);
                LayoutElement le = tRect.gameObject.AddComponent<LayoutElement>();
                le.flexibleWidth = 1f;
                le.preferredHeight = 48f;

                Image bg = EconomyHUD.CreatePanel(tRect, LumiWorldUITheme.Inset);
                bg.raycastTarget = true;
                LumiWorldUITheme.AddSoftElevation(tRect.gameObject, 2.5f, 0.13f);

                Button btn = tRect.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.navigation = new Navigation { mode = Navigation.Mode.None };

                Image ic = EconomyHUD.CreateIcon("Glyph", tRect, LumiWorldUITheme.Glyph(t.glyph), 26f, 12f);

                TextMeshProUGUI lbl = EconomyHUD.CreateText("Lbl", tRect, 15.5f, LumiWorldUITheme.Text, TextAlignmentOptions.MidlineLeft);
                ApplyHeadingFont(lbl);
                EconomyHUD.Stretch(lbl.rectTransform);
                lbl.rectTransform.offsetMin = new Vector2(44f, 0f);
                lbl.rectTransform.offsetMax = new Vector2(-6f, 0f);
                lbl.enableAutoSizing = true;
                lbl.fontSizeMin = 12f;
                lbl.fontSizeMax = 15.5f;
                lbl.textWrappingMode = TextWrappingModes.NoWrap;
                lbl.text = t.label;

                CraftingUI.TabFilter captured = t.filter;
                btn.onClick.AddListener(() => onTabSelected(captured));

                b.tabButtons.Add((captured, btn, bg, null, ic, lbl));
            }

            // Subtle recessed Well behind Recipe Scroll List
            RectTransform listWell = EconomyHUD.CreateRect("RecipeListWell", b.leftColumn);
            listWell.anchorMin = Vector2.zero;
            listWell.anchorMax = Vector2.one;
            listWell.offsetMin = Vector2.zero;
            listWell.offsetMax = new Vector2(0f, -58f);
            Image wellBg = EconomyHUD.CreatePanel(listWell, LumiWorldUITheme.Well);
            wellBg.raycastTarget = false;

            // Recipe Scroll List directly below category bar
            RectTransform listArea = EconomyHUD.CreateRect("RecipeScrollArea", b.leftColumn);
            listArea.anchorMin = Vector2.zero;
            listArea.anchorMax = Vector2.one;
            listArea.offsetMin = new Vector2(4f, 4f);
            listArea.offsetMax = new Vector2(-4f, -62f);

            b.recipeScrollRect = listArea.gameObject.AddComponent<ScrollRect>();
            b.recipeScrollRect.horizontal = false;
            b.recipeScrollRect.vertical = true;
            b.recipeScrollRect.scrollSensitivity = 32f;
            b.recipeScrollRect.movementType = ScrollRect.MovementType.Clamped;

            RectTransform viewport = EconomyHUD.CreateRect("Viewport", listArea);
            EconomyHUD.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            b.recipeScrollRect.viewport = viewport;

            b.recipeListContent = EconomyHUD.CreateRect("Content", viewport);
            b.recipeListContent.anchorMin = new Vector2(0f, 1f);
            b.recipeListContent.anchorMax = new Vector2(1f, 1f);
            b.recipeListContent.pivot = new Vector2(0f, 1f);
            b.recipeListContent.sizeDelta = new Vector2(0f, 400f);
            b.recipeScrollRect.content = b.recipeListContent;

            VerticalLayoutGroup layout = b.recipeListContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(4, 6, 4, 10);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = b.recipeListContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var allRecipes = CraftingCatalog.All;
            for (int i = 0; i < allRecipes.Count; i++)
            {
                var r = allRecipes[i];
                var card = BuildRecipeCard(b.recipeListContent, r, onSelectRecipe);
                b.recipeCards.Add(card);
            }
        }

        public static RecipeCardView BuildRecipeCard(RectTransform container, CraftingRecipeData recipe, Action<CraftingRecipeData> onSelect)
        {
            RectTransform row = EconomyHUD.CreateRect("Recipe_" + recipe.recipeID, container);
            row.sizeDelta = new Vector2(0f, 78f);

            LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 78f;
            le.preferredHeight = 78f;
            le.flexibleWidth = 1f;

            // 3D elevated bright card (#FCFBF8) without wire outline when unselected
            Image bg = EconomyHUD.CreatePanel(row, LumiWorldUITheme.Slot);
            bg.raycastTarget = true;
            LumiWorldUITheme.AddSoftElevation(row.gameObject, 3.5f, 0.16f);

            // Selected border (dark graphite, no checkmark per P-01/P-02)
            Image selectedBorder = EconomyHUD.AddBlueBorder(row, LumiWorldUITheme.Primary);
            selectedBorder.gameObject.name = "SelectedBorder";
            selectedBorder.gameObject.SetActive(false);

            Button btn = row.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(() => onSelect(recipe));

            ResourceData outRes = recipe.outputResource;
            Sprite qIcon = recipe != null ? recipe.GetIcon() : (outRes != null ? outRes.icon : null);
            Image icon = EconomyHUD.CreateIcon("Icon", row, qIcon != null ? qIcon : EconomyHUD.CircleSprite, 56f, 12f);
            if (qIcon == null && outRes != null) icon.color = outRes.resourceColor;

            TextMeshProUGUI nameLbl = EconomyHUD.CreateText("Name", row, 16.5f, LumiWorldUITheme.Text, TextAlignmentOptions.MidlineLeft);
            ApplyHeadingFont(nameLbl);
            nameLbl.rectTransform.anchorMin = new Vector2(0f, 0f);
            nameLbl.rectTransform.anchorMax = new Vector2(1f, 1f);
            nameLbl.rectTransform.offsetMin = new Vector2(78f, 8f);
            nameLbl.rectTransform.offsetMax = new Vector2(-96f, -8f);
            nameLbl.enableAutoSizing = true;
            nameLbl.fontSizeMin = 12.5f;
            nameLbl.fontSizeMax = 16.5f;
            nameLbl.textWrappingMode = TextWrappingModes.Normal;
            string cardOutId = recipe.outputResource != null ? recipe.outputResource.resourceID : recipe.recipeID;
            nameLbl.text = ResourceInventoryUIPresentation.LocalizeResourceName(cardOutId, recipe.recipeName);

            // Status Indicator (Circular icon + colored text, NO box border or pill background per P-01 mockup)
            RectTransform statusGroupRect = EconomyHUD.CreateRect("StatusGroup", row);
            statusGroupRect.anchorMin = new Vector2(1f, 0.5f);
            statusGroupRect.anchorMax = new Vector2(1f, 0.5f);
            statusGroupRect.pivot = new Vector2(1f, 0.5f);
            statusGroupRect.anchoredPosition = new Vector2(-12f, 0f);
            statusGroupRect.sizeDelta = new Vector2(82f, 28f);

            Image statusIcon = EconomyHUD.CreateIcon("StatusIcon", statusGroupRect, LumiWorldUITheme.StatusReadySprite, 20f, 2f);

            TextMeshProUGUI statusLbl = EconomyHUD.CreateText("Status", statusGroupRect, 14.5f, LumiWorldUITheme.Success, TextAlignmentOptions.MidlineLeft);
            ApplyHeadingFont(statusLbl);
            EconomyHUD.Stretch(statusLbl.rectTransform);
            statusLbl.rectTransform.offsetMin = new Vector2(27f, 0f);
            statusLbl.rectTransform.offsetMax = Vector2.zero;
            statusLbl.textWrappingMode = TextWrappingModes.NoWrap;

            return new RecipeCardView
            {
                recipe = recipe,
                root = row,
                background = bg,
                selectedBorder = selectedBorder,
                icon = icon,
                name = nameLbl,
                status = statusLbl,
                statusIcon = statusIcon,
                button = btn
            };
        }

        private static void BuildRightInspector(
            RectTransform panel,
            CraftingUIStyleConfig style,
            Action<int> onAdjustQty,
            Action onStartCraft,
            CraftingUIBindings b)
        {
            b.rightInspector = EconomyHUD.CreateRect("RightCol", panel);
            b.rightInspector.anchorMin = b.rightInspector.anchorMax = new Vector2(0f, 1f);
            b.rightInspector.pivot = new Vector2(0f, 1f);
            b.rightInspector.anchoredPosition = new Vector2(466f, -86f);
            b.rightInspector.sizeDelta = new Vector2(590f, 528f);
            EconomyHUD.CreatePanel(b.rightInspector, LumiWorldUITheme.Surface);
            LumiWorldUITheme.AddSoftElevation(b.rightInspector.gameObject, 4f, 0.15f);

            // Top Output Header
            RectTransform header = EconomyHUD.CreateRect("Header", b.rightInspector);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0f, 1f);
            header.anchoredPosition = Vector2.zero;
            header.sizeDelta = new Vector2(0f, 132f);

            RectTransform showSlot = EconomyHUD.CreateRect("ShowSlot", header);
            showSlot.anchorMin = new Vector2(0f, 1f);
            showSlot.anchorMax = new Vector2(0f, 1f);
            showSlot.pivot = new Vector2(0f, 1f);
            showSlot.anchoredPosition = new Vector2(22f, -16f);
            showSlot.sizeDelta = new Vector2(100f, 100f);
            EconomyHUD.CreatePanel(showSlot, LumiWorldUITheme.Window);

            b.inspectorIcon = EconomyHUD.CreateIcon("Icon", showSlot, EconomyHUD.CircleSprite, 84f, 8f);

            b.inspectorTitle = EconomyHUD.CreateText("Title", header, 26f, LumiWorldUITheme.Text, TextAlignmentOptions.MidlineLeft);
            ApplyHeadingFont(b.inspectorTitle);
            b.inspectorTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            b.inspectorTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            b.inspectorTitle.rectTransform.pivot = new Vector2(0f, 1f);
            b.inspectorTitle.rectTransform.offsetMin = new Vector2(138f, -66f);
            b.inspectorTitle.rectTransform.offsetMax = new Vector2(-20f, -20f);
            b.inspectorTitle.enableAutoSizing = true;
            b.inspectorTitle.fontSizeMin = 17f;
            b.inspectorTitle.fontSizeMax = 26f;
            b.inspectorTitle.textWrappingMode = TextWrappingModes.Normal;

            RectTransform outRow = EconomyHUD.CreateRect("OutputRow", header);
            outRow.anchorMin = new Vector2(0f, 1f);
            outRow.anchorMax = new Vector2(1f, 1f);
            outRow.pivot = new Vector2(0f, 1f);
            outRow.offsetMin = new Vector2(138f, -102f);
            outRow.offsetMax = new Vector2(-20f, -70f);

            Sprite outGlyph = LumiWorldUITheme.Glyph("G-18");
            if (outGlyph != null)
            {
                Image outIc = EconomyHUD.CreateIcon("OutIcon", outRow, outGlyph, 22f, 0f);
                outIc.color = LumiWorldUITheme.Muted;
            }

            b.inspectorOutputText = EconomyHUD.CreateText("OutputCount", outRow, 16f, LumiWorldUITheme.Muted, TextAlignmentOptions.MidlineLeft);
            ApplyHeadingFont(b.inspectorOutputText);
            EconomyHUD.Stretch(b.inspectorOutputText.rectTransform);
            b.inspectorOutputText.rectTransform.offsetMin = new Vector2(outGlyph != null ? 28f : 0f, 0f);
            b.inspectorOutputText.text = "Output ×1";

            // Divider
            RectTransform div = EconomyHUD.CreateRect("Divider", b.rightInspector);
            div.anchorMin = new Vector2(0f, 1f);
            div.anchorMax = new Vector2(1f, 1f);
            div.pivot = new Vector2(0.5f, 1f);
            div.anchoredPosition = new Vector2(0f, -130f);
            div.sizeDelta = new Vector2(-40f, 2f);
            div.gameObject.AddComponent<Image>().color = LumiWorldUITheme.Inset;

            // "Ingredients" Heading
            TextMeshProUGUI ingHead = EconomyHUD.CreateText("IngredientsHeading", b.rightInspector, 16f, LumiWorldUITheme.Muted, TextAlignmentOptions.MidlineLeft);
            ApplyHeadingFont(ingHead);
            ingHead.rectTransform.anchorMin = new Vector2(0f, 1f);
            ingHead.rectTransform.anchorMax = new Vector2(1f, 1f);
            ingHead.rectTransform.pivot = new Vector2(0f, 1f);
            ingHead.rectTransform.offsetMin = new Vector2(20f, -166f);
            ingHead.rectTransform.offsetMax = new Vector2(-20f, -138f);
            ingHead.text = "Ingredients";

            // Ingredients Container (P-02: clean rows, red fraction when insufficient, click opens P-15 popover)
            b.ingredientsContainer = EconomyHUD.CreateRect("Ingredients", b.rightInspector);
            b.ingredientsContainer.anchorMin = new Vector2(0f, 1f);
            b.ingredientsContainer.anchorMax = new Vector2(1f, 1f);
            b.ingredientsContainer.pivot = new Vector2(0.5f, 1f);
            b.ingredientsContainer.anchoredPosition = new Vector2(0f, -172f);
            b.ingredientsContainer.sizeDelta = new Vector2(-40f, 212f);

            VerticalLayoutGroup ingLayout = b.ingredientsContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            ingLayout.padding = new RectOffset(0, 0, 0, 0);
            ingLayout.spacing = 10f;
            ingLayout.childControlWidth = true;
            ingLayout.childControlHeight = false;
            ingLayout.childForceExpandWidth = true;
            ingLayout.childForceExpandHeight = false;

            // Quantity + Duration Bar
            RectTransform qtyBar = EconomyHUD.CreateRect("QtyBar", b.rightInspector);
            qtyBar.anchorMin = new Vector2(0f, 0f);
            qtyBar.anchorMax = new Vector2(1f, 0f);
            qtyBar.pivot = new Vector2(0.5f, 0f);
            qtyBar.anchoredPosition = new Vector2(0f, 80f);
            qtyBar.sizeDelta = new Vector2(-40f, 44f);

            TextMeshProUGUI qtyCaption = EconomyHUD.CreateText("QtyCaption", qtyBar, 15.5f, LumiWorldUITheme.Muted, TextAlignmentOptions.MidlineLeft);
            ApplyHeadingFont(qtyCaption);
            qtyCaption.rectTransform.anchorMin = new Vector2(0f, 0f);
            qtyCaption.rectTransform.anchorMax = new Vector2(0f, 1f);
            qtyCaption.rectTransform.pivot = new Vector2(0f, 0.5f);
            qtyCaption.rectTransform.anchoredPosition = Vector2.zero;
            qtyCaption.rectTransform.sizeDelta = new Vector2(78f, 0f);
            qtyCaption.text = "Quantity";

            RectTransform decRect = EconomyHUD.CreateRect("BtnDecrease", qtyBar);
            decRect.anchorMin = decRect.anchorMax = new Vector2(0f, 0.5f);
            decRect.pivot = new Vector2(0f, 0.5f);
            decRect.anchoredPosition = new Vector2(82f, 0f);
            decRect.sizeDelta = new Vector2(40f, 38f);
            b.btnDecrease = EconomyHUD.AddButton(decRect, LumiWorldUITheme.Inset);
            LumiWorldUITheme.AddSoftElevation(decRect.gameObject, 2f, 0.12f);
            b.btnDecrease.onClick.AddListener(() => onAdjustQty(-1));
            TextMeshProUGUI decLbl = EconomyHUD.CreateText("L", decRect, 20f, LumiWorldUITheme.Text, TextAlignmentOptions.Center);
            ApplyHeadingFont(decLbl);
            EconomyHUD.Stretch(decLbl.rectTransform);
            decLbl.text = "−";

            RectTransform qtyBox = EconomyHUD.CreateRect("QtyBox", qtyBar);
            qtyBox.anchorMin = qtyBox.anchorMax = new Vector2(0f, 0.5f);
            qtyBox.pivot = new Vector2(0f, 0.5f);
            qtyBox.anchoredPosition = new Vector2(128f, 0f);
            qtyBox.sizeDelta = new Vector2(58f, 38f);
            EconomyHUD.CreatePanel(qtyBox, LumiWorldUITheme.Slot);
            LumiWorldUITheme.AddSoftElevation(qtyBox.gameObject, 2f, 0.12f);
            b.quantityText = EconomyHUD.CreateText("Qty", qtyBox, 17f, LumiWorldUITheme.Text, TextAlignmentOptions.Center);
            ApplyHeadingFont(b.quantityText);
            EconomyHUD.Stretch(b.quantityText.rectTransform);
            b.quantityText.text = "1";

            RectTransform incRect = EconomyHUD.CreateRect("BtnIncrease", qtyBar);
            incRect.anchorMin = incRect.anchorMax = new Vector2(0f, 0.5f);
            incRect.pivot = new Vector2(0f, 0.5f);
            incRect.anchoredPosition = new Vector2(192f, 0f);
            incRect.sizeDelta = new Vector2(40f, 38f);
            b.btnIncrease = EconomyHUD.AddButton(incRect, LumiWorldUITheme.Inset);
            LumiWorldUITheme.AddSoftElevation(incRect.gameObject, 2f, 0.12f);
            b.btnIncrease.onClick.AddListener(() => onAdjustQty(1));
            TextMeshProUGUI incLbl = EconomyHUD.CreateText("L", incRect, 20f, LumiWorldUITheme.Text, TextAlignmentOptions.Center);
            ApplyHeadingFont(incLbl);
            EconomyHUD.Stretch(incLbl.rectTransform);
            incLbl.text = "+";

            RectTransform durBox = EconomyHUD.CreateRect("DurationBox", qtyBar);
            durBox.anchorMin = new Vector2(1f, 0f);
            durBox.anchorMax = new Vector2(1f, 1f);
            durBox.pivot = new Vector2(1f, 0.5f);
            durBox.anchoredPosition = Vector2.zero;
            durBox.sizeDelta = new Vector2(106f, 0f);

            b.durationText = EconomyHUD.CreateText("Duration", durBox, 16.5f, LumiWorldUITheme.Muted, TextAlignmentOptions.MidlineRight);
            ApplyHeadingFont(b.durationText);
            EconomyHUD.Stretch(b.durationText.rectTransform);
            b.durationText.text = "10s";

            // Primary [G-02] Craft CTA Button
            RectTransform craftBtnRect = EconomyHUD.CreateRect("CraftBtn", b.rightInspector);
            craftBtnRect.anchorMin = new Vector2(0f, 0f);
            craftBtnRect.anchorMax = new Vector2(1f, 0f);
            craftBtnRect.pivot = new Vector2(0.5f, 0f);
            craftBtnRect.anchoredPosition = new Vector2(0f, 18f);
            craftBtnRect.sizeDelta = new Vector2(-40f, 52f);

            b.btnStartCraft = EconomyHUD.AddButton(craftBtnRect, LumiWorldUITheme.Primary);
            b.btnStartCraftBg = craftBtnRect.GetComponent<Image>();
            LumiWorldUITheme.AddSoftElevation(craftBtnRect.gameObject, 3.5f, 0.18f);
            b.btnStartCraft.onClick.AddListener(() => onStartCraft());

            RectTransform craftContent = EconomyHUD.CreateRect("CraftLabelBox", craftBtnRect);
            craftContent.anchorMin = craftContent.anchorMax = craftContent.pivot = new Vector2(0.5f, 0.5f);
            craftContent.sizeDelta = new Vector2(128f, 52f);

            b.btnStartCraftIcon = EconomyHUD.CreateIcon("Hammer", craftContent, LumiWorldUITheme.Glyph("G-02"), 26f, 6f);
            b.btnStartCraftIcon.color = Color.white;

            b.btnStartCraftLabel = EconomyHUD.CreateText("Lbl", craftContent, 19f, Color.white, TextAlignmentOptions.MidlineLeft);
            ApplyHeadingFont(b.btnStartCraftLabel);
            EconomyHUD.Stretch(b.btnStartCraftLabel.rectTransform);
            b.btnStartCraftLabel.rectTransform.offsetMin = new Vector2(40f, 0f);
            b.btnStartCraftLabel.text = "Craft";
        }

        private static void BuildQueueDrawer(
            RectTransform panel,
            CraftingUIStyleConfig style,
            Action onCloseDrawer,
            Action onClaimAll,
            CraftingUIBindings b)
        {
            b.queueDrawer = EconomyHUD.CreateRect("QueueDrawer", panel);
            b.queueDrawer.anchorMin = b.queueDrawer.anchorMax = new Vector2(0f, 1f);
            b.queueDrawer.pivot = new Vector2(0f, 1f);
            b.queueDrawer.anchoredPosition = new Vector2(736f, -86f);
            b.queueDrawer.sizeDelta = new Vector2(320f, 528f);
            EconomyHUD.CreatePanel(b.queueDrawer, LumiWorldUITheme.Well);
            LumiWorldUITheme.AddSoftElevation(b.queueDrawer.gameObject, 4f, 0.15f);

            // Drawer Header: "Queue" + [Claim all (N)] + [×]
            RectTransform topBar = EconomyHUD.CreateRect("TopBar", b.queueDrawer);
            topBar.anchorMin = new Vector2(0f, 1f);
            topBar.anchorMax = new Vector2(1f, 1f);
            topBar.pivot = new Vector2(0f, 1f);
            topBar.anchoredPosition = new Vector2(0f, -14f);
            topBar.sizeDelta = new Vector2(0f, 42f);

            TextMeshProUGUI qTitle = EconomyHUD.CreateText("QueueTitle", topBar, 21f, LumiWorldUITheme.Text, TextAlignmentOptions.MidlineLeft);
            ApplyHeadingFont(qTitle);
            qTitle.rectTransform.anchorMin = new Vector2(0f, 0f);
            qTitle.rectTransform.anchorMax = new Vector2(0f, 1f);
            qTitle.rectTransform.pivot = new Vector2(0f, 0.5f);
            qTitle.rectTransform.anchoredPosition = new Vector2(16f, 0f);
            qTitle.rectTransform.sizeDelta = new Vector2(96f, 0f);
            qTitle.text = "Queue";

            RectTransform claimAllRect = EconomyHUD.CreateRect("ClaimAllBtn", topBar);
            claimAllRect.anchorMin = claimAllRect.anchorMax = new Vector2(1f, 0.5f);
            claimAllRect.pivot = new Vector2(1f, 0.5f);
            claimAllRect.anchoredPosition = new Vector2(-56f, 0f);
            claimAllRect.sizeDelta = new Vector2(130f, 38f);
            b.btnClaimAll = EconomyHUD.AddButton(claimAllRect, LumiWorldUITheme.Primary);
            LumiWorldUITheme.AddSoftElevation(claimAllRect.gameObject, 2.5f, 0.15f);
            b.btnClaimAll.onClick.AddListener(() => onClaimAll());
            b.btnClaimAllLabel = EconomyHUD.CreateText("Lbl", claimAllRect, 14f, Color.white, TextAlignmentOptions.Center);
            ApplyHeadingFont(b.btnClaimAllLabel);
            EconomyHUD.Stretch(b.btnClaimAllLabel.rectTransform);
            b.btnClaimAllLabel.text = "Claim all (0)";

            RectTransform closeDrawerRect = EconomyHUD.CreateRect("CloseQueue", topBar);
            closeDrawerRect.anchorMin = closeDrawerRect.anchorMax = new Vector2(1f, 0.5f);
            closeDrawerRect.pivot = new Vector2(1f, 0.5f);
            closeDrawerRect.anchoredPosition = new Vector2(-12f, 0f);
            closeDrawerRect.sizeDelta = new Vector2(38f, 38f);
            Button closeDrawerBtn = EconomyHUD.AddButton(closeDrawerRect, LumiWorldUITheme.Surface);
            LumiWorldUITheme.AddSoftElevation(closeDrawerRect.gameObject, 2f, 0.13f);
            closeDrawerBtn.onClick.AddListener(() => onCloseDrawer?.Invoke());
            Sprite closeGlyph = LumiWorldUITheme.Glyph("Close");
            if (closeGlyph != null)
            {
                Image ic = EconomyHUD.CreateIcon("CloseIc", closeDrawerRect, closeGlyph, 18f, 10f);
                ic.color = LumiWorldUITheme.Text;
            }
            else
            {
                TextMeshProUGUI xLbl = EconomyHUD.CreateText("X", closeDrawerRect, 16f, LumiWorldUITheme.Text, TextAlignmentOptions.Center);
                EconomyHUD.Stretch(xLbl.rectTransform);
                xLbl.text = "×";
            }

            // Queue Scroll List
            RectTransform scrollArea = EconomyHUD.CreateRect("ScrollArea", b.queueDrawer);
            scrollArea.anchorMin = Vector2.zero;
            scrollArea.anchorMax = Vector2.one;
            scrollArea.offsetMin = new Vector2(12f, 12f);
            scrollArea.offsetMax = new Vector2(-12f, -66f);

            b.queueScrollRect = scrollArea.gameObject.AddComponent<ScrollRect>();
            b.queueScrollRect.horizontal = false;
            b.queueScrollRect.vertical = true;
            b.queueScrollRect.scrollSensitivity = 32f;
            b.queueScrollRect.movementType = ScrollRect.MovementType.Clamped;

            RectTransform viewport = EconomyHUD.CreateRect("Viewport", scrollArea);
            EconomyHUD.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            b.queueScrollRect.viewport = viewport;

            b.queueListContent = EconomyHUD.CreateRect("Content", viewport);
            b.queueListContent.anchorMin = new Vector2(0f, 1f);
            b.queueListContent.anchorMax = new Vector2(1f, 1f);
            b.queueListContent.pivot = new Vector2(0f, 1f);
            b.queueListContent.sizeDelta = new Vector2(0f, 400f);
            b.queueScrollRect.content = b.queueListContent;

            VerticalLayoutGroup qLayout = b.queueListContent.gameObject.AddComponent<VerticalLayoutGroup>();
            qLayout.padding = new RectOffset(2, 4, 2, 10);
            qLayout.spacing = 12f;
            qLayout.childControlWidth = true;
            qLayout.childControlHeight = false;
            qLayout.childForceExpandWidth = true;
            qLayout.childForceExpandHeight = false;

            ContentSizeFitter qFitter = b.queueListContent.gameObject.AddComponent<ContentSizeFitter>();
            qFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        public static QueueCardView BuildQueueCard(
            RectTransform container,
            ActiveCraftingJob job,
            CraftingUIStyleConfig style,
            Action<ActiveCraftingJob, string> onClaimSuccess,
            Action<string> onClaimFail)
        {
            var recipe = job.Recipe;
            var outRes = recipe != null ? recipe.outputResource : null;

            RectTransform row = EconomyHUD.CreateRect("JobRow", container);
            row.sizeDelta = new Vector2(0f, 110f);
            LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 110f;
            le.preferredHeight = 110f;
            le.flexibleWidth = 1f;

            EconomyHUD.CreatePanel(row, LumiWorldUITheme.Slot);
            LumiWorldUITheme.AddSoftElevation(row.gameObject, 3.5f, 0.16f);

            Sprite rowIcon = recipe != null ? recipe.GetIcon() : (outRes != null ? outRes.icon : null);
            Image icon = EconomyHUD.CreateIcon("Icon", row, rowIcon != null ? rowIcon : EconomyHUD.CircleSprite, 56f, 12f);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 1f);
            icon.rectTransform.pivot = new Vector2(0f, 1f);
            icon.rectTransform.anchoredPosition = new Vector2(12f, -12f);
            if (rowIcon == null && outRes != null) icon.color = outRes.resourceColor;

<<<<<<< HEAD
            TextMeshProUGUI title = EconomyHUD.CreateText("Title", row, 15.5f, style.textColor, TextAlignmentOptions.Left);
            title.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            title.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            title.rectTransform.pivot = new Vector2(0f, 0.5f);
            title.rectTransform.anchoredPosition = new Vector2(68f, 11f);
            title.rectTransform.sizeDelta = new Vector2(200f, 24f);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Ellipsis;
=======
            TextMeshProUGUI title = EconomyHUD.CreateText("Title", row, 16f, LumiWorldUITheme.Text, TextAlignmentOptions.MidlineLeft);
            ApplyHeadingFont(title);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.offsetMin = new Vector2(76f, -58f);
            title.rectTransform.offsetMax = new Vector2(-12f, -12f);
            title.enableAutoSizing = true;
            title.fontSizeMin = 12f;
            title.fontSizeMax = 16f;
            title.textWrappingMode = TextWrappingModes.Normal;
>>>>>>> Acs
            string qId = recipe != null ? (recipe.outputResource != null ? recipe.outputResource.resourceID : recipe.recipeID) : "";
            string rName = recipe != null ? ResourceInventoryUIPresentation.LocalizeResourceName(qId, recipe.recipeName) : "Crafted Product";
            title.text = $"{rName} ×{job.TotalOutputAmount}";

<<<<<<< HEAD
            TextMeshProUGUI status = EconomyHUD.CreateText("Status", row, 12.5f, style.mutedColor, TextAlignmentOptions.Left);
            status.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            status.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            status.rectTransform.pivot = new Vector2(0f, 0.5f);
            status.rectTransform.anchoredPosition = new Vector2(68f, -13f);
            status.rectTransform.sizeDelta = new Vector2(200f, 20f);
            status.textWrappingMode = TextWrappingModes.NoWrap;
            status.overflowMode = TextOverflowModes.Ellipsis;
=======
            // State Group ("In progress" pill OR icon-only "✓ Ready")
            RectTransform statePillRect = EconomyHUD.CreateRect("StatePill", row);
            statePillRect.anchorMin = new Vector2(0f, 0f);
            statePillRect.anchorMax = new Vector2(0f, 0f);
            statePillRect.pivot = new Vector2(0f, 0f);
            statePillRect.anchoredPosition = new Vector2(76f, 26f);
            statePillRect.sizeDelta = new Vector2(102f, 26f);
            Image statePill = EconomyHUD.CreatePanel(statePillRect, LumiWorldUITheme.Inset);
>>>>>>> Acs

            Image readyIcon = EconomyHUD.CreateIcon("ReadyIcon", statePillRect, LumiWorldUITheme.StatusReadySprite, 20f, 0f);
            readyIcon.gameObject.SetActive(false);

            TextMeshProUGUI statePillText = EconomyHUD.CreateText("StateText", statePillRect, 13f, LumiWorldUITheme.Text, TextAlignmentOptions.Center);
            ApplyHeadingFont(statePillText);
            EconomyHUD.Stretch(statePillText.rectTransform);
            statePillText.text = "In progress";

            // Remaining time text
            TextMeshProUGUI status = EconomyHUD.CreateText("Status", row, 13f, LumiWorldUITheme.Muted, TextAlignmentOptions.MidlineRight);
            status.rectTransform.anchorMin = new Vector2(1f, 0f);
            status.rectTransform.anchorMax = new Vector2(1f, 0f);
            status.rectTransform.pivot = new Vector2(1f, 0f);
            status.rectTransform.anchoredPosition = new Vector2(-12f, 26f);
            status.rectTransform.sizeDelta = new Vector2(120f, 26f);

            // Progress Bar Track (bottom of card when in progress)
            RectTransform barBg = EconomyHUD.CreateRect("BarBg", row);
<<<<<<< HEAD
            barBg.anchorMin = new Vector2(0f, 0.5f);
            barBg.anchorMax = new Vector2(0f, 0.5f);
            barBg.pivot = new Vector2(0f, 0.5f);
            barBg.anchoredPosition = new Vector2(285f, 0f);
            barBg.sizeDelta = new Vector2(480f, 16f);
            Image bgImg = EconomyHUD.CreatePanel(barBg, new Color(0.88f, 0.92f, 0.96f, 1f));
            bgImg.sprite = EconomyHUD.RoundedSprite;
            bgImg.type = Image.Type.Sliced;
            Mask mask = barBg.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
=======
            barBg.anchorMin = new Vector2(0f, 0f);
            barBg.anchorMax = new Vector2(1f, 0f);
            barBg.pivot = new Vector2(0.5f, 0f);
            barBg.anchoredPosition = new Vector2(0f, 10f);
            barBg.sizeDelta = new Vector2(-24f, 10f);
            EconomyHUD.CreatePanel(barBg, LumiWorldUITheme.Inset);
>>>>>>> Acs

            RectTransform barFill = EconomyHUD.CreateRect("BarFill", barBg);
            barFill.anchorMin = Vector2.zero;
            barFill.anchorMax = new Vector2(0f, 1f);
            barFill.pivot = new Vector2(0f, 0.5f);
            barFill.sizeDelta = new Vector2(0f, 0f);
<<<<<<< HEAD
            Image fillImg = EconomyHUD.CreatePanel(barFill, style.accentColor);
            fillImg.sprite = EconomyHUD.RoundedSprite;
            fillImg.type = Image.Type.Sliced;
=======
            EconomyHUD.CreatePanel(barFill, LumiWorldUITheme.Success);
>>>>>>> Acs

            // Claim Button (bottom-right when completed)
            RectTransform btnRect = EconomyHUD.CreateRect("ClaimBtn", row);
<<<<<<< HEAD
            btnRect.anchorMin = new Vector2(1f, 0.5f);
            btnRect.anchorMax = new Vector2(1f, 0.5f);
            btnRect.pivot = new Vector2(1f, 0.5f);
            btnRect.anchoredPosition = new Vector2(-16f, 0f);
            btnRect.sizeDelta = new Vector2(96f, 38f);
            Button claimBtn = EconomyHUD.AddButton(btnRect, style.accentColor);
            TextMeshProUGUI claimLbl = EconomyHUD.CreateText("Lbl", btnRect, 15f, Color.white, TextAlignmentOptions.Center);
=======
            btnRect.anchorMin = new Vector2(1f, 0f);
            btnRect.anchorMax = new Vector2(1f, 0f);
            btnRect.pivot = new Vector2(1f, 0f);
            btnRect.anchoredPosition = new Vector2(-12f, 14f);
            btnRect.sizeDelta = new Vector2(92f, 36f);
            Button claimBtn = EconomyHUD.AddButton(btnRect, LumiWorldUITheme.Primary);
            LumiWorldUITheme.AddSoftElevation(btnRect.gameObject, 2.5f, 0.16f);
            TextMeshProUGUI claimLbl = EconomyHUD.CreateText("Lbl", btnRect, 14.5f, Color.white, TextAlignmentOptions.Center);
            ApplyHeadingFont(claimLbl);
>>>>>>> Acs
            EconomyHUD.Stretch(claimLbl.rectTransform);
            claimLbl.text = "Claim";

            claimBtn.onClick.AddListener(() =>
            {
                if (CraftingQueueSystem.Instance.TryClaimJob(job, out string err))
                    onClaimSuccess(job, rName);
                else
                    onClaimFail(err);
            });

            return new QueueCardView
            {
                job = job,
                root = row,
                icon = icon,
                title = title,
                statePill = statePill,
                readyIcon = readyIcon,
                statePillText = statePillText,
                status = status,
                progressBarTrack = barBg.gameObject,
                progressBarFill = barFill,
                claimButton = claimBtn,
                claimLabel = claimLbl
            };
        }

        private static void ApplyHeadingFont(TextMeshProUGUI label)
        {
            if (label != null && LumiWorldUITheme.Heading != null)
                label.font = LumiWorldUITheme.Heading;
        }
    }
}
