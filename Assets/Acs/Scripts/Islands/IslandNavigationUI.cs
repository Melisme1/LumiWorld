using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LumiWorld.Acs.Islands
{
    public sealed class IslandNavigationUI : MonoBehaviour
    {
        private IslandRuntimeManager islands;
        private readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>();
        private Button openButton;
        private Button formButton;
        private TextMeshProUGUI openLabel;
        private WetBiomePresentation presentation;
        private GameObject root;

        private IEnumerator Start()
        {
            while (IslandRuntimeManager.Instance == null || CardDeskController.Instance == null) yield return null;
            islands = IslandRuntimeManager.Instance;
            if (WorldLaunch.IsVisit) yield break;
            var canvas = CardDeskController.Instance.GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) yield break;
            root = new GameObject("IslandNavigation", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, -150);
            rect.sizeDelta = new Vector2(570, 40);
            float x = 0;
            foreach (var island in islands.Catalog.islands)
            {
                string id = island.id;
                var button = LumiWorldUITheme.CreatePillButton(rect, id, island.displayName, null, false, 14,
                    () => islands.FocusIsland(id), out _, out _, out _);
                LumiWorldUITheme.PlaceTopLeft(button.GetComponent<RectTransform>(), x, 0, 120, 36);
                buttons.Add(id, button);
                x += 128;
            }
            openButton = LumiWorldUITheme.CreatePillButton(rect, "OpenIsland", "Open · 200 Coins", null, true, 14,
                () => islands.RequestUnlock(islands.ActiveIslandId), out _, out _, out openLabel);
            LumiWorldUITheme.PlaceTopLeft(openButton.GetComponent<RectTransform>(), x, 0, 160, 36);
            presentation = GetComponent<WetBiomePresentation>();
            formButton = LumiWorldUITheme.CreatePillButton(rect, "FormBiome", "Form biome", null, true, 14,
                () => presentation?.RequestFormation(), out _, out _, out _);
            LumiWorldUITheme.PlaceTopLeft(formButton.GetComponent<RectTransform>(), x, 0, 160, 36);
        }

        private void Update()
        {
            if (islands == null || openButton == null) return;
            foreach (var pair in buttons) pair.Value.gameObject.SetActive(islands.Stage(pair.Key) != IslandAccessStage.Hidden);
            openButton.gameObject.SetActive(islands.Stage(islands.ActiveIslandId) == IslandAccessStage.Discovered);
            var active = islands.Catalog.Find(islands.ActiveIslandId);
            if (active != null) openLabel.text = $"Open · {active.unlockCoins} Coins";
            formButton.gameObject.SetActive(islands.IsUnlocked(islands.ActiveIslandId) && islands.ActiveIslandId == "wet" &&
                presentation != null && presentation.HasFormationCandidate);
            bool dragging = HexCameraController.Instance?.IsCardDragging == true;
            foreach (var pair in buttons) pair.Value.interactable = !dragging;
            openButton.interactable = !dragging;
            formButton.interactable = !dragging;
        }

        private void OnDestroy()
        {
            if (root != null) Destroy(root);
        }
    }
}
