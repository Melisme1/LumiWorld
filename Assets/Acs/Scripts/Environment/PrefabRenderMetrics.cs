using System.Collections.Generic;
using UnityEngine;

namespace LumiWorld.Acs
{
    /// <summary>Measures active visible geometry, using LOD0 and preserving authored model transforms.</summary>
    public static class PrefabRenderMetrics
    {
        public static bool TryMeasureAuthored(GameObject root, out Bounds bounds)
        {
            // Remove placement/ancestors, retain the prefab's own rotation and scale. Root position
            // is intentionally excluded: generator assigns a new position at the surface contact.
            Matrix4x4 authored = Matrix4x4.TRS(Vector3.zero, root.transform.localRotation, root.transform.localScale);
            return TryMeasure(root, authored * root.transform.worldToLocalMatrix, out bounds);
        }

        public static bool TryMeasureWorld(GameObject root, out Bounds bounds)
        {
            return TryMeasure(root, Matrix4x4.identity, out bounds);
        }

        public static bool TryMeasureRelative(GameObject root, Transform relativeTo, out Bounds bounds)
        {
            return TryMeasure(root, relativeTo.worldToLocalMatrix, out bounds);
        }

        private static bool TryMeasure(GameObject root, Matrix4x4 worldToMeasurement, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            var excluded = new HashSet<Renderer>();
            var primary = new HashSet<Renderer>();
            foreach (LODGroup group in root.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] levels = group.GetLODs();
                for (int i = 0; i < levels.Length; i++)
                    foreach (Renderer renderer in levels[i].renderers)
                        if (renderer != null) (i == 0 ? primary : excluded).Add(renderer);
            }
            excluded.ExceptWith(primary);

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || excluded.Contains(renderer) || !IsActiveBelowRoot(renderer.transform, root.transform))
                    continue;

                Bounds local;
                if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
                    local = skin.localBounds;
                else if (renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
                    local = filter.sharedMesh.bounds;
                else
                    continue;

                Matrix4x4 matrix = worldToMeasurement * renderer.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = local.center + Vector3.Scale(local.extents, new Vector3(
                        (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    point = matrix.MultiplyPoint3x4(point);
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            return found && bounds.size.sqrMagnitude > .000001f;
        }

        private static bool IsActiveBelowRoot(Transform transform, Transform root)
        {
            for (Transform current = transform; current != null && current != root; current = current.parent)
                if (!current.gameObject.activeSelf) return false;
            return true;
        }
    }
}
