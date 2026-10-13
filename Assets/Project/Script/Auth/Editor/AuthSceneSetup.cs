using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > LumiWorld > Set Up Login Scene
///
/// Creates Assets/Scenes/Login.unity and puts it first in Build Settings, with
/// MapBuilding right after it, so a built game opens on the login screen.
/// Safe to run more than once: an existing Login scene is left untouched.
///
/// In the Editor you can still press Play directly in MapBuilding without
/// logging in — only builds start at the login screen.
/// </summary>
public static class AuthSceneSetup
{
    private const string LoginScenePath = "Assets/Scenes/Login.unity";
    private const string GameScenePath = "Assets/Scenes/MapBuilding.unity";

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

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
        {
            Debug.LogError(
                "AuthSceneSetup: " + GameScenePath + " not found. " +
                "Update GameScenePath in AuthSceneSetup.cs and the Game Scene Name on the AuthScreen component."
            );
            return;
        }

        PutScenesInBuildOrder();
        EditorSceneManager.OpenScene(LoginScenePath);

        Debug.Log("AuthSceneSetup: done. Login is scene 0, MapBuilding is scene 1. Press Play to test.");
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

        scenes.RemoveAll(s => s.path == LoginScenePath || s.path == GameScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(LoginScenePath, true));
        scenes.Insert(1, new EditorBuildSettingsScene(GameScenePath, true));

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
