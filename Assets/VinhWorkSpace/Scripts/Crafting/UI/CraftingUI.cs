using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
        private const float PanelWidth = 960f;
        private const float PanelHeight = 600f;
        private const float HeaderHeight = 88f;
        private const float LeftColWidth = 430f;
        private const float RightColWidth = 470f;
        private const float Spacing = 16f;

        [Header("Nút mở trên HUD")]
        [SerializeField] private Vector2 buttonCorner = new Vector2(-20f, -180f);
        [SerializeField] private Vector2 buttonSize = new Vector2(170f, 44f);

        [Header("Màu sắc giao diện (Look & Feel)")]
        [SerializeField] private Color hudColor = new Color(0.06f, 0.09f, 0.16f, 0.88f);
        [SerializeField] private Color backdropColor = new Color(0f, 0f, 0f, 0.50f);
        [SerializeField] private Color panelColor = new Color(0.08f, 0.11f, 0.19f, 0.98f);
        [SerializeField] private Color cardColor = new Color(0.13f, 0.17f, 0.27f, 1f);
        [SerializeField] private Color selectedCardColor = new Color(0.20f, 0.28f, 0.44f, 1f);
        [SerializeField] private Color sectionColor = new Color(0.05f, 0.07f, 0.13f, 0.90f);
        [SerializeField] private Color slotColor = new Color(0.07f, 0.09f, 0.16f, 0.95f);
        [SerializeField] private Color textColor = new Color(0.93f, 0.95f, 0.99f, 1f);
        [SerializeField] private Color mutedColor = new Color(0.62f, 0.68f, 0.80f, 1f);
        [SerializeField] private Color enoughColor = new Color(0.45f, 0.95f, 0.55f, 1f);
        [SerializeField] private Color missingColor = new Color(1f, 0.62f, 0.45f, 1f);
        [SerializeField] private Color accentColor = new Color(0.20f, 0.66f, 0.38f, 1f);
        [SerializeField] private Color secondaryColor = new Color(0.26f, 0.30f, 0.42f, 1f);
        [SerializeField] private Color tabActiveColor = new Color(0.24f, 0.47f, 0.85f, 1f);
        [SerializeField] private Color tabInactiveColor = new Color(0.16f, 0.20f, 0.30f, 1f);

        private RectTransform openButton;
        private GameObject badge;
        private TextMeshProUGUI badgeText;
        private Coroutine buttonPunch;

        private GameObject window;
        private RectTransform panel;
        private float panelScale = 1f;
        private Coroutine popRoutine;

        // Tabs
        private enum TabFilter { All, Food, Refined, Herbal, Paper, Queue }
        private TabFilter currentTab = TabFilter.All;
        private readonly List<(TabFilter tab, Button btn, Image bg, TextMeshProUGUI label)> tabButtons = new List<(TabFilter, Button, Image, TextMeshProUGUI)>();

        // Content areas
        private RectTransform craftContentArea;
        private RectTransform queueContentArea;

        // Recipe List
        private ScrollRect recipeScrollRect;
        private RectTransform recipeListContent;
        private readonly List<RecipeCardView> recipeCards = new List<RecipeCardView>();
        private CraftingRecipeData selectedRecipe;

        // Inspector Controls
        private Image inspectorIcon;
        private TextMeshProUGUI inspectorTitle;
        private TextMeshProUGUI inspectorCategory;
        private TextMeshProUGUI inspectorEdibleTag;
        private TextMeshProUGUI inspectorDesc;
        private RectTransform ingredientsContainer;
        private TextMeshProUGUI rawBreakdownText;
        private TextMeshProUGUI durationText;
        private TextMeshProUGUI quantityText;
        private Button btnDecrease;
        private Button btnIncrease;
        private Button btnMax;
        private Button btnStartCraft;
        private TextMeshProUGUI btnStartCraftLabel;
        private int selectedQuantity = 1;

        // Queue Controls
        private ScrollRect queueScrollRect;
        private RectTransform queueListContent;
        private readonly List<QueueCardView> queueCards = new List<QueueCardView>();
        private Button btnClaimAll;
        private TextMeshProUGUI btnClaimAllLabel;
        private TextMeshProUGUI queueSummaryText;
        private ResourceInventory subscribedInventory;
        private CraftingQueueSystem subscribedQueue;

        public bool IsOpen => window != null && window.activeSelf;

        private class RecipeCardView
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

        private class QueueCardView
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

        private void Start()
        {
            BuildOpenButton();
            BuildWindow();

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

            // Chọn công thức đầu tiên làm mặc định
            if (CraftingCatalog.All.Count > 0)
            {
                SelectRecipe(CraftingCatalog.All[0]);
            }

            Refresh();
        }

        private void Update()
        {
            // Cập nhật đếm ngược hàng đợi thời gian thực khi đang mở
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
        }

        // =========================================================
        // MỞ VÀ ĐÓNG BẢNG
        // =========================================================

        public void Open()
        {
            if (window == null || IsOpen) return;

            transform.SetAsLastSibling();
            window.SetActive(true);
            EconomyHUD.BringWindowToFront(window);

            Rect area = ((RectTransform)transform).rect;
            panelScale = area.width > 0f && area.height > 0f
                ? Mathf.Clamp(Mathf.Min((area.width - 32f) / panel.sizeDelta.x, (area.height - 32f) / panel.sizeDelta.y), 0.5f, 1f)
                : 1f;

            Refresh();

            if (popRoutine != null) StopCoroutine(popRoutine);
            popRoutine = StartCoroutine(PopIn());
        }

        public void Close()
        {
            if (!IsOpen) return;
            window.SetActive(false);
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
                panel.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            panel.localScale = new Vector3(panelScale, panelScale, 1f);
            popRoutine = null;
        }

        // =========================================================
        // CẬP NHẬT GIAO DIỆN & HUY HIỆU
        // =========================================================

        private void HandleInventoryChanged(string id, int newAmount, int delta) => Refresh();
        private void HandleJobUpdated(ActiveCraftingJob job) => Refresh();

        private void Refresh()
        {
            RefreshBadge();
            if (!IsOpen) return;

            RefreshTabs();
            if (currentTab == TabFilter.Queue)
            {
                craftContentArea.gameObject.SetActive(false);
                queueContentArea.gameObject.SetActive(true);
                RefreshQueueList();
            }
            else
            {
                craftContentArea.gameObject.SetActive(true);
                queueContentArea.gameObject.SetActive(false);
                RefreshRecipeList();
                RefreshInspector();
            }
        }

        private void RefreshBadge()
        {
            int completed = CraftingQueueSystem.Instance.GetCompletedCount();
            badge.SetActive(completed > 0);
            badgeText.text = completed.ToString(CultureInfo.InvariantCulture);
        }

        private void RefreshTabs()
        {
            foreach (var item in tabButtons)
            {
                bool active = item.tab == currentTab;
                item.bg.color = active ? tabActiveColor : tabInactiveColor;
                item.label.color = active ? Color.white : mutedColor;
            }
        }

        private void RefreshRecipeList()
        {
            var inventory = ResourceInventory.Instance;
            CraftingRecipeData firstVisible = null;

            foreach (var card in recipeCards)
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
                    card.status.text = $"Có thể làm: <color=#{ColorUtility.ToHtmlStringRGB(enoughColor)}><b>x{maxCraft}</b></color>";
                    card.status.color = textColor;
                }
                else
                {
                    card.status.text = $"<color=#{ColorUtility.ToHtmlStringRGB(missingColor)}>Thiếu nguyên liệu</color>";
                }
            }

            // Nếu món đang chọn không thuộc tab hiện tại, tự động chọn món đầu tiên hiển thị
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
            if (selectedRecipe == null) return;

            var inventory = ResourceInventory.Instance;
            var outRes = selectedRecipe.outputResource;

            inspectorTitle.text = selectedRecipe.recipeName;
            inspectorDesc.text = selectedRecipe.description;

            // Hiển thị Icon thành phẩm trong khung lớn
            Sprite mainIcon = selectedRecipe.GetIcon();
            if (mainIcon != null)
            {
                inspectorIcon.sprite = mainIcon;
                inspectorIcon.color = Color.white;
            }
            else
            {
                inspectorIcon.sprite = EconomyHUD.CircleSprite;
                inspectorIcon.color = outRes != null ? outRes.resourceColor : new Color(0.7f, 0.45f, 0.2f, 1f);
            }

            inspectorCategory.text = selectedRecipe.category switch
            {
                CraftingCategory.Food => "THỨC ĂN",
                CraftingCategory.Refined => "TINH CHẾ",
                CraftingCategory.Herbal => "DƯỢC LIỆU",
                CraftingCategory.Paper => "THỦ CÔNG",
                _ => "CHẾ BIẾN"
            };

            inspectorEdibleTag.gameObject.SetActive(selectedRecipe.isEdible);
            if (selectedRecipe.isEdible)
            {
                inspectorEdibleTag.text = $"ĂN TRỰC TIẾP (+{selectedRecipe.feedingExpBonus} EXP)";
            }

            // Xóa và dựng lại danh sách nguyên liệu trực tiếp (với khung slot riêng cho từng icon)
            EconomyHUD.ClearChildren(ingredientsContainer);
            foreach (var input in selectedRecipe.inputs)
            {
                if (input == null) continue;
                ResourceData res = CraftingCatalog.GetResource(input.resourceId);
                string resName = res != null && !string.IsNullOrEmpty(res.resourceName) ? res.resourceName : input.resourceId;
                int owned = inventory.GetAmount(input.resourceId);
                int needed = input.amount * selectedQuantity;
                bool enough = owned >= needed;

                RectTransform row = EconomyHUD.CreateRect("IngRow", ingredientsContainer);
                row.sizeDelta = new Vector2(0f, 38f);
                LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
                le.minHeight = 38f;
                le.preferredHeight = 38f;
                le.flexibleWidth = 1f;
                EconomyHUD.CreatePanel(row, sectionColor);

                // Khung slot ảnh nguyên liệu (chừa chỗ sẵn sàng để thêm sprite icon)
                RectTransform iconSlot = EconomyHUD.CreateRect("IconSlot", row);
                iconSlot.anchorMin = new Vector2(0f, 0.5f);
                iconSlot.anchorMax = new Vector2(0f, 0.5f);
                iconSlot.pivot = new Vector2(0f, 0.5f);
                iconSlot.anchoredPosition = new Vector2(8f, 0f);
                iconSlot.sizeDelta = new Vector2(32f, 32f);
                EconomyHUD.CreatePanel(iconSlot, slotColor);

                Image icon = EconomyHUD.CreateIcon("Icon", iconSlot, res != null && res.icon != null ? res.icon : EconomyHUD.CircleSprite, 26f, 3f);
                if (res != null) icon.color = res.icon == null ? res.resourceColor : Color.white;

                // Tên nguyên liệu: căn sát bên cạnh khung slot icon
                TextMeshProUGUI nameLbl = EconomyHUD.CreateText("Name", row, 15f, textColor, TextAlignmentOptions.Left);
                nameLbl.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                nameLbl.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                nameLbl.rectTransform.pivot = new Vector2(0f, 0.5f);
                nameLbl.rectTransform.anchoredPosition = new Vector2(48f, 0f);
                nameLbl.rectTransform.sizeDelta = new Vector2(240f, 28f);
                nameLbl.textWrappingMode = TextWrappingModes.NoWrap;
                nameLbl.overflowMode = TextOverflowModes.Ellipsis;
                nameLbl.text = resName;

                // Số lượng có/cần căn mép phải
                TextMeshProUGUI countLbl = EconomyHUD.CreateText("Count", row, 15f, enough ? enoughColor : missingColor, TextAlignmentOptions.Right);
                countLbl.rectTransform.anchorMin = new Vector2(1f, 0.5f);
                countLbl.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                countLbl.rectTransform.pivot = new Vector2(1f, 0.5f);
                countLbl.rectTransform.anchoredPosition = new Vector2(-12f, 0f);
                countLbl.rectTransform.sizeDelta = new Vector2(110f, 28f);
                countLbl.textWrappingMode = TextWrappingModes.NoWrap;
                countLbl.text = $"{owned} / {needed}";
            }

            // Cây nguyên liệu thô phân rã
            rawBreakdownText.text = "<b>Nguyên liệu gốc:</b> " + CraftingCalculator.FormatRawBreakdown(selectedRecipe, selectedQuantity);

            // Thời gian chế biến
            float totalDuration = selectedRecipe.craftDurationSeconds * selectedQuantity;
            durationText.text = totalDuration > 0
                ? $"Thời gian hoàn thành: <b>{totalDuration:0.#} giây</b> ({(selectedRecipe.craftDurationSeconds):0.#}s/mẻ)"
                : "Thời gian: <b>Tức thì</b>";

            quantityText.text = selectedQuantity.ToString(CultureInfo.InvariantCulture);

            // Nút Bắt đầu chế biến
            bool canCraft = CraftingCalculator.CanCraft(selectedRecipe, inventory, selectedQuantity);
            btnStartCraft.interactable = canCraft;
            btnStartCraftLabel.text = canCraft ? $"CHẾ BIẾN (x{selectedQuantity})" : "KHÔNG ĐỦ NGUYÊN LIỆU";
            btnStartCraftLabel.color = canCraft ? Color.white : mutedColor;
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
            if (selectedRecipe == null) return;

            var queue = CraftingQueueSystem.Instance;
            if (queue.TryStartCrafting(selectedRecipe, selectedQuantity, out string error))
            {
                StartCoroutine(EconomyHUD.FloatText(panel, $"- Chế biến {selectedRecipe.recipeName} x{selectedQuantity}!", accentColor, 18f, new Vector2(0f, 60f), new Vector2(0f, 130f), 1.2f));
                Refresh();
            }
            else
            {
                StartCoroutine(EconomyHUD.FloatText(panel, error, missingColor, 16f, new Vector2(0f, 60f), new Vector2(0f, 130f), 1.5f));
            }
        }

        // =========================================================
        // HÀNG ĐỢI (QUEUE)
        // =========================================================

        private void RefreshQueueList()
        {
            EconomyHUD.ClearChildren(queueListContent);
            queueCards.Clear();

            var queue = CraftingQueueSystem.Instance;
            var jobs = queue.ActiveJobs;

            int completedCount = queue.GetCompletedCount();
            btnClaimAll.interactable = completedCount > 0;
            btnClaimAllLabel.text = completedCount > 0 ? $"NHẬN TẤT CẢ ({completedCount})" : "NHẬN TẤT CẢ";

            queueSummaryText.text = $"Đang hoạt động: <b>{jobs.Count}/{queue.MaxConcurrentJobs}</b> mẻ chế biến";

            if (jobs.Count == 0)
            {
                TextMeshProUGUI empty = EconomyHUD.CreateText("Empty", queueListContent, 18f, mutedColor, TextAlignmentOptions.Center);
                empty.rectTransform.sizeDelta = new Vector2(PanelWidth - 64f, 80f);
                empty.text = "Hàng đợi đang trống. Hãy chọn món bên danh mục để bắt đầu chế biến!";
                return;
            }

            foreach (var job in jobs)
            {
                var card = BuildQueueCard(job);
                queueCards.Add(card);
            }

            RefreshQueueProgress();
        }

        private QueueCardView BuildQueueCard(ActiveCraftingJob job)
        {
            var recipe = job.Recipe;
            var outRes = recipe != null ? recipe.outputResource : null;

            RectTransform row = EconomyHUD.CreateRect("JobRow", queueListContent);
            row.sizeDelta = new Vector2(PanelWidth - 64f, 68f);
            EconomyHUD.CreatePanel(row, cardColor);

            // Khung slot icon thành phẩm
            RectTransform slot = EconomyHUD.CreateRect("Slot", row);
            slot.anchorMin = new Vector2(0f, 0.5f);
            slot.anchorMax = new Vector2(0f, 0.5f);
            slot.pivot = new Vector2(0f, 0.5f);
            slot.anchoredPosition = new Vector2(10f, 0f);
            slot.sizeDelta = new Vector2(48f, 48f);
            EconomyHUD.CreatePanel(slot, slotColor);

            Sprite rowIcon = recipe != null ? recipe.GetIcon() : (outRes != null ? outRes.icon : null);
            Image icon = EconomyHUD.CreateIcon("Icon", slot, rowIcon != null ? rowIcon : EconomyHUD.CircleSprite, 40f, 4f);
            if (rowIcon == null && outRes != null) icon.color = outRes.resourceColor;

            // Info
            TextMeshProUGUI title = EconomyHUD.CreateText("Title", row, 17f, textColor, TextAlignmentOptions.Left);
            title.rectTransform.anchoredPosition = new Vector2(68f, 10f);
            title.rectTransform.sizeDelta = new Vector2(300f, 26f);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Ellipsis;
            string rName = recipe != null ? recipe.recipeName : "Thành phẩm";
            title.text = $"{rName} <b>x{job.TotalOutputAmount}</b>";

            // Status label
            TextMeshProUGUI status = EconomyHUD.CreateText("Status", row, 14f, mutedColor, TextAlignmentOptions.Left);
            status.rectTransform.anchoredPosition = new Vector2(68f, -14f);
            status.rectTransform.sizeDelta = new Vector2(300f, 22f);
            status.textWrappingMode = TextWrappingModes.NoWrap;
            status.overflowMode = TextOverflowModes.Ellipsis;

            // Progress bar
            RectTransform barBg = EconomyHUD.CreateRect("BarBg", row);
            barBg.anchorMin = new Vector2(0.48f, 0.5f);
            barBg.anchorMax = new Vector2(0.48f, 0.5f);
            barBg.pivot = new Vector2(0f, 0.5f);
            barBg.sizeDelta = new Vector2(240f, 16f);
            EconomyHUD.CreatePanel(barBg, sectionColor);

            RectTransform barFill = EconomyHUD.CreateRect("BarFill", barBg);
            barFill.anchorMin = Vector2.zero;
            barFill.anchorMax = new Vector2(0f, 1f);
            barFill.pivot = new Vector2(0f, 0.5f);
            barFill.sizeDelta = new Vector2(0f, 0f);
            EconomyHUD.CreatePanel(barFill, accentColor);

            // Button Claim
            RectTransform btnRect = EconomyHUD.CreateRect("ClaimBtn", row);
            btnRect.anchorMin = new Vector2(1f, 0.5f);
            btnRect.anchorMax = new Vector2(1f, 0.5f);
            btnRect.pivot = new Vector2(1f, 0.5f);
            btnRect.anchoredPosition = new Vector2(-12f, 0f);
            btnRect.sizeDelta = new Vector2(110f, 38f);
            Button claimBtn = EconomyHUD.AddButton(btnRect, accentColor);
            TextMeshProUGUI claimLbl = EconomyHUD.CreateText("Lbl", btnRect, 15f, Color.white, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(claimLbl.rectTransform);
            claimLbl.text = "NHẬN";

            claimBtn.onClick.AddListener(() =>
            {
                if (CraftingQueueSystem.Instance.TryClaimJob(job, out string err))
                {
                    StartCoroutine(EconomyHUD.FloatText(panel, $"+ Nhận {rName} x{job.TotalOutputAmount} vào kho!", accentColor, 18f, new Vector2(0f, 60f), new Vector2(0f, 130f), 1.2f));
                    Refresh();
                }
                else
                {
                    StartCoroutine(EconomyHUD.FloatText(panel, err, missingColor, 16f, new Vector2(0f, 60f), new Vector2(0f, 130f), 1.5f));
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

        private void RefreshQueueProgress()
        {
            var queue = CraftingQueueSystem.Instance;
            foreach (var card in queueCards)
            {
                if (card.job == null) continue;

                float progress = queue.GetProgress(card.job);
                float remaining = queue.GetRemainingSeconds(card.job);

                // Cập nhật thanh tiến độ (chiều rộng tối đa 240px)
                card.progressBarFill.sizeDelta = new Vector2(240f * progress, 0f);

                if (card.job.isCompleted)
                {
                    card.status.text = $"<color=#{ColorUtility.ToHtmlStringRGB(enoughColor)}><b>ĐÃ HOÀN THÀNH</b></color>";
                    card.claimButton.interactable = true;
                    card.claimLabel.text = "NHẬN";
                }
                else
                {
                    card.status.text = $"Đang chế biến... còn lại <b>{remaining:0.#}s</b>";
                    card.claimButton.interactable = false;
                    card.claimLabel.text = $"{remaining:0}s";
                }
            }
        }

        // =========================================================
        // XÂY DỰNG GIAO DIỆN (PROCEDURAL UI)
        // =========================================================

        private void BuildOpenButton()
        {
            // Neo góc trên phải, pivot ở giữa để nút nảy quanh tâm và cùng hàng với Orders và Shop
            openButton = EconomyHUD.CreateRect("CraftingOpenButton", transform);
            openButton.anchorMin = new Vector2(1f, 1f);
            openButton.anchorMax = new Vector2(1f, 1f);
            openButton.pivot = new Vector2(0.5f, 0.5f);
            openButton.sizeDelta = buttonSize;
            openButton.anchoredPosition = buttonCorner - buttonSize * 0.5f;

            Button btn = EconomyHUD.AddButton(openButton, hudColor);
            btn.onClick.AddListener(Toggle);

            // Chữ "Chế biến" căn giữa hoàn toàn, font 20f bold đồng bộ với nút Orders và Shop
            TextMeshProUGUI label = EconomyHUD.CreateText("Label", openButton, 20f, Color.white, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(label.rectTransform);
            label.text = "Chế biến";

            // Huy hiệu báo mẻ đã hoàn thành (nằm góc trên phải nút, chỉ hiện khi có mẻ xong)
            RectTransform badgeRect = EconomyHUD.CreateRect("Badge", openButton);
            badgeRect.anchorMin = new Vector2(1f, 1f);
            badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(-4f, -4f);
            badgeRect.sizeDelta = new Vector2(24f, 24f);
            Image badgeImg = badgeRect.gameObject.AddComponent<Image>();
            badgeImg.sprite = EconomyHUD.CircleSprite;
            badgeImg.color = accentColor;
            badgeImg.raycastTarget = false;
            badge = badgeRect.gameObject;

            badgeText = EconomyHUD.CreateText("Count", badgeRect, 13f, Color.white, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(badgeText.rectTransform);
            badge.SetActive(false);
        }

        private void BuildWindow()
        {
            RectTransform windowRect = EconomyHUD.CreateRect("Window", transform);
            EconomyHUD.Stretch(windowRect);
            window = windowRect.gameObject;
            EconomyHUD.RegisterWindow(window, Close);

            // Backdrop
            RectTransform backdropRect = EconomyHUD.CreateRect("Backdrop", windowRect);
            EconomyHUD.Stretch(backdropRect);
            Image backdrop = backdropRect.gameObject.AddComponent<Image>();
            backdrop.color = backdropColor;
            Button backdropBtn = backdropRect.gameObject.AddComponent<Button>();
            backdropBtn.transition = Selectable.Transition.None;
            backdropBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            backdropBtn.onClick.AddListener(Close);

            // Panel chính
            panel = EconomyHUD.CreateRect("Panel", windowRect);
            panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            EconomyHUD.CreatePanel(panel, panelColor).raycastTarget = true;

            BuildHeader();
            BuildTabs();

            // Vùng nội dung Chế biến (Cột Trái Danh sách + Cột Phải Chi tiết)
            craftContentArea = EconomyHUD.CreateRect("CraftContent", panel);
            craftContentArea.anchorMin = new Vector2(0f, 0f);
            craftContentArea.anchorMax = new Vector2(1f, 1f);
            craftContentArea.offsetMin = new Vector2(Spacing, Spacing);
            craftContentArea.offsetMax = new Vector2(-Spacing, -(HeaderHeight + 42f));

            BuildLeftRecipeList();
            BuildRightInspector();

            // Vùng nội dung Hàng đợi (Queue)
            queueContentArea = EconomyHUD.CreateRect("QueueContent", panel);
            queueContentArea.anchorMin = new Vector2(0f, 0f);
            queueContentArea.anchorMax = new Vector2(1f, 1f);
            queueContentArea.offsetMin = new Vector2(Spacing, Spacing);
            queueContentArea.offsetMax = new Vector2(-Spacing, -(HeaderHeight + 42f));
            BuildQueueView();

            window.SetActive(false);
        }

        private void BuildHeader()
        {
            // Tiêu đề chính: Chiều rộng rộng rãi 750px, thụt lề 28px tránh góc bo tròn và NoWrap chống rớt chữ
            TextMeshProUGUI title = EconomyHUD.CreateText("Title", panel, 24f, textColor, TextAlignmentOptions.Left);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(0f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(28f, -16f);
            title.rectTransform.sizeDelta = new Vector2(750f, 32f);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.text = "XƯỞNG CHẾ BIẾN TÀI NGUYÊN";

            // Phụ đề nằm ngay dưới, thụt lề 28px
            TextMeshProUGUI subtitle = EconomyHUD.CreateText("Subtitle", panel, 14f, mutedColor, TextAlignmentOptions.Left);
            subtitle.fontStyle = FontStyles.Normal;
            subtitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            subtitle.rectTransform.anchorMax = new Vector2(0f, 1f);
            subtitle.rectTransform.pivot = new Vector2(0f, 1f);
            subtitle.rectTransform.anchoredPosition = new Vector2(28f, -50f);
            subtitle.rectTransform.sizeDelta = new Vector2(750f, 22f);
            subtitle.textWrappingMode = TextWrappingModes.NoWrap;
            subtitle.text = "Sơ chế thực phẩm dinh dưỡng cho thú, tinh luyện dược liệu & thủ công giấy";


            // Nút đóng (X)
            RectTransform closeRect = EconomyHUD.CreateRect("CloseButton", panel);
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-Spacing, -16f);
            closeRect.sizeDelta = new Vector2(40f, 36f);
            EconomyHUD.AddButton(closeRect, secondaryColor).onClick.AddListener(Close);
            TextMeshProUGUI closeLbl = EconomyHUD.CreateText("Lbl", closeRect, 18f, Color.white, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(closeLbl.rectTransform);
            closeLbl.text = "X";
        }

        private void BuildTabs()
        {
            RectTransform bar = EconomyHUD.CreateRect("TabBar", panel);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0f, 1f);
            bar.anchoredPosition = new Vector2(Spacing, -HeaderHeight);
            bar.sizeDelta = new Vector2(PanelWidth - Spacing * 2f, 36f);

            var tabs = new (TabFilter filter, string label, float width)[]
            {
                (TabFilter.All, "Tất cả", 90f),
                (TabFilter.Food, "Thức ăn", 100f),
                (TabFilter.Refined, "Tinh chế", 100f),
                (TabFilter.Herbal, "Dược liệu", 100f),
                (TabFilter.Paper, "Thủ công", 100f),
                (TabFilter.Queue, "Hàng đợi (Queue)", 150f)
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
                bg.color = tabInactiveColor;

                Button btn = tRect.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.navigation = new Navigation { mode = Navigation.Mode.None };

                TextMeshProUGUI lbl = EconomyHUD.CreateText("Lbl", tRect, 14f, mutedColor, TextAlignmentOptions.Center);
                EconomyHUD.Stretch(lbl.rectTransform);
                lbl.text = t.label;

                TabFilter captured = t.filter;
                btn.onClick.AddListener(() =>
                {
                    currentTab = captured;
                    if (recipeScrollRect != null) recipeScrollRect.verticalNormalizedPosition = 1f;
                    Refresh();
                });

                tabButtons.Add((captured, btn, bg, lbl));
                curX += t.width + 8f;
            }
        }

        private void BuildLeftRecipeList()
        {
            RectTransform col = EconomyHUD.CreateRect("LeftCol", craftContentArea);
            col.anchorMin = new Vector2(0f, 0f);
            col.anchorMax = new Vector2(0f, 1f);
            col.pivot = new Vector2(0f, 0.5f);
            col.sizeDelta = new Vector2(LeftColWidth, 0f);
            col.anchoredPosition = Vector2.zero;

            recipeScrollRect = col.gameObject.AddComponent<ScrollRect>();
            recipeScrollRect.horizontal = false;
            recipeScrollRect.vertical = true;
            // Tăng độ mượt cuộn chuột (smooth scrolling)
            recipeScrollRect.scrollSensitivity = 35f;
            recipeScrollRect.movementType = ScrollRect.MovementType.Elastic;
            recipeScrollRect.elasticity = 0.1f;
            recipeScrollRect.inertia = true;
            recipeScrollRect.decelerationRate = 0.135f;

            RectTransform viewport = EconomyHUD.CreateRect("Viewport", col);
            EconomyHUD.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            recipeScrollRect.viewport = viewport;

            recipeListContent = EconomyHUD.CreateRect("Content", viewport);
            recipeListContent.anchorMin = new Vector2(0f, 1f);
            recipeListContent.anchorMax = new Vector2(1f, 1f);
            recipeListContent.pivot = new Vector2(0f, 1f);
            recipeListContent.sizeDelta = new Vector2(0f, 400f);
            recipeScrollRect.content = recipeListContent;

            // Sử dụng VerticalLayoutGroup và ContentSizeFitter để các món luôn tự động dồn lên trên cùng
            VerticalLayoutGroup layout = recipeListContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(2, 6, 4, 8);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = recipeListContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // Dựng các ô công thức
            var allRecipes = CraftingCatalog.All;
            for (int i = 0; i < allRecipes.Count; i++)
            {
                var r = allRecipes[i];
                var card = BuildRecipeCard(recipeListContent, r);
                recipeCards.Add(card);
            }
        }

        private RecipeCardView BuildRecipeCard(RectTransform container, CraftingRecipeData recipe)
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
            bg.color = cardColor;

            Button btn = row.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(() => SelectRecipe(recipe));

            // Khung slot ảnh thành phẩm: Chừa chỗ vuông 50x50px bo góc riêng biệt
            RectTransform slot = EconomyHUD.CreateRect("IconSlot", row);
            slot.anchorMin = new Vector2(0f, 0.5f);
            slot.anchorMax = new Vector2(0f, 0.5f);
            slot.pivot = new Vector2(0f, 0.5f);
            slot.anchoredPosition = new Vector2(10f, 0f);
            slot.sizeDelta = new Vector2(50f, 50f);
            EconomyHUD.CreatePanel(slot, slotColor);

            ResourceData outRes = recipe.outputResource;
            Sprite qIcon = recipe != null ? recipe.GetIcon() : (outRes != null ? outRes.icon : null);
            Image icon = EconomyHUD.CreateIcon("Icon", slot, qIcon != null ? qIcon : EconomyHUD.CircleSprite, 42f, 4f);
            if (qIcon == null && outRes != null) icon.color = outRes.resourceColor;

            // Tên thành phẩm: Neo mép trái row (0f, 0.5f), Auto-size chống tràn chữ và xuống dòng
            TextMeshProUGUI nameLbl = EconomyHUD.CreateText("Name", row, 15f, textColor, TextAlignmentOptions.Left);
            nameLbl.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            nameLbl.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            nameLbl.rectTransform.pivot = new Vector2(0f, 0.5f);
            nameLbl.rectTransform.anchoredPosition = new Vector2(68f, 12f);
            nameLbl.rectTransform.sizeDelta = new Vector2(250f, 24f);
            nameLbl.enableAutoSizing = true;
            nameLbl.fontSizeMin = 11f;
            nameLbl.fontSizeMax = 15f;
            nameLbl.textWrappingMode = TextWrappingModes.NoWrap;
            nameLbl.overflowMode = TextOverflowModes.Ellipsis;
            nameLbl.text = recipe.recipeName;

            // Trạng thái (Có thể làm xN hoặc Thiếu nguyên liệu)
            TextMeshProUGUI statusLbl = EconomyHUD.CreateText("Status", row, 13f, mutedColor, TextAlignmentOptions.Left);
            statusLbl.fontStyle = FontStyles.Normal;
            statusLbl.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            statusLbl.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            statusLbl.rectTransform.pivot = new Vector2(0f, 0.5f);
            statusLbl.rectTransform.anchoredPosition = new Vector2(68f, -14f);
            statusLbl.rectTransform.sizeDelta = new Vector2(250f, 20f);
            statusLbl.textWrappingMode = TextWrappingModes.NoWrap;
            statusLbl.overflowMode = TextOverflowModes.Ellipsis;

            // Huy hiệu "ĂN ĐƯỢC" bên phải
            RectTransform edBadge = EconomyHUD.CreateRect("EdibleBadge", row);
            edBadge.anchorMin = new Vector2(1f, 0.5f);
            edBadge.anchorMax = new Vector2(1f, 0.5f);
            edBadge.pivot = new Vector2(1f, 0.5f);
            edBadge.anchoredPosition = new Vector2(-12f, 0f);
            edBadge.sizeDelta = new Vector2(76f, 26f);
            EconomyHUD.CreatePanel(edBadge, new Color(0.12f, 0.35f, 0.22f, 0.95f));
            TextMeshProUGUI edLbl = EconomyHUD.CreateText("Lbl", edBadge, 11f, enoughColor, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(edLbl.rectTransform);
            edLbl.text = "ĂN ĐƯỢC";
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

        private void BuildRightInspector()
        {
            RectTransform col = EconomyHUD.CreateRect("RightCol", craftContentArea);
            col.anchorMin = new Vector2(1f, 0f);
            col.anchorMax = new Vector2(1f, 1f);
            col.pivot = new Vector2(1f, 0.5f);
            col.sizeDelta = new Vector2(RightColWidth, 0f);
            col.anchoredPosition = Vector2.zero;
            EconomyHUD.CreatePanel(col, sectionColor);

            // ================= Header Showcase (Thành phẩm) =================
            RectTransform header = EconomyHUD.CreateRect("Header", col);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0f, 1f);
            header.anchoredPosition = Vector2.zero;
            header.sizeDelta = new Vector2(0f, 120f);

            // Khung trưng bày ảnh thành phẩm lớn (Product Showcase Slot 76x76px)
            RectTransform showSlot = EconomyHUD.CreateRect("ShowSlot", header);
            showSlot.anchorMin = new Vector2(0f, 1f);
            showSlot.anchorMax = new Vector2(0f, 1f);
            showSlot.pivot = new Vector2(0f, 1f);
            showSlot.anchoredPosition = new Vector2(16f, -14f);
            showSlot.sizeDelta = new Vector2(76f, 76f);
            EconomyHUD.CreatePanel(showSlot, slotColor);

            inspectorIcon = EconomyHUD.CreateIcon("Icon", showSlot, EconomyHUD.CircleSprite, 64f, 6f);
            inspectorIcon.rectTransform.anchoredPosition = new Vector2(6f, 0f);

            // Cụm tiêu đề và thông tin: Neo mép trên trái (0f, 1f) để NẰM GỌN BÊN TRONG PANEL
            inspectorTitle = EconomyHUD.CreateText("Title", header, 22f, textColor, TextAlignmentOptions.Left);
            inspectorTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            inspectorTitle.rectTransform.anchorMax = new Vector2(0f, 1f);
            inspectorTitle.rectTransform.pivot = new Vector2(0f, 1f);
            inspectorTitle.rectTransform.anchoredPosition = new Vector2(106f, -14f);
            inspectorTitle.rectTransform.sizeDelta = new Vector2(348f, 28f);
            inspectorTitle.enableAutoSizing = true;
            inspectorTitle.fontSizeMin = 14f;
            inspectorTitle.fontSizeMax = 22f;
            inspectorTitle.textWrappingMode = TextWrappingModes.NoWrap;
            inspectorTitle.overflowMode = TextOverflowModes.Ellipsis;

            // Dòng badges: Danh mục + Ăn trực tiếp
            inspectorCategory = EconomyHUD.CreateText("Category", header, 12f, tabActiveColor, TextAlignmentOptions.Left);
            inspectorCategory.rectTransform.anchorMin = new Vector2(0f, 1f);
            inspectorCategory.rectTransform.anchorMax = new Vector2(0f, 1f);
            inspectorCategory.rectTransform.pivot = new Vector2(0f, 1f);
            inspectorCategory.rectTransform.anchoredPosition = new Vector2(106f, -44f);
            inspectorCategory.rectTransform.sizeDelta = new Vector2(85f, 22f);

            inspectorEdibleTag = EconomyHUD.CreateText("EdibleTag", header, 12f, enoughColor, TextAlignmentOptions.Left);
            inspectorEdibleTag.rectTransform.anchorMin = new Vector2(0f, 1f);
            inspectorEdibleTag.rectTransform.anchorMax = new Vector2(0f, 1f);
            inspectorEdibleTag.rectTransform.pivot = new Vector2(0f, 1f);
            inspectorEdibleTag.rectTransform.anchoredPosition = new Vector2(196f, -44f);
            inspectorEdibleTag.rectTransform.sizeDelta = new Vector2(250f, 22f);
            inspectorEdibleTag.textWrappingMode = TextWrappingModes.NoWrap;
            inspectorEdibleTag.overflowMode = TextOverflowModes.Ellipsis;

            inspectorDesc = EconomyHUD.CreateText("Desc", header, 13f, mutedColor, TextAlignmentOptions.Left);
            inspectorDesc.fontStyle = FontStyles.Normal;
            inspectorDesc.rectTransform.anchorMin = new Vector2(0f, 1f);
            inspectorDesc.rectTransform.anchorMax = new Vector2(0f, 1f);
            inspectorDesc.rectTransform.pivot = new Vector2(0f, 1f);
            inspectorDesc.rectTransform.anchoredPosition = new Vector2(106f, -68f);
            inspectorDesc.rectTransform.sizeDelta = new Vector2(348f, 44f);
            inspectorDesc.textWrappingMode = TextWrappingModes.Normal;
            inspectorDesc.overflowMode = TextOverflowModes.Ellipsis;

            // ================= Container danh sách nguyên liệu =================
            ingredientsContainer = EconomyHUD.CreateRect("Ingredients", col);
            ingredientsContainer.anchorMin = new Vector2(0f, 1f);
            ingredientsContainer.anchorMax = new Vector2(1f, 1f);
            ingredientsContainer.pivot = new Vector2(0f, 1f);
            ingredientsContainer.anchoredPosition = new Vector2(16f, -126f);
            ingredientsContainer.sizeDelta = new Vector2(-32f, 136f);

            VerticalLayoutGroup ingLayout = ingredientsContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            ingLayout.padding = new RectOffset(0, 0, 0, 0);
            ingLayout.spacing = 6f;
            ingLayout.childControlWidth = true;
            ingLayout.childControlHeight = false;
            ingLayout.childForceExpandWidth = true;
            ingLayout.childForceExpandHeight = false;

            // ================= Khung phân rã nguyên liệu thô gốc =================
            RectTransform rawBox = EconomyHUD.CreateRect("RawBox", col);
            rawBox.anchorMin = new Vector2(0f, 0f);
            rawBox.anchorMax = new Vector2(1f, 0f);
            rawBox.pivot = new Vector2(0.5f, 0f);
            rawBox.anchoredPosition = new Vector2(0f, 120f);
            rawBox.sizeDelta = new Vector2(-32f, 54f);
            EconomyHUD.CreatePanel(rawBox, new Color(0.09f, 0.13f, 0.22f, 0.95f));

            rawBreakdownText = EconomyHUD.CreateText("RawTxt", rawBox, 13f, mutedColor, TextAlignmentOptions.Left);
            rawBreakdownText.fontStyle = FontStyles.Normal;
            rawBreakdownText.textWrappingMode = TextWrappingModes.Normal;
            EconomyHUD.Stretch(rawBreakdownText.rectTransform);
            rawBreakdownText.rectTransform.offsetMin = new Vector2(12f, 6f);
            rawBreakdownText.rectTransform.offsetMax = new Vector2(-12f, -6f);

            // ================= Thời gian hoàn thành =================
            durationText = EconomyHUD.CreateText("Duration", col, 14f, textColor, TextAlignmentOptions.Center);
            durationText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            durationText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            durationText.rectTransform.pivot = new Vector2(0.5f, 0f);
            durationText.rectTransform.anchoredPosition = new Vector2(0f, 88f);
            durationText.rectTransform.sizeDelta = new Vector2(RightColWidth - 32f, 26f);

            // ================= Bộ chọn số lượng (Stepper căn chỉnh chuẩn) =================
            RectTransform qtyBar = EconomyHUD.CreateRect("QtyBar", col);
            qtyBar.anchorMin = new Vector2(0.5f, 0f);
            qtyBar.anchorMax = new Vector2(0.5f, 0f);
            qtyBar.pivot = new Vector2(0.5f, 0f);
            qtyBar.anchoredPosition = new Vector2(0f, 50f);
            qtyBar.sizeDelta = new Vector2(RightColWidth - 32f, 34f);

            // Nút Giảm [-]
            btnDecrease = EconomyHUD.AddButton(CreateCenterSubRect(qtyBar, -80f, new Vector2(36f, 32f)), secondaryColor);
            btnDecrease.onClick.AddListener(() => AdjustQuantity(-1));
            TextMeshProUGUI decLbl = EconomyHUD.CreateText("L", btnDecrease.GetComponent<RectTransform>(), 16f, Color.white, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(decLbl.rectTransform);
            decLbl.text = "-";

            // Hộp hiển thị số lượng ở giữa
            RectTransform qtyBox = CreateCenterSubRect(qtyBar, 0f, new Vector2(80f, 32f));
            EconomyHUD.CreatePanel(qtyBox, slotColor);
            quantityText = EconomyHUD.CreateText("Qty", qtyBox, 16f, textColor, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(quantityText.rectTransform);

            // Nút Tăng [+]
            btnIncrease = EconomyHUD.AddButton(CreateCenterSubRect(qtyBar, +80f, new Vector2(36f, 32f)), secondaryColor);
            btnIncrease.onClick.AddListener(() => AdjustQuantity(1));
            TextMeshProUGUI incLbl = EconomyHUD.CreateText("L", btnIncrease.GetComponent<RectTransform>(), 16f, Color.white, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(incLbl.rectTransform);
            incLbl.text = "+";

            // Nút Tối đa bên phải
            RectTransform maxRect = EconomyHUD.CreateRect("MaxBtn", qtyBar);
            maxRect.anchorMin = new Vector2(1f, 0.5f);
            maxRect.anchorMax = new Vector2(1f, 0.5f);
            maxRect.pivot = new Vector2(1f, 0.5f);
            maxRect.anchoredPosition = new Vector2(0f, 0f);
            maxRect.sizeDelta = new Vector2(84f, 32f);
            btnMax = EconomyHUD.AddButton(maxRect, secondaryColor);
            btnMax.onClick.AddListener(SetMaxQuantity);
            TextMeshProUGUI maxLbl = EconomyHUD.CreateText("L", maxRect, 14f, Color.white, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(maxLbl.rectTransform);
            maxLbl.text = "Tối đa";

            // ================= Nút Bắt đầu Chế biến =================
            RectTransform craftBtnRect = EconomyHUD.CreateRect("CraftBtn", col);
            craftBtnRect.anchorMin = new Vector2(0.5f, 0f);
            craftBtnRect.anchorMax = new Vector2(0.5f, 0f);
            craftBtnRect.pivot = new Vector2(0.5f, 0f);
            craftBtnRect.anchoredPosition = new Vector2(0f, 6f);
            craftBtnRect.sizeDelta = new Vector2(RightColWidth - 32f, 40f);

            btnStartCraft = EconomyHUD.AddButton(craftBtnRect, accentColor);
            btnStartCraft.onClick.AddListener(StartCraftingCurrent);
            btnStartCraftLabel = EconomyHUD.CreateText("Lbl", craftBtnRect, 16f, Color.white, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(btnStartCraftLabel.rectTransform);
            btnStartCraftLabel.text = "BẮT ĐẦU CHẾ BIẾN";
        }

        private RectTransform CreateCenterSubRect(RectTransform parent, float xOffset, Vector2 size)
        {
            RectTransform rect = EconomyHUD.CreateRect("Sub", parent);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(xOffset, 0f);
            rect.sizeDelta = size;
            return rect;
        }

        private void BuildQueueView()
        {
            // Header hàng đợi: Nút Nhận tất cả + thống kê
            RectTransform topBar = EconomyHUD.CreateRect("TopBar", queueContentArea);
            topBar.anchorMin = new Vector2(0f, 1f);
            topBar.anchorMax = new Vector2(1f, 1f);
            topBar.pivot = new Vector2(0f, 1f);
            topBar.anchoredPosition = Vector2.zero;
            topBar.sizeDelta = new Vector2(0f, 44f);

            queueSummaryText = EconomyHUD.CreateText("Summary", topBar, 16f, textColor, TextAlignmentOptions.Left);
            queueSummaryText.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            queueSummaryText.rectTransform.sizeDelta = new Vector2(400f, 36f);

            RectTransform claimAllRect = EconomyHUD.CreateRect("ClaimAllBtn", topBar);
            claimAllRect.anchorMin = new Vector2(1f, 0.5f);
            claimAllRect.anchorMax = new Vector2(1f, 0.5f);
            claimAllRect.pivot = new Vector2(1f, 0.5f);
            claimAllRect.anchoredPosition = Vector2.zero;
            claimAllRect.sizeDelta = new Vector2(160f, 38f);
            btnClaimAll = EconomyHUD.AddButton(claimAllRect, accentColor);
            btnClaimAll.onClick.AddListener(() =>
            {
                int claimed = CraftingQueueSystem.Instance.ClaimAllCompleted();
                if (claimed > 0)
                {
                    StartCoroutine(EconomyHUD.FloatText(panel, $"Đã nhận {claimed} mẻ thành phẩm vào kho!", accentColor, 18f, new Vector2(0f, 60f), new Vector2(0f, 130f), 1.2f));
                    Refresh();
                }
            });
            btnClaimAllLabel = EconomyHUD.CreateText("Lbl", claimAllRect, 14f, Color.white, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(btnClaimAllLabel.rectTransform);
            btnClaimAllLabel.text = "NHẬN TẤT CẢ";

            // Danh sách các mẻ đang chờ
            RectTransform scrollArea = EconomyHUD.CreateRect("ScrollArea", queueContentArea);
            scrollArea.anchorMin = Vector2.zero;
            scrollArea.anchorMax = Vector2.one;
            scrollArea.offsetMin = Vector2.zero;
            scrollArea.offsetMax = new Vector2(0f, -50f);

            queueScrollRect = scrollArea.gameObject.AddComponent<ScrollRect>();
            queueScrollRect.horizontal = false;
            queueScrollRect.vertical = true;
            queueScrollRect.scrollSensitivity = 35f;
            queueScrollRect.movementType = ScrollRect.MovementType.Elastic;
            queueScrollRect.elasticity = 0.1f;
            queueScrollRect.inertia = true;
            queueScrollRect.decelerationRate = 0.135f;

            RectTransform viewport = EconomyHUD.CreateRect("Viewport", scrollArea);
            EconomyHUD.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            queueScrollRect.viewport = viewport;

            queueListContent = EconomyHUD.CreateRect("Content", viewport);
            queueListContent.anchorMin = new Vector2(0f, 1f);
            queueListContent.anchorMax = new Vector2(1f, 1f);
            queueListContent.pivot = new Vector2(0f, 1f);
            queueListContent.sizeDelta = new Vector2(0f, 400f);
            queueScrollRect.content = queueListContent;

            VerticalLayoutGroup qLayout = queueListContent.gameObject.AddComponent<VerticalLayoutGroup>();
            qLayout.padding = new RectOffset(4, 4, 4, 8);
            qLayout.spacing = 8f;
            qLayout.childControlWidth = true;
            qLayout.childControlHeight = false;
            qLayout.childForceExpandWidth = true;
            qLayout.childForceExpandHeight = false;

            ContentSizeFitter qFitter = queueListContent.gameObject.AddComponent<ContentSizeFitter>();
            qFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }
}
