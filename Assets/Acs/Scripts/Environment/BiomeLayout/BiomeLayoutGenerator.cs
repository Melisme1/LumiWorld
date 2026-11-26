using System;
using System.Collections.Generic;
using UnityEngine;
using static LumiWorld.Acs.DioramaPlan;

namespace LumiWorld.Acs
{
    public struct BiomeLayoutBuildResult
    {
        public int placedPropCount;
        public int preservedLockedCount;
    }

    public static class BiomeLayoutGenerator
    {
        public static void RefreshZones(BiomeLayoutController controller)
        {
            var props = new List<Prop>();
            foreach (Transform group in controller.PropsContainer)
                foreach (Transform instance in group)
                    props.Add(ReadCurrent(controller.transform, instance, controller.Profile));
            BiomeZoneData zones = BiomeActivityZoneBuilder.Build(controller.Profile, BiomeSurfaceReader.Read(controller.transform), props);
            BiomeActivityZoneBuilder.Apply(controller.ActivityZones, controller.ClusterSize, controller.Profile, zones);
        }

        public static BiomeLayoutBuildResult Generate(Transform biomeRoot, Transform propsContainer, BiomeLayoutProfile profile,
            int clusterSize, int seed, bool preserveLocked, BiomeActivityZones activityZones,
            Func<GameObject, Transform, GameObject> instantiator = null, BiomeLayoutEditActions editActions = null)
        {
            Surface surface = BiomeSurfaceReader.Read(biomeRoot);
            var excluded = new List<string>();
            PreparedBiomeLayer[] layers = BiomeLayoutPreparation.Prepare(profile, clusterSize, excluded);
            var locked = new List<Prop>();
            var preserved = new List<Transform>();
            if (preserveLocked)
                foreach (Transform group in propsContainer)
                    foreach (Transform instance in group)
                        if (instance.GetComponent<BiomePropLock>() != null)
                        {
                            locked.Add(ReadCurrent(biomeRoot, instance, profile));
                            preserved.Add(instance);
                        }
            List<Prop> poses = Place(surface, Array.ConvertAll(layers, layer => layer.Rule),
                Array.ConvertAll(layers, layer => layer.Variants), clusterSize, seed, locked);
            // All geometry/planning/zone failures happen before existing objects are touched.
            BiomeZoneData zones = BiomeActivityZoneBuilder.Build(profile, surface, poses);
            var oldGroups = new List<GameObject>();
            foreach (Transform group in propsContainer) oldGroups.Add(group.gameObject);
            var staging = new GameObject("Diorama staging");
            staging.SetActive(false);
            staging.transform.SetParent(propsContainer, false);
            var groups = new Dictionary<string, Transform>();
            try
            {
                foreach (PreparedBiomeLayer layer in layers)
                    AddGroup(layer.Rule.roleId, staging.transform, groups);
                for (int i = locked.Count; i < poses.Count; i++)
                {
                    Prop pose = poses[i];
                    PreparedBiomeLayer layer = Array.Find(layers, candidate => candidate.Rule.roleId == pose.role);
                    GameObject source = layer.Assets[pose.variant];
                    Transform parent = groups[pose.role];
                    GameObject instance = instantiator == null ? UnityEngine.Object.Instantiate(source, parent) : instantiator(source, parent);
                    if (instance == null) throw new InvalidOperationException("Could not instantiate " + source.name);
                    instance.name = source.name + "_" + (pose.slot + 1).ToString("D2");
                    BiomeLayoutPreparation.ApplyPose(instance.transform, source, layer.Bounds[pose.variant], layer.Rule, pose);
                }

                foreach (Prop pose in locked)
                    if (!groups.ContainsKey(pose.role)) AddGroup(pose.role, staging.transform, groups);
                while (staging.transform.childCount > 0)
                {
                    Transform group = staging.transform.GetChild(0);
                    group.SetParent(propsContainer, false);
                    editActions?.registerCreatedGroup?.Invoke(group.gameObject);
                }

                // Record new groups before reparenting locks, so Undo restores locks before removing the groups.
                for (int i = 0; i < preserved.Count; i++)
                {
                    Prop pose = locked[i];
                    BiomePropLock tag = preserved[i].GetComponent<BiomePropLock>();
                    tag.roleId = pose.role;
                    tag.slot = pose.slot;
                    if (editActions?.reparentPreserved != null) editActions.reparentPreserved(preserved[i], groups[pose.role]);
                    else preserved[i].SetParent(groups[pose.role], true);
                }
                foreach (GameObject oldGroup in oldGroups)
                    if (editActions?.destroyExistingGroup != null) editActions.destroyExistingGroup(oldGroup);
                    else Remove(oldGroup);
                BiomeActivityZoneBuilder.Apply(activityZones, clusterSize, profile, zones);
            }
            finally { Remove(staging); }

            if (excluded.Count > 0)
                Debug.LogWarning("[BiomeLayout] Skipped incompatible assets:\n" + string.Join("\n", excluded), biomeRoot);
            return new BiomeLayoutBuildResult { placedPropCount = poses.Count, preservedLockedCount = locked.Count };
        }

        private static void AddGroup(string role, Transform parent, Dictionary<string, Transform> groups)
        {
            var group = new GameObject(role).transform;
            group.SetParent(parent, false);
            groups.Add(role, group);
        }

        private static Prop ReadCurrent(Transform root, Transform instance, BiomeLayoutProfile profile)
        {
            BiomePropLock tag = instance.GetComponent<BiomePropLock>();
            string role = tag != null && !string.IsNullOrEmpty(tag.roleId) ? tag.roleId : instance.parent.name;
            BiomePropLayer rule = Array.Find(profile.layers, layer => layer.roleId == role);
            Vector3 point = root.InverseTransformPoint(instance.position);
            float visualRadius = 0, height = 0;
            if (PrefabRenderMetrics.TryMeasureRelative(instance.gameObject, root, out Bounds bounds))
            {
                point = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                visualRadius = Mathf.Max(bounds.size.x, bounds.size.z) * .5f;
                height = bounds.size.y;
            }
            float radius = rule == null || rule.obstacleRadius <= 0 ? 0
                : rule.useTierPresets ? visualRadius * rule.solidFootprintFraction : rule.obstacleRadius;
            return new Prop
            {
                role = role, slot = tag != null && tag.slot >= 0 ? tag.slot : ReadSlot(instance),
                x = point.x, y = point.y, z = point.z, radius = radius, visualRadius = visualRadius, height = height
            };
        }

        private static int ReadSlot(Transform instance)
        {
            int split = instance.name.LastIndexOf('_');
            return split >= 0 && int.TryParse(instance.name.Substring(split + 1), out int index) ? index - 1 : instance.GetSiblingIndex();
        }

        private static void Remove(GameObject gameObject)
        {
            if (Application.isPlaying)
            {
                gameObject.SetActive(false);
                UnityEngine.Object.Destroy(gameObject);
            }
            else UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }
}
