using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LumiWorld.Acs.Editor
{
    public sealed class LowpolyNaturePrefabBuilder : EditorWindow
    {
        private const string PrefabsFolder = "Assets/Acs/Art/Flower/Prefabs";
        private static readonly Regex ModelName = new Regex(@"^model_(\d+)\.(\d+)\.fbx$", RegexOptions.CultureInvariant);

        private enum State { Ready, Existing, MaterialIssue, ModelIssue }

        private sealed class Entry
        {
            public string ModelPath;
            public string PrefabPath;
            public string Name;
            public State State;
            public string Detail;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private Vector2 scroll;

        [MenuItem("Tools/LumiWorld/Lowpoly Nature/Create Prefabs")]
        private static void Open()
        {
            GetWindow<LowpolyNaturePrefabBuilder>("Lowpoly Nature Prefabs");
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Tạo prefab cho Lowpoly nature pack trong Flower/Prefabs. Mỗi prefab có object cha ở gốc " +
                "và FBX con tên Visual để chỉnh vị trí chân cây sau này. Prefab đã có sẽ không bị ghi đè.",
                MessageType.Info);

            if (GUILayout.Button("Quét lại FBX và prefab")) Refresh();

            int ready = 0;
            int existing = 0;
            int issues = 0;
            foreach (Entry entry in entries)
            {
                if (entry.State == State.Ready) ready++;
                else if (entry.State == State.Existing) existing++;
                else issues++;
            }

            EditorGUILayout.LabelField(
                $"Sẵn sàng: {ready}  |  Đã có prefab: {existing}  |  Cần kiểm tra: {issues}",
                EditorStyles.boldLabel);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (Entry entry in entries)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(Path.GetFileName(entry.ModelPath), GUILayout.Width(150));
                EditorGUILayout.LabelField(entry.Name, GUILayout.Width(130));
                EditorGUILayout.LabelField(entry.Detail);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            using (new EditorGUI.DisabledScope(ready == 0))
            {
                if (GUILayout.Button($"Tạo {ready} prefab sẵn sàng")) CreateReady();
            }
        }

        private void Refresh()
        {
            entries.Clear();
            if (!AssetDatabase.IsValidFolder(LowpolyNatureMaterialRemapper.ModelsFolder) ||
                !AssetDatabase.IsValidFolder(PrefabsFolder)) return;

            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { LowpolyNatureMaterialRemapper.ModelsFolder }))
            {
                string modelPath = AssetDatabase.GUIDToAssetPath(guid);
                Match match = ModelName.Match(Path.GetFileName(modelPath));
                if (!match.Success || !int.TryParse(match.Groups[1].Value, out int group) ||
                    !int.TryParse(match.Groups[2].Value, out int number)) continue;

                string name = PrefabName(group, number);
                if (name == null) continue;

                var entry = new Entry
                {
                    ModelPath = modelPath,
                    PrefabPath = $"{PrefabsFolder}/{name}.prefab",
                    Name = name
                };
                entries.Add(entry);

                if (AssetDatabase.LoadAssetAtPath<GameObject>(entry.PrefabPath) != null)
                {
                    SetState(entry, State.Existing, "Đã tồn tại, sẽ giữ nguyên");
                    continue;
                }

                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (model == null || model.GetComponentsInChildren<Renderer>(true).Length == 0)
                {
                    SetState(entry, State.ModelIssue, "Không thấy mesh trong FBX");
                    continue;
                }

                string materialName = LowpolyNatureMaterialRemapper.MaterialForGroup(group);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(
                    $"{LowpolyNatureMaterialRemapper.MaterialsFolder}/{materialName}.mat");
                var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
                if (material == null || importer == null ||
                    !importer.GetExternalObjectMap().TryGetValue(
                        new AssetImporter.SourceAssetIdentifier(typeof(Material), materialName),
                        out UnityEngine.Object mapped) || mapped != material)
                {
                    SetState(entry, State.MaterialIssue, "FBX chưa remap đúng material");
                    continue;
                }

                SetState(entry, State.Ready, "Sẵn sàng");
            }

            entries.Sort((a, b) => string.CompareOrdinal(a.ModelPath, b.ModelPath));
            Repaint();
        }

        private static string PrefabName(int group, int number)
        {
            if (number < 1) return null;
            switch (group)
            {
                case 0: return $"Fern_{number}";
                case 1: return $"Flower_{number}";
                case 2: return $"Flower_{number + 10}";
                case 3: case 4: case 5: case 6: case 7: return $"Grass_{group - 2}";
                case 8: return $"Plant_{number}";
                case 9: return $"Sunflower_{number}";
                case 10: return $"Broadleaf_{number}";
                case 11: return $"WaterPlant_{number}";
                default: return null;
            }
        }

        private static void SetState(Entry entry, State state, string detail)
        {
            entry.State = state;
            entry.Detail = detail;
        }

        private void CreateReady()
        {
            int created = 0;
            Scene previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (Entry entry in entries)
                {
                    if (entry.State != State.Ready) continue;
                    GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(entry.ModelPath);
                    if (model == null || AssetDatabase.LoadAssetAtPath<GameObject>(entry.PrefabPath) != null)
                        continue;

                    var root = new GameObject(entry.Name);
                    try
                    {
                        SceneManager.MoveGameObjectToScene(root, previewScene);
                        var visual = PrefabUtility.InstantiatePrefab(model, previewScene) as GameObject;
                        if (visual == null) throw new InvalidOperationException($"Không tạo được Visual từ {entry.ModelPath}");
                        visual.name = "Visual";
                        visual.transform.SetParent(root.transform, false);

                        if (PrefabUtility.SaveAsPrefabAsset(root, entry.PrefabPath) == null)
                            throw new InvalidOperationException($"Không lưu được {entry.PrefabPath}");
                        created++;
                    }
                    finally
                    {
                        DestroyImmediate(root);
                    }
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
                AssetDatabase.SaveAssets();
                Refresh();
            }

            Debug.Log($"Lowpoly Nature: đã tạo {created} prefab trong {PrefabsFolder}.");
        }
    }
}
