using TMPro;
using UnityEngine;

namespace LumiWorld.Acs
{
    [CreateAssetMenu(menuName = "LumiWorld/UI/Resource Inventory Skin")]
    public sealed class ResourceInventoryUISkin : ScriptableObject
    {
        public GameObject uiPrefab;
        public TMP_FontAsset font;
        public Sprite panelHabitat, windowMain, surfaceInset, buttonPrimary, buttonSecondary;
        public Sprite slotResource, slotSelected, slotLocked, buttonIcon, iconWarehouse, iconClose, iconLock;
        public Color ivory = new Color32(247, 242, 232, 255);
        public Color sage = new Color32(120, 147, 109, 255);
        public Color forest = new Color32(67, 91, 69, 255);
        public Color gold = new Color32(198, 174, 120, 255);
        public Color muted = new Color32(115, 115, 100, 255);
        public float panelPPU = 2.25f, windowPPU = 2.5f, insetPPU = 4f, buttonPPU = 8f;
    }
}
