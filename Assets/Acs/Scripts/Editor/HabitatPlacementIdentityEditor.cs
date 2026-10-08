using UnityEditor;
using UnityEngine;

namespace LumiWorld.Acs.Editor
{
    [CustomEditor(typeof(HabitatPlacementIdentity))]
    public sealed class HabitatPlacementIdentityEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                DrawPropertiesExcluding(serializedObject, "m_Script", "qualityRank");
            var identity = (HabitatPlacementIdentity)target;
            EditorGUI.BeginChangeCheck();
            var nextRank = (CreatureQualityRank)EditorGUILayout.EnumPopup("Quality Rank GDD", identity.QualityRank);
            if (EditorGUI.EndChangeCheck())
            {
                if (Application.isPlaying) identity.TrySetQualityRank(nextRank);
                else
                {
                    serializedObject.FindProperty("qualityRank").intValue = (int)nextRank;
                    serializedObject.ApplyModifiedProperties();
                }
            }
            EditorGUILayout.HelpBox("Rank I–III chỉ điều chỉnh contribution của production GDD. Sao và trait prototype " +
                "giữ riêng; care và lưu rank sẽ được nối sau.", MessageType.Info);
        }
    }
}
