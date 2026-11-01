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
        public Image icon;
        public TextMeshProUGUI name;
        public TextMeshProUGUI status;
        public GameObject edibleBadge;
        public Button button;
    }

    public class QueueCardView
    {
        public ActiveCraftingJob job;
        public RectTransform root;
        public Image icon;
        public TextMeshProUGUI title;
        public TextMeshProUGUI status;
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
        public RectTransform openButton;
        public GameObject badge;
        public TextMeshProUGUI badgeText;

        public GameObject window;
        public RectTransform panel;

        public RectTransform craftContentArea;
        public RectTransform queueContentArea;

        public readonly List<(CraftingUI.TabFilter tab, Button btn, Image bg, TextMeshProUGUI label)> tabButtons =
            new List<(CraftingUI.TabFilter, Button, Image, TextMeshProUGUI)>();

        public ScrollRect recipeScrollRect;
        public RectTransform recipeListContent;
        public readonly List<RecipeCardView> recipeCards = new List<RecipeCardView>();

        public Image inspectorIcon;
        public TextMeshProUGUI inspectorTitle;
        public TextMeshProUGUI inspectorCategory;
        public TextMeshProUGUI inspectorEdibleTag;
        public TextMeshProUGUI inspectorDesc;
        public RectTransform ingredientsContainer;
        public TextMeshProUGUI rawBreakdownText;
        public TextMeshProUGUI durationText;
        public TextMeshProUGUI quantityText;
        public Button btnDecrease;
        public Button btnIncrease;
        public Button btnMax;
        public Button btnStartCraft;
        public TextMeshProUGUI btnStartCraftLabel;

        public ScrollRect queueScrollRect;
        public RectTransform queueListContent;
        public Button btnClaimAll;
        public TextMeshProUGUI btnClaimAllLabel;
        public TextMeshProUGUI queueSummaryText;
        public RectTransform balanceChip;
        public TextMeshProUGUI balanceText;
    }

    /// <summary>
    /// Xây dựng toàn bộ giao diện Canvas UI (Procedural UI) cho hệ thống Chế biến (CraftingUI).
    /// </summary>
    public static class CraftingUIBuilder
    {
        public const float PanelWidth = 960f;
        public const float PanelHeight = 600f;
        public const float HeaderHeight = 88f;
        public const float LeftColWidth = 430f;
        public const float RightColWidth = 470f;
        public const float Spacing = 16f;

        public static CraftingUIBindings BuildAll(
            Transform parentTransform,
            CraftingUIStyleConfig style,
            Action onToggle,
            Action onClose,
            Action<CraftingUI.TabFilter> onTabSelected,
            Action<CraftingRecipeData> onSelectRecipe,
            Action<int> onAdjustQty,
            Action onMaxQty,
            Action onStartCraft,
            Action onClaimAll)
        {
            var b = new CraftingUIBindings();

            BuildOpenButton(parentTransform, style, onToggle, b);
            BuildWindow(parentTransform, style, onClose, onTabSelected, onSelectRecipe, onAdjustQty, onMaxQty, onStartCraft, onClaimAll, b);

            return b;
        }

        private static void BuildOpenButton(Transform parent, CraftingUIStyleConfig style, Action onToggle, CraftingUIBindings b)
        {
            b.openButton = EconomyHUD.CreateRect("CraftingOpenButton", parent);
            b.openButton.anchorMin = new Vector2(1f, 1f);
            b.openButton.anchorMax = new Vector2(1f, 1f);
            b.openButton.pivot = new Vector2(0.5f, 0.5f);
            b.openButton.sizeDelta = style.buttonSize;
            b.openButton.anchoredPosition = style.buttonCorner - style.buttonSize * 0.5f;

            Button btn = EconomyHUD.AddButton(b.openButton, style.hudColor);
            btn.onClick.AddListener(() => onToggle());

            TextMeshProUGUI label = EconomyHUD.CreateText("Label", b.openButton, 20f, Color.white, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(label.rectTransform);
            label.text = "Crafting";

            RectTransform badgeRect = EconomyHUD.CreateRect("Badge", b.openButton);
            badgeRect.anchorMin = new Vector2(1f, 1f);
            badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(-4f, -4f);
            badgeRect.sizeDelta = new Vector2(24f, 24f);
            Image badgeImg = badgeRect.gameObject.AddComponent<Image>();
            badgeImg.sprite = EconomyHUD.CircleSprite;
            badgeImg.color = style.accentColor;
            badgeImg.raycastTarget = false;
            b.badge = badgeRect.gameObject;

            b.badgeText = EconomyHUD.CreateText("Count", badgeRect, 13f, Color.white, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(b.badgeText.rectTransform);
            b.badge.SetActive(false);
        }

        private static void BuildWindow(
            Transform parent,
            CraftingUIStyleConfig style,
            Action onClose,
            Action<CraftingUI.TabFilter> onTabSelected,
            Action<CraftingRecipeData> onSelectRecipe,
            Action<int> onAdjustQty,
            Action onMaxQty,
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
            backdrop.color = style.backdropColor;
            Button backdropBtn = backdropRect.gameObject.AddComponent<Button>();
            backdropBtn.transition = Selectable.Transition.None;
            backdropBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            backdropBtn.onClick.AddListener(() => onClose());

            // Panel chính
            b.panel = EconomyHUD.CreateRect("Panel", windowRect);
            b.panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            EconomyHUD.CreatePanel(b.panel, style.panelColor).raycastTarget = true;

            BuildHeader(b.panel, style, onClose, b);
            BuildTabs(b.panel, style, onTabSelected, b);

            // Vùng nội dung Chế biến
            b.craftContentArea = EconomyHUD.CreateRect("CraftContent", b.panel);
            b.craftContentArea.anchorMin = new Vector2(0f, 0f);
            b.craftContentArea.anchorMax = new Vector2(1f, 1f);
            b.craftContentArea.offsetMin = new Vector2(Spacing, Spacing);
            b.craftContentArea.offsetMax = new Vector2(-Spacing, -(HeaderHeight + 42f));

            BuildLeftRecipeList(b.craftContentArea, style, onSelectRecipe, b);
            BuildRightInspector(b.craftContentArea, style, onAdjustQty, onMaxQty, onStartCraft, b);

            // Vùng nội dung Hàng đợi
            b.queueContentArea = EconomyHUD.CreateRect("QueueContent", b.panel);
            b.queueContentArea.anchorMin = new Vector2(0f, 0f);
            b.queueContentArea.anchorMax = new Vector2(1f, 1f);
            b.queueContentArea.offsetMin = new Vector2(Spacing, Spacing);
            b.queueContentArea.offsetMax = new Vector2(-Spacing, -(HeaderHeight + 42f));
            BuildQueueView(b.queueContentArea, style, onClaimAll, b);

            b.window.SetActive(false);
        }

        private static void BuildHeader(RectTransform panel, CraftingUIStyleConfig style, Action onClose, CraftingUIBindings b)
        {
            TextMeshProUGUI title = EconomyHUD.CreateText("Title", panel, 24f, style.textColor, TextAlignmentOptions.Left);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(0f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(28f, -16f);
            title.rectTransform.sizeDelta = new Vector2(750f, 32f);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.text = "RESOURCE WORKSHOP";

            TextMeshProUGUI subtitle = EconomyHUD.CreateText("Subtitle", panel, 14f, style.mutedColor, TextAlignmentOptions.Left);
            subtitle.fontStyle = FontStyles.Normal;
            subtitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            subtitle.rectTransform.anchorMax = new Vector2(0f, 1f);
            subtitle.rectTransform.pivot = new Vector2(0f, 1f);
            subtitle.rectTransform.anchoredPosition = new Vector2(28f, -50f);
            subtitle.rectTransform.sizeDelta = new Vector2(750f, 22f);
            subtitle.textWrappingMode = TextWrappingModes.NoWrap;
            subtitle.text = "Nutritious animal feed, herbal remedies & crafted paper";

            b.balanceChip = EconomyHUD.CreateRect("Balance", panel);
            b.balanceChip.anchorMin = new Vector2(1f, 1f);
            b.balanceChip.anchorMax = new Vector2(1f, 1f);
            b.balanceChip.pivot = new Vector2(0.5f, 0.5f);
            b.balanceChip.sizeDelta = new Vector2(150f, 38f);
            b.balanceChip.anchoredPosition = new Vector2(-(Spacing + 40f + 12f + 75f), -34f);
            EconomyHUD.CreateBorderedPanel(b.balanceChip, new Color(0.06f, 0.09f, 0.16f, 0.92f), new Color(0.08f, 0.58f, 0.95f, 0.60f));
            EconomyHUD.CreateIcon("CoinIcon", b.balanceChip, EconomyHUD.CoinSprite, 26f, 10f);

            b.balanceText = EconomyHUD.CreateText("Value", b.balanceChip, 20f, Color.white, TextAlignmentOptions.Right);
            b.balanceText.fontStyle = FontStyles.Bold;
            RectTransform valueRect = b.balanceText.rectTransform;
            valueRect.anchorMin = Vector2.zero;
            valueRect.anchorMax = Vector2.one;
            valueRect.offsetMin = new Vector2(42f, 0f);
            valueRect.offsetMax = new Vector2(-12f, 0f);

            RectTransform closeRect = EconomyHUD.CreateRect("CloseButton", panel);
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-Spacing, -16f);
            closeRect.sizeDelta = new Vector2(40f, 36f);
            EconomyHUD.AddButton(closeRect, style.secondaryColor).onClick.AddListener(() => onClose());
            TextMeshProUGUI closeLbl = EconomyHUD.CreateText("Lbl", closeRect, 18f, style.textColor, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(closeLbl.rectTransform);
            closeLbl.text = "X";
        }

        private static void BuildTabs(RectTransform panel, CraftingUIStyleConfig style, Action<CraftingUI.TabFilter> onTabSelected, CraftingUIBindings b)
        {
            RectTransform bar = EconomyHUD.CreateRect("TabBar", panel);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0f, 1f);
            bar.anchoredPosition = new Vector2(Spacing, -HeaderHeight);
            bar.sizeDelta = new Vector2(PanelWidth - Spacing * 2f, 36f);

            var tabs = new (CraftingUI.TabFilter filter, string label, float width)[]
            {
                (CraftingUI.TabFilter.All, "All", 80f),
                (CraftingUI.TabFilter.Food, "Food", 90f),
                (CraftingUI.TabFilter.Refined, "Refined", 95f),
                (CraftingUI.TabFilter.Herbal, "Medicine", 105f),
                (CraftingUI.TabFilter.Paper, "Craft", 90f),
                (CraftingUI.TabFilter.Queue, "Queue", 110f)
            };

            float curX = 0f;
            foreach (var t in tabs)
            {
                RectTransform tRect = EconomyHUD.CreateRect(t.filter.ToString(), bar);
                tRect.anchorMin = new Vector2(0f, 0.5f);
                tRect.anchorMax = new Vector2(0f, 0.5f);
                tRect.pivot = new Vector2(0f, 0.5f);
                tRect.anchoredPosition = new Vector2(curX, 0f);
                tRect.sizeDelta = new Vector2(t.width, 34f);

                Image bg = tRect.gameObject.AddComponent<Image>();
                bg.sprite = EconomyHUD.RoundedSprite;
                bg.type = Image.Type.Sliced;
                bg.color = style.tabInactiveColor;

                Button btn = tRect.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.navigation = new Navigation { mode = Navigation.Mode.None };

                TextMeshProUGUI lbl = EconomyHUD.CreateText("Lbl", tRect, 14f, style.mutedColor, TextAlignmentOptions.Center);
                EconomyHUD.Stretch(lbl.rectTransform);
                lbl.text = t.label;

                CraftingUI.TabFilter captured = t.filter;
                btn.onClick.AddListener(() => onTabSelected(captured));

                b.tabButtons.Add((captured, btn, bg, lbl));
                curX += t.width + 8f;
            }
        }

        private static void BuildLeftRecipeList(RectTransform container, CraftingUIStyleConfig style, Action<CraftingRecipeData> onSelectRecipe, CraftingUIBindings b)
        {
            RectTransform col = EconomyHUD.CreateRect("LeftCol", container);
            col.anchorMin = new Vector2(0f, 0f);
            col.anchorMax = new Vector2(0f, 1f);
            col.pivot = new Vector2(0f, 0.5f);
            col.sizeDelta = new Vector2(LeftColWidth, 0f);
            col.anchoredPosition = Vector2.zero;

            b.recipeScrollRect = col.gameObject.AddComponent<ScrollRect>();
            b.recipeScrollRect.horizontal = false;
            b.recipeScrollRect.vertical = true;
            b.recipeScrollRect.scrollSensitivity = 35f;
            b.recipeScrollRect.movementType = ScrollRect.MovementType.Elastic;
            b.recipeScrollRect.elasticity = 0.1f;
            b.recipeScrollRect.inertia = true;
            b.recipeScrollRect.decelerationRate = 0.135f;

            RectTransform viewport = EconomyHUD.CreateRect("Viewport", col);
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
            layout.padding = new RectOffset(2, 6, 4, 8);
            layout.spacing = 6f;
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
                var card = BuildRecipeCard(b.recipeListContent, r, style, onSelectRecipe);
                b.recipeCards.Add(card);
            }
        }

        public static RecipeCardView BuildRecipeCard(RectTransform container, CraftingRecipeData recipe, CraftingUIStyleConfig style, Action<CraftingRecipeData> onSelect)
        {
            RectTransform row = EconomyHUD.CreateRect("Recipe_" + recipe.recipeID, container);
            row.sizeDelta = new Vector2(0f, 72f);

            LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 72f;
            le.preferredHeight = 72f;
            le.flexibleWidth = 1f;

            Image bg = row.gameObject.AddComponent<Image>();
            bg.sprite = EconomyHUD.RoundedSprite;
            bg.type = Image.Type.Sliced;
            bg.color = style.cardColor;
            EconomyHUD.AddBlueBorder(row);

            Button btn = row.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(() => onSelect(recipe));

            RectTransform slot = EconomyHUD.CreateRect("IconSlot", row);
            slot.anchorMin = new Vector2(0f, 0.5f);
            slot.anchorMax = new Vector2(0f, 0.5f);
            slot.pivot = new Vector2(0f, 0.5f);
            slot.anchoredPosition = new Vector2(10f, 0f);
            slot.sizeDelta = new Vector2(50f, 50f);
            EconomyHUD.CreatePanel(slot, style.slotColor);

            ResourceData outRes = recipe.outputResource;
            Sprite qIcon = recipe != null ? recipe.GetIcon() : (outRes != null ? outRes.icon : null);
            Image icon = EconomyHUD.CreateIcon("Icon", slot, qIcon != null ? qIcon : EconomyHUD.CircleSprite, 42f, 4f);
            if (qIcon == null && outRes != null) icon.color = outRes.resourceColor;

            TextMeshProUGUI nameLbl = EconomyHUD.CreateText("Name", row, 15f, style.textColor, TextAlignmentOptions.Left);
            nameLbl.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            nameLbl.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            nameLbl.rectTransform.pivot = new Vector2(0f, 0.5f);
            nameLbl.rectTransform.anchoredPosition = new Vector2(68f, 12f);
            nameLbl.rectTransform.sizeDelta = new Vector2(250f, 24f);
            nameLbl.enableAutoSizing = true;
            nameLbl.fontSizeMin = 11f;
            nameLbl.fontSizeMax = 15f;
            nameLbl.textWrappingMode = TextWrappingModes.NoWrap;
            string cardOutId = recipe.outputResource != null ? recipe.outputResource.resourceID : recipe.recipeID;
            nameLbl.text = ResourceInventoryUIPresentation.LocalizeResourceName(cardOutId, recipe.recipeName);

            TextMeshProUGUI statusLbl = EconomyHUD.CreateText("Status", row, 13f, style.mutedColor, TextAlignmentOptions.Left);
            statusLbl.fontStyle = FontStyles.Normal;
            statusLbl.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            statusLbl.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            statusLbl.rectTransform.pivot = new Vector2(0f, 0.5f);
            statusLbl.rectTransform.anchoredPosition = new Vector2(68f, -14f);
            statusLbl.rectTransform.sizeDelta = new Vector2(250f, 20f);
            statusLbl.textWrappingMode = TextWrappingModes.NoWrap;
            statusLbl.overflowMode = TextOverflowModes.Ellipsis;

            RectTransform edBadge = EconomyHUD.CreateRect("EdibleBadge", row);
            edBadge.anchorMin = new Vector2(1f, 0.5f);
            edBadge.anchorMax = new Vector2(1f, 0.5f);
            edBadge.pivot = new Vector2(1f, 0.5f);
            edBadge.anchoredPosition = new Vector2(-12f, 0f);
            edBadge.sizeDelta = new Vector2(76f, 26f);
            EconomyHUD.CreatePanel(edBadge, new Color(0.12f, 0.35f, 0.22f, 0.95f));
            TextMeshProUGUI edLbl = EconomyHUD.CreateText("Lbl", edBadge, 11f, style.enoughColor, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(edLbl.rectTransform);
            edLbl.text = "EDIBLE";
            edBadge.gameObject.SetActive(recipe.isEdible);

            return new RecipeCardView
            {
                recipe = recipe,
                root = row,
                background = bg,
                icon = icon,
                name = nameLbl,
                status = statusLbl,
                edibleBadge = edBadge.gameObject,
                button = btn
            };
        }

        private static void BuildRightInspector(
            RectTransform container,
            CraftingUIStyleConfig style,
            Action<int> onAdjustQty,
            Action onMaxQty,
            Action onStartCraft,
            CraftingUIBindings b)
        {
            RectTransform col = EconomyHUD.CreateRect("RightCol", container);
            col.anchorMin = new Vector2(1f, 0f);
            col.anchorMax = new Vector2(1f, 1f);
            col.pivot = new Vector2(1f, 0.5f);
            col.sizeDelta = new Vector2(RightColWidth, 0f);
            col.anchoredPosition = Vector2.zero;
            EconomyHUD.CreateBorderedPanel(col, style.sectionColor);

            RectTransform header = EconomyHUD.CreateRect("Header", col);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0f, 1f);
            header.anchoredPosition = Vector2.zero;
            header.sizeDelta = new Vector2(0f, 120f);

            RectTransform showSlot = EconomyHUD.CreateRect("ShowSlot", header);
            showSlot.anchorMin = new Vector2(0f, 1f);
            showSlot.anchorMax = new Vector2(0f, 1f);
            showSlot.pivot = new Vector2(0f, 1f);
            showSlot.anchoredPosition = new Vector2(16f, -14f);
            showSlot.sizeDelta = new Vector2(76f, 76f);
            EconomyHUD.CreatePanel(showSlot, style.slotColor);

            b.inspectorIcon = EconomyHUD.CreateIcon("Icon", showSlot, EconomyHUD.CircleSprite, 64f, 6f);
            b.inspectorIcon.rectTransform.anchoredPosition = new Vector2(6f, 0f);

            b.inspectorTitle = EconomyHUD.CreateText("Title", header, 22f, style.textColor, TextAlignmentOptions.Left);
            b.inspectorTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            b.inspectorTitle.rectTransform.anchorMax = new Vector2(0f, 1f);
            b.inspectorTitle.rectTransform.pivot = new Vector2(0f, 1f);
            b.inspectorTitle.rectTransform.anchoredPosition = new Vector2(106f, -14f);
            b.inspectorTitle.rectTransform.sizeDelta = new Vector2(348f, 28f);
            b.inspectorTitle.enableAutoSizing = true;
            b.inspectorTitle.fontSizeMin = 14f;
            b.inspectorTitle.fontSizeMax = 22f;
            b.inspectorTitle.textWrappingMode = TextWrappingModes.NoWrap;
            b.inspectorTitle.overflowMode = TextOverflowModes.Ellipsis;

            b.inspectorCategory = EconomyHUD.CreateText("Category", header, 12f, style.tabActiveColor, TextAlignmentOptions.Left);
            b.inspectorCategory.rectTransform.anchorMin = new Vector2(0f, 1f);
            b.inspectorCategory.rectTransform.anchorMax = new Vector2(0f, 1f);
            b.inspectorCategory.rectTransform.pivot = new Vector2(0f, 1f);
            b.inspectorCategory.rectTransform.anchoredPosition = new Vector2(106f, -44f);
            b.inspectorCategory.rectTransform.sizeDelta = new Vector2(85f, 22f);

            b.inspectorEdibleTag = EconomyHUD.CreateText("EdibleTag", header, 12f, style.enoughColor, TextAlignmentOptions.Left);
            b.inspectorEdibleTag.rectTransform.anchorMin = new Vector2(0f, 1f);
            b.inspectorEdibleTag.rectTransform.anchorMax = new Vector2(0f, 1f);
            b.inspectorEdibleTag.rectTransform.pivot = new Vector2(0f, 1f);
            b.inspectorEdibleTag.rectTransform.anchoredPosition = new Vector2(196f, -44f);
            b.inspectorEdibleTag.rectTransform.sizeDelta = new Vector2(250f, 22f);
            b.inspectorEdibleTag.textWrappingMode = TextWrappingModes.NoWrap;
            b.inspectorEdibleTag.overflowMode = TextOverflowModes.Ellipsis;

            b.inspectorDesc = EconomyHUD.CreateText("Desc", header, 13f, style.mutedColor, TextAlignmentOptions.Left);
            b.inspectorDesc.fontStyle = FontStyles.Normal;
            b.inspectorDesc.rectTransform.anchorMin = new Vector2(0f, 1f);
            b.inspectorDesc.rectTransform.anchorMax = new Vector2(0f, 1f);
            b.inspectorDesc.rectTransform.pivot = new Vector2(0f, 1f);
            b.inspectorDesc.rectTransform.anchoredPosition = new Vector2(106f, -68f);
            b.inspectorDesc.rectTransform.sizeDelta = new Vector2(348f, 44f);
            b.inspectorDesc.textWrappingMode = TextWrappingModes.Normal;
            b.inspectorDesc.overflowMode = TextOverflowModes.Ellipsis;

            b.ingredientsContainer = EconomyHUD.CreateRect("Ingredients", col);
            b.ingredientsContainer.anchorMin = new Vector2(0f, 1f);
            b.ingredientsContainer.anchorMax = new Vector2(1f, 1f);
            b.ingredientsContainer.pivot = new Vector2(0f, 1f);
            b.ingredientsContainer.anchoredPosition = new Vector2(16f, -126f);
            b.ingredientsContainer.sizeDelta = new Vector2(-32f, 136f);

            VerticalLayoutGroup ingLayout = b.ingredientsContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            ingLayout.padding = new RectOffset(0, 0, 0, 0);
            ingLayout.spacing = 6f;
            ingLayout.childControlWidth = true;
            ingLayout.childControlHeight = false;
            ingLayout.childForceExpandWidth = true;
            ingLayout.childForceExpandHeight = false;

            RectTransform rawBox = EconomyHUD.CreateRect("RawBox", col);
            rawBox.anchorMin = new Vector2(0f, 0f);
            rawBox.anchorMax = new Vector2(1f, 0f);
            rawBox.pivot = new Vector2(0.5f, 0f);
            rawBox.anchoredPosition = new Vector2(0f, 120f);
            rawBox.sizeDelta = new Vector2(-32f, 54f);
            EconomyHUD.CreatePanel(rawBox, new Color(0.09f, 0.13f, 0.22f, 0.95f));

            b.rawBreakdownText = EconomyHUD.CreateText("RawTxt", rawBox, 13f, style.mutedColor, TextAlignmentOptions.Left);
            b.rawBreakdownText.fontStyle = FontStyles.Normal;
            b.rawBreakdownText.textWrappingMode = TextWrappingModes.Normal;
            EconomyHUD.Stretch(b.rawBreakdownText.rectTransform);
            b.rawBreakdownText.rectTransform.offsetMin = new Vector2(12f, 6f);
            b.rawBreakdownText.rectTransform.offsetMax = new Vector2(-12f, -6f);

            b.durationText = EconomyHUD.CreateText("Duration", col, 14f, style.textColor, TextAlignmentOptions.Center);
            b.durationText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            b.durationText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            b.durationText.rectTransform.pivot = new Vector2(0.5f, 0f);
            b.durationText.rectTransform.anchoredPosition = new Vector2(0f, 88f);
            b.durationText.rectTransform.sizeDelta = new Vector2(RightColWidth - 32f, 26f);

            RectTransform qtyBar = EconomyHUD.CreateRect("QtyBar", col);
            qtyBar.anchorMin = new Vector2(0.5f, 0f);
            qtyBar.anchorMax = new Vector2(0.5f, 0f);
            qtyBar.pivot = new Vector2(0.5f, 0f);
            qtyBar.anchoredPosition = new Vector2(0f, 50f);
            qtyBar.sizeDelta = new Vector2(RightColWidth - 32f, 34f);

            b.btnDecrease = EconomyHUD.AddButton(CreateCenterSubRect(qtyBar, -80f, new Vector2(36f, 32f)), style.secondaryColor);
            b.btnDecrease.onClick.AddListener(() => onAdjustQty(-1));
            TextMeshProUGUI decLbl = EconomyHUD.CreateText("L", b.btnDecrease.GetComponent<RectTransform>(), 16f, Color.white, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(decLbl.rectTransform);
            decLbl.text = "-";

            RectTransform qtyBox = CreateCenterSubRect(qtyBar, 0f, new Vector2(80f, 32f));
            EconomyHUD.CreatePanel(qtyBox, style.slotColor);
            b.quantityText = EconomyHUD.CreateText("Qty", qtyBox, 16f, style.textColor, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(b.quantityText.rectTransform);

            b.btnIncrease = EconomyHUD.AddButton(CreateCenterSubRect(qtyBar, +80f, new Vector2(36f, 32f)), style.secondaryColor);
            b.btnIncrease.onClick.AddListener(() => onAdjustQty(1));
            TextMeshProUGUI incLbl = EconomyHUD.CreateText("L", b.btnIncrease.GetComponent<RectTransform>(), 16f, Color.white, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(incLbl.rectTransform);
            incLbl.text = "+";

            RectTransform maxRect = EconomyHUD.CreateRect("MaxBtn", qtyBar);
            maxRect.anchorMin = new Vector2(1f, 0.5f);
            maxRect.anchorMax = new Vector2(1f, 0.5f);
            maxRect.pivot = new Vector2(1f, 0.5f);
            maxRect.anchoredPosition = new Vector2(0f, 0f);
            maxRect.sizeDelta = new Vector2(84f, 32f);
            b.btnMax = EconomyHUD.AddButton(maxRect, style.secondaryColor);
            b.btnMax.onClick.AddListener(() => onMaxQty());
            TextMeshProUGUI maxLbl = EconomyHUD.CreateText("L", maxRect, 14f, Color.white, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(maxLbl.rectTransform);
            maxLbl.text = "Max";

            RectTransform craftBtnRect = EconomyHUD.CreateRect("CraftBtn", col);
            craftBtnRect.anchorMin = new Vector2(0.5f, 0f);
            craftBtnRect.anchorMax = new Vector2(0.5f, 0f);
            craftBtnRect.pivot = new Vector2(0.5f, 0f);
            craftBtnRect.anchoredPosition = new Vector2(0f, 6f);
            craftBtnRect.sizeDelta = new Vector2(RightColWidth - 32f, 40f);

            b.btnStartCraft = EconomyHUD.AddButton(craftBtnRect, style.accentColor);
            b.btnStartCraft.onClick.AddListener(() => onStartCraft());
            b.btnStartCraftLabel = EconomyHUD.CreateText("Lbl", craftBtnRect, 16f, Color.white, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(b.btnStartCraftLabel.rectTransform);
            b.btnStartCraftLabel.text = "START CRAFTING";
        }

        public static RectTransform CreateCenterSubRect(RectTransform parent, float xOffset, Vector2 size)
        {
            RectTransform rect = EconomyHUD.CreateRect("Sub", parent);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(xOffset, 0f);
            rect.sizeDelta = size;
            return rect;
        }

        private static void BuildQueueView(RectTransform container, CraftingUIStyleConfig style, Action onClaimAll, CraftingUIBindings b)
        {
            RectTransform topBar = EconomyHUD.CreateRect("TopBar", container);
            topBar.anchorMin = new Vector2(0f, 1f);
            topBar.anchorMax = new Vector2(1f, 1f);
            topBar.pivot = new Vector2(0f, 1f);
            topBar.anchoredPosition = Vector2.zero;
            topBar.sizeDelta = new Vector2(0f, 44f);

            b.queueSummaryText = EconomyHUD.CreateText("Summary", topBar, 16f, style.textColor, TextAlignmentOptions.Left);
            b.queueSummaryText.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            b.queueSummaryText.rectTransform.sizeDelta = new Vector2(400f, 36f);

            RectTransform claimAllRect = EconomyHUD.CreateRect("ClaimAllBtn", topBar);
            claimAllRect.anchorMin = new Vector2(1f, 0.5f);
            claimAllRect.anchorMax = new Vector2(1f, 0.5f);
            claimAllRect.pivot = new Vector2(1f, 0.5f);
            claimAllRect.anchoredPosition = Vector2.zero;
            claimAllRect.sizeDelta = new Vector2(160f, 38f);
            b.btnClaimAll = EconomyHUD.AddButton(claimAllRect, style.accentColor);
            b.btnClaimAll.onClick.AddListener(() => onClaimAll());
            b.btnClaimAllLabel = EconomyHUD.CreateText("Lbl", claimAllRect, 14f, Color.white, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(b.btnClaimAllLabel.rectTransform);
            b.btnClaimAllLabel.text = "CLAIM ALL";

            RectTransform scrollArea = EconomyHUD.CreateRect("ScrollArea", container);
            scrollArea.anchorMin = Vector2.zero;
            scrollArea.anchorMax = Vector2.one;
            scrollArea.offsetMin = Vector2.zero;
            scrollArea.offsetMax = new Vector2(0f, -50f);

            b.queueScrollRect = scrollArea.gameObject.AddComponent<ScrollRect>();
            b.queueScrollRect.horizontal = false;
            b.queueScrollRect.vertical = true;
            b.queueScrollRect.scrollSensitivity = 35f;
            b.queueScrollRect.movementType = ScrollRect.MovementType.Elastic;
            b.queueScrollRect.elasticity = 0.1f;
            b.queueScrollRect.inertia = true;
            b.queueScrollRect.decelerationRate = 0.135f;

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
            qLayout.padding = new RectOffset(4, 4, 4, 8);
            qLayout.spacing = 8f;
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
            row.sizeDelta = new Vector2(PanelWidth - 64f, 68f);
            EconomyHUD.CreateBorderedPanel(row, style.cardColor);

            RectTransform slot = EconomyHUD.CreateRect("Slot", row);
            slot.anchorMin = new Vector2(0f, 0.5f);
            slot.anchorMax = new Vector2(0f, 0.5f);
            slot.pivot = new Vector2(0f, 0.5f);
            slot.anchoredPosition = new Vector2(10f, 0f);
            slot.sizeDelta = new Vector2(48f, 48f);
            EconomyHUD.CreatePanel(slot, style.slotColor);

            Sprite rowIcon = recipe != null ? recipe.GetIcon() : (outRes != null ? outRes.icon : null);
            Image icon = EconomyHUD.CreateIcon("Icon", slot, rowIcon != null ? rowIcon : EconomyHUD.CircleSprite, 40f, 4f);
            if (rowIcon == null && outRes != null) icon.color = outRes.resourceColor;

            TextMeshProUGUI title = EconomyHUD.CreateText("Title", row, 17f, style.textColor, TextAlignmentOptions.Left);
            title.rectTransform.anchoredPosition = new Vector2(68f, 10f);
            title.rectTransform.sizeDelta = new Vector2(300f, 26f);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            string qId = recipe != null ? (recipe.outputResource != null ? recipe.outputResource.resourceID : recipe.recipeID) : "";
            string rName = recipe != null ? ResourceInventoryUIPresentation.LocalizeResourceName(qId, recipe.recipeName) : "Crafted Product";
            title.text = $"{rName} <b>x{job.TotalOutputAmount}</b>";

            TextMeshProUGUI status = EconomyHUD.CreateText("Status", row, 14f, style.mutedColor, TextAlignmentOptions.Left);
            status.rectTransform.anchoredPosition = new Vector2(68f, -14f);
            status.rectTransform.sizeDelta = new Vector2(300f, 22f);
            status.textWrappingMode = TextWrappingModes.NoWrap;
            status.overflowMode = TextOverflowModes.Ellipsis;

            RectTransform barBg = EconomyHUD.CreateRect("BarBg", row);
            barBg.anchorMin = new Vector2(0.48f, 0.5f);
            barBg.anchorMax = new Vector2(0.48f, 0.5f);
            barBg.pivot = new Vector2(0f, 0.5f);
            barBg.sizeDelta = new Vector2(240f, 16f);
            EconomyHUD.CreatePanel(barBg, style.sectionColor);

            RectTransform barFill = EconomyHUD.CreateRect("BarFill", barBg);
            barFill.anchorMin = Vector2.zero;
            barFill.anchorMax = new Vector2(0f, 1f);
            barFill.pivot = new Vector2(0f, 0.5f);
            barFill.sizeDelta = new Vector2(0f, 0f);
            EconomyHUD.CreatePanel(barFill, style.accentColor);

            RectTransform btnRect = EconomyHUD.CreateRect("ClaimBtn", row);
            btnRect.anchorMin = new Vector2(1f, 0.5f);
            btnRect.anchorMax = new Vector2(1f, 0.5f);
            btnRect.pivot = new Vector2(1f, 0.5f);
            btnRect.anchoredPosition = new Vector2(-12f, 0f);
            btnRect.sizeDelta = new Vector2(110f, 38f);
            Button claimBtn = EconomyHUD.AddButton(btnRect, style.accentColor);
            TextMeshProUGUI claimLbl = EconomyHUD.CreateText("Lbl", btnRect, 15f, Color.white, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(claimLbl.rectTransform);
            claimLbl.text = "CLAIM";

            claimBtn.onClick.AddListener(() =>
            {
                if (CraftingQueueSystem.Instance.TryClaimJob(job, out string err))
                {
                    onClaimSuccess(job, rName);
                }
                else
                {
                    onClaimFail(err);
                }
            });

            return new QueueCardView
            {
                job = job,
                root = row,
                icon = icon,
                title = title,
                status = status,
                progressBarFill = barFill,
                claimButton = claimBtn,
                claimLabel = claimLbl
            };
        }
    }
}

