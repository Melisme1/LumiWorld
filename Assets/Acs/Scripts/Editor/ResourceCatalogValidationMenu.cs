using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LumiWorld.Acs.Editor
{
    public static class ResourceCatalogValidationMenu
    {
        private const string SelectedMenu = "Tools/LumiWorld/Resources/Validate Selected Catalog";
        private const string ReferenceMenu = "Tools/LumiWorld/Resources/Validate Selected Catalog Against GDD Reference";

        [MenuItem(SelectedMenu)]
        private static void ValidateSelected()
        {
            ValidateAndLog(Selection.activeObject as ResourceProductionCatalog);
        }

        [MenuItem(SelectedMenu, true)]
        private static bool CanValidateSelected()
        {
            return Selection.activeObject is ResourceProductionCatalog;
        }

        [MenuItem(ReferenceMenu)]
        private static void ValidateSelectedAgainstReference()
        {
            ValidateAndLog(Selection.activeObject as ResourceProductionCatalog, true);
        }

        [MenuItem(ReferenceMenu, true)]
        private static bool CanValidateSelectedAgainstReference()
        {
            return CanValidateSelected();
        }

        [MenuItem("Tools/LumiWorld/Resources/Validate All Catalogs")]
        private static void ValidateAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:ResourceProductionCatalog");
            if (guids.Length == 0) Debug.LogWarning("[LumiWorld Resources] Chưa có Resource Production Catalog.");
            foreach (string guid in guids)
                ValidateAndLog(AssetDatabase.LoadAssetAtPath<ResourceProductionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        private static void ValidateAndLog(ResourceProductionCatalog catalog, bool requireCompleteDesignReference = false)
        {
            ResourceCatalogValidationResult result = ResourceCatalogValidator.Validate(catalog, requireCompleteDesignReference);
            ValidateResourcePathsAndIds(catalog, result);
            foreach (string error in result.errors) Debug.LogError("[LumiWorld Resources] " + error, catalog);
            foreach (string warning in result.warnings) Debug.LogWarning("[LumiWorld Resources] " + warning, catalog);
            string summary = "[LumiWorld Resources] " + (catalog != null ? catalog.name : "Catalog") +
                ": " + result.errors.Count + " lỗi, " + result.warnings.Count + " cảnh báo.";
            if (result.IsValid) Debug.Log(summary + " Data hợp lệ. Validator chỉ kiểm tra dữ liệu.", catalog);
            else Debug.LogError(summary + " Sửa các lỗi phía trên rồi chạy validator lại.", catalog);
        }

        private static void ValidateResourcePathsAndIds(ResourceProductionCatalog catalog, ResourceCatalogValidationResult result)
        {
            var loadedIds = new Dictionary<string, string>();
            foreach (string guid in AssetDatabase.FindAssets("t:ResourceData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("/Resources/")) continue;
                ResourceData data = AssetDatabase.LoadAssetAtPath<ResourceData>(path);
                if (data == null || string.IsNullOrEmpty(data.resourceID)) continue;
                if (loadedIds.TryGetValue(data.resourceID, out string previousPath))
                    result.errors.Add("Trùng Resource ID trong Unity Resources: " + data.resourceID + " tại " + previousPath + " và " + path + ".");
                else loadedIds.Add(data.resourceID, path);
            }
            if (catalog == null || catalog.resources == null) return;
            foreach (ResourceProductionDefinition definition in catalog.resources)
            {
                if (definition == null || definition.resource == null) continue;
                string path = AssetDatabase.GetAssetPath(definition.resource);
                if (!path.StartsWith("Assets/", System.StringComparison.Ordinal) || !path.Contains("/Resources/"))
                    result.errors.Add(definition.ResourceId + ": đặt ResourceData trong một folder Resources thuộc Assets " +
                        "(ví dụ Assets/Acs/Data/Resources) để loader kho hiện có tìm thấy.");
            }
        }
    }
}
