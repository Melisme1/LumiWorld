using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Dựng scene WorldMap (bản đồ thế giới kiểu Ikariam) và thêm vào Build Settings, để không phải gắn
/// component bằng tay. Chạy lại được: scene cũ bị thay bằng bản mới.
/// </summary>
public static class WorldMapSceneTools
{
    private const string ScenePath = "Assets/Scenes/" + WorldMapController.SceneName + ".unity";

    [MenuItem("Tools/LumiWorld/Tạo scene WorldMap")]
    public static void CreateScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(20f, -10f, -10f);
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 10f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.11f, 0.53f, 0.78f);
        cameraObject.AddComponent<WorldMapCamera>();

        // Project chỉ bật Input System mới nên UI dùng InputSystemUIInputModule
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var mapObject = new GameObject("WorldMap");
        var controller = mapObject.AddComponent<WorldMapController>();

        // Có IslandArtSet trong project thì gán sẵn; không có thì controller dùng hình tạm
        string artGuid = AssetDatabase.FindAssets("t:IslandArtSet").FirstOrDefault();
        if (artGuid != null)
        {
            var art = AssetDatabase.LoadAssetAtPath<IslandArtSet>(AssetDatabase.GUIDToAssetPath(artGuid));
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("art").objectReferenceValue = art;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        Debug.Log("[LumiWorld World] Đã tạo " + ScenePath + " và thêm vào Build Settings.");
    }

    private static void AddToBuildSettings(string path)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Any(s => s.path == path)) return;

        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
