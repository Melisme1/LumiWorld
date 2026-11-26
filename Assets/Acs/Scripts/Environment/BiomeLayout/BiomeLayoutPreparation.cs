using System;
using System.Collections.Generic;
using UnityEngine;

namespace LumiWorld.Acs
{
    public sealed class PreparedBiomeLayer
    {
        public DioramaRule Rule { get; internal set; }
        public GameObject[] Assets { get; internal set; }
        public Bounds[] Bounds { get; internal set; }
        public DioramaPlan.Variant[] Variants { get; internal set; }
    }

    public static class BiomeLayoutPreparation
    {
        public static PreparedBiomeLayer[] Prepare(BiomeLayoutProfile profile, int tier, List<string> excludedAssets = null)
        {
            if (profile == null || profile.layers == null) throw new InvalidOperationException("Assign a layout profile with layers.");
            var result = new PreparedBiomeLayer[profile.layers.Length];
            var measurements = new Dictionary<GameObject, Bounds>();
            for (int i = 0; i < result.Length; i++)
            {
                BiomePropLayer layer = profile.layers[i];
                if (layer.allowedVisualOverlap < 0 || layer.allowedVisualOverlap > 1 ||
                    layer.solidFootprintFraction < 0 || layer.solidFootprintFraction > 1)
                    throw new InvalidOperationException("Overlap and solid footprint fraction must be between 0 and 1: " + layer.roleId);
                BiomePropTier preset = layer.GetTier(tier);
                int count = layer.useTierPresets ? (preset.enabled ? preset.count : 0)
                    : tier == 1 ? layer.count1 : tier == 3 ? layer.count3 : tier == 6 ? layer.count6 : layer.count10;
                if (count < 0) throw new InvalidOperationException("Count cannot be negative: " + layer.roleId);
                GameObject[] assets = (layer.useTierPresets ? preset.assets : layer.pool) ?? Array.Empty<GameObject>();
                var bounds = new Bounds[assets.Length];
                var variants = new List<DioramaPlan.Variant>();
                for (int assetIndex = 0; assetIndex < assets.Length && count > 0; assetIndex++)
                {
                    GameObject asset = assets[assetIndex];
                    if (asset == null) throw new InvalidOperationException("Missing asset: " + layer.roleId);
                    if (!measurements.TryGetValue(asset, out Bounds measured))
                    {
                        if (!PrefabRenderMetrics.TryMeasureAuthored(asset, out measured))
                            throw new InvalidOperationException("No visible mesh: " + asset.name);
                        measurements.Add(asset, measured);
                    }
                    bounds[assetIndex] = measured;
                    float footprint = Mathf.Max(measured.size.x, measured.size.z);
                    float minimum = layer.scaleMin, maximum = layer.scaleMax;
                    if (layer.useTierPresets && !DioramaSizing.TryGetScaleRange(measured.size.y, footprint, preset.size, out minimum, out maximum))
                    {
                        excludedAssets?.Add(layer.roleId + ": " + asset.name + " cannot meet both height and width limits.");
                        continue;
                    }
                    variants.Add(new DioramaPlan.Variant
                    {
                        sourceIndex = assetIndex, height = measured.size.y, footprint = footprint,
                        scaleMin = minimum, scaleMax = maximum, measured = layer.useTierPresets
                    });
                }
                if (count > 0 && variants.Count == 0)
                    throw new InvalidOperationException("No asset fits the limits for " + layer.roleId + ". Edit its palette or dimensions.");
                DioramaRule rule = layer.CopyForCount(count);
                if (rule.crossingLength > 0 && count > 0)
                {
                    if (variants.Count != 1) throw new InvalidOperationException("A bridge layer requires one measured bridge asset.");
                    rule.crossingLength = bounds[variants[0].sourceIndex].size.x;
                }
                result[i] = new PreparedBiomeLayer { Rule = rule, Assets = assets, Bounds = bounds, Variants = variants.ToArray() };
            }
            return result;
        }

        public static void ApplyPose(Transform instance, GameObject source, Bounds authoredBounds, DioramaRule rule, DioramaPlan.Prop pose)
        {
            Quaternion rotation = Quaternion.Euler(0, pose.yaw, 0) * Quaternion.Euler(0, 0, -pose.tilt);
            instance.localRotation = rotation * source.transform.localRotation;
            instance.localScale = source.transform.localScale * pose.scale;
            // The transformed AABB is conservative for tilt, and ensures the measured bottom meets the surface.
            Bounds rotated = RotateBounds(authoredBounds, rotation, pose.scale);
            Vector3 correction = rule.preserveAuthoredPivot
                ? new Vector3(0, rotated.min.y, 0)
                : new Vector3(rotated.center.x, rotated.min.y, rotated.center.z);
            instance.localPosition = new Vector3(pose.x, pose.y, pose.z) - correction;
        }

        private static Bounds RotateBounds(Bounds bounds, Quaternion rotation, float scale)
        {
            var result = new Bounds(rotation * bounds.center * scale, Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                result.Encapsulate(rotation * point * scale);
            }
            return result;
        }
    }
}
