using System;
using LumiWorld.Acs;
using UnityEditor;
using UnityEngine;

namespace LumiWorld.EditorTools
{
    public static class WetIslandLayoutValidation
    {
        [MenuItem("LumiWorld/WetIsland/Validate 12 Diorama Prefabs")]
        public static void Validate()
        {
            foreach (var biome in new[]{"Lilywater","Reedmarsh","Streamstone","Rainleaf"})
            foreach (var size in new[]{3,6,10})
            {
                string path=$"Assets/Acs/Hex/WetIsland/Biomes/Prefabs/Biome_{biome}_{size}Hex.prefab";
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var c=root.GetComponent<BiomeLayoutController>();
                    if(c==null || c.Profile==null || c.ClusterSize!=size)throw new InvalidOperationException(path);
                    if (c.Profile.sizingVersion < WetIslandTierPresetSetup.Version)
                        throw new InvalidOperationException("Prepare Measured Tier Presets before validation: " + path);
                    ValidateSurface(c);
                    int count=0;
                    foreach(Transform group in c.PropsContainer)foreach(Transform prop in group)
                    {
                        if(PrefabUtility.GetCorrespondingObjectFromSource(prop.gameObject)==null)
                            throw new InvalidOperationException("Missing art source: "+prop.name+" in "+path);
                        count++;
                    }
                    ValidatePlacedSizes(c);
                    var result=c.Regenerate(false,BiomeLayoutControllerEditor.InstantiateLinkedPrefab);
                    ValidatePlacedSizes(c);
                    int expected = 0;
                    foreach (BiomePropLayer layer in c.Profile.layers)
                    {
                        BiomePropTier tier = layer.GetTier(size);
                        expected += tier.enabled ? tier.count : 0;
                    }
                    if(result.placedPropCount!=expected || c.ActivityZones.WalkableSpots.Count==0 ||
                       (biome!="Rainleaf" && c.ActivityZones.SwimmableSpots.Count==0))
                        throw new InvalidOperationException("Baked/regenerated layout mismatch in "+path);
                    Debug.Log($"[Diorama] {biome} {size}: stored dimensions and regeneration checks passed. Visual review remains separate.");
                }
                finally {PrefabUtility.UnloadPrefabContents(root);}
            }
        }

        private static void ValidatePlacedSizes(BiomeLayoutController controller)
        {
            foreach (Transform group in controller.PropsContainer)
            {
                BiomePropLayer layer = Array.Find(controller.Profile.layers, entry => entry.roleId == group.name);
                if (layer == null || !layer.useTierPresets) continue;
                DioramaSizeLimits limits = layer.GetTier(controller.ClusterSize).size;
                foreach (Transform prop in group)
                {
                    if (prop.GetComponent<BiomePropLock>() != null) continue;
                    GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(prop.gameObject);
                    if (source == null || !PrefabRenderMetrics.TryMeasureAuthored(source, out Bounds bounds))
                        throw new InvalidOperationException("Cannot measure " + prop.name);
                    Vector3 authored = source.transform.localScale;
                    if (Mathf.Abs(authored.x) < .00001f || Mathf.Abs(authored.y) < .00001f || Mathf.Abs(authored.z) < .00001f)
                        throw new InvalidOperationException("Zero authored scale: " + source.name);
                    Vector3 ratio = new Vector3(prop.localScale.x / authored.x, prop.localScale.y / authored.y, prop.localScale.z / authored.z);
                    if (!DioramaSizing.TryGetScaleRange(bounds.size.y, Mathf.Max(bounds.size.x, bounds.size.z), limits, out float min, out float max) ||
                        ratio.x < min - .001f || ratio.x > max + .001f || Mathf.Abs(ratio.x - ratio.y) > .001f || Mathf.Abs(ratio.x - ratio.z) > .001f)
                        throw new InvalidOperationException("Stored prop exceeds tier dimensions: " + prop.name + ". Regenerate and bake this prefab first.");
                }
            }
        }

        private static void ValidateSurface(BiomeLayoutController controller)
        {
            var profile = controller.Profile;
            var size = controller.ClusterSize;
            var mask = size == 3 ? profile.waterMask3Hex : size == 6 ? profile.waterMask6Hex : profile.waterMask10Hex;
            if (mask == null) return;
            var hexes = HexBiomeSurfaceMeshBuilder.GetUprightTriangleHexes(size);
            var generated = HexBiomeSurfaceMeshBuilder.BuildWaterSurfaceMesh("Validation", hexes, mask, profile.surfaceSettings);
            try
            {
                var baked = controller.transform.Find("Water_Surface").GetComponent<MeshFilter>().sharedMesh;
                if (generated.vertexCount == 0 || (baked.bounds.center - generated.bounds.center).magnitude > .12f ||
                    (baked.bounds.size - generated.bounds.size).magnitude > .2f)
                    throw new InvalidOperationException("Water mask and baked mesh differ in "+controller.name);
            }
            finally { UnityEngine.Object.DestroyImmediate(generated); }
        }
    }
}
