using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LumiWorld.Acs.Islands
{
    /// <summary>
    /// Top-center collapsible Island Switcher HUD mirroring P-03 Card Tray's Warm Stone monolith style:
    /// - Uses vector-mesh RoundedCapsuleGraphic (rectangular text core + circular end-caps, zero sprite stretching/scratches).
    /// - Scaled cleanly in 1280x720 design units via ResourceInventoryUILayout.FitScale so it stays crisp and legible.
    /// - Supports Hide/Show toggle via integrated bottom chevron dome (collapses into a compact pill showing active island + chevron).
    /// </summary>
    public sealed class IslandNavigationUI : MonoBehaviour
    {
        private const float TopMargin = 10f;
        private const float ExpandedBodyHeight = 52f;
        private const float ChevronDomeHeight = 20f;
        private const float ChevronDomeHalfWidth = 26f;
        private const float ExpandedTotalHeight = ExpandedBodyHeight + ChevronDomeHeight;
        private const float CollapsedWidth = 176f;
        private const float CollapsedHeight = 32f;
        private const float ButtonHeight = 38f;
        private const float ButtonGap = 8f;
        private const float TrayHorizontalPad = 14f;

        private sealed class IslandButtonView
        {
            public string id;
            public string displayName;
            public RectTransform rect;
            public Button button;
            public RoundedCapsuleGraphic pill;
            public Image icon;
            public TextMeshProUGUI label;
            public float width;
        }

        private IslandRuntimeManager islands;
        private WetBiomePresentation presentation;
        private readonly List<IslandButtonView> islandButtons = new List<IslandButtonView>();

        private RectTransform hudRoot;
        private RectTransform trayRect;
        private RoundedCapsuleGraphic trayGraphic;
        private RectTransform contentRow;
        private RectTransform chevronBtnRect;
        private Image chevronGlyphImg;

        private RectTransform collapsedRow;
        private Image collapsedIcon;
        private TextMeshProUGUI collapsedLabel;

        private Button openButton;
        private RectTransform openButtonRect;
        private RoundedCapsuleGraphic openButtonPill;
        private TextMeshProUGUI openLabel;

        private Button formButton;
        private RectTransform formButtonRect;
        private RoundedCapsuleGraphic formButtonPill;

        private bool isExpanded = true;
        private int lastScreenW;
        private int lastScreenH;
        private float lastCanvasScale;
        private string lastLayoutSignature;

        private IEnumerator Start()
        {
            while (IslandRuntimeManager.Instance == null || CardDeskController.Instance == null) yield return null;
            islands = IslandRuntimeManager.Instance;
            if (WorldLaunch.IsVisit) yield break;

            var canvas = CardDeskController.Instance.GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) yield break;

            presentation = GetComponent<WetBiomePresentation>();
            BuildHud(canvas.transform);
            SyncResponsiveScale(force: true);
            RefreshStateAndLayout(forceLayout: true);
        }

        private void BuildHud(Transform canvasRoot)
        {
            GameObject rootObj = new GameObject("IslandNavigation", typeof(RectTransform));
            rootObj.transform.SetParent(canvasRoot, false);
            hudRoot = (RectTransform)rootObj.transform;
            hudRoot.anchorMin = new Vector2(0.5f, 1f);
            hudRoot.anchorMax = new Vector2(0.5f, 1f);
            hudRoot.pivot = new Vector2(0.5f, 1f);
            hudRoot.anchoredPosition = new Vector2(0f, -TopMargin);
            hudRoot.sizeDelta = new Vector2(460f, ExpandedTotalHeight);

            // 1. Monolithic Warm Stone Vector Background (top dock + integrated bottom chevron dome)
            GameObject trayObj = new GameObject("P02_IslandTrayMonolith", typeof(RectTransform), typeof(RoundedCapsuleGraphic));
            trayRect = (RectTransform)trayObj.transform;
            trayRect.SetParent(hudRoot, false);
            trayRect.anchorMin = new Vector2(0.5f, 1f);
            trayRect.anchorMax = new Vector2(0.5f, 1f);
            trayRect.pivot = new Vector2(0.5f, 1f);
            trayRect.anchoredPosition = Vector2.zero;
            trayRect.sizeDelta = new Vector2(300f, ExpandedTotalHeight);

            trayGraphic = trayObj.GetComponent<RoundedCapsuleGraphic>();
            trayGraphic.ConfigureStyle(
                fill: new Color(0.937f, 0.918f, 0.886f, 1f),       // Warm Stone #EFEAE2 (exact P-03 match)
                border: new Color(0.835f, 0.808f, 0.765f, 1f),     // Crisp hairline #D5CEC3
                shadow: new Color(0.145f, 0.106f, 0.071f, 0.17f),
                borderWidth: 1.5f);
            trayGraphic.raycastTarget = true;

            // 2. Expanded Island Buttons Row
            GameObject rowObj = new GameObject("IslandButtonsRow", typeof(RectTransform));
            contentRow = (RectTransform)rowObj.transform;
            contentRow.SetParent(trayRect, false);
            contentRow.anchorMin = new Vector2(0.5f, 1f);
            contentRow.anchorMax = new Vector2(0.5f, 1f);
            contentRow.pivot = new Vector2(0.5f, 1f);
            contentRow.anchoredPosition = new Vector2(0f, -(ExpandedBodyHeight - ButtonHeight) * 0.5f);
            contentRow.sizeDelta = new Vector2(280f, ButtonHeight);

            foreach (var island in islands.Catalog.islands)
            {
                string id = island.id;
                float btnW = Mathf.Clamp(58f + island.displayName.Length * 8.6f, 120f, 168f);
                var view = CreateIslandCapsuleButton(contentRow, id, island.displayName, btnW, () =>
                {
                    GameAudioManager.Instance?.PlayClick();
                    islands.FocusIsland(id);
                });
                islandButtons.Add(view);
            }

            // Action pills (Unlock Island / Form Biome) using the same crisp vector capsule
            openButton = CreateActionCapsuleButton(contentRow, "OpenIsland", "Open · 200 Coins", 170f, () =>
            {
                GameAudioManager.Instance?.PlayClick();
                islands.RequestUnlock(islands.ActiveIslandId);
            }, out openButtonRect, out openButtonPill, out openLabel);

            formButton = CreateActionCapsuleButton(contentRow, "FormBiome", "Form biome", 132f, () =>
            {
                GameAudioManager.Instance?.PlayClick();
                presentation?.RequestFormation();
            }, out formButtonRect, out formButtonPill, out _);

            // 3. Compact Collapsed Row (Active Island Icon + Name + ChevronDown)
            GameObject colObj = new GameObject("CollapsedRow", typeof(RectTransform), typeof(Button));
            collapsedRow = (RectTransform)colObj.transform;
            collapsedRow.SetParent(trayRect, false);
            collapsedRow.anchorMin = Vector2.zero;
            collapsedRow.anchorMax = Vector2.one;
            collapsedRow.offsetMin = Vector2.zero;
            collapsedRow.offsetMax = Vector2.zero;

            Button colBtn = colObj.GetComponent<Button>();
            colBtn.transition = Selectable.Transition.None;
            colBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            colBtn.onClick.AddListener(ToggleExpanded);

            GameObject colIcObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            RectTransform colIcRect = (RectTransform)colIcObj.transform;
            colIcRect.SetParent(collapsedRow, false);
            colIcRect.anchorMin = colIcRect.anchorMax = new Vector2(0f, 0.5f);
            colIcRect.pivot = new Vector2(0f, 0.5f);
            colIcRect.anchoredPosition = new Vector2(14f, 0f);
            colIcRect.sizeDelta = new Vector2(17f, 17f);
            collapsedIcon = colIcObj.GetComponent<Image>();
            collapsedIcon.preserveAspect = true;
            collapsedIcon.color = LumiWorldUITheme.Text;
            collapsedIcon.raycastTarget = false;

            collapsedLabel = LumiWorldUITheme.CreateLabel(collapsedRow, "Label", "Starter", 13.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
            collapsedLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            collapsedLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            collapsedLabel.rectTransform.offsetMin = new Vector2(36f, 0f);
            collapsedLabel.rectTransform.offsetMax = new Vector2(-28f, 0f);

            // 4. Integrated Chevron Toggle Button (bottom-center dome when expanded, right side when collapsed)
            GameObject chevObj = new GameObject("P02_IslandChevronButton", typeof(RectTransform), typeof(Image), typeof(Button));
            chevronBtnRect = (RectTransform)chevObj.transform;
            chevronBtnRect.SetParent(trayRect, false);
            chevronBtnRect.anchorMin = new Vector2(0.5f, 0f);
            chevronBtnRect.anchorMax = new Vector2(0.5f, 0f);
            chevronBtnRect.pivot = new Vector2(0.5f, 0f);
            chevronBtnRect.sizeDelta = new Vector2(64f, 22f);

            Image chevHit = chevObj.GetComponent<Image>();
            chevHit.color = Color.clear;
            chevHit.raycastTarget = true;

            GameObject chevIcObj = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
            RectTransform chevIcRect = (RectTransform)chevIcObj.transform;
            chevIcRect.SetParent(chevronBtnRect, false);
            chevIcRect.anchorMin = chevIcRect.anchorMax = chevIcRect.pivot = new Vector2(0.5f, 0.5f);
            chevIcRect.sizeDelta = new Vector2(12f, 12f);
            chevIcRect.anchoredPosition = Vector2.zero;

            chevronGlyphImg = chevIcObj.GetComponent<Image>();
            chevronGlyphImg.preserveAspect = true;
            chevronGlyphImg.color = Color.white;
            chevronGlyphImg.raycastTarget = false;

            Button chevBtn = chevObj.GetComponent<Button>();
            chevBtn.targetGraphic = chevronGlyphImg;
            chevBtn.transition = Selectable.Transition.ColorTint;
            ColorBlock cb = chevBtn.colors;
            cb.normalColor = new Color(0.35f, 0.30f, 0.25f, 1f);
            cb.highlightedColor = LumiWorldUITheme.Text;
            cb.pressedColor = new Color(0.15f, 0.11f, 0.08f, 1f);
            cb.selectedColor = cb.normalColor;
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            chevBtn.colors = cb;
            chevBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            chevBtn.onClick.AddListener(ToggleExpanded);
        }

        private static IslandButtonView CreateIslandCapsuleButton(
            Transform parent,
            string id,
            string displayName,
            float width,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject obj = new GameObject("IslandBtn_" + id, typeof(RectTransform), typeof(RoundedCapsuleGraphic), typeof(Button));
            RectTransform rt = (RectTransform)obj.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(width, ButtonHeight);

            RoundedCapsuleGraphic pill = obj.GetComponent<RoundedCapsuleGraphic>();
            pill.ConfigurePill(
                fill: new Color(0.890f, 0.867f, 0.831f, 1f),
                border: new Color(0.845f, 0.818f, 0.778f, 1f),
                radius: ButtonHeight * 0.5f,
                fullCapsule: true,
                borderWidth: 1.1f,
                shadowAlpha: 0.0f);
            pill.raycastTarget = true;

            Button btn = obj.GetComponent<Button>();
            btn.targetGraphic = pill;
            btn.transition = Selectable.Transition.ColorTint;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.97f, 0.96f, 0.94f, 1f);
            cb.pressedColor = new Color(0.91f, 0.89f, 0.86f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.88f, 0.86f, 0.83f, 0.7f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(onClick);

            Sprite islandGlyph = LumiWorldUITheme.IslandGlyph(id);
            Image iconImg = null;
            float textLeft = 16f;
            if (islandGlyph != null)
            {
                GameObject icObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                RectTransform icRect = (RectTransform)icObj.transform;
                icRect.SetParent(rt, false);
                icRect.anchorMin = icRect.anchorMax = new Vector2(0f, 0.5f);
                icRect.pivot = new Vector2(0f, 0.5f);
                icRect.anchoredPosition = new Vector2(13f, 0f);
                icRect.sizeDelta = new Vector2(18f, 18f);
                iconImg = icObj.GetComponent<Image>();
                iconImg.sprite = islandGlyph;
                iconImg.preserveAspect = true;
                iconImg.color = LumiWorldUITheme.Text;
                iconImg.raycastTarget = false;
                textLeft = 36f;
            }

            TextMeshProUGUI lbl = LumiWorldUITheme.CreateLabel(rt, "Label", displayName, 14.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
            lbl.rectTransform.anchorMin = Vector2.zero;
            lbl.rectTransform.anchorMax = Vector2.one;
            lbl.rectTransform.offsetMin = new Vector2(textLeft, 0f);
            lbl.rectTransform.offsetMax = new Vector2(-10f, 0f);

            return new IslandButtonView
            {
                id = id,
                displayName = displayName,
                rect = rt,
                button = btn,
                pill = pill,
                icon = iconImg,
                label = lbl,
                width = width
            };
        }

        private static Button CreateActionCapsuleButton(
            Transform parent,
            string name,
            string text,
            float width,
            UnityEngine.Events.UnityAction onClick,
            out RectTransform rect,
            out RoundedCapsuleGraphic pill,
            out TextMeshProUGUI label)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(RoundedCapsuleGraphic), typeof(Button));
            rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(width, ButtonHeight);

            pill = obj.GetComponent<RoundedCapsuleGraphic>();
            pill.ConfigurePill(
                fill: LumiWorldUITheme.Primary,
                border: new Color(0.20f, 0.19f, 0.17f, 1f),
                radius: ButtonHeight * 0.5f,
                fullCapsule: true,
                borderWidth: 1.1f,
                shadowAlpha: 0.16f,
                shadowDepth: 2f,
                shadowBlur: 4f);
            pill.raycastTarget = true;

            Button btn = obj.GetComponent<Button>();
            btn.targetGraphic = pill;
            LumiWorldUITheme.ApplyButtonDisabledNeutral(btn);
            btn.onClick.AddListener(onClick);

            label = LumiWorldUITheme.CreateLabel(rect, "Label", text, 12.5f, Color.white, true, TextAlignmentOptions.Center);
            LumiWorldUITheme.Stretch(label.rectTransform, 10f);
            return btn;
        }

        public void ToggleExpanded()
        {
            isExpanded = !isExpanded;
            GameAudioManager.Instance?.PlayClick();
            RefreshStateAndLayout(forceLayout: true);
        }

        private void LateUpdate()
        {
            SyncResponsiveScale();
        }

        private void Update()
        {
            if (islands == null || openButton == null) return;
            RefreshStateAndLayout(forceLayout: false);
        }

        private void RefreshStateAndLayout(bool forceLayout)
        {
            if (islands == null || trayRect == null) return;

            bool dragging = HexCameraController.Instance?.IsCardDragging == true;
            string activeId = islands.ActiveIslandId ?? string.Empty;
            bool showOpen = islands.Stage(activeId) == IslandAccessStage.Discovered;
            bool showForm = islands.IsUnlocked(activeId) && activeId == "wet" &&
                            presentation != null && presentation.HasFormationCandidate;

            var activeDef = islands.Catalog.Find(activeId);
            if (activeDef != null)
            {
                if (openLabel != null) openLabel.text = $"Open · {activeDef.unlockCoins} Coins";
                if (collapsedLabel != null) collapsedLabel.text = activeDef.displayName;
                if (collapsedIcon != null)
                {
                    collapsedIcon.sprite = LumiWorldUITheme.IslandGlyph(activeDef.id);
                    collapsedIcon.enabled = collapsedIcon.sprite != null;
                }
            }

            // Update button visibility, active/inactive styling, and interactability
            int visibleIslandsMask = 0;
            for (int i = 0; i < islandButtons.Count; i++)
            {
                var view = islandButtons[i];
                bool visible = islands.Stage(view.id) != IslandAccessStage.Hidden;
                if (view.button.gameObject.activeSelf != visible) view.button.gameObject.SetActive(visible);
                if (visible) visibleIslandsMask |= (1 << i);

                bool isActive = string.Equals(view.id, activeId, System.StringComparison.OrdinalIgnoreCase);
                if (isActive)
                {
                    view.pill.ConfigurePill(
                        fill: LumiWorldUITheme.Slot,
                        border: new Color(0.87f, 0.84f, 0.80f, 1f),
                        radius: ButtonHeight * 0.5f,
                        fullCapsule: true,
                        borderWidth: 1.2f,
                        shadowAlpha: 0.14f,
                        shadowDepth: 2f,
                        shadowBlur: 4f);
                }
                else
                {
                    view.pill.ConfigurePill(
                        fill: new Color(0.890f, 0.867f, 0.831f, 1f),
                        border: new Color(0.848f, 0.822f, 0.782f, 1f),
                        radius: ButtonHeight * 0.5f,
                        fullCapsule: true,
                        borderWidth: 1.0f,
                        shadowAlpha: 0f);
                }

                Color fg = isActive ? LumiWorldUITheme.Text : new Color(0.39f, 0.34f, 0.30f, 1f);
                if (view.label != null) view.label.color = fg;
                if (view.icon != null) view.icon.color = fg;
                view.button.interactable = !dragging;
            }

            if (openButton.gameObject.activeSelf != showOpen) openButton.gameObject.SetActive(showOpen);
            if (formButton.gameObject.activeSelf != showForm) formButton.gameObject.SetActive(showForm);
            openButton.interactable = !dragging;
            formButton.interactable = !dragging;

            string sig = $"{isExpanded}:{visibleIslandsMask}:{showOpen}:{showForm}";
            if (!forceLayout && sig == lastLayoutSignature) return;
            lastLayoutSignature = sig;

            if (isExpanded)
            {
                float x = 0f;
                for (int i = 0; i < islandButtons.Count; i++)
                {
                    var view = islandButtons[i];
                    if (!view.button.gameObject.activeSelf) continue;
                    view.rect.anchoredPosition = new Vector2(x, 0f);
                    x += view.width + ButtonGap;
                }
                if (showOpen)
                {
                    openButtonRect.anchoredPosition = new Vector2(x, 0f);
                    x += openButtonRect.sizeDelta.x + ButtonGap;
                }
                if (showForm)
                {
                    formButtonRect.anchoredPosition = new Vector2(x, 0f);
                    x += formButtonRect.sizeDelta.x + ButtonGap;
                }

                float rowWidth = Mathf.Max(140f, x - ButtonGap);
                float trayWidth = rowWidth + TrayHorizontalPad * 2f;

                contentRow.sizeDelta = new Vector2(rowWidth, ButtonHeight);
                contentRow.gameObject.SetActive(true);
                collapsedRow.gameObject.SetActive(false);

                trayRect.sizeDelta = new Vector2(trayWidth, ExpandedTotalHeight);
                hudRoot.sizeDelta = new Vector2(trayWidth, ExpandedTotalHeight);
                trayGraphic.ConfigureTopCollapsibleTray(
                    expandedWithDome: true,
                    trayBodyHeight: ExpandedBodyHeight,
                    radius: 22f,
                    newDomeHalfWidth: ChevronDomeHalfWidth,
                    newDomeHeight: ChevronDomeHeight,
                    newDomeBaseFillet: 8f,
                    newDomeBottomRadius: 9f);

                chevronBtnRect.anchorMin = new Vector2(0.5f, 0f);
                chevronBtnRect.anchorMax = new Vector2(0.5f, 0f);
                chevronBtnRect.pivot = new Vector2(0.5f, 0f);
                chevronBtnRect.anchoredPosition = new Vector2(0f, 0f);
                chevronBtnRect.sizeDelta = new Vector2(64f, 22f);
                chevronGlyphImg.sprite = LumiWorldUITheme.Glyph("ChevronUp");
                chevronGlyphImg.rectTransform.anchoredPosition = new Vector2(0f, 1.5f);
            }
            else
            {
                contentRow.gameObject.SetActive(false);
                collapsedRow.gameObject.SetActive(true);

                trayRect.sizeDelta = new Vector2(CollapsedWidth, CollapsedHeight);
                hudRoot.sizeDelta = new Vector2(CollapsedWidth, CollapsedHeight);
                trayGraphic.ConfigureTopCollapsibleTray(
                    expandedWithDome: false,
                    trayBodyHeight: CollapsedHeight,
                    radius: CollapsedHeight * 0.5f);

                chevronBtnRect.anchorMin = new Vector2(1f, 0.5f);
                chevronBtnRect.anchorMax = new Vector2(1f, 0.5f);
                chevronBtnRect.pivot = new Vector2(1f, 0.5f);
                chevronBtnRect.anchoredPosition = new Vector2(-6f, 0f);
                chevronBtnRect.sizeDelta = new Vector2(24f, CollapsedHeight);
                chevronGlyphImg.sprite = LumiWorldUITheme.Glyph("ChevronDown");
                chevronGlyphImg.rectTransform.anchoredPosition = Vector2.zero;
            }
        }

        private void SyncResponsiveScale(bool force = false)
        {
            if (hudRoot == null) return;
            Canvas rootCanvas = hudRoot.GetComponentInParent<Canvas>()?.rootCanvas;
            if (rootCanvas == null || rootCanvas.scaleFactor <= 0.01f) return;

            if (!force && lastScreenW == Screen.width && lastScreenH == Screen.height &&
                Mathf.Approximately(lastCanvasScale, rootCanvas.scaleFactor))
            {
                return;
            }

            lastScreenW = Screen.width;
            lastScreenH = Screen.height;
            lastCanvasScale = rootCanvas.scaleFactor;

            Vector2 pixels = ((RectTransform)rootCanvas.transform).rect.size * rootCanvas.scaleFactor;
            float fit = ResourceInventoryUILayout.FitScale(pixels.x, pixels.y);
            if (fit <= 0.01f) return;

            float scale = fit / rootCanvas.scaleFactor;
            hudRoot.localScale = Vector3.one * scale;
            hudRoot.anchoredPosition = new Vector2(0f, -TopMargin * scale);
        }

        private void OnDestroy()
        {
            if (hudRoot != null) Destroy(hudRoot.gameObject);
        }
    }
}
