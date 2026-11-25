using System.Collections.Generic;
using UnityEngine;

namespace LumiWorld.Acs
{
    /// <summary>Runtime surface shared by the home hexes of a composed biome.</summary>
    public sealed class BiomeSurface : MonoBehaviour
    {
        private readonly List<Collider> surfaces = new List<Collider>();
        private Mesh groundMesh;

        public void Configure(float elevationOffset)
        {
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
                    AddSurface(filter);
                }
                else
                {
                    // Lift authored plants and water without stretching their proportions.
                    child.localPosition += Vector3.up * elevationOffset;
                    if (child.name.StartsWith("Water"))
                        foreach (var filter in child.GetComponentsInChildren<MeshFilter>()) AddSurface(filter);
                }
            }
        }

        private void AddSurface(MeshFilter filter)
        {
            var collider = filter.GetComponent<MeshCollider>();
            if (collider == null) collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.enabled = true;
            surfaces.Add(collider);
        }

        public bool TryGetHeight(Vector3 position, out float height)
        {
            height = float.NegativeInfinity;
            var ray = new Ray(new Vector3(position.x, transform.position.y + 100, position.z), Vector3.down);
            foreach (var surface in surfaces)
                if (surface != null && surface.Raycast(ray, out var hit, 200)) height = Mathf.Max(height, hit.point.y);
            return !float.IsNegativeInfinity(height);
        }

        private void OnDestroy() { if (groundMesh != null) Destroy(groundMesh); }
    }
}
