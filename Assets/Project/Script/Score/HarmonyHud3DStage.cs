using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Isolated off-world URP Live 3D studio stage for the Harmony HUD.
/// Renders HarmonyCore.fbx (with mouse-tracking parallax and orbiting specular glint)
/// and a single shared rotating TerrainCharge.fbx into transparent ARGB32 RenderTextures.
/// Uses short-range Point Lights far off-world so island lighting is completely unaffected.
/// </summary>
public sealed class HarmonyHud3DStage : MonoBehaviour
{
    private static readonly Vector3 CoreStageOrigin = new Vector3(-3500f, 1500f, -3500f);
    private static readonly Vector3 ChargeStageOrigin = new Vector3(-3540f, 1500f, -3500f);

    private RenderTexture coreRt;
    private RenderTexture chargeRt;

    private Camera coreCamera;
    private Camera chargeCamera;

    private Transform corePivot;
    private Transform coreModelHolder;
    private Light coreGlintLight;

    private Transform chargePivot;
    private Light chargeAccentLight;

    private Vector2 targetTilt;
    private Vector2 currentTilt;
    private bool isHovered;
    private bool isPlacementMode;
    private bool chargesVisible;

    private float pulseTimer;
    private float pulseDuration = 0.65f;
    private float pulseAngleAmp;
    private float chargeSpinAngle;
    private float glintAngle;

    public RenderTexture CoreTexture => coreRt;
    public RenderTexture ChargeTexture => chargeRt;
    public bool HasCoreModel => coreModelHolder != null;
    public bool HasChargeModel => chargePivot != null && chargePivot.childCount > 0;

    public Texture2D SparkleTexture { get; private set; }
    public Texture2D HaloTexture { get; private set; }

    public static HarmonyHud3DStage Create(Transform owner)
    {
        GameObject go = new GameObject("HarmonyHud3DStage");
        if (owner != null)
            go.transform.SetParent(owner, false);
        var stage = go.AddComponent<HarmonyHud3DStage>();
        stage.Initialize();
        return stage;
    }

    private void Initialize()
    {
        transform.position = CoreStageOrigin;

        HarmonyHudAssets manifest = Resources.Load<HarmonyHudAssets>("UI/Harmony/HarmonyHudAssets");
        GameObject corePrefab = manifest != null ? manifest.harmonyCorePrefab : null;
        GameObject chargePrefab = manifest != null ? manifest.terrainChargePrefab : null;
        SparkleTexture = manifest != null ? manifest.sparkleTexture : null;
        HaloTexture = manifest != null ? manifest.haloTexture : null;

#if UNITY_EDITOR
        if (corePrefab == null)
            corePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Acs/Art/UI/HarmonyCrystals/HarmonyCore.fbx");
        if (chargePrefab == null)
            chargePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Acs/Art/UI/HarmonyCrystals/TerrainCharge.fbx");
        if (SparkleTexture == null)
            SparkleTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Project/ParticalSystem/GabrielAguiarProductions/FreeQuickEffectsVol1/Textures/Flare00.PNG");
        if (HaloTexture == null)
            HaloTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Project/ParticalSystem/GabrielAguiarProductions/FreeQuickEffectsVol1/Textures/Circle01_v1.png");
#endif

        if (corePrefab != null)
            BuildCoreStage(corePrefab);

        if (chargePrefab != null)
            BuildChargeStage(chargePrefab);
    }

    private void BuildCoreStage(GameObject corePrefab)
    {
        coreRt = CreateTransparentRT(512, 512, "HarmonyCore_LiveRT");

        GameObject root = new GameObject("CoreStageRoot");
        root.transform.SetParent(transform, false);
        root.transform.position = CoreStageOrigin;

        coreCamera = CreateOrthoStudioCamera(root.transform, "CoreStudioCamera", coreRt, 1.0f, 4.0f);

        // 4 gentle raking Point Lights (outdoor scene sun + skybox already provide base fill,
        // so keep intensities subtle to preserve deep crystalline facets without blowing out to white)
        CreatePointLight(root.transform, "CoreKeyLight", new Vector3(-2.5f, 2.2f, 1.4f), new Color(0.95f, 0.98f, 1f), 3.4f, 10f);
        CreatePointLight(root.transform, "CoreGlacierFill", new Vector3(2.4f, -1.5f, 1.3f), new Color(0.35f, 0.76f, 0.96f), 2.2f, 10f);
        CreatePointLight(root.transform, "CoreRimLight", new Vector3(1.2f, 1.6f, -2.0f), new Color(0.74f, 1f, 0.94f), 2.8f, 10f);
        coreGlintLight = CreatePointLight(root.transform, "CoreGlintOrbiter", new Vector3(0.4f, 1.1f, 1.55f), new Color(0.94f, 0.99f, 1f), 4.5f, 10f);

        corePivot = new GameObject("CorePivot").transform;
        corePivot.SetParent(root.transform, false);
        corePivot.localPosition = Vector3.zero;

        GameObject inst = Instantiate(corePrefab, corePivot);
        inst.name = "HarmonyCoreMesh";
        StripNonVisualComponents(inst);
        NormalizeModelTransform(inst, targetMaxExtent: 1.62f, preferVerticalTall: false);
        TuneCrystalExposure(inst, isCore: true);
        coreModelHolder = inst.transform;
    }

    private void BuildChargeStage(GameObject chargePrefab)
    {
        chargeRt = CreateTransparentRT(160, 256, "TerrainCharge_LiveRT");

        GameObject root = new GameObject("ChargeStageRoot");
        root.transform.SetParent(transform, false);
        root.transform.position = ChargeStageOrigin;

        chargeCamera = CreateOrthoStudioCamera(root.transform, "ChargeStudioCamera", chargeRt, 1.0f, 4.0f);
        chargeCamera.enabled = false; // Enabled only when availableCharges > 0

        CreatePointLight(root.transform, "ChargeKeyLight", new Vector3(-2.1f, 2.0f, 1.4f), new Color(0.98f, 0.99f, 0.94f), 3.6f, 10f);
        CreatePointLight(root.transform, "ChargeEmeraldFill", new Vector3(2.0f, -1.0f, 1.4f), new Color(0.40f, 0.92f, 0.62f), 2.4f, 10f);
        chargeAccentLight = CreatePointLight(root.transform, "ChargeRimLight", new Vector3(0.2f, 1.6f, -1.6f), new Color(0.82f, 1f, 0.72f), 2.4f, 10f);

        chargePivot = new GameObject("ChargePivot").transform;
        chargePivot.SetParent(root.transform, false);
        chargePivot.localPosition = Vector3.zero;

        GameObject inst = Instantiate(chargePrefab, chargePivot);
        inst.name = "TerrainChargeMesh";
        StripNonVisualComponents(inst);
        NormalizeModelTransform(inst, targetMaxExtent: 1.68f, preferVerticalTall: true);
        TuneCrystalExposure(inst, isCore: false);
    }

    public void SetCoreHover(bool hovered, Vector2 normalizedPointerOffset)
    {
        isHovered = hovered;
        targetTilt = hovered
            ? new Vector2(Mathf.Clamp(normalizedPointerOffset.x, -1f, 1f), Mathf.Clamp(normalizedPointerOffset.y, -1f, 1f))
            : Vector2.zero;
    }

    public void SetChargesState(bool anyVisible, bool placementMode)
    {
        chargesVisible = anyVisible;
        isPlacementMode = placementMode;
        if (chargeCamera != null)
            chargeCamera.enabled = anyVisible;
    }

    public void TriggerPulse(bool milestone)
    {
        pulseDuration = milestone ? 0.85f : 0.55f;
        pulseTimer = pulseDuration;
        pulseAngleAmp = milestone ? 34f : 18f;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float t = Time.unscaledTime;

        if (corePivot != null)
        {
            currentTilt = Vector2.Lerp(currentTilt, targetTilt, Mathf.Clamp01(dt * 9f));

            float idleYaw = Mathf.Sin(t * 0.85f) * 6.5f;
            float idlePitch = Mathf.Cos(t * 0.65f) * 3.5f;
            float idleFloat = Mathf.Sin(t * 1.45f) * 0.016f;

            float pulseYaw = 0f;
            float pulseScale = 1f;
            float pulseBoost = 0f;
            if (pulseTimer > 0f)
            {
                pulseTimer = Mathf.Max(0f, pulseTimer - dt);
                float u = 1f - (pulseTimer / Mathf.Max(0.01f, pulseDuration));
                float decay = Mathf.Exp(-u * 4.2f);
                pulseYaw = Mathf.Sin(u * Mathf.PI * 2.2f) * pulseAngleAmp * decay;
                pulseScale = 1f + Mathf.Sin(u * Mathf.PI) * 0.055f;
                pulseBoost = Mathf.Sin(u * Mathf.PI);
            }

            float yaw = idleYaw - currentTilt.x * 16f + pulseYaw;
            float pitch = idlePitch + currentTilt.y * 13f;
            float hoverScale = isHovered ? 1.035f : 1f;

            corePivot.localPosition = new Vector3(0f, idleFloat, 0f);
            corePivot.localRotation = Quaternion.Euler(pitch, yaw, 0f);
            corePivot.localScale = Vector3.one * (hoverScale * pulseScale);

            if (coreGlintLight != null)
            {
                float speed = isHovered ? 1.15f : 0.55f;
                glintAngle += dt * (speed + pulseBoost * 2.4f);
                float gx = Mathf.Cos(glintAngle) * 1.95f;
                float gy = Mathf.Sin(glintAngle * 1.35f) * 1.45f + 0.35f;
                float gz = 1.35f + Mathf.Sin(glintAngle * 0.7f) * 0.35f;
                coreGlintLight.transform.localPosition = new Vector3(gx, gy, gz);
                coreGlintLight.intensity = 4.5f + (isHovered ? 2.5f : 0f) + pulseBoost * 6.5f;
            }
        }

        if (chargePivot != null && chargesVisible)
        {
            float spinSpeed = isPlacementMode ? 68f : 26f;
            chargeSpinAngle = (chargeSpinAngle + dt * spinSpeed) % 360f;
            float bob = Mathf.Sin(t * (isPlacementMode ? 4.2f : 1.9f)) * 0.025f;
            chargePivot.localPosition = new Vector3(0f, bob, 0f);
            chargePivot.localRotation = Quaternion.Euler(7f, chargeSpinAngle, 0f);

            if (chargeAccentLight != null)
            {
                float pulse = isPlacementMode ? (0.5f + 0.5f * Mathf.Sin(t * 5.2f)) : 0f;
                chargeAccentLight.intensity = 2.4f + pulse * 3.8f;
            }
        }
    }

    private static void TuneCrystalExposure(GameObject root, bool isCore)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            // Instantiates runtime copies only; disk .mat assets remain untouched
            Material[] mats = renderers[i].materials;
            for (int m = 0; m < mats.Length; m++)
            {
                Material mat = mats[m];
                if (mat == null) continue;

                bool isTransparentRim = mat.HasProperty("_Surface") && mat.GetFloat("_Surface") > 0.5f;
                if (isCore)
                {
                    if (isTransparentRim)
                    {
                        if (mat.HasProperty("_BaseColor"))
                            mat.SetColor("_BaseColor", new Color(0.76f, 0.92f, 1f, 0.32f));
                        if (mat.HasProperty("_Smoothness"))
                            mat.SetFloat("_Smoothness", 0.86f);
                    }
                    else
                    {
                        if (mat.HasProperty("_BaseColor"))
                            mat.SetColor("_BaseColor", new Color(0.46f, 0.72f, 0.90f, 1f));
                        if (mat.HasProperty("_Smoothness"))
                            mat.SetFloat("_Smoothness", 0.80f);
                        if (mat.HasProperty("_BumpScale"))
                            mat.SetFloat("_BumpScale", 0.88f);
                    }
                }
                else
                {
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", new Color(0.66f, 0.84f, 0.70f, 1f));
                    if (mat.HasProperty("_Smoothness"))
                        mat.SetFloat("_Smoothness", 0.82f);
                }
            }
            renderers[i].materials = mats;
        }
    }

    private void OnDestroy()
    {
        if (coreCamera != null) coreCamera.targetTexture = null;
        if (chargeCamera != null) chargeCamera.targetTexture = null;

        if (coreRt != null)
        {
            coreRt.Release();
            Destroy(coreRt);
            coreRt = null;
        }
        if (chargeRt != null)
        {
            chargeRt.Release();
            Destroy(chargeRt);
            chargeRt = null;
        }
    }

    private static RenderTexture CreateTransparentRT(int width, int height, string rtName)
    {
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = rtName,
            antiAliasing = 4,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            useMipMap = false,
            autoGenerateMips = false
        };
        rt.Create();
        return rt;
    }

    private static Camera CreateOrthoStudioCamera(Transform parent, string camName, RenderTexture target, float orthoSize, float zDist)
    {
        GameObject camGo = new GameObject(camName, typeof(Camera));
        camGo.transform.SetParent(parent, false);
        camGo.transform.localPosition = new Vector3(0f, 0f, zDist);
        camGo.transform.LookAt(parent.position);

        Camera cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = orthoSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 12f;
        cam.cullingMask = -1;
        cam.targetTexture = target;
        cam.depth = -20f;

        var urp = camGo.GetComponent<UniversalAdditionalCameraData>();
        if (urp == null) urp = camGo.AddComponent<UniversalAdditionalCameraData>();
        urp.renderType = CameraRenderType.Base;
        urp.renderPostProcessing = false;
        urp.renderShadows = false;
        return cam;
    }

    private static Light CreatePointLight(Transform parent, string lightName, Vector3 localPos, Color color, float intensity, float range)
    {
        GameObject go = new GameObject(lightName, typeof(Light));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        Light l = go.GetComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    private static void StripNonVisualComponents(GameObject root)
    {
        foreach (var col in root.GetComponentsInChildren<Collider>(true))
            Destroy(col);
        foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
            Destroy(rb);
    }

    private static void NormalizeModelTransform(GameObject instance, float targetMaxExtent, bool preferVerticalTall)
    {
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        if (!TryComputeBounds(instance, out Bounds b))
            return;

        // Auto-orient if FBX imported lying flat on XZ instead of upright XY
        if (!preferVerticalTall && b.size.y < b.size.x * 0.45f && b.size.y < b.size.z * 0.45f)
        {
            instance.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            TryComputeBounds(instance, out b);
        }
        else if (preferVerticalTall && b.size.z > b.size.y * 1.25f)
        {
            instance.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            TryComputeBounds(instance, out b);
        }

        float refExtent = preferVerticalTall
            ? Mathf.Max(0.0001f, b.size.y)
            : Mathf.Max(0.0001f, Mathf.Max(b.size.x, b.size.y));

        float scale = targetMaxExtent / refExtent;
        instance.transform.localScale = Vector3.one * scale;

        if (TryComputeBounds(instance, out Bounds scaledBounds))
        {
            Vector3 worldOffset = instance.transform.parent.position - scaledBounds.center;
            instance.transform.position += worldOffset;
        }
    }

    private static bool TryComputeBounds(GameObject root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasAny = false;
        bounds = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            if (!hasAny)
            {
                bounds = renderers[i].bounds;
                hasAny = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }
        return hasAny;
    }
}
