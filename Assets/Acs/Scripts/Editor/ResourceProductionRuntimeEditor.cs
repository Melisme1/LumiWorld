using UnityEditor;
using UnityEngine;

namespace LumiWorld.Acs.Editor
{
    [CustomEditor(typeof(ResourceProductionRuntime))]
    public sealed class ResourceProductionRuntimeEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            bool changed = serializedObject.ApplyModifiedProperties();
            var runtime = (ResourceProductionRuntime)target;
            if (changed && Application.isPlaying) runtime.ApplySettingsChange();
            EditorGUILayout.HelpBox("Settings ở component này là balance chạy thực tế. Sản lượng tích lũy liên tục " +
                "vào buffer. Dùng nút Thu để chuyển vào Inventory; save/load world và offline ở bước 8. " +
                "Chỉnh ngoài Play rồi Ctrl+S để giữ cấu hình qua lần chạy sau.", MessageType.Info);
            string reason = "Thiếu Settings.";
            bool valid = runtime.Settings != null && runtime.Settings.TryValidate(out reason);
            EditorGUILayout.HelpBox(reason, valid ? MessageType.Info : MessageType.Warning);
            if (valid)
            {
                EditorGUILayout.LabelField("T1 base / phút", runtime.Settings.Tier1UnitsPerMinute.ToString("0.###"));
                EditorGUILayout.LabelField("T2 base / phút", runtime.Settings.Tier2UnitsPerMinute.ToString("0.###"));
            }
            if (!Application.isPlaying) return;
            EditorGUILayout.LabelField("Trạng thái", runtime.Status);
            if (GUILayout.Button("Settle Production Now")) runtime.SettleNow();
            if (GUILayout.Button("Log Production Summary")) runtime.LogSummary();
            if (GUILayout.Button("Thu tất cả vào Inventory")) { runtime.CollectAll(); return; }
            if (runtime.LastCollection != null)
            {
                var receipt = runtime.LastCollection;
                EditorGUILayout.HelpBox(receipt.Message + " Nhận " + receipt.TotalTransferred +
                    "; còn chờ " + receipt.TotalRemaining + ".", receipt.TotalTransferred > 0 ? MessageType.Info : MessageType.Warning);
                foreach (var item in receipt.Items)
                    EditorGUILayout.LabelField(item.ResourceName, "+" + item.Transferred + "; còn " + item.Remaining);
            }
            foreach (var state in runtime.States)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(state.isRecovery ? "Recovery buffer" : "Habitat", state.habitatId);
                using (new EditorGUI.DisabledScope(!state.buffers.Exists(b => b.amount > 0)))
                    if (GUILayout.Button(state.isRecovery ? "Thu recovery buffer" : "Thu toàn bộ habitat này"))
                    { runtime.CollectHabitat(state.habitatId); return; }
                foreach (var rate in state.rates)
                {
                    EditorGUILayout.LabelField(rate.tier + " Slot " + (rate.index + 1), rate.unitsPerMinute.ToString("0.###") + "/phút");
                    EditorGUILayout.LabelField("Tài nguyên", DisplayName(runtime, rate.resourceId));
                    if (!string.IsNullOrEmpty(rate.resourceId)) EditorGUILayout.LabelField("Điều kiện", rate.reason);
                }
                if (state.buffers.Count == 0) EditorGUILayout.LabelField("Buffer", "Chưa có sản lượng.");
                foreach (var buffer in state.buffers)
                {
                    int cap = runtime.Settings == null ? 0 : buffer.tier == ResourceTier.Tier1 ?
                        runtime.Settings.tier1BufferCap : runtime.Settings.tier2BufferCap;
                    EditorGUILayout.LabelField(DisplayName(runtime, buffer.resourceId), buffer.amount + " / " + cap +
                        " (phần lẻ " + buffer.fractionalCarry.ToString("0.###") + ")");
                    if (buffer.amount >= cap) EditorGUILayout.LabelField("Buffer", "Đầy: giữ hàng, bỏ sản lượng mới vượt cap.");
                    using (new EditorGUI.DisabledScope(buffer.amount <= 0))
                        if (GUILayout.Button("Thu " + DisplayName(runtime, buffer.resourceId)))
                        { runtime.CollectResource(state.habitatId, buffer.resourceId); return; }
                }
            }
        }

        private static string DisplayName(ResourceProductionRuntime runtime, string id)
        {
            var definition = HabitatEligibilityService.FindDefinition(runtime.HabitatManager != null ? runtime.HabitatManager.Catalog : null, id);
            return definition != null && definition.resource != null ? definition.resource.resourceName :
                string.IsNullOrEmpty(id) ? "Chưa chọn" : id;
        }
    }
}
