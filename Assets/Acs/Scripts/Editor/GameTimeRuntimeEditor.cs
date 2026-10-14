using UnityEditor;
using UnityEngine;
using LumiWorld.Acs.TimeSystem;

namespace LumiWorld.Acs.Editor
{
    [CustomEditor(typeof(GameTimeRuntime))]
    public sealed class GameTimeRuntimeEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Pause dừng đồng hồ gameplay và timeScale. Mất focus vẫn chạy. " +
                "Clock UTC dùng cho save; không dùng trực tiếp để vượt qua pause. UI vẫn hoạt động để Resume.", MessageType.Info);
            if (!Application.isPlaying) return;
            var runtime = (GameTimeRuntime)target;
            var now = runtime.Clock.Read();
            EditorGUILayout.LabelField("Trạng thái", now.IsPaused ? "Đang pause" : "Đang chạy");
            EditorGUILayout.LabelField("Gameplay seconds", now.GameSeconds.ToString("0.00"));
            EditorGUILayout.LabelField("UTC seconds", now.UtcSeconds.ToString("0.000"));
            EditorGUILayout.LabelField("Timers chung", runtime.Timers.Count.ToString());
            if (GUILayout.Button(now.IsPaused ? "Resume Gameplay" : "Pause Gameplay")) runtime.TogglePause();
        }
    }
}
