using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LumiWorld.Acs.Islands;
using UnityEngine;

namespace LumiWorld.Acs
{
    /// <summary>Displays confirmed topology and requests approval; never owns membership or save data.</summary>
    public sealed class WetBiomePresentation : MonoBehaviour
    {
        private HabitatRuntimeManager habitats;
        private HexWorldGenerator world;
        private IslandCatalog catalog;
        private bool changed;
        private readonly HashSet<string> dismissed = new HashSet<string>();
        private readonly Dictionary<string, GameObject> visuals = new Dictionary<string, GameObject>();
        private readonly Dictionary<Renderer, bool> hiddenRenderers = new Dictionary<Renderer, bool>();
        private readonly Dictionary<Collider, bool> hiddenColliders = new Dictionary<Collider, bool>();
        private readonly List<HexTileInfo> surfacedTiles = new List<HexTileInfo>();
        private List<BiomeFormationCandidate> candidates = new List<BiomeFormationCandidate>();
        public bool HasFormationCandidate => candidates.Count > 0;

        private IEnumerator Start()
        {
            while (!HabitatRuntimeManager.TryGetActive(out habitats) || IslandRuntimeManager.Instance == null) yield return null;
            world = FindAnyObjectByType<HexWorldGenerator>();
            catalog = IslandRuntimeManager.Instance.Catalog;
            habitats.HabitatsChanged += OnChanged;
            OnChanged();
        }

        private void OnChanged() => changed = true;

        private void LateUpdate()
        {
            if (changed) { changed = false; RefreshVisuals(); candidates = habitats.GetFormationCandidates(); }
            if (habitats == null || WorldLaunch.IsVisit || IslandRuntimeManager.Instance?.ActiveIslandId != "wet" ||
                HexCameraController.Instance?.IsCardDragging == true ||
                (HexSinglePlacementController.Instance != null && HexSinglePlacementController.Instance.IsPreviewing)) return;

            // Auto-form / auto-upgrade 10-hex and 6-hex equilateral triangles immediately (priority 10 > 6 > 3).
            var autoCandidate = candidates.FirstOrDefault(c => c.MemberIds.Count >= 6 && !dismissed.Contains(c.Key));
            if (autoCandidate != null)
            {
                if (!habitats.ConfirmFormation(autoCandidate.Key))
                {
                    dismissed.Add(autoCandidate.Key);
                }
                return;
            }

            if (GameDialogUI.IsAnyOpen || EconomyHUD.IsWindowOpen) return;

            // Show confirm popup only for 3-hex triangle formation.
            var candidate = candidates.FirstOrDefault(c => c.MemberIds.Count == 3 && !dismissed.Contains(c.Key));
            if (candidate != null) ShowCandidate(candidate);
        }

        public void RequestFormation()
        {
            if (habitats == null || GameDialogUI.IsAnyOpen) return;
            var candidate = habitats.GetFormationCandidates().FirstOrDefault();
            if (candidate != null) ShowCandidate(candidate);
        }

        private void ShowCandidate(BiomeFormationCandidate candidate)
        {
            dismissed.Add(candidate.Key);
            var member = habitats.Habitats.SelectMany(h => h.members).First(m => m.placementId == candidate.MemberIds[0]);
            GameDialogUI.ShowConfirm($"{(candidate.ExistingHabitatId == null ? "Form" : "Upgrade")} {member.affinity} with {candidate.MemberIds.Count} hexes?",
                () => { if (!habitats.ConfirmFormation(candidate.Key)) GameDialogUI.ShowNotice("These tiles changed. Place matching Nature at the same elevation and try again."); },
                title: "FORM BIOME", confirmText: "Form now", cancelText: "Later");
        }

        private void RefreshVisuals()
        {
            if (habitats == null || world == null || catalog == null) return;
            var keep = new HashSet<string>();
            RestoreGround();
            foreach (var habitat in habitats.Habitats)
            {
                if (habitat.island != HabitatIsland.Wet) continue;
                var prefab = catalog.Visuals(habitat.natureCardId)?.ForSize(habitat.HexCount);
                if (prefab == null || !TryPose(habitat, out var position, out var rotation)) continue;
                string key = habitat.natureCardId + ":" + habitat.members[0].elevationLevel + ":" + string.Join("|", habitat.members.Select(m => m.placementId).OrderBy(id => id));
                keep.Add(key);
                if (!visuals.ContainsKey(key))
                {
                    var visual = Instantiate(prefab, position, rotation, transform);
                    visual.name = $"Biome_{habitat.affinity}_{habitat.HexCount}_{habitat.habitatId}";
                    var home = world.MapTiles[habitat.members[0].hex];
                    var (top, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(home);
                    visual.AddComponent<BiomeSurface>().Configure(top - home.transform.position.y - 1f, habitat.habitatId);
                    visuals.Add(key, visual);
                    StartCoroutine(Reveal(visual.transform));
                }
                var surfaceComp = visuals[key].GetComponent<BiomeSurface>();
                if (surfaceComp != null) surfaceComp.SetHabitatId(habitat.habitatId);
                foreach (var member in habitat.members)
                    if (world.MapTiles.TryGetValue(member.hex, out var tile) && tile != null)
                    {
                        var info = tile.GetComponent<HexTileInfo>();
                        info.biomeSurface = visuals[key].GetComponent<BiomeSurface>();
                        surfacedTiles.Add(info);
                        foreach (var collider in tile.GetComponentsInChildren<Collider>(true))
                        {
                            if (collider.GetComponentInParent<PlacedCard>() != null) continue;
                            hiddenColliders[collider] = collider.enabled;
                            collider.enabled = false;
                        }
                        foreach (var renderer in tile.GetComponentsInChildren<Renderer>(true))
                        {
                            var record = renderer.GetComponentInParent<PlacedCard>();
                            if (record != null && record.CardType != CardType.Terrain) continue;
                            if (!hiddenRenderers.ContainsKey(renderer)) hiddenRenderers.Add(renderer, renderer.enabled);
                            renderer.enabled = false;
                        }
                    }
            }
            foreach (var key in visuals.Keys.Where(k => !keep.Contains(k)).ToList())
            { if (visuals[key] != null) { visuals[key].SetActive(false); Destroy(visuals[key]); } visuals.Remove(key); }
        }

        private bool TryPose(HabitatState habitat, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            foreach (var member in habitat.members) position += HexMetrics.HexToWorldPosition(member.hex, world.SpawnOffsetY);
            position /= habitat.HexCount;
            if (habitat.HexCount == 1) return true;
            var authored = HexBiomeSurfaceMeshBuilder.GetUprightTriangleHexes(habitat.HexCount);
            var actual = new HashSet<HexCoordinates>(habitat.members.Select(m => m.hex));
            for (int step = 0; step < 6; step++)
                foreach (var anchor in actual)
                {
                    var offset = anchor - authored[0].Rotate60Clockwise(step);
                    if (!authored.All(h => actual.Contains(offset + h.Rotate60Clockwise(step)))) continue;
                    rotation = Quaternion.Euler(0, -60 * step, 0);
                    return true;
                }
            return false;
        }

        private static IEnumerator Reveal(Transform target)
        {
            var scale = target.localScale;
            float elapsed = 0;
            while (target != null && elapsed < .28f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / .28f);
                target.localScale = Vector3.Scale(scale, new Vector3(1, Mathf.Lerp(.9f, 1, t), 1));
                yield return null;
            }
            if (target != null) target.localScale = scale;
        }

        private void RestoreGround()
        {
            foreach (var pair in hiddenRenderers) if (pair.Key != null) pair.Key.enabled = pair.Value;
            hiddenRenderers.Clear();
            foreach (var pair in hiddenColliders) if (pair.Key != null) pair.Key.enabled = pair.Value;
            hiddenColliders.Clear();
            foreach (var info in surfacedTiles) if (info != null) info.biomeSurface = null;
            surfacedTiles.Clear();
        }
        private void OnDestroy()
        {
            if (habitats != null) habitats.HabitatsChanged -= OnChanged;
            RestoreGround();
        }
    }
}
