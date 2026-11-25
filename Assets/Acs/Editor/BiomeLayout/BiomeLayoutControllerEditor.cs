#if UNITY_EDITOR
using LumiWorld.Acs;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LumiWorld.EditorTools
{
    [CustomEditor(typeof(BiomeLayoutController))]
    public sealed class BiomeLayoutControllerEditor : Editor
    {
        private readonly BiomeTierInspector tierInspector = new BiomeTierInspector();

        public override void OnInspectorGUI()
        {
            BiomeLayoutController controller = (BiomeLayoutController)target;

            DrawDefaultInspector();
            using (new EditorGUI.DisabledScope(Application.isPlaying))
                tierInspector.Draw(controller);
            if (EditorUtility.IsPersistent(controller) || Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Open a scene instance or Prefab Mode to edit this habitat.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(8);
            BiomeActivityZones zones = controller.GetComponent<BiomeActivityZones>();

            // Live ecological & activity zone summary
            int walkable = zones != null ? zones.WalkableSpots.Count : 0;
            int swimmable = zones != null ? zones.SwimmableSpots.Count : 0;
            int obstacles = zones != null ? zones.HardObstacles.Count : 0;

            EditorGUILayout.HelpBox(
                $"Cluster: {controller.ClusterSize} Hex  |  Seed: {controller.LayoutSeed}\n" +
                $"Props Placed: {controller.LastPlacedProps} (Locked: {controller.LastLockedProps})\n" +
                $"ActivityZones -> Walkable: {walkable}  |  Swimmable: {swimmable}  |  Obstacles: {obstacles}",
                MessageType.Info);

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Create 3 Seed Preview Options"))
                CreateSeedPreviews(controller);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Regenerate Layout", GUILayout.Height(28)))
                {
                    RegenerateWithPrefabLinks(controller, preserveLocked: false);
                }

                if (GUILayout.Button("Next Seed (+1)", GUILayout.Height(28)))
                {
                    Undo.RecordObject(controller, "Next Biome Layout Seed");
                    controller.LayoutSeed++;
                    EditorUtility.SetDirty(controller);
                    RegenerateWithPrefabLinks(controller, preserveLocked: false);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reroll Unlocked Only", GUILayout.Height(26)))
                {
                    Undo.RecordObject(controller, "Reroll Unlocked Biome Props");
                    controller.LayoutSeed++;
                    EditorUtility.SetDirty(controller);
                    RegenerateWithPrefabLinks(controller, preserveLocked: true);
                }


            }

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Lock All Props"))
                {
                    SetLockOnAllCurrentProps(controller, locked: true);
                }

                if (GUILayout.Button("Unlock All Props"))
                {
                    SetLockOnAllCurrentProps(controller, locked: false);
                }
            }

            if (zones != null)
            {
                string gizmoLabel;
                if (zones.HideGizmos)
                {
                    gizmoLabel = "ActivityZone Gizmos: OFF / Hidden (Click to Selected Only)";
                }
                else if (zones.DrawGizmosAlways)
                {
                    gizmoLabel = "ActivityZone Gizmos: Always ON (Click to Hide OFF)";
                }
                else
                {
                    gizmoLabel = "ActivityZone Gizmos: Selected Only (Click to Hide OFF)";
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(gizmoLabel))
                    {
                        Undo.RecordObject(zones, "Toggle ActivityZone Gizmos");
                        if (zones.HideGizmos)
                        {
                            zones.HideGizmos = false;
                            zones.DrawGizmosAlways = false;
                        }
                        else
                        {
                            zones.HideGizmos = true;
                        }
                        EditorUtility.SetDirty(zones);
                        SceneView.RepaintAll();
                    }

                    if (!zones.HideGizmos && GUILayout.Button(zones.DrawGizmosAlways ? "Selected Only" : "Always ON", GUILayout.Width(100)))
                    {
                        Undo.RecordObject(zones, "Switch ActivityZone Gizmo Mode");
                        zones.DrawGizmosAlways = !zones.DrawGizmosAlways;
                        EditorUtility.SetDirty(zones);
                        SceneView.RepaintAll();
                    }
                }
            }

            string bakeTargetPath = ResolveSourcePrefabPath(controller);
            if (!string.IsNullOrEmpty(bakeTargetPath))
            {
                EditorGUILayout.Space(4);
                if (GUILayout.Button("Bake Layout Changes Back to Source Prefab", GUILayout.Height(26)))
                {
                    var stage = PrefabStageUtility.GetPrefabStage(controller.gameObject);
                    BiomeLayoutGenerator.RefreshZones(controller);
                    bool saved;
                    if (stage != null && stage.prefabContentsRoot == controller.gameObject)
                    {
                        PrefabUtility.SaveAsPrefabAsset(controller.gameObject, stage.assetPath, out saved);
                    }
                    else
                    {
                        EnsureMutableRootForRegeneration(controller);
                        PrefabUtility.SaveAsPrefabAssetAndConnect(controller.gameObject, bakeTargetPath,
                            InteractionMode.UserAction, out saved);
                    }
                    if (saved) Debug.Log($"[BiomeLayout] Saved {bakeTargetPath}", controller);
                    else Debug.LogError($"[BiomeLayout] Could not save {bakeTargetPath}", controller);
                }
            }
        }

        public static BiomeLayoutBuildResult RegenerateWithPrefabLinks(
            BiomeLayoutController controller,
            bool preserveLocked)
        {
            if (controller == null) return default;
            if (controller.Profile == null)
            {
                Debug.LogWarning($"[BiomeLayout] '{controller.name}' has no BiomeLayoutProfile assigned.");
                return default;
            }

            // Validate dimensions and dependencies before unpacking the user's connected root.
            BiomeLayoutPreparation.Prepare(controller.Profile, controller.ClusterSize);

            // Unity forbids destroying child GameObjects inside a connected Prefab Instance.
            // Remember the source prefab path and unpack ONLY the outermost biome root (keeping all art props linked!).
            EnsureMutableRootForRegeneration(controller);

            Undo.RegisterFullObjectHierarchyUndo(controller.gameObject, "Regenerate Biome Layout");

            BiomeLayoutBuildResult result = controller.Regenerate(
                preserveLocked: preserveLocked,
                instantiator: InstantiateLinkedPrefab,
                editActions: new BiomeLayoutEditActions
                {
                    registerCreatedGroup = group => Undo.RegisterCreatedObjectUndo(group, "Create Biome Layer"),
                    destroyExistingGroup = Undo.DestroyObjectImmediate,
                    reparentPreserved = (prop, parent) => Undo.SetTransformParent(prop, parent, "Preserve Locked Biome Prop")
                });

            EditorUtility.SetDirty(controller);
            if (controller.ActivityZones != null)
            {
                EditorUtility.SetDirty(controller.ActivityZones);
            }
            SceneView.RepaintAll();
            return result;
        }

        private static string ResolveSourcePrefabPath(BiomeLayoutController controller)
        {
            if (controller == null) return "";
            var stage = PrefabStageUtility.GetPrefabStage(controller.gameObject);
            if (stage != null && stage.prefabContentsRoot == controller.gameObject) return stage.assetPath;
            if (PrefabUtility.IsPartOfPrefabInstance(controller.gameObject))
            {
                string livePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(controller.gameObject);
                if (!string.IsNullOrEmpty(livePath)) return livePath;
            }
            return controller.SourcePrefabAssetPath ?? "";
        }

        private static void CreateSeedPreviews(BiomeLayoutController source)
        {
            var created = new System.Collections.Generic.List<GameObject>();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Biome Seed Options");
            try
            {
                float step = BiomeSurfaceReader.Read(source.transform).width + 1.25f;
                for (int i = 1; i <= 3; i++)
                {
                    GameObject preview = Object.Instantiate(source.gameObject, source.transform.parent);
                    created.Add(preview);
                    preview.name = source.name + "_Option_" + i;
                    preview.transform.position = source.transform.TransformPoint(Vector3.right * step * i);
                    BiomeLayoutController controller = preview.GetComponent<BiomeLayoutController>();
                    controller.LayoutSeed = source.LayoutSeed + i;
                    controller.SourcePrefabAssetPath = "";
                    EnsureMutableRootForRegeneration(controller);
                    controller.SourcePrefabAssetPath = "";
                    controller.Regenerate(false, InstantiateLinkedPrefab);
                    Undo.RegisterCreatedObjectUndo(preview, "Create Biome Seed Option");
                }
                Undo.CollapseUndoOperations(undoGroup);
                Selection.objects = created.ToArray();
            }
            catch (System.Exception error)
            {
                foreach (GameObject preview in created) if (preview != null) Object.DestroyImmediate(preview);
                Debug.LogException(error);
            }
        }

        private static void EnsureMutableRootForRegeneration(BiomeLayoutController controller)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(controller.gameObject)) return;

            GameObject root = PrefabUtility.GetNearestPrefabInstanceRoot(controller.gameObject);
            if (root == null) return;

            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
            if (!string.IsNullOrEmpty(assetPath))
            {
                controller.SourcePrefabAssetPath = assetPath;
            }

            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.UserAction);
        }

        public static GameObject InstantiateLinkedPrefab(GameObject prefab, Transform parent)
        {
            if (prefab == null) return null;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (instance == null)
            {
                instance = Object.Instantiate(prefab, parent);
            }
            return instance;
        }

        private static void SetLockOnAllCurrentProps(BiomeLayoutController controller, bool locked)
        {
            Transform container = controller.PropsContainer;
            if (container == null) return;

            Undo.RegisterFullObjectHierarchyUndo(container.gameObject, locked ? "Lock All Biome Props" : "Unlock All Biome Props");

            for (int g = 0; g < container.childCount; g++)
            {
                Transform group = container.GetChild(g);
                for (int i = 0; i < group.childCount; i++)
                {
                    GameObject prop = group.GetChild(i).gameObject;
                    BiomePropLock existing = prop.GetComponent<BiomePropLock>();
                    if (locked && existing == null)
                    {
                        BiomePropLock added = Undo.AddComponent<BiomePropLock>(prop);
                        added.roleId = group.name;
                    }
                    else if (!locked && existing != null)
                    {
                        Undo.DestroyObjectImmediate(existing);
                    }
                }
            }

            EditorUtility.SetDirty(controller);
        }
    }
}
#endif
