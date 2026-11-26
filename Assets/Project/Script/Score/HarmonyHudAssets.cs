using UnityEngine;

/// <summary>
/// Runtime resource manifest for the Live 3D Harmony HUD crystals and additive FX textures.
/// Auto-populated by LumiWorldUIProductionSetup into Resources/UI/Harmony/HarmonyHudAssets.asset.
/// </summary>
[CreateAssetMenu(fileName = "HarmonyHudAssets", menuName = "LumiWorld/UI/Harmony HUD Assets")]
public class HarmonyHudAssets : ScriptableObject
{
    public GameObject harmonyCorePrefab;
    public GameObject terrainChargePrefab;
    public Texture2D sparkleTexture;
    public Texture2D haloTexture;
}
