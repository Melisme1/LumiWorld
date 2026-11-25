using System;
using System.Collections.Generic;
using System.Text;
using LumiWorld.Acs;
using UnityEditor;
using UnityEngine;

namespace LumiWorld.EditorTools
{
    /// <summary>Edits the selected profile inline; no generation or asset mutation during repaint.</summary>
    internal sealed class BiomeTierInspector
    {
        private readonly HashSet<string> expanded = new HashSet<string>();
        private int selectedTier;
        private string sizeReport;
        private Vector2 reportScroll;

        public void Draw(BiomeLayoutController controller)
        {
            BiomeLayoutProfile profile = controller.Profile;
            if (profile == null) return;
            bool isSingleHex = controller.ClusterSize == 1;
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(
                isSingleHex ? "Asset Presets — 1 Hex (Single Nature Tile)" : "Asset Presets — 3 / 6 / 10 Hex",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("These settings belong to the shared profile. Make an option copy to experiment independently. Changes take effect when you regenerate.", MessageType.Info);
            if (GUILayout.Button("Make Independent Profile Option")) DuplicateProfile(controller);
            if (profile.sizingVersion < WetIslandTierPresetSetup.Version)
            {
                EditorGUILayout.HelpBox("This profile still uses authored scale. Prepare metre-based presets before regenerating.", MessageType.Warning);
                if (GUILayout.Button("Prepare Measured Tier Presets"))
                {
                    try { WetIslandTierPresetSetup.Prepare(profile); }
                    catch (Exception error) { Debug.LogException(error); }
                }
                return;
            }

            if (isSingleHex)
            {
                selectedTier = 1;
            }
            else
            {
                if (selectedTier != 3 && selectedTier != 6 && selectedTier != 10) selectedTier = controller.ClusterSize;
                int tab = selectedTier == 3 ? 0 : selectedTier == 6 ? 1 : 2;
                tab = GUILayout.Toolbar(tab, new[] { "3 Hex", "6 Hex", "10 Hex" });
                selectedTier = tab == 0 ? 3 : tab == 1 ? 6 : 10;
                if (selectedTier != controller.ClusterSize)
                    EditorGUILayout.HelpBox($"Editing {selectedTier}-hex settings. This object's surface is {controller.ClusterSize} hex; Generate uses its own tier.", MessageType.Info);
            }

            var serialized = new SerializedObject(profile);
            serialized.Update();
            SerializedProperty layers = serialized.FindProperty("layers");
            string tierField = selectedTier == 1 ? "hex1" : selectedTier == 3 ? "hex3" : selectedTier == 6 ? "hex6" : "hex10";
            for (int index = 0; index < layers.arraySize; index++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(index);
                string role = layer.FindPropertyRelative("roleId").stringValue;
                SerializedProperty tier = layer.FindPropertyRelative(tierField);
                string title = role + " — " + tier.FindPropertyRelative("count").intValue + " props";
                bool open = EditorGUILayout.Foldout(expanded.Contains(role), title, true);
                if (open) expanded.Add(role); else expanded.Remove(role);
                if (!open) continue;
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(tier.FindPropertyRelative("enabled"));
                EditorGUILayout.PropertyField(tier.FindPropertyRelative("count"));
                EditorGUILayout.PropertyField(tier.FindPropertyRelative("assets"), new GUIContent("Asset Options"), true);
                EditorGUILayout.PropertyField(tier.FindPropertyRelative("size"), new GUIContent("Dimensions (metres)"), true);
                SerializedProperty advanced = layer.FindPropertyRelative("guides");
                advanced.isExpanded = EditorGUILayout.Foldout(advanced.isExpanded, "Placement (shared across tiers)", true);
                if (advanced.isExpanded)
                {
                    foreach (string field in new[] { "guides", "clusterAroundParentRole", "radiusMin", "radiusMax", "spread", "spacing",
                        "allowedVisualOverlap", "obstacleRadius", "solidFootprintFraction", "ground", "waterHeight", "preserveAuthoredPivot",
                        "facing", "yaw", "tiltMin", "tiltMax", "yOffset", "parentHeightFraction", "hangFromParent" })
                        EditorGUILayout.PropertyField(layer.FindPropertyRelative(field), true);
                }
                EditorGUI.indentLevel--;
            }
            if (serialized.ApplyModifiedProperties()) sizeReport = null;
            if (GUILayout.Button("Check Asset Sizes for This Tier")) sizeReport = BuildReport(profile, selectedTier);
            if (!string.IsNullOrEmpty(sizeReport))
            {
                reportScroll = EditorGUILayout.BeginScrollView(reportScroll, GUILayout.MaxHeight(240));
                EditorGUILayout.SelectableLabel(sizeReport, EditorStyles.wordWrappedLabel,
                    GUILayout.MinHeight(EditorStyles.wordWrappedLabel.CalcHeight(new GUIContent(sizeReport), EditorGUIUtility.currentViewWidth - 60)));
                EditorGUILayout.EndScrollView();
            }
        }

        private static string BuildReport(BiomeLayoutProfile profile, int tier)
        {
            var text = new StringBuilder();
            try
            {
                var excluded = new List<string>();
                foreach (PreparedBiomeLayer layer in BiomeLayoutPreparation.Prepare(profile, tier, excluded))
                    foreach (DioramaPlan.Variant variant in layer.Variants)
                        text.AppendLine($"{layer.Rule.roleId} / {layer.Assets[variant.sourceIndex].name}\n" +
                            $"  Source H {variant.height:F2}, W {variant.footprint:F2} m → " +
                            $"H {variant.height * variant.scaleMin:F2}–{variant.height * variant.scaleMax:F2}, " +
                            $"W {variant.footprint * variant.scaleMin:F2}–{variant.footprint * variant.scaleMax:F2} m");
                foreach (string issue in excluded) text.AppendLine("Excluded: " + issue);
            }
            catch (Exception error) { text.AppendLine(error.Message); }
            return text.ToString();
        }

        private static void DuplicateProfile(BiomeLayoutController controller)
        {
            string sourcePath = AssetDatabase.GetAssetPath(controller.Profile);
            string directory = System.IO.Path.GetDirectoryName(sourcePath).Replace('\\', '/');
            string path = AssetDatabase.GenerateUniqueAssetPath(directory + "/" + controller.Profile.name + "_Option.asset");
            BiomeLayoutProfile copy = UnityEngine.Object.Instantiate(controller.Profile);
            copy.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(copy, path);
            Undo.RecordObject(controller, "Assign Independent Biome Profile");
            controller.Profile = copy;
            if (PrefabUtility.IsPartOfPrefabInstance(controller)) PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            EditorUtility.SetDirty(controller);
            EditorGUIUtility.PingObject(copy);
        }
    }
}
