using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > LumiWorld > Set Up Login Scene
///
/// Creates Assets/Scenes/Login.unity and puts it in Build Settings right after
/// MainMenu. The game still starts at MainMenu; the menu opens the login screen
/// when a player is not logged in, and the login screen returns to the menu.
/// Safe to run more than once: an existing Login scene is left untouched.
///
/// In the Editor you can still press Play directly in MapBuilding without
/// logging in.
/// </summary>
public static class AuthSceneSetup
{
    private const string LoginScenePath = "Assets/Scenes/Login.unity";
    private const string GameScenePath = "Assets/Scenes/MapBuilding.unity";
    private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Tools/LumiWorld/Set Up Login Scene")]
    public static void SetUpLoginScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LoginScenePath) == null)
        {
            CreateLoginScene();
        }
        else
        {
            Debug.Log("AuthSceneSetup: " + LoginScenePath + " already exists, leaving it untouched.");
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null
            || AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath) == null)
        {
            Debug.LogError(
                "AuthSceneSetup: " + GameScenePath + " or " + MenuScenePath + " not found. " +
                "Update the scene paths in AuthSceneSetup.cs and the Game Scene Name on the AuthScreen component."
            );
            return;
        }

        PutScenesInBuildOrder();
        EditorSceneManager.OpenScene(LoginScenePath);

        Debug.Log("AuthSceneSetup: done. MainMenu is scene 0, Login is scene 1, MapBuilding is scene 2. Press Play to test.");
    }

    private static void CreateLoginScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // The UI covers the whole screen, but a camera still clears the
        // framebuffer so a build never shows garbage behind it.
        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";

        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(22, 48, 43, 255);

        new GameObject("AuthScreen").AddComponent<AuthScreen>();

        EditorSceneManager.SaveScene(scene, LoginScenePath);
        Debug.Log("AuthSceneSetup: created " + LoginScenePath);
    }

    private static void PutScenesInBuildOrder()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        // Game mở ở MainMenu (scene 0), màn đăng nhập ngay sau đó, rồi MapBuilding
        scenes.RemoveAll(s => s.path == LoginScenePath || s.path == GameScenePath || s.path == MenuScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(MenuScenePath, true));
        scenes.Insert(1, new EditorBuildSettingsScene(LoginScenePath, true));
        scenes.Insert(2, new EditorBuildSettingsScene(GameScenePath, true));

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
