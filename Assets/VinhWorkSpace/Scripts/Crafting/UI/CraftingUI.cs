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
    /// Giao diện Chế biến Tài nguyên (Crafting UI).
    /// - Nút "Chế biến" trên HUD thẳng hàng, cùng style với Orders và Shop.
    /// - Huy hiệu đếm số mẻ đã chế biến xong đang chờ nhận.
    /// - Bảng Chế biến: lọc theo danh mục, xem cây nguyên liệu thô phân rã,
    ///   chọn số lượng mẻ và đếm ngược hàng đợi theo lõi Time System.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CraftingUI : MonoBehaviour
    {
        public enum TabFilter { All, Food, Refined, Herbal, Paper, Queue }

        [Header("Nút mở trên HUD")]
        [SerializeField] private Vector2 buttonCorner = new Vector2(-20f, -232f);
        [SerializeField] private Vector2 buttonSize = new Vector2(170f, 44f);

        [Header("Màu sắc giao diện (Look & Feel)")]
        [SerializeField] private Color hudColor = new Color(0.01f, 0.60f, 0.90f, 0.95f);
        [SerializeField] private Color backdropColor = new Color(0.04f, 0.08f, 0.14f, 0.65f);
        [SerializeField] private Color panelColor = new Color(0.85f, 0.91f, 0.96f, 0.98f);
        [SerializeField] private Color cardColor = new Color(1f, 1f, 1f, 0.95f);
        [SerializeField] private Color selectedCardColor = new Color(0.88f, 0.94f, 0.99f, 1f);
        [SerializeField] private Color sectionColor = new Color(0.92f, 0.96f, 1f, 0.95f);
        [SerializeField] private Color slotColor = new Color(0.85f, 0.92f, 0.98f, 0.95f);
        [SerializeField] private Color textColor = new Color(0.06f, 0.12f, 0.22f, 1f);
        [SerializeField] private Color mutedColor = new Color(0.38f, 0.46f, 0.58f, 1f);
        [SerializeField] private Color enoughColor = new Color(0.05f, 0.65f, 0.35f, 1f);
        [SerializeField] private Color missingColor = new Color(0.90f, 0.35f, 0.25f, 1f);
        [SerializeField] private Color accentColor = new Color(0.01f, 0.60f, 0.90f, 1f);
        [SerializeField] private Color secondaryColor = new Color(0.84f, 0.90f, 0.96f, 1f);
        [SerializeField] private Color tabActiveColor = new Color(0.01f, 0.60f, 0.90f, 1f);
        [SerializeField] private Color tabInactiveColor = new Color(0.88f, 0.93f, 0.97f, 1f);

        private CraftingUIBindings ui;
        private float panelScale = 1f;
        private Coroutine popRoutine;

        private TabFilter currentTab = TabFilter.All;
        private CraftingRecipeData selectedRecipe;
        private int selectedQuantity = 1;

        private readonly List<QueueCardView> queueCards = new List<QueueCardView>();
        private ResourceInventory subscribedInventory;
        private CraftingQueueSystem subscribedQueue;
        private CurrencyWallet subscribedWallet;

        public bool IsOpen => ui != null && ui.window != null && ui.window.activeSelf;

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
            ui = CraftingUIBuilder.BuildAll(
                transform,
                GetStyleConfig(),
                Toggle,
                Close,
                OnTabSelected,
                SelectRecipe,
                AdjustQuantity,
                SetMaxQuantity,
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

            subscribedWallet = CurrencyWallet.Instance;
            if (subscribedWallet != null)
                subscribedWallet.OnBalanceChanged += HandleBalanceChanged;

            if (CraftingCatalog.All.Count > 0)
            {
                SelectRecipe(CraftingCatalog.All[0]);
            }

            Refresh();
        }

        private void Update()
        {
            if (IsOpen && currentTab == TabFilter.Queue)
            {
                RefreshQueueProgress();
            }
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

            if (subscribedWallet != null) subscribedWallet.OnBalanceChanged -= HandleBalanceChanged;
            subscribedWallet = null;
        }

        // =========================================================
        // MỞ VÀ ĐÓNG BẢNG
        // =========================================================

        public void Open()
        {
            if (ui == null || ui.window == null || IsOpen) return;

            transform.SetAsLastSibling();
            ui.window.SetActive(true);
            EconomyHUD.BringWindowToFront(ui.window);

            Rect area = ((RectTransform)transform).rect;
            panelScale = area.width > 0f && area.height > 0f
                ? Mathf.Clamp(Mathf.Min((area.width - 32f) / ui.panel.sizeDelta.x, (area.height - 32f) / ui.panel.sizeDelta.y), 0.5f, 1f)
                : 1f;

            Refresh();

            if (popRoutine != null) StopCoroutine(popRoutine);
            popRoutine = StartCoroutine(PopIn());
        }

        public void Close()
        {
            if (!IsOpen) return;
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
                float scale = panelScale * Mathf.Lerp(0.92f, 1f, 1f - (1f - t) * (1f - t));
                ui.panel.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            ui.panel.localScale = new Vector3(panelScale, panelScale, 1f);
            popRoutine = null;
        }

        // =========================================================
        // CẬP NHẬT GIAO DIỆN & HUY HIỆU
        // =========================================================

        private void HandleInventoryChanged(string id, int newAmount, int delta) => Refresh();
        private void HandleJobUpdated(ActiveCraftingJob job) => Refresh();
        private void HandleBalanceChanged(int balance, int delta, CoinReason reason) => RefreshBalance();

        private void RefreshBalance()
        {
            if (ui != null && ui.balanceText != null && CurrencyWallet.Instance != null)
                ui.balanceText.text = CurrencyWallet.Instance.Balance.ToString("N0");
        }

        private void Refresh()
        {
            RefreshBadge();
            RefreshBalance();
            if (!IsOpen) return;

            RefreshTabs();
            if (currentTab == TabFilter.Queue)
            {
                ui.craftContentArea.gameObject.SetActive(false);
                ui.queueContentArea.gameObject.SetActive(true);
                RefreshQueueList();
            }
            else
            {
                ui.craftContentArea.gameObject.SetActive(true);
                ui.queueContentArea.gameObject.SetActive(false);
                RefreshRecipeList();
                RefreshInspector();
            }
        }

        private void RefreshBadge()
        {
            if (ui == null || ui.badge == null) return;
            int completed = CraftingQueueSystem.Instance.GetCompletedCount();
            ui.badge.SetActive(completed > 0);
            ui.badgeText.text = completed.ToString(CultureInfo.InvariantCulture);
        }

        private void RefreshTabs()
        {
            if (ui == null) return;
            foreach (var item in ui.tabButtons)
            {
                bool active = item.tab == currentTab;
                item.bg.color = active ? tabActiveColor : tabInactiveColor;
                item.label.color = active ? Color.white : new Color(0.28f, 0.38f, 0.50f, 1f);
            }
        }

        private void OnTabSelected(TabFilter tab)
        {
            currentTab = tab;
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
                card.background.color = isSelected ? selectedCardColor : cardColor;

                int maxCraft = CraftingCalculator.GetMaxCraftableCount(card.recipe, inventory);
                if (maxCraft > 0)
                {
                    card.status.text = $"Craftable: <color=#{ColorUtility.ToHtmlStringRGB(enoughColor)}><b>x{maxCraft}</b></color>";
                    card.status.color = textColor;
                }
                else
                {
                    card.status.text = $"<color=#{ColorUtility.ToHtmlStringRGB(missingColor)}>Missing ingredients</color>";
                }
            }

            if (selectedRecipe != null && !MatchesFilter(selectedRecipe, currentTab) && firstVisible != null)
            {
                SelectRecipe(firstVisible);
            }
        }

        private bool MatchesFilter(CraftingRecipeData recipe, TabFilter filter)
        {
            if (recipe == null) return false;
            return filter switch
            {
                TabFilter.All => true,
                TabFilter.Food => recipe.category == CraftingCategory.Food,
                TabFilter.Refined => recipe.category == CraftingCategory.Refined,
                TabFilter.Herbal => recipe.category == CraftingCategory.Herbal,
                TabFilter.Paper => recipe.category == CraftingCategory.Paper,
                _ => true
            };
        }

        private void SelectRecipe(CraftingRecipeData recipe)
        {
            selectedRecipe = recipe;
            selectedQuantity = 1;
            Refresh();
        }

        private void RefreshInspector()
        {
            if (selectedRecipe == null || ui == null) return;

            var inventory = ResourceInventory.Instance;
            var outRes = selectedRecipe.outputResource;

            string outId = selectedRecipe.outputResource != null ? selectedRecipe.outputResource.resourceID : selectedRecipe.recipeID;
            ui.inspectorTitle.text = ResourceInventoryUIPresentation.LocalizeResourceName(outId, selectedRecipe.recipeName);
            ui.inspectorDesc.text = ResourceInventoryUIPresentation.LocalizeResourceDescription(outId, selectedRecipe.description);

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

            ui.inspectorCategory.text = selectedRecipe.category switch
            {
                CraftingCategory.Food => "FOOD",
                CraftingCategory.Refined => "REFINED",
                CraftingCategory.Herbal => "HERBAL",
                CraftingCategory.Paper => "CRAFT",
                _ => "PROCESSING"
            };

            ui.inspectorEdibleTag.gameObject.SetActive(selectedRecipe.isEdible);
            if (selectedRecipe.isEdible)
            {
                ui.inspectorEdibleTag.text = $"EDIBLE (+{selectedRecipe.feedingExpBonus} EXP)";
            }

            EconomyHUD.ClearChildren(ui.ingredientsContainer);
            foreach (var input in selectedRecipe.inputs)
            {
                if (input == null) continue;
                ResourceData res = CraftingCatalog.GetResource(input.resourceId);
                string rawResName = res != null && !string.IsNullOrEmpty(res.resourceName) ? res.resourceName : input.resourceId;
                string resName = ResourceInventoryUIPresentation.LocalizeResourceName(input.resourceId, rawResName);
                int owned = inventory.GetAmount(input.resourceId);
                int needed = input.amount * selectedQuantity;
                bool enough = owned >= needed;

                RectTransform row = EconomyHUD.CreateRect("IngRow", ui.ingredientsContainer);
                row.sizeDelta = new Vector2(0f, 38f);
                LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
                le.minHeight = 38f;
                le.preferredHeight = 38f;
                le.flexibleWidth = 1f;
                EconomyHUD.CreateBorderedPanel(row, sectionColor);

                RectTransform iconSlot = EconomyHUD.CreateRect("IconSlot", row);
                iconSlot.anchorMin = new Vector2(0f, 0.5f);
                iconSlot.anchorMax = new Vector2(0f, 0.5f);
                iconSlot.pivot = new Vector2(0f, 0.5f);
                iconSlot.anchoredPosition = new Vector2(8f, 0f);
                iconSlot.sizeDelta = new Vector2(32f, 32f);
                EconomyHUD.CreatePanel(iconSlot, slotColor);

                Image icon = EconomyHUD.CreateIcon("Icon", iconSlot, res != null && res.icon != null ? res.icon : EconomyHUD.CircleSprite, 26f, 3f);
                if (res != null) icon.color = res.icon == null ? res.resourceColor : Color.white;

                TextMeshProUGUI nameLbl = EconomyHUD.CreateText("Name", row, 15f, textColor, TextAlignmentOptions.Left);
                nameLbl.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                nameLbl.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                nameLbl.rectTransform.pivot = new Vector2(0f, 0.5f);
                nameLbl.rectTransform.anchoredPosition = new Vector2(48f, 0f);
                nameLbl.rectTransform.sizeDelta = new Vector2(240f, 28f);
                nameLbl.textWrappingMode = TextWrappingModes.NoWrap;
                nameLbl.overflowMode = TextOverflowModes.Ellipsis;
                nameLbl.text = resName;

                TextMeshProUGUI countLbl = EconomyHUD.CreateText("Count", row, 15f, enough ? enoughColor : missingColor, TextAlignmentOptions.Right);
                countLbl.rectTransform.anchorMin = new Vector2(1f, 0.5f);
                countLbl.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                countLbl.rectTransform.pivot = new Vector2(1f, 0.5f);
                countLbl.rectTransform.anchoredPosition = new Vector2(-12f, 0f);
                countLbl.rectTransform.sizeDelta = new Vector2(110f, 28f);
                countLbl.textWrappingMode = TextWrappingModes.NoWrap;
                countLbl.text = $"{owned} / {needed}";
            }

            ui.rawBreakdownText.text = "<b>Base Materials:</b> " + CraftingCalculator.FormatRawBreakdown(selectedRecipe, selectedQuantity);

            float totalDuration = selectedRecipe.craftDurationSeconds * selectedQuantity;
            ui.durationText.text = totalDuration > 0
                ? $"Duration: <b>{totalDuration:0.#}s</b> ({(selectedRecipe.craftDurationSeconds):0.#}s/batch)"
                : "Duration: <b>Instant</b>";

            ui.quantityText.text = selectedQuantity.ToString(CultureInfo.InvariantCulture);

            bool canCraft = CraftingCalculator.CanCraft(selectedRecipe, inventory, selectedQuantity);
            ui.btnStartCraft.interactable = canCraft;
            ui.btnStartCraftLabel.text = canCraft ? $"CRAFT (x{selectedQuantity})" : "NOT ENOUGH MATERIALS";
            ui.btnStartCraftLabel.color = canCraft ? Color.white : mutedColor;
        }

        private void AdjustQuantity(int delta)
        {
            if (selectedRecipe == null) return;
            var inventory = ResourceInventory.Instance;
            int max = Mathf.Max(1, CraftingCalculator.GetMaxCraftableCount(selectedRecipe, inventory));

            selectedQuantity = Mathf.Clamp(selectedQuantity + delta, 1, Mathf.Max(1, max));
            RefreshInspector();
        }

        private void SetMaxQuantity()
        {
            if (selectedRecipe == null) return;
            var inventory = ResourceInventory.Instance;
            int max = CraftingCalculator.GetMaxCraftableCount(selectedRecipe, inventory);
            selectedQuantity = Mathf.Max(1, max);
            RefreshInspector();
        }

        private void StartCraftingCurrent()
        {
            if (selectedRecipe == null || ui == null) return;

            var queue = CraftingQueueSystem.Instance;
            if (queue.TryStartCrafting(selectedRecipe, selectedQuantity, out string error))
            {
                StartCoroutine(EconomyHUD.FloatText(ui.panel, $"- Crafted {selectedRecipe.recipeName} x{selectedQuantity}!", accentColor, 18f, new Vector2(0f, 60f), new Vector2(0f, 130f), 1.2f));
                Refresh();
            }
            else
            {
                StartCoroutine(EconomyHUD.FloatText(ui.panel, error, missingColor, 16f, new Vector2(0f, 60f), new Vector2(0f, 130f), 1.5f));
            }
        }

        // =========================================================
        // HÀNG ĐỢI (QUEUE)
        // =========================================================

        private void ClaimAllCompletedJobs()
        {
            int claimed = CraftingQueueSystem.Instance.ClaimAllCompleted();
            if (claimed > 0)
            {
                StartCoroutine(EconomyHUD.FloatText(ui.panel, $"Claimed {claimed} crafted batches into warehouse!", accentColor, 18f, new Vector2(0f, 60f), new Vector2(0f, 130f), 1.2f));
                Refresh();
            }
        }

        private void RefreshQueueList()
        {
            if (ui == null) return;
            EconomyHUD.ClearChildren(ui.queueListContent);
            queueCards.Clear();

            var queue = CraftingQueueSystem.Instance;
            var jobs = queue.ActiveJobs;

            int completedCount = queue.GetCompletedCount();
            ui.btnClaimAll.interactable = completedCount > 0;
            ui.btnClaimAllLabel.text = completedCount > 0 ? $"CLAIM ALL ({completedCount})" : "CLAIM ALL";

            ui.queueSummaryText.text = $"Active: <b>{jobs.Count}/{queue.MaxConcurrentJobs}</b> crafting batches";

            if (jobs.Count == 0)
            {
                TextMeshProUGUI empty = EconomyHUD.CreateText("Empty", ui.queueListContent, 18f, mutedColor, TextAlignmentOptions.Center);
                empty.rectTransform.sizeDelta = new Vector2(CraftingUIBuilder.PanelWidth - 64f, 80f);
                empty.text = "Queue is empty. Select a recipe from the catalog to start crafting!";
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
                        StartCoroutine(EconomyHUD.FloatText(ui.panel, $"+ Claimed {rName} x{j.TotalOutputAmount} to warehouse!", accentColor, 18f, new Vector2(0f, 60f), new Vector2(0f, 130f), 1.2f));
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
            foreach (var card in queueCards)
            {
                if (card.job == null) continue;

                float progress = queue.GetProgress(card.job);
                float remaining = queue.GetRemainingSeconds(card.job);

                card.progressBarFill.sizeDelta = new Vector2(240f * progress, 0f);

                if (card.job.isCompleted)
                {
                    card.status.text = $"<color=#{ColorUtility.ToHtmlStringRGB(enoughColor)}><b>COMPLETED</b></color>";
                    card.claimButton.interactable = true;
                    card.claimLabel.text = "CLAIM";
                }
                else
                {
                    card.status.text = $"Crafting... <b>{remaining:0.#}s</b> remaining";
                    card.claimButton.interactable = false;
                    card.claimLabel.text = $"{remaining:0}s";
                }
            }
        }
    }
}
