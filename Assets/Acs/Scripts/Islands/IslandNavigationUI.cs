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
        private readonly Dictionary<string, GameObject> covers = new Dictionary<string, GameObject>();
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
                if (id == "starter") continue;
                // Cover and boundary have one lifecycle: both disappear when this island opens.
                var cloudRoot = new GameObject("IslandClouds_" + id);
                cloudRoot.transform.SetParent(transform, false);
                covers.Add(id, cloudRoot);
                var cover = new GameObject("CloudCover_" + id);
                cover.transform.SetParent(cloudRoot.transform, false);
                cover.transform.position = HexMetrics.HexToWorldPosition(island.center, 0);
                var extent = LandExtent(island);
                cover.AddComponent<FloatingCloudsManager>().ConfigureLowCover(extent, 90);
                var previous = islands.Catalog.Find(island.prerequisiteIsland);
                if (previous != null)
                {
                    var boundary = new GameObject("LowCloudBoundary_" + id);
                    boundary.transform.SetParent(cloudRoot.transform, false);
                    Vector3 from = HexMetrics.HexToWorldPosition(previous.center, 0);
                    Vector3 to = HexMetrics.HexToWorldPosition(island.center, 0);
                    boundary.transform.position = (from + to) * .5f;
                    boundary.transform.rotation = Quaternion.LookRotation(to - from);
                    boundary.AddComponent<FloatingCloudsManager>().ConfigureLowCover(new Vector2(Mathf.Max(extent.x, extent.y), 7), 22);
                }
            }
            openButton = LumiWorldUITheme.CreatePillButton(rect, "OpenIsland", "Open · 200 Coins", null, true, 14,
                () => islands.RequestUnlock(islands.ActiveIslandId), out _, out _, out openLabel);
            LumiWorldUITheme.PlaceTopLeft(openButton.GetComponent<RectTransform>(), x, 0, 160, 36);
            presentation = GetComponent<WetBiomePresentation>();
            formButton = LumiWorldUITheme.CreatePillButton(rect, "FormBiome", "Form biome", null, true, 14,
                () => presentation?.RequestFormation(), out _, out _, out _);
            LumiWorldUITheme.PlaceTopLeft(formButton.GetComponent<RectTransform>(), x, 0, 160, 36);
        }

        private static Vector2 LandExtent(IslandDefinition island)
        {
            var bounds = new Bounds(HexMetrics.HexToWorldPosition(island.center, 0), Vector3.zero);
            foreach (var hex in island.CreateBoundary().LandHexes) bounds.Encapsulate(HexMetrics.HexToWorldPosition(hex, 0));
            return new Vector2(bounds.size.x + 6, bounds.size.z + 6);
        }

        private void Update()
        {
            if (islands == null || openButton == null) return;
            foreach (var pair in buttons) pair.Value.gameObject.SetActive(islands.Stage(pair.Key) != IslandAccessStage.Hidden);
            foreach (var pair in covers)
                if (pair.Value != null && islands.IsUnlocked(pair.Key))
                {
                    pair.Value.SetActive(false);
                    Destroy(pair.Value);
                }
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
            foreach (var cover in covers.Values) if (cover != null) Destroy(cover);
            covers.Clear();
        }
    }
}
