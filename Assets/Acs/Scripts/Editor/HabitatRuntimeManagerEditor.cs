using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LumiWorld.Acs.Editor
{
    [CustomEditor(typeof(HabitatRuntimeManager))]
    public sealed class HabitatRuntimeManagerEditor : UnityEditor.Editor
    {
        private readonly Dictionary<string, bool> expanded = new Dictionary<string, bool>();
        private readonly Dictionary<string, string> pending = new Dictionary<string, string>();
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "habitats", "diagnostics");
            serializedObject.ApplyModifiedProperties();
            var manager = (HabitatRuntimeManager)target;
            EditorGUILayout.Space();
            bool hasProduction = manager.GetComponent<ResourceProductionRuntime>() != null;
            EditorGUILayout.HelpBox(hasProduction ? "Chọn tài nguyên tại đây; rate và buffer xem trong Resource Production Runtime bên dưới. " +
                "Thay đổi Play Mode chưa save/load." : "Quản lý nhóm, cư dân, slot và lựa chọn. Thêm Resource Production Runtime " +
                "ở bước 6 để tích lũy tài nguyên mới.", MessageType.Info);
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Gán Catalog và World Generator, lưu scene rồi Play để kiểm tra Runtime Habitats.", MessageType.Info);
                return;
            }
            if (GUILayout.Button("Refresh Habitats")) manager.RefreshNow();
            if (GUILayout.Button("Log Habitat Summary")) manager.LogSummary();
            foreach (string message in manager.Diagnostics) EditorGUILayout.HelpBox(message, MessageType.Warning);
            EditorGUILayout.LabelField("Runtime Habitats", manager.Habitats.Count.ToString());
            foreach (var habitat in manager.Habitats) DrawHabitat(manager, habitat);
        }

        private void DrawHabitat(HabitatRuntimeManager manager, HabitatState habitat)
        {
            if (!expanded.TryGetValue(habitat.habitatId, out bool isOpen)) isOpen = true;
            isOpen = EditorGUILayout.Foldout(isOpen, habitat.affinity + " — " + habitat.HexCount + " hex — " +
                habitat.residents.Count + "/" + habitat.CreatureCapacity + " creature", true);
            expanded[habitat.habitatId] = isOpen;
            if (!isOpen) return;
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Habitat ID", habitat.habitatId);
            EditorGUILayout.LabelField("Nature ID", habitat.natureCardId);
            EditorGUILayout.LabelField("Island ID", habitat.islandId);
            foreach (var member in habitat.members) EditorGUILayout.LabelField("Hex", member.hex.ToString());
            foreach (var resident in habitat.residents)
                EditorGUILayout.LabelField(resident.speciesId + " / " + resident.role, "Home " + resident.homeHex);
            foreach (var slot in habitat.slots) DrawSlot(manager, habitat, slot);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        private void DrawSlot(HabitatRuntimeManager manager, HabitatState habitat, HabitatResourceSlot slot)
        {
            string label = slot.tier + " Slot " + (slot.index + 1);
            bool unlocked = HabitatEligibilityService.IsSlotUnlocked(habitat, slot.tier, slot.index);
            EditorGUILayout.LabelField(label, unlocked ? "Mở" : "Khóa");
            var selected = HabitatEligibilityService.FindDefinition(manager.Catalog, slot.selectedResourceId);
            string selectedName = string.IsNullOrEmpty(slot.selectedResourceId) ? "Chưa chọn" :
                selected != null && selected.resource != null ? selected.resource.resourceName : slot.selectedResourceId;
            EditorGUILayout.LabelField("Đang chọn", selectedName);
            var current = HabitatEligibilityService.EvaluateSelection(habitat, slot.tier, slot.index,
                slot.selectedResourceId, manager.Catalog);
            EditorGUILayout.HelpBox(current.Reason, current.IsEligible ? MessageType.Info : MessageType.Warning);
            if (!unlocked) return;

            var ids = new List<string> { string.Empty };
            var names = new List<string> { "— Bỏ lựa chọn —" };
            foreach (var definition in manager.Catalog.GetResources(habitat.affinity, slot.tier))
            {
                ids.Add(definition.ResourceId);
                var availability = HabitatEligibilityService.EvaluateSelection(habitat, slot.tier, slot.index,
                    definition.ResourceId, manager.Catalog);
                names.Add((definition.resource != null ? definition.resource.resourceName : definition.ResourceId) +
                    (availability.IsEligible ? "" : " [Thiếu điều kiện]"));
            }
            string key = habitat.habitatId + ":" + slot.tier + ":" + slot.index;
            if (!pending.TryGetValue(key, out string candidateId)) candidateId = slot.selectedResourceId;
            int candidateIndex = ids.IndexOf(candidateId);
            candidateIndex = EditorGUILayout.Popup("Chọn tài nguyên", Mathf.Max(0, candidateIndex), names.ToArray());
            candidateId = ids[candidateIndex];
            pending[key] = candidateId;
            var eligibility = HabitatEligibilityService.EvaluateSelection(habitat, slot.tier, slot.index, candidateId, manager.Catalog);
            bool canApply = string.IsNullOrEmpty(candidateId) || eligibility.IsEligible;
            if (!canApply) EditorGUILayout.HelpBox(eligibility.Reason, MessageType.Warning);
            using (new EditorGUI.DisabledScope(!canApply))
                if (GUILayout.Button("Apply " + label) &&
                    !manager.TrySelectResource(habitat.habitatId, slot.tier, slot.index, candidateId, out string reason))
                    Debug.LogWarning("[LumiWorld Habitat] " + reason, manager);
        }
    }
}
