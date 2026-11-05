using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace LumiWorld.Acs.Editor
{
    public sealed class LowpolyNatureMaterialRemapper : EditorWindow
    {
        internal const string ModelsFolder = "Assets/Acs/Art/Flower/Models";
        internal const string MaterialsFolder = "Assets/Acs/Art/Flower/Materials";

        private static readonly Regex ModelName = new Regex(@"^model_(\d+)\.\d+\.fbx$", RegexOptions.CultureInvariant);
        private static readonly string[] MaterialByGroup =
        {
            "M_Fern", "M_Flowers_A", "M_Flowers_B", "M_Grass_Test",
            "M_Grass_Test", "M_Grass_Test", "M_Grass_Test", "M_Grass_Test",
            "M_Plants", "M_Sunflower", "M_Broadleaf", "M_WaterPlants"
        };

        internal static string MaterialForGroup(int group)
        {
            return group >= 0 && group < MaterialByGroup.Length ? MaterialByGroup[group] : null;
        }

        private enum State { Ready, AlreadyMapped, MissingMaterial, Conflict }

        private sealed class Entry
        {
            public string Path;
            public string MaterialName;
            public Material Material;
            public State State;
            public string Detail;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private Vector2 scroll;

        [MenuItem("Tools/LumiWorld/Lowpoly Nature/Remap FBX Materials")]
        private static void Open()
        {
            GetWindow<LowpolyNatureMaterialRemapper>("Lowpoly Nature Materials");
        }

        public static void ApplyAvailableFromCommandLine()
        {
            var window = CreateInstance<LowpolyNatureMaterialRemapper>();
            try
            {
                window.Refresh();
                window.ApplyReady();
            }
            finally
            {
                DestroyImmediate(window);
            }
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Chỉ kiểm tra FBX trong Flower/Models. Tên material trong FBX phải khớp nhóm model_0–model_11 " +
                "và file .mat cùng tên phải có trong Flower/Materials. Material đã gán sẽ được giữ nguyên.",
                MessageType.Info);

            if (GUILayout.Button("Quét lại FBX và material")) Refresh();

            int ready = 0;
            int mapped = 0;
            int missing = 0;
            int conflicts = 0;
            foreach (Entry entry in entries)
            {
                switch (entry.State)
                {
                    case State.Ready: ready++; break;
                    case State.AlreadyMapped: mapped++; break;
                    case State.MissingMaterial: missing++; break;
                    case State.Conflict: conflicts++; break;
                }
            }

            EditorGUILayout.LabelField(
                $"Sẵn sàng: {ready}  |  Đã ghép: {mapped}  |  Thiếu material: {missing}  |  Cần kiểm tra: {conflicts}",
                EditorStyles.boldLabel);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (Entry entry in entries)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(Path.GetFileName(entry.Path), GUILayout.Width(145));
                EditorGUILayout.LabelField(entry.MaterialName, GUILayout.Width(140));
                EditorGUILayout.LabelField(entry.Detail);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            using (new EditorGUI.DisabledScope(ready == 0))
            {
                if (GUILayout.Button($"Áp dụng cho {ready} FBX sẵn sàng")) ApplyReady();
            }
        }

        private void Refresh()
        {
            entries.Clear();
            if (!AssetDatabase.IsValidFolder(ModelsFolder)) return;

            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Match match = ModelName.Match(Path.GetFileName(path));
                if (!match.Success || !int.TryParse(match.Groups[1].Value, out int group) ||
                    group < 0 || group >= MaterialByGroup.Length) continue;

                string materialName = MaterialByGroup[group];
                Material material = AssetDatabase.LoadAssetAtPath<Material>(
                    $"{MaterialsFolder}/{materialName}.mat");
                var entry = new Entry { Path = path, MaterialName = materialName, Material = material };
                entries.Add(entry);

                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
                {
                    SetState(entry, State.Conflict, "Không đọc được Model Importer");
                    continue;
                }

                var sourceId = new AssetImporter.SourceAssetIdentifier(typeof(Material), materialName);
                var remaps = importer.GetExternalObjectMap();
                if (remaps.TryGetValue(sourceId, out UnityEngine.Object existing) && existing != null)
                {
                    SetState(entry, existing == material ? State.AlreadyMapped : State.Conflict,
                        existing == material ? "Đã ghép đúng" : $"Đang ghép với {existing.name}");
                    continue;
                }

                if (material == null)
                {
                    SetState(entry, State.MissingMaterial, "Chưa có file .mat cùng tên");
                    continue;
                }

                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var sourceNames = new HashSet<string>(StringComparer.Ordinal);
                if (model != null)
                {
                    foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                    foreach (Material source in renderer.sharedMaterials)
                        if (source != null) sourceNames.Add(source.name);
                }

                if (sourceNames.Count != 1 || !sourceNames.Contains(materialName))
                {
                    SetState(entry, State.Conflict,
                        sourceNames.Count == 0 ? "Không thấy material trong FBX" :
                        "Tên material FBX khác: " + string.Join(", ", sourceNames));
                    continue;
                }

                SetState(entry, State.Ready, "Khớp tên, sẵn sàng ghép");
            }

            entries.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
            Repaint();
        }

        private static void SetState(Entry entry, State state, string detail)
        {
            entry.State = state;
            entry.Detail = detail;
        }

        private void ApplyReady()
        {
            int applied = 0;
            try
            {
                foreach (Entry entry in entries)
                {
                    if (entry.State != State.Ready) continue;
                    if (!(AssetImporter.GetAtPath(entry.Path) is ModelImporter importer)) continue;

                    importer.AddRemap(
                        new AssetImporter.SourceAssetIdentifier(typeof(Material), entry.MaterialName),
                        entry.Material);
                    importer.SaveAndReimport();
                    applied++;
                }
            }
            finally
            {
                Refresh();
            }

            Debug.Log($"Lowpoly Nature: đã ghép material cho {applied} FBX. Xem cửa sổ để biết file còn thiếu hoặc cần kiểm tra.");
        }
    }
}
