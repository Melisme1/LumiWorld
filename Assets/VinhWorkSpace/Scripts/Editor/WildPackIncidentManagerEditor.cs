using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Inspector cho WildPackIncidentManager:
/// Hiển thị nút bấm lớn trực quan để Tester / Designer có thể kích hoạt
/// hoặc dọn dẹp sự cố thú hoang ngay trong Play Mode.
/// </summary>
[CustomEditor(typeof(WildPackIncidentManager))]
public class WildPackIncidentManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 1. Vẽ giao diện mặc định (Fields, Config, Timers)
        DrawDefaultInspector();

        EditorGUILayout.Space(14);

        WildPackIncidentManager manager = (WildPackIncidentManager)target;

        EditorGUILayout.LabelField("🎮 BẢNG ĐIỀU KHIỂN SỰ CỐ (DEBUG CONTROLS)", EditorStyles.boldLabel);

        if (Application.isPlaying)
        {
            int habitatCount = 0;
            if (LumiWorld.Acs.HabitatRuntimeManager.TryGetActive(out var hm) && hm.Habitats != null)
            {
                habitatCount = hm.Habitats.Count;
            }

            int incidentCount = manager.ActiveIncidents != null ? manager.ActiveIncidents.Count : 0;

            EditorGUILayout.HelpBox(
                $"• Cụm Biome / Habitat hiện có: {habitatCount}\n" +
                $"• Số đàn thú hoang đang hoạt động: {incidentCount}\n" +
                $"💡 Gợi ý: Click trực tiếp vào con Alpha trên bản đồ để mở Bảng Quan Sát & Tương Tác!",
                MessageType.Info
            );

            EditorGUILayout.Space(6);

            // Nút Spawn Thú Hoang (Màu vàng cam nổi bật)
            GUI.backgroundColor = new Color(1f, 0.6f, 0.15f);
            if (GUILayout.Button("⚡ SPAWN THÚ HOANG NGAY (TRIGGER INCIDENT)", GUILayout.Height(42)))
            {
                bool success = manager.TriggerNewIncident();
                if (!success)
                {
                    Debug.LogWarning("[Wild Incident] Không thể sinh sự cố! Kiểm tra console để xem lý do (Ví dụ cần tối thiểu 3 ô cùng loại để tạo cụm).");
                }
            }

            EditorGUILayout.Space(4);

            // Nút Giải Phóng (Màu xanh lá)
            GUI.backgroundColor = new Color(0.3f, 0.75f, 0.45f);
            if (GUILayout.Button("🛡️ GIẢI PHÓNG TOÀN BỘ ĐẢO (RESOLVE ALL)", GUILayout.Height(32)))
            {
                manager.ResolveAllIncidents();
            }

            GUI.backgroundColor = Color.white;
        }
        else
        {
            EditorGUILayout.HelpBox("▶️ Hãy bấm PLAY để kích hoạt các nút điều khiển kiểm thử sự cố.", MessageType.None);
        }
    }
}
