using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a highlight/hologram mesh shell that mirrors the full intermediate transform hierarchy
/// (e.g., MeshPivot scale/rotation) between a source tile root and each active MeshFilter.
/// </summary>
public static class HexHighlightShellUtility
{
    public static bool IsPartOfPlacedProp(Transform t, Transform tileTransform)
    {
        Transform curr = t;
        while (curr != null && curr != tileTransform)
        {
            if (curr.GetComponent<PlacedCard>() != null || curr.name.StartsWith("Habitat_", StringComparison.Ordinal))
            {
                return true;
            }
            curr = curr.parent;
        }
        return false;
    }

    public static void PopulateMeshHierarchy(
        Transform sourceRoot,
        Transform shellRoot,
        Material overrideMaterial,
        bool skipPlacedProps,
        Action<MeshRenderer> onRendererCreated = null)
    {
        if (sourceRoot == null || shellRoot == null) return;

        var nodeMap = new Dictionary<Transform, Transform>
        {
            [sourceRoot] = shellRoot
        };

        MeshFilter[] sourceFilters = sourceRoot.GetComponentsInChildren<MeshFilter>(false);
        for (int i = 0; i < sourceFilters.Length; i++)
        {
            MeshFilter srcMf = sourceFilters[i];
            if (srcMf == null || srcMf.sharedMesh == null) continue;

            MeshRenderer srcMr = srcMf.GetComponent<MeshRenderer>();
            if (srcMr == null || !srcMr.enabled) continue;

            if (skipPlacedProps && IsPartOfPlacedProp(srcMf.transform, sourceRoot)) continue;

            Transform targetNode = srcMf.transform == sourceRoot
                ? CreateChildNode(shellRoot, "Mesh", Vector3.zero, Quaternion.identity, Vector3.one)
                : EnsureMirroredHierarchy(sourceRoot, srcMf.transform, nodeMap);

            if (targetNode.GetComponent<MeshFilter>() != null)
            {
                targetNode = CreateChildNode(
                    targetNode.parent != null ? targetNode.parent : shellRoot,
                    srcMf.name + "_Mesh",
                    targetNode.localPosition,
                    targetNode.localRotation,
                    targetNode.localScale);
            }

            MeshFilter mf = targetNode.gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = srcMf.sharedMesh;

            MeshRenderer mr = targetNode.gameObject.AddComponent<MeshRenderer>();
            if (overrideMaterial != null)
            {
                int subMeshCount = Mathf.Max(1, srcMf.sharedMesh.subMeshCount);
                if (subMeshCount == 1)
                {
                    mr.sharedMaterial = overrideMaterial;
                }
                else
                {
                    var mats = new Material[subMeshCount];
                    for (int m = 0; m < subMeshCount; m++) mats[m] = overrideMaterial;
                    mr.sharedMaterials = mats;
                }
            }

            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            onRendererCreated?.Invoke(mr);
        }
    }

    private static Transform EnsureMirroredHierarchy(
        Transform sourceRoot,
        Transform srcTransform,
        Dictionary<Transform, Transform> nodeMap)
    {
        if (nodeMap.TryGetValue(srcTransform, out Transform existing))
            return existing;

        Transform parentMirrored = srcTransform.parent != null && srcTransform.parent != sourceRoot
            ? EnsureMirroredHierarchy(sourceRoot, srcTransform.parent, nodeMap)
            : nodeMap[sourceRoot];

        Transform cloned = CreateChildNode(
            parentMirrored,
            srcTransform.name,
            srcTransform.localPosition,
            srcTransform.localRotation,
            srcTransform.localScale);
        nodeMap[srcTransform] = cloned;
        return cloned;
    }

    private static Transform CreateChildNode(
        Transform parent,
        string name,
        Vector3 localPos,
        Quaternion localRot,
        Vector3 localScale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = localScale;
        return go.transform;
    }
}
