using UnityEditor;

namespace LumiWorld.Acs.Editor
{
    /// <summary>
    /// Forwards the legacy Inventory UI setup menu item to LumiWorldUIProductionSetup.
    /// Old IvorySage/LumiWorld_Resource_Inventory_UI_Assets pack has been retired in favor of Production UI.
    /// </summary>
    public static class ResourceInventoryUISetup
    {
        public const string Vietnamese = LumiWorldUITheme.VietnameseChars;

        [MenuItem("Tools/LumiWorld/Resources/Setup Inventory UI Skin")]
        public static void Setup() => LumiWorldUIProductionSetup.Setup();
    }
}
