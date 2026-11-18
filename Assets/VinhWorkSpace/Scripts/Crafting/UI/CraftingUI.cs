using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LumiWorld.Acs;

namespace LumiWorld.Economy.Crafting
{
    /// <summary>
    /// Giao diện Chế biến Tài nguyên (Crafting UI — P-01 / P-02 / P-15).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CraftingUI : MonoBehaviour
    {
        public enum TabFilter { All, Food, Refined, Herbal, Paper, Queue }

        [Header("Nút mở trên HUD")]
        [SerializeField] private Vector2 buttonCorner = new Vector2(-20f, -232f);
        [SerializeField] private Vector2 buttonSize = new Vector2(170f, 44f);

        [Header("Màu sắc giao diện (Look & Feel)")]
        [SerializeField] private Color hudColor = new Color(0.27f, 0.27f, 0.25f, 1f);
        [SerializeField] private Color backdropColor = new Color(0.14f, 0.12f, 0.10f, 0.48f);
        [SerializeField] private Color panelColor = new Color(0.96f, 0.95f, 0.93f, 1f);
        [SerializeField] private Color cardColor = new Color(0.98f, 0.98f, 0.96f, 1f);
        [SerializeField] private Color selectedCardColor = new Color(0.98f, 0.98f, 0.96f, 1f);
        [SerializeField] private Color sectionColor = new Color(0.91f, 0.89f, 0.87f, 1f);
        [SerializeField] private Color slotColor = new Color(0.96f, 0.95f, 0.93f, 1f);
        [SerializeField] private Color textColor = new Color(0.21f, 0.16f, 0.12f, 1f);
        [SerializeField] private Color mutedColor = new Color(0.46f, 0.42f, 0.38f, 1f);
        [SerializeField] private Color enoughColor = new Color(0.37f, 0.52f, 0.31f, 1f);
        [SerializeField] private Color missingColor = new Color(0.67f, 0.32f, 0.28f, 1f);
        [SerializeField] private Color accentColor = new Color(0.27f, 0.27f, 0.25f, 1f);
        [SerializeField] private Color secondaryColor = new Color(0.91f, 0.89f, 0.87f, 1f);
        [SerializeField] private Color tabActiveColor = new Color(0.98f, 0.98f, 0.96f, 1f);
        [SerializeField] private Color tabInactiveColor = new Color(0.91f, 0.89f, 0.87f, 1f);

        private CraftingUIBindings ui;
        private float panelScale = 1f;
        private Coroutine popRoutine;

        private TabFilter currentTab = TabFilter.Food;
        private bool queueOpen;
        private CraftingRecipeData selectedRecipe;
        private int selectedQuantity = 1;

        private readonly List<QueueCardView> queueCards = new List<QueueCardView>();
        private ResourceInventory subscribedInventory;
        private CraftingQueueSystem subscribedQueue;

        public bool IsOpen => ui != null && ui.window != null && ui.window.activeSelf;

        public void OpenRecipe(CraftingRecipeData recipe)
        {
            if (recipe == null || ui == null) return;
            currentTab = recipe.category == CraftingCategory.Food ? TabFilter.Food :
                recipe.category == CraftingCategory.Herbal ? TabFilter.Herbal : TabFilter.Paper;
            selectedRecipe = recipe;
            selectedQuantity = 1;
            CloseIngredientPopover();
            Open();
            Refresh();
        }

        private CraftingUIStyleConfig GetStyleConfig() => new CraftingUIStyleConfig
        {
            buttonCorner = buttonCorner,
            buttonSize = buttonSize,
            hudColor = hudColor,
            backdropColor = backdropColor,
            panelColor = panelColor,
            cardColor = cardColor,
            selectedCardColor = selectedCardColor,
            sectionColor = sectionColor,
            slotColor = slotColor,
            textColor = textColor,
            mutedColor = mutedColor,
            enoughColor = enoughColor,
            missingColor = missingColor,
            accentColor = accentColor,
            secondaryColor = secondaryColor,
            tabActiveColor = tabActiveColor,
            tabInactiveColor = tabInactiveColor
        };

        private void Start()
        {
            panelColor = LumiWorldUITheme.Window;
            cardColor = LumiWorldUITheme.Surface;
            selectedCardColor = LumiWorldUITheme.Surface;
            sectionColor = LumiWorldUITheme.Inset;
            slotColor = LumiWorldUITheme.Window;
            textColor = LumiWorldUITheme.Text;
            mutedColor = LumiWorldUITheme.Muted;
            enoughColor = LumiWorldUITheme.Success;
            missingColor = LumiWorldUITheme.Error;
            accentColor = hudColor = LumiWorldUITheme.Primary;
            secondaryColor = tabInactiveColor = LumiWorldUITheme.Inset;
            tabActiveColor = LumiWorldUITheme.Surface;

            ui = CraftingUIBuilder.BuildAll(
                transform,
                GetStyleConfig(),
                SwitchToInventory,
                Close,
                ToggleQueueDrawer,
                OnTabSelected,
                SelectRecipe,
                AdjustQuantity,
                StartCraftingCurrent,
                ClaimAllCompletedJobs
            );

            subscribedInventory = ResourceInventory.Instance;
            if (subscribedInventory != null) subscribedInventory.OnInventoryChanged += HandleInventoryChanged;

            subscribedQueue = CraftingQueueSystem.Instance;
            if (subscribedQueue != null)
            {
                subscribedQueue.OnJobStarted += HandleJobUpdated;
                subscribedQueue.OnJobCompleted += HandleJobUpdated;
                subscribedQueue.OnJobClaimed += HandleJobUpdated;
                subscribedQueue.OnQueueChanged += Refresh;
            }

            if (CraftingCatalog.All.Count > 0)
                SelectRecipe(CraftingCatalog.All[0]);

            Refresh();
        }

        private void Update()
        {
            if (!IsOpen) return;
            if (popRoutine == null && ui != null && ui.panel != null)
            {
                panelScale = ComputePanelScale();
                ui.panel.localScale = new Vector3(panelScale, panelScale, 1f);
            }
            if (queueOpen)
                RefreshQueueProgress();
        }

        private float ComputePanelScale()
        {
            var invCtrl = ResourceInventoryUIController.Instance;
            if (invCtrl != null && transform.lossyScale.x > 0.0001f && invCtrl.transform.lossyScale.x > 0.0001f)
            {
                return invCtrl.transform.lossyScale.x / transform.lossyScale.x;
            }

            Canvas rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (rootCanvas != null && rootCanvas.scaleFactor > 0f)
            {
                Vector2 pixels = ((RectTransform)rootCanvas.transform).rect.size * rootCanvas.scaleFactor;
                float fit = ResourceInventoryUILayout.FitScale(pixels.x, pixels.y);
                float parentPixelScale = transform.localScale.x * rootCanvas.scaleFactor;
                if (fit > 0f && parentPixelScale > 0.0001f)
                    return fit / parentPixelScale;
            }
            Rect area = ((RectTransform)transform).rect;
            return area.width > 0f && area.height > 0f
                ? ResourceInventoryUILayout.FitScale(area.width, area.height)
                : 1f;
        }

        private void OnDestroy()
        {
            if (subscribedInventory != null) subscribedInventory.OnInventoryChanged -= HandleInventoryChanged;
            subscribedInventory = null;

            if (subscribedQueue != null)
            {
                subscribedQueue.OnJobStarted -= HandleJobUpdated;
                subscribedQueue.OnJobCompleted -= HandleJobUpdated;
                subscribedQueue.OnJobClaimed -= HandleJobUpdated;
                subscribedQueue.OnQueueChanged -= Refresh;
            }
            subscribedQueue = null;
        }

        private void SwitchToInventory()
        {
            Close();
            ResourceInventoryUIController.Instance?.OpenWarehouse();
        }

        private void ToggleQueueDrawer()
        {
            queueOpen = !queueOpen;
            CloseIngredientPopover();
            CraftingUIBuilder.ApplyQueueLayout(ui, queueOpen);
            Refresh();
        }

        // =========================================================
        // OPEN & CLOSE WINDOW
        // =========================================================

        public void Open()
        {
            if (ui == null || ui.window == null) return;
            if (!IsOpen)
            {
                queueOpen = false;
                CraftingUIBuilder.ApplyQueueLayout(ui, false);
                CloseIngredientPopover();

                transform.SetAsLastSibling();
                ui.window.SetActive(true);
                EconomyHUD.BringWindowToFront(ui.window);

                panelScale = ComputePanelScale();

                if (popRoutine != null) StopCoroutine(popRoutine);
                popRoutine = StartCoroutine(PopIn());
            }
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            CloseIngredientPopover();
            ui.window.SetActive(false);
            EconomyHUD.MarkWindowClosed();
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        private IEnumerator PopIn()
        {
            const float duration = 0.14f;
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(time / duration);
                float scale = panelScale * Mathf.Lerp(0.94f, 1f, 1f - (1f - t) * (1f - t));
                ui.panel.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            ui.panel.localScale = new Vector3(panelScale, panelScale, 1f);
            popRoutine = null;
        }

        // =========================================================
        // REFRESH UI & QUEUE BADGE
        // =========================================================

        private void HandleInventoryChanged(string id, int newAmount, int delta) => Refresh();
        private void HandleJobUpdated(ActiveCraftingJob job) => Refresh();

        private void Refresh()
        {
            RefreshBadge();
            if (!IsOpen) return;

            RefreshTabs();
            RefreshRecipeList();
            RefreshInspector();
            if (queueOpen)
                RefreshQueueList();
        }

        private void RefreshBadge()
        {
            if (ui == null) return;
            var queue = CraftingQueueSystem.Instance;
            int totalJobs = queue != null ? queue.ActiveJobs.Count : 0;
            int completed = queue != null ? queue.GetCompletedCount() : 0;

            if (ui.queueToggleLabel != null)
                ui.queueToggleLabel.text = $"Queue ({totalJobs})";

            CollapsibleHUDToolbar.Instance?.SetCraftingReadyCount(completed);
        }

        private void RefreshTabs()
        {
            if (ui == null) return;
            foreach (var item in ui.tabButtons)
            {
                bool active = item.tab == currentTab;
                item.bg.color = active ? LumiWorldUITheme.Slot : LumiWorldUITheme.Inset;
                if (item.border != null) item.border.gameObject.SetActive(active);
                item.label.color = active ? LumiWorldUITheme.Text : LumiWorldUITheme.Muted;
                if (item.icon != null)
                    item.icon.color = active ? LumiWorldUITheme.Text : LumiWorldUITheme.Muted;
            }
        }

        private void OnTabSelected(TabFilter tab)
        {
            if (tab == TabFilter.Queue)
            {
                ToggleQueueDrawer();
                return;
            }
            currentTab = tab;
            CloseIngredientPopover();
            if (ui.recipeScrollRect != null) ui.recipeScrollRect.verticalNormalizedPosition = 1f;
            Refresh();
        }

        private void RefreshRecipeList()
        {
            if (ui == null) return;
            var inventory = ResourceInventory.Instance;
            CraftingRecipeData firstVisible = null;

            foreach (var card in ui.recipeCards)
            {
                bool matchesTab = MatchesFilter(card.recipe, currentTab);
                card.root.gameObject.SetActive(matchesTab);
                if (!matchesTab) continue;

                if (firstVisible == null) firstVisible = card.recipe;

                bool isSelected = selectedRecipe != null && selectedRecipe.recipeID == card.recipe.recipeID;
                card.background.color = LumiWorldUITheme.Slot;
                if (card.selectedBorder != null)
                    card.selectedBorder.gameObject.SetActive(isSelected);

                int maxCraft = CraftingCalculator.GetMaxCraftableCount(card.recipe, inventory);
                if (maxCraft > 0)
                {
                    if (card.statusIcon != null)
                    {
                        card.statusIcon.sprite = LumiWorldUITheme.StatusReadySprite;
                        card.statusIcon.color = Color.white;
                    }
                    card.status.text = "Ready";
                    card.status.color = LumiWorldUITheme.Success;
                }
                else
                {
                    if (card.statusIcon != null)
                    {
                        card.statusIcon.sprite = LumiWorldUITheme.StatusLowSprite;
                        card.statusIcon.color = Color.white;
                    }
                    card.status.text = "Low";
                    card.status.color = LumiWorldUITheme.Warning;
                }
            }

            if ((selectedRecipe == null || !MatchesFilter(selectedRecipe, currentTab)) && firstVisible != null)
                SelectRecipe(firstVisible);
        }

        private bool MatchesFilter(CraftingRecipeData recipe, TabFilter filter)
        {
            if (recipe == null) return false;
            return filter switch
            {
                TabFilter.Food => recipe.category == CraftingCategory.Food,
                TabFilter.Herbal => recipe.category == CraftingCategory.Herbal,
                TabFilter.Paper => recipe.category == CraftingCategory.Paper || recipe.category == CraftingCategory.Refined,
                _ => true
            };
        }

        private void SelectRecipe(CraftingRecipeData recipe)
        {
            selectedRecipe = recipe;
            selectedQuantity = 1;
            CloseIngredientPopover();
            Refresh();
        }

        private void RefreshInspector()
        {
            if (selectedRecipe == null || ui == null) return;

            var inventory = ResourceInventory.Instance;
            var outRes = selectedRecipe.outputResource;

            string outId = outRes != null ? outRes.resourceID : selectedRecipe.recipeID;
            ui.inspectorTitle.text = ResourceInventoryUIPresentation.LocalizeResourceName(outId, selectedRecipe.recipeName);

            int outputPerBatch = Mathf.Max(1, selectedRecipe.outputAmount);
            if (ui.inspectorOutputText != null)
                ui.inspectorOutputText.text = $"Output ×{outputPerBatch * selectedQuantity}";

            Sprite mainIcon = selectedRecipe.GetIcon();
            if (mainIcon != null)
            {
                ui.inspectorIcon.sprite = mainIcon;
                ui.inspectorIcon.color = Color.white;
            }
            else
            {
                ui.inspectorIcon.sprite = EconomyHUD.CircleSprite;
                ui.inspectorIcon.color = outRes != null ? outRes.resourceColor : new Color(0.7f, 0.45f, 0.2f, 1f);
            }

            EconomyHUD.ClearChildren(ui.ingredientsContainer);
            foreach (var input in selectedRecipe.inputs)
            {
                if (input == null) continue;
                ResourceData res = CraftingCatalog.GetResource(input.resourceId);
                string rawResName = res != null && !string.IsNullOrEmpty(res.resourceName) ? res.resourceName : input.resourceId;
                string resName = ResourceInventoryUIPresentation.LocalizeResourceName(input.resourceId, rawResName);
                int owned = inventory != null ? inventory.GetAmount(input.resourceId) : 0;
                int needed = input.amount * selectedQuantity;
                bool enough = owned >= needed;

                RectTransform row = EconomyHUD.CreateRect("IngRow_" + input.resourceId, ui.ingredientsContainer);
                row.sizeDelta = new Vector2(0f, 56f);
                LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
                le.minHeight = 56f;
                le.preferredHeight = 56f;
                le.flexibleWidth = 1f;

                Color rowFill = enough ? LumiWorldUITheme.Window : new Color(0.96f, 0.90f, 0.89f, 1f);
                Image rowBg = EconomyHUD.CreatePanel(row, rowFill);
                rowBg.raycastTarget = true;

                // Click ingredient row / icon -> open P-15 Resource Source Popover
                Button rowBtn = row.gameObject.AddComponent<Button>();
                rowBtn.targetGraphic = rowBg;
                rowBtn.navigation = new Navigation { mode = Navigation.Mode.None };
                string capturedResourceId = input.resourceId;
                rowBtn.onClick.AddListener(() => OpenIngredientPopover(capturedResourceId));

                Image icon = EconomyHUD.CreateIcon("Icon", row, res != null && res.icon != null ? res.icon : EconomyHUD.CircleSprite, 44f, 12f);
                if (res != null) icon.color = res.icon == null ? res.resourceColor : Color.white;

                TextMeshProUGUI nameLbl = EconomyHUD.CreateText("Name", row, 16f, LumiWorldUITheme.Text, TextAlignmentOptions.MidlineLeft);
                if (LumiWorldUITheme.Heading != null) nameLbl.font = LumiWorldUITheme.Heading;
                nameLbl.rectTransform.anchorMin = new Vector2(0f, 0f);
                nameLbl.rectTransform.anchorMax = new Vector2(1f, 1f);
                nameLbl.rectTransform.offsetMin = new Vector2(66f, 4f);
                nameLbl.rectTransform.offsetMax = new Vector2(-132f, -4f);
                nameLbl.enableAutoSizing = true;
                nameLbl.fontSizeMin = 12f;
                nameLbl.fontSizeMax = 16f;
                nameLbl.textWrappingMode = TextWrappingModes.NoWrap;
                nameLbl.overflowMode = TextOverflowModes.Ellipsis;
                nameLbl.text = resName;

                string countStr = $"{owned} / {needed}";
                TextMeshProUGUI countLbl = EconomyHUD.CreateText("Count", row, 16.5f, enough ? LumiWorldUITheme.Text : LumiWorldUITheme.Error, TextAlignmentOptions.MidlineRight);
                if (LumiWorldUITheme.Heading != null) countLbl.font = LumiWorldUITheme.Heading;
                countLbl.rectTransform.anchorMin = new Vector2(1f, 0f);
                countLbl.rectTransform.anchorMax = new Vector2(1f, 1f);
                countLbl.rectTransform.pivot = new Vector2(1f, 0.5f);
                countLbl.rectTransform.anchoredPosition = new Vector2(-16f, 0f);
                countLbl.rectTransform.sizeDelta = new Vector2(96f, 0f);
                countLbl.textWrappingMode = TextWrappingModes.NoWrap;
                countLbl.text = countStr;

                if (enough && LumiWorldUITheme.StatusReadySprite != null)
                {
                    float approxTextWidth = Mathf.Max(46f, countStr.Length * 9.2f);
                    Image chk = EconomyHUD.CreateIcon("ReadyCheck", row, LumiWorldUITheme.StatusReadySprite, 20f, 0f);
                    chk.rectTransform.anchorMin = chk.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                    chk.rectTransform.pivot = new Vector2(1f, 0.5f);
                    chk.rectTransform.anchoredPosition = new Vector2(-22f - approxTextWidth, 0f);
                }
            }

            float totalDuration = selectedRecipe.craftDurationSeconds * selectedQuantity;
            ui.durationText.text = totalDuration > 0 ? $"{totalDuration:0.#}s" : "Instant";
            ui.quantityText.text = selectedQuantity.ToString(CultureInfo.InvariantCulture);

            bool canCraft = CraftingCalculator.CanCraft(selectedRecipe, inventory, selectedQuantity);
            ui.btnStartCraft.interactable = canCraft;
            ui.btnStartCraftLabel.text = "Craft";
            ui.btnStartCraftLabel.color = canCraft ? Color.white : LumiWorldUITheme.Text;
            if (ui.btnStartCraftIcon != null)
                ui.btnStartCraftIcon.color = canCraft ? Color.white : LumiWorldUITheme.Text;
        }

        // =========================================================
        // P-15 RESOURCE SOURCE POPOVER
        // =========================================================

        private void OpenIngredientPopover(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId) || ui == null || ui.panel == null) return;
            CloseIngredientPopover();

            ResourceData res = CraftingCatalog.GetResource(resourceId);
            string rawName = res != null && !string.IsNullOrEmpty(res.resourceName) ? res.resourceName : resourceId;
            string displayName = ResourceInventoryUIPresentation.LocalizeResourceName(resourceId, rawName);
            int owned = ResourceInventory.Instance != null ? ResourceInventory.Instance.GetAmount(resourceId) : 0;

            CraftingRecipeData subRecipe = CraftingCatalog.FindByOutput(resourceId);
            BiomeAffinity? sourceAffinity = ResolveBiomeAffinity(resourceId);
            string biomeName = sourceAffinity.HasValue ? sourceAffinity.Value.ToString() : "Leafwood";
            string sourceLabel = subRecipe != null ? "Crafting recipe" : $"{biomeName} harvest";
            Sprite sourceIcon = subRecipe != null ? LumiWorldUITheme.Glyph("G-02") : LumiWorldUITheme.BiomeEmblem(biomeName);

            RectTransform overlay = EconomyHUD.CreateRect("IngredientPopoverOverlay", ui.panel);
            EconomyHUD.Stretch(overlay);
            ui.ingredientPopover = overlay;

            Image dismissImg = overlay.gameObject.AddComponent<Image>();
            dismissImg.color = new Color(0.14f, 0.12f, 0.10f, 0.18f);
            dismissImg.raycastTarget = true;
            Button dismissBtn = overlay.gameObject.AddComponent<Button>();
            dismissBtn.transition = Selectable.Transition.None;
            dismissBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            dismissBtn.onClick.AddListener(CloseIngredientPopover);

            RectTransform card = EconomyHUD.CreateRect("PopoverCard", overlay);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = new Vector2(120f, -10f);
            card.sizeDelta = new Vector2(316f, 240f);
            Image cardBg = EconomyHUD.CreateBorderedPanel(card, LumiWorldUITheme.Surface, LumiWorldUITheme.Edge);
            cardBg.raycastTarget = true;
            LumiWorldUITheme.AddSoftElevation(card.gameObject, 5f, 0.22f);

            // Consume clicks inside popover card
            Button cardBlocker = card.gameObject.AddComponent<Button>();
            cardBlocker.transition = Selectable.Transition.None;
            cardBlocker.navigation = new Navigation { mode = Navigation.Mode.None };

            // Top: Icon + Name + Owned + [×]
            Sprite itemIcon = res != null && res.icon != null ? res.icon : EconomyHUD.CircleSprite;
            Image ic = EconomyHUD.CreateIcon("ItemIcon", card, itemIcon, 54f, 18f);
            ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0f, 1f);
            ic.rectTransform.pivot = new Vector2(0f, 1f);
            ic.rectTransform.anchoredPosition = new Vector2(18f, -18f);

            TextMeshProUGUI title = EconomyHUD.CreateText("Title", card, 19f, LumiWorldUITheme.Text, TextAlignmentOptions.MidlineLeft);
            if (LumiWorldUITheme.Heading != null) title.font = LumiWorldUITheme.Heading;
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.offsetMin = new Vector2(82f, -46f);
            title.rectTransform.offsetMax = new Vector2(-54f, -16f);
            title.enableAutoSizing = true;
            title.fontSizeMin = 14f;
            title.fontSizeMax = 19f;
            title.text = displayName;

            TextMeshProUGUI ownedTxt = EconomyHUD.CreateText("Owned", card, 14f, LumiWorldUITheme.Muted, TextAlignmentOptions.MidlineLeft);
            ownedTxt.rectTransform.anchorMin = new Vector2(0f, 1f);
            ownedTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
            ownedTxt.rectTransform.pivot = new Vector2(0f, 1f);
            ownedTxt.rectTransform.offsetMin = new Vector2(82f, -70f);
            ownedTxt.rectTransform.offsetMax = new Vector2(-20f, -46f);
            ownedTxt.text = $"Owned: {owned}";

            RectTransform closeRect = EconomyHUD.CreateRect("ClosePopover", card);
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-14f, -14f);
            closeRect.sizeDelta = new Vector2(32f, 32f);
            Button closeBtn = EconomyHUD.AddButton(closeRect, LumiWorldUITheme.Inset);
            closeBtn.onClick.AddListener(CloseIngredientPopover);
            Sprite closeSp = LumiWorldUITheme.Glyph("Close");
            if (closeSp != null)
            {
                Image xIc = EconomyHUD.CreateIcon("X", closeRect, closeSp, 16f, 8f);
                xIc.color = LumiWorldUITheme.Text;
            }

            // Divider
            RectTransform div = EconomyHUD.CreateRect("Div", card);
            div.anchorMin = new Vector2(0f, 1f);
            div.anchorMax = new Vector2(1f, 1f);
            div.pivot = new Vector2(0.5f, 1f);
            div.anchoredPosition = new Vector2(0f, -82f);
            div.sizeDelta = new Vector2(-36f, 1.5f);
            div.gameObject.AddComponent<Image>().color = LumiWorldUITheme.Inset;

            // Source Heading
            TextMeshProUGUI srcHead = EconomyHUD.CreateText("SourceHeading", card, 13.5f, LumiWorldUITheme.Muted, TextAlignmentOptions.MidlineLeft);
            srcHead.rectTransform.anchorMin = new Vector2(0f, 1f);
            srcHead.rectTransform.anchorMax = new Vector2(1f, 1f);
            srcHead.rectTransform.pivot = new Vector2(0f, 1f);
            srcHead.rectTransform.offsetMin = new Vector2(18f, -112f);
            srcHead.rectTransform.offsetMax = new Vector2(-18f, -90f);
            srcHead.text = "Source";

            // Source Pill (with colored Biome Emblem B-01..B-07 per P-15)
            RectTransform srcPill = EconomyHUD.CreateRect("SourcePill", card);
            srcPill.anchorMin = new Vector2(0f, 1f);
            srcPill.anchorMax = new Vector2(1f, 1f);
            srcPill.pivot = new Vector2(0.5f, 1f);
            srcPill.anchoredPosition = new Vector2(0f, -116f);
            srcPill.sizeDelta = new Vector2(-36f, 48f);
            EconomyHUD.CreatePanel(srcPill, LumiWorldUITheme.Window);

            if (sourceIcon != null)
            {
                Image srcIc = EconomyHUD.CreateIcon("BiomeIcon", srcPill, sourceIcon, 36f, 10f);
                srcIc.color = subRecipe != null ? LumiWorldUITheme.Text : Color.white;
            }

            TextMeshProUGUI srcTxt = EconomyHUD.CreateText("SourceText", srcPill, 15f, LumiWorldUITheme.Text, TextAlignmentOptions.MidlineLeft);
            if (LumiWorldUITheme.Heading != null) srcTxt.font = LumiWorldUITheme.Heading;
            EconomyHUD.Stretch(srcTxt.rectTransform);
            srcTxt.rectTransform.offsetMin = new Vector2(54f, 0f);
            srcTxt.rectTransform.offsetMax = new Vector2(-12f, 0f);
            srcTxt.text = sourceLabel;

            // [View source] Button (kept inside P-15 popover per confirmed spec)
            RectTransform viewBtnRect = EconomyHUD.CreateRect("ViewSourceBtn", card);
            viewBtnRect.anchorMin = new Vector2(0f, 0f);
            viewBtnRect.anchorMax = new Vector2(1f, 0f);
            viewBtnRect.pivot = new Vector2(0.5f, 0f);
            viewBtnRect.anchoredPosition = new Vector2(0f, 16f);
            viewBtnRect.sizeDelta = new Vector2(-36f, 44f);

            Button viewBtn = EconomyHUD.AddButton(viewBtnRect, LumiWorldUITheme.Primary);
            LumiWorldUITheme.AddSoftElevation(viewBtnRect.gameObject, 2.5f, 0.16f);
            TextMeshProUGUI viewLbl = EconomyHUD.CreateText("Lbl", viewBtnRect, 15.5f, Color.white, TextAlignmentOptions.Center);
            if (LumiWorldUITheme.Heading != null) viewLbl.font = LumiWorldUITheme.Heading;
            EconomyHUD.Stretch(viewLbl.rectTransform);
            viewLbl.text = "View source";

            viewBtn.onClick.AddListener(() =>
            {
                CloseIngredientPopover();
                if (subRecipe != null)
                {
                    OpenRecipe(subRecipe);
                    return;
                }
                Close();
                var invCtrl = ResourceInventoryUIController.Instance;
                if (invCtrl != null)
                {
                    if (!sourceAffinity.HasValue || !invCtrl.SelectFirstHabitatOfBiome(sourceAffinity.Value))
                        invCtrl.OpenWarehouseForResource(resourceId);
                }
            });
        }

        private void CloseIngredientPopover()
        {
            if (ui != null && ui.ingredientPopover != null)
            {
                if (Application.isPlaying) Destroy(ui.ingredientPopover.gameObject);
                else DestroyImmediate(ui.ingredientPopover.gameObject);
                ui.ingredientPopover = null;
            }
        }

        private static BiomeAffinity? ResolveBiomeAffinity(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId)) return null;
            string lower = resourceId.ToLowerInvariant();
            if (lower.Contains("leafwood") || lower.Contains("oak") || lower.Contains("bark") || lower.Contains("sticks") || lower.Contains("tender_leaves") || lower.Contains("raspberries") || lower.Contains("acorns"))
                return BiomeAffinity.Leafwood;
            if (lower.Contains("bloomfield") || lower.Contains("daisies") || lower.Contains("dandelion") || lower.Contains("nectar") || lower.Contains("lavender") || lower.Contains("wheat") || lower.Contains("wheet"))
                return BiomeAffinity.Bloomfield;
            if (lower.Contains("windheath") || lower.Contains("foxtail") || lower.Contains("lichen") || lower.Contains("rosemary") || lower.Contains("juniper") || lower.Contains("flax") || lower.Contains("blueberries"))
                return BiomeAffinity.Windheath;
            if (lower.Contains("reedmarsh")) return BiomeAffinity.Reedmarsh;
            if (lower.Contains("lilywater")) return BiomeAffinity.Lilywater;
            if (lower.Contains("streamstone")) return BiomeAffinity.Streamstone;
            if (lower.Contains("rainleaf")) return BiomeAffinity.Rainleaf;
            return BiomeAffinity.Leafwood;
        }

        private void AdjustQuantity(int delta)
        {
            if (selectedRecipe == null) return;
            var inventory = ResourceInventory.Instance;
            int max = Mathf.Max(1, CraftingCalculator.GetMaxCraftableCount(selectedRecipe, inventory));
            selectedQuantity = Mathf.Clamp(selectedQuantity + delta, 1, Mathf.Max(9, max));
            RefreshInspector();
        }

        private void StartCraftingCurrent()
        {
            if (selectedRecipe == null || ui == null) return;

            var queue = CraftingQueueSystem.Instance;
            if (queue.TryStartCrafting(selectedRecipe, selectedQuantity, out string error))
            {
                StartCoroutine(EconomyHUD.FloatText(ui.panel, $"- Crafted {selectedRecipe.recipeName} ×{selectedQuantity}", accentColor, 17f, new Vector2(0f, 60f), new Vector2(0f, 130f), 1.2f));
                Refresh();
            }
            else
            {
                StartCoroutine(EconomyHUD.FloatText(ui.panel, error, missingColor, 16f, new Vector2(0f, 60f), new Vector2(0f, 130f), 1.5f));
            }
        }

        // =========================================================
        // QUEUE DRAWER (P-01)
        // =========================================================

        private void ClaimAllCompletedJobs()
        {
            int claimed = CraftingQueueSystem.Instance.ClaimAllCompleted();
            if (claimed > 0)
            {
                StartCoroutine(EconomyHUD.FloatText(ui.panel, $"Claimed {claimed} crafted batches!", enoughColor, 17f, new Vector2(0f, 60f), new Vector2(0f, 130f), 1.2f));
                Refresh();
            }
        }

        private void RefreshQueueList()
        {
            if (ui == null || ui.queueListContent == null) return;
            EconomyHUD.ClearChildren(ui.queueListContent);
            queueCards.Clear();

            var queue = CraftingQueueSystem.Instance;
            if (queue == null) return;
            var jobs = queue.ActiveJobs;

            int completedCount = queue.GetCompletedCount();
            ui.btnClaimAll.interactable = completedCount > 0;
            ui.btnClaimAllLabel.text = $"Claim all ({completedCount})";

            if (jobs.Count == 0)
            {
                TextMeshProUGUI empty = EconomyHUD.CreateText("Empty", ui.queueListContent, 15.5f, mutedColor, TextAlignmentOptions.Center);
                empty.rectTransform.sizeDelta = new Vector2(280f, 90f);
                empty.text = "Your queue is empty.\nSelect a recipe and press Craft.";
                return;
            }

            var style = GetStyleConfig();
            foreach (var job in jobs)
            {
                var card = CraftingUIBuilder.BuildQueueCard(
                    ui.queueListContent,
                    job,
                    style,
                    (j, rName) =>
                    {
                        StartCoroutine(EconomyHUD.FloatText(ui.panel, $"+ Claimed {rName} ×{j.TotalOutputAmount}", enoughColor, 17f, new Vector2(0f, 60f), new Vector2(0f, 130f), 1.2f));
                        Refresh();
                    },
                    (err) =>
                    {
                        StartCoroutine(EconomyHUD.FloatText(ui.panel, err, missingColor, 16f, new Vector2(0f, 60f), new Vector2(0f, 130f), 1.5f));
                    });
                queueCards.Add(card);
            }

            RefreshQueueProgress();
        }

        private void RefreshQueueProgress()
        {
            var queue = CraftingQueueSystem.Instance;
            if (queue == null) return;
            foreach (var card in queueCards)
            {
                if (card.job == null) continue;

                float progress = queue.GetProgress(card.job);
                float remaining = queue.GetRemainingSeconds(card.job);

                if (card.progressBarFill != null)
                    card.progressBarFill.sizeDelta = new Vector2(264f * Mathf.Clamp01(progress), 0f);

                if (card.job.isCompleted)
                {
                    if (card.statePill != null) card.statePill.enabled = false;
                    if (card.readyIcon != null) card.readyIcon.gameObject.SetActive(true);
                    if (card.statePillText != null)
                    {
                        card.statePillText.text = "Ready";
                        card.statePillText.color = LumiWorldUITheme.Success;
                        card.statePillText.alignment = TextAlignmentOptions.MidlineLeft;
                        card.statePillText.rectTransform.offsetMin = new Vector2(26f, 0f);
                    }
                    card.status.text = "";
                    if (card.progressBarTrack != null) card.progressBarTrack.SetActive(false);
                    card.claimButton.gameObject.SetActive(true);
                    card.claimButton.interactable = true;
                    card.claimLabel.text = "Claim";
                }
                else
                {
                    if (card.statePill != null)
                    {
                        card.statePill.enabled = true;
                        card.statePill.color = LumiWorldUITheme.Inset;
                    }
                    if (card.readyIcon != null) card.readyIcon.gameObject.SetActive(false);
                    if (card.statePillText != null)
                    {
                        card.statePillText.text = "In progress";
                        card.statePillText.color = LumiWorldUITheme.Text;
                        card.statePillText.alignment = TextAlignmentOptions.Center;
                        card.statePillText.rectTransform.offsetMin = Vector2.zero;
                    }
                    card.status.text = $"{Mathf.CeilToInt(remaining)}s remaining";
                    if (card.progressBarTrack != null) card.progressBarTrack.SetActive(true);
                    card.claimButton.gameObject.SetActive(false);
                }
            }
        }
    }
}
