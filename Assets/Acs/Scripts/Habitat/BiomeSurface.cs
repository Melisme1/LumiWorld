using System.Collections.Generic;
using UnityEngine;

namespace LumiWorld.Acs
{
    /// <summary>Runtime surface shared by the home hexes of a composed biome.</summary>
    public sealed class BiomeSurface : MonoBehaviour
    {
        private readonly List<Collider> surfaces = new List<Collider>();
        private readonly List<Collider> groundSurfaces = new List<Collider>();
        private readonly List<Collider> waterSurfaces = new List<Collider>();
        private Mesh groundMesh;
        public string HabitatId { get; private set; }
        public bool HasWaterSurface => waterSurfaces.Count > 0;

        public void SetHabitatId(string habitatId)
        {
            if (!string.IsNullOrEmpty(habitatId)) HabitatId = habitatId;
        }

        public void Configure(float elevationOffset, string habitatId = null)
        {
            SetHabitatId(habitatId);
            surfaces.Clear();
            groundSurfaces.Clear();
            waterSurfaces.Clear();
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (Transform child in transform)
            {
                if (child.name == "Ground_Basin")
                {
                    var filter = child.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    groundMesh = Instantiate(filter.sharedMesh);
                    var vertices = groundMesh.vertices;
                    for (int i = 0; i < vertices.Length; i++)
                        vertices[i].y += elevationOffset * Mathf.Clamp01(vertices[i].y / .9f);
                    groundMesh.vertices = vertices;
                    groundMesh.RecalculateBounds();
                    groundMesh.RecalculateNormals();
                    filter.sharedMesh = groundMesh;
                    AddSurface(filter, isWater: false);
                }
                else
                {
                    // Lift authored plants and water without stretching their proportions.
                    child.localPosition += Vector3.up * elevationOffset;
                    if (child.name == "Water_Surface")
                        foreach (var filter in child.GetComponentsInChildren<MeshFilter>()) AddSurface(filter, isWater: true);
                }
            }
        }

        private void AddSurface(MeshFilter filter, bool isWater)
        {
            if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable) return;
            var collider = filter.GetComponent<MeshCollider>();
            if (collider == null) collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.enabled = true;
            surfaces.Add(collider);
            if (isWater) waterSurfaces.Add(collider);
            else groundSurfaces.Add(collider);
        }

        public bool TryGetHeight(Vector3 position, out float height)
        {
            height = float.NegativeInfinity;
            var ray = new Ray(new Vector3(position.x, transform.position.y + 100, position.z), Vector3.down);
            foreach (var surface in surfaces)
                if (surface != null && surface.Raycast(ray, out var hit, 200)) height = Mathf.Max(height, hit.point.y);
            return !float.IsNegativeInfinity(height);
        }

        /// <summary>
        /// Samples ground elevation vs water elevation at an XZ point on this biome surface.
        /// hasWater is true when water sits above ground (waterDepth &gt; 0).
        /// </summary>
        public bool TrySampleSurface(
            Vector3 position,
            out float groundY,
            out float waterY,
            out bool hasWater,
            out float waterDepth)
        {
            groundY = float.NegativeInfinity;
            waterY = float.NegativeInfinity;
            hasWater = false;
            waterDepth = 0f;

            var ray = new Ray(new Vector3(position.x, transform.position.y + 100f, position.z), Vector3.down);
            for (int i = 0; i < groundSurfaces.Count; i++)
            {
                var c = groundSurfaces[i];
                if (c != null && c.Raycast(ray, out var hit, 200f))
                    groundY = Mathf.Max(groundY, hit.point.y);
            }
            for (int i = 0; i < waterSurfaces.Count; i++)
            {
                var c = waterSurfaces[i];
                if (c != null && c.Raycast(ray, out var hit, 200f))
                    waterY = Mathf.Max(waterY, hit.point.y);
            }

            bool hitGround = !float.IsNegativeInfinity(groundY);
            bool hitWater = !float.IsNegativeInfinity(waterY);
            if (!hitGround && !hitWater) return false;

            if (hitWater)
            {
                if (!hitGround)
                {
                    groundY = waterY - 0.35f;
                    waterDepth = 0.35f;
                    hasWater = true;
                }
                else
                {
                    waterDepth = Mathf.Max(0f, waterY - groundY);
                    hasWater = waterDepth > 0.015f;
                }
            }
            else
            {
                waterY = groundY;
                waterDepth = 0f;
                hasWater = false;
            }

            return true;
        }

        public static bool TryResolveClickedHabitat(
            in RaycastHit hit,
            HabitatRuntimeManager manager,
            out HabitatState matchedHabitat,
            out bool hitBlockingSurface)
        {
            matchedHabitat = null;
            hitBlockingSurface = false;
            if (hit.collider == null || manager == null || manager.Habitats == null) return false;

            var identity = hit.collider.GetComponentInParent<HabitatPlacementIdentity>();
            if (identity != null)
            {
                foreach (var habitat in manager.Habitats)
                {
                    if (habitat != null && habitat.IsFormedBiome &&
                        (habitat.members.Exists(m => m.placementId == identity.PlacementId) ||
                         habitat.residents.Exists(r => r.individualId == identity.PlacementId)))
                    {
                        matchedHabitat = habitat;
                        return true;
                    }
                }
            }

            var surface = hit.collider.GetComponentInParent<BiomeSurface>();
            if (surface != null)
            {
                hitBlockingSurface = true;
                if (hit.normal.y < 0.35f) return false;

                if (!string.IsNullOrEmpty(surface.HabitatId))
                    matchedHabitat = manager.FindHabitat(surface.HabitatId);

                if (matchedHabitat == null)
                    matchedHabitat = manager.FindHabitatByHex(HexMetrics.WorldToHex(hit.point));

                if (matchedHabitat != null && !matchedHabitat.IsFormedBiome)
                    matchedHabitat = null;

                return matchedHabitat != null;
            }

            var tile = hit.collider.GetComponentInParent<HexTileInfo>();
            if (tile != null)
            {
                hitBlockingSurface = true;
                if (!HexMetrics.IsTopSurfaceHit(hit)) return false;

                matchedHabitat = manager.FindHabitatByHex(tile.coordinates);
                if (matchedHabitat != null && !matchedHabitat.IsFormedBiome)
                    matchedHabitat = null;

                return matchedHabitat != null;
            }

            return false;
        }

        private void OnDestroy() { if (groundMesh != null) Destroy(groundMesh); }
    }
}
