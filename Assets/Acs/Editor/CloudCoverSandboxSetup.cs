using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using LumiWorld.Acs.Islands;

namespace LumiWorld.Acs.Editor
{
    /// <summary>
    /// Builds the isolated Sandbox test scene Assets/Acs/Art/Test/CloudCover_Test.unity
    /// with Starter Island (unlocked) at its real catalog coordinate (0,0,0) and
    /// Wet Island (locked by default, toggleable Unlock/Lock) at its real catalog coordinate (90,0,0),
    /// surrounded by the full World Cloud Sea (Layer 1 misty fog + Layer 2 Painterly 2.5D / Smooth 3D SDF cloud bank).
    /// Does NOT touch MapBuilding.unity or IslandNavigationUI.cs.
    /// Menu: LumiWorld > Tools > Build Cloud Cover Sandbox Scene
    /// </summary>
    public static class CloudCoverSandboxSetup
    {
        public const string ScenePath = "Assets/Acs/Art/Test/CloudCover_Test.unity";
        public const string MaterialsFolder = "Assets/Acs/Art/Environment/Clouds";
        public const string CloudAtlasPath = "Assets/Acs/Art/Environment/Clouds/T_CozyCloudPuffs_Atlas.png";

        private const string OceanMaterialGuid = "6096a2fd66df1ad47aa66f1c3accf05c";
        private const string SkyboxMaterialGuid = "7ea14bfe7b9a16148970250afcdc011a";
        private const string IslandCatalogPath = "Assets/Acs/Data/Islands/IslandCatalog.asset";

        [MenuItem("LumiWorld/Tools/Build Cloud Cover Sandbox Scene", priority = 30)]
        public static void BuildSandboxSceneMenu()
        {
            BuildSandboxScene();
        }

        public static Scene BuildSandboxScene()
        {
            EnsureFolder(MaterialsFolder);
            EnsureFolder("Assets/Acs/Art/Test");

            // Ensure Painterly Cloud Atlas texture is cleanly imported with smooth alpha & mipmaps
            ConfigureCloudAtlasImport();
            Texture2D cloudAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(CloudAtlasPath);
            if (cloudAtlas == null)
                throw new System.InvalidOperationException($"Missing Painterly Cloud Atlas texture at {CloudAtlasPath}");

            Shader worldSeaShader = Shader.Find("LumiWorld/Environment/CozyWorldCloudSea");
            Shader spriteBankShader = Shader.Find("LumiWorld/Environment/CozyCloudSpriteBank");
            Shader smoothSdfShader = Shader.Find("LumiWorld/Environment/CozyCloudPuff");

            if (worldSeaShader == null || !worldSeaShader.isSupported)
                throw new System.InvalidOperationException("CozyWorldCloudSea shader missing or unsupported.");
            if (spriteBankShader == null || !spriteBankShader.isSupported)
                throw new System.InvalidOperationException("CozyCloudSpriteBank shader missing or unsupported.");
            if (smoothSdfShader == null || !smoothSdfShader.isSupported)
                throw new System.InvalidOperationException("CozyCloudPuff shader missing or unsupported.");

            Material matWorldSea = CreateOrUpdateMaterial(
                $"{MaterialsFolder}/M_CozyWorldCloudSea.mat",
                worldSeaShader,
                m =>
                {
                    m.SetColor("_TopColor", new Color(0.988f, 0.978f, 0.948f, 1f));
                    m.SetColor("_ShadowColor", new Color(0.68f, 0.83f, 0.94f, 1f));
                    m.SetColor("_RimColor", new Color(1.0f, 0.96f, 0.85f, 1f));
                    m.SetFloat("_RimStrength", 0.22f);
                    m.SetFloat("_NoiseScale", 0.068f);
                    m.SetVector("_DriftSpeed", new Vector4(0.14f, 0.05f, 0f, 0f));
                    m.SetFloat("_BillowHeight", 1.15f);
                    m.SetFloat("_EdgeSoftness", 0.30f);
                    m.SetFloat("_EdgeNoiseStrength", 0.46f);
                    m.SetFloat("_OverallOpacity", 0.98f);
                    m.SetFloat("_WorldFadeStart", 155f);
                    m.SetFloat("_WorldFadeEnd", 200f);
                });

            Material matSpriteBank = CreateOrUpdateMaterial(
                $"{MaterialsFolder}/M_CozyCloudSpriteBank.mat",
                spriteBankShader,
                m =>
                {
                    m.SetTexture("_MainTex", cloudAtlas);
                    m.SetColor("_TopColor", new Color(0.996f, 0.990f, 0.966f, 1f));
                    m.SetColor("_ShadowColor", new Color(0.68f, 0.83f, 0.94f, 1f));
                    m.SetColor("_RimColor", new Color(1.0f, 0.98f, 0.91f, 1f));
                    m.SetFloat("_RimStrength", 0.24f);
                    m.SetFloat("_BillboardTilt", 0.64f);
                    m.SetFloat("_DetailScale", 0.36f);
                    m.SetFloat("_EdgeFluffStrength", 0.32f);
                    m.SetFloat("_InnerDetailStrength", 0.18f);
                    m.SetFloat("_Opacity", 0.95f);
                    m.enableInstancing = true;
                });

            Material matSmoothSdf = CreateOrUpdateMaterial(
                $"{MaterialsFolder}/M_CozyCloudPuff_SmoothSDF.mat",
                smoothSdfShader,
                m =>
                {
                    m.SetColor("_TopColor", new Color(0.992f, 0.984f, 0.955f, 1f));
                    m.SetColor("_ShadowColor", new Color(0.62f, 0.80f, 0.93f, 1f));
                    m.SetColor("_RimColor", new Color(1.0f, 0.96f, 0.86f, 1f));
                    m.SetFloat("_RimPower", 2.1f);
                    m.SetFloat("_RimStrength", 0.28f);
                    m.SetFloat("_WobbleSpeed", 0.42f);
                    m.SetFloat("_WobbleAmp", 0.035f);
                    m.SetFloat("_NoiseScale", 0.40f);
                    m.SetFloat("_SubsurfaceWrap", 0.56f);
                    m.SetFloat("_EdgeFadeStart", 0.08f);
                    m.SetFloat("_EdgeFadeEnd", 0.62f);
                    m.SetFloat("_EdgeNoiseStrength", 0.24f);
                    m.SetFloat("_OverallOpacity", 0.92f);
                    m.enableInstancing = true;
                });

            AssetDatabase.SaveAssets();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Lighting & Atmosphere matching MapBuilding.unity (with overview-friendly fog range)
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.7607843f, 0.8862745f, 0.9607843f, 1f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 65f;
            RenderSettings.fogEndDistance = 220f;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientSkyColor = new Color(0.212f, 0.227f, 0.259f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.114f, 0.125f, 0.133f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.047f, 0.043f, 0.035f, 1f);
            RenderSettings.ambientIntensity = 1f;

            Material skybox = LoadMaterialByGuid(SkyboxMaterialGuid);
            if (skybox != null) RenderSettings.skybox = skybox;

            // 2. Exact Directional Light parity with MapBuilding.unity
            GameObject lightGo = new GameObject("Directional Light", typeof(Light));
            lightGo.transform.position = new Vector3(0f, 3f, 0f);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light dirLight = lightGo.GetComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(1f, 0.95686275f, 0.8392157f, 1f);
            dirLight.intensity = 1f;
            dirLight.shadows = LightShadows.Soft;
            RenderSettings.sun = dirLight;

            // Resolve real catalog centers (Starter = (0,0,0), Wet = (90,0,0))
            IslandCatalog catalog = AssetDatabase.LoadAssetAtPath<IslandCatalog>(IslandCatalogPath);
            IslandDefinition starterDef = catalog != null ? catalog.Find("starter") : null;
            IslandDefinition wetDef = catalog != null ? catalog.Find("wet") : null;

            Vector3 starterPos = starterDef != null
                ? HexMetrics.HexToWorldPosition(starterDef.center, 0f)
                : Vector3.zero;
            Vector3 wetPos = wetDef != null
                ? HexMetrics.HexToWorldPosition(wetDef.center, 0f)
                : new Vector3(90f, 0f, 0f);

            // 3. Main Camera starting cleanly over Starter Island (with Overview zoom headroom)
            GameObject camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(UniversalAdditionalCameraData), typeof(HexCameraController));
            camGo.tag = "MainCamera";
            Camera cam = camGo.GetComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;
            Quaternion camRot = Quaternion.Euler(50f, -30f, 0f);
            camGo.transform.rotation = camRot;
            camGo.transform.position = starterPos + camRot * (Vector3.back * 32f);

            HexCameraController camCtrl = camGo.GetComponent<HexCameraController>();
            var camSo = new SerializedObject(camCtrl);
            camSo.FindProperty("autoSetupStartingView").boolValue = true;
            camSo.FindProperty("startingEulerAngles").vector3Value = new Vector3(50f, -30f, 0f);
            camSo.FindProperty("startingDistance").floatValue = 32f;
            camSo.FindProperty("lookAtCenter").vector3Value = starterPos;
            camSo.FindProperty("maxHeight").floatValue = 135f;
            camSo.ApplyModifiedPropertiesWithoutUndo();

            // 4. Ocean Plane following camera (scaled generously for 2-island overview)
            GameObject oceanGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            oceanGo.name = "Ocean";
            Object.DestroyImmediate(oceanGo.GetComponent<Collider>());
            oceanGo.transform.position = new Vector3(0f, -0.3f, 0f);
            oceanGo.transform.localScale = new Vector3(65f, 1f, 65f);
            MeshRenderer oceanRend = oceanGo.GetComponent<MeshRenderer>();
            oceanRend.shadowCastingMode = ShadowCastingMode.Off;
            oceanRend.receiveShadows = true;
            Material oceanMat = LoadMaterialByGuid(OceanMaterialGuid);
            if (oceanMat != null) oceanRend.sharedMaterial = oceanMat;

            OceanFollowCamera follow = oceanGo.AddComponent<OceanFollowCamera>();
            var followSo = new SerializedObject(follow);
            followSo.FindProperty("targetCamera").objectReferenceValue = cam;
            followSo.FindProperty("waterLevelY").floatValue = -0.3f;
            followSo.ApplyModifiedPropertiesWithoutUndo();

            // 5. Real Starter Island (radius-3 cozy starter cluster) + Neighboring Wet Island (19-hex cluster)
            BuildMockIslandCluster("MockStarterIsland", starterDef, starterPos, hexRadius: 3, useLushMix: false);
            BuildMockIslandCluster("MockWetIsland", wetDef, wetPos, hexRadius: 2, useLushMix: true);

            // Optional debug footprint rings (off by default, toggleable in HUD)
            GameObject ringsRoot = new GameObject("IslandFootprintRings");
            BuildFootprintRing(ringsRoot.transform, "StarterRing", starterPos, 24f, 3.2f, new Color(0.20f, 0.80f, 0.45f, 0.9f));
            BuildFootprintRing(ringsRoot.transform, "WetRing", wetPos, 28f, 3.2f, new Color(1.0f, 0.48f, 0.22f, 0.9f));
            ringsRoot.SetActive(false);

            // 6. World-Scale Dual-Layer Cozy Cloud Sea System
            GameObject cloudGo = new GameObject("WorldCloudSeaSystem", typeof(CozyWorldCloudSystem));
            CozyWorldCloudSystem cloudSystem = cloudGo.GetComponent<CozyWorldCloudSystem>();

            var zones = new List<CloudClearZone>
            {
                new CloudClearZone
                {
                    id = "starter",
                    displayName = "Starter Island",
                    worldCenter = starterPos,
                    clearRadius = 24f,
                    isUnlocked = true,
                    currentOpen = 1f
                },
                new CloudClearZone
                {
                    id = "wet",
                    displayName = "Wet Island",
                    worldCenter = wetPos,
                    clearRadius = 28f,
                    isUnlocked = false,
                    currentOpen = 0f
                }
            };

            Vector3 archipelagoCenter = (starterPos + wetPos) * 0.5f;
            cloudSystem.ConfigureSystem(matWorldSea, matSpriteBank, matSmoothSdf, archipelagoCenter, 420f, zones);
            cloudSystem.SetLiveTuning(height: 3.0f, softness: 0.30f, scale: 1.0f, speed: 1.0f);
            cloudSystem.SetStyle(WorldCloudStyle.Painterly25D);

            // 7. Interactive Sandbox HUD Controller
            GameObject hudGo = new GameObject("SandboxController", typeof(CloudCoverSandboxController));
            CloudCoverSandboxController ctrl = hudGo.GetComponent<CloudCoverSandboxController>();
            ctrl.ConfigureReferences(cloudSystem, ringsRoot, starterPos, wetPos);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return scene;
        }

        private static void ConfigureCloudAtlasImport()
        {
            AssetDatabase.ImportAsset(CloudAtlasPath, ImportAssetOptions.ForceUpdate);
            TextureImporter ti = AssetImporter.GetAtPath(CloudAtlasPath) as TextureImporter;
            if (ti == null) return;

            bool changed = false;
            if (ti.textureType != TextureImporterType.Default) { ti.textureType = TextureImporterType.Default; changed = true; }
            if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; changed = true; }
            if (ti.alphaSource != TextureImporterAlphaSource.FromInput) { ti.alphaSource = TextureImporterAlphaSource.FromInput; changed = true; }
            if (!ti.mipmapEnabled) { ti.mipmapEnabled = true; changed = true; }
            if (ti.wrapMode != TextureWrapMode.Clamp) { ti.wrapMode = TextureWrapMode.Clamp; changed = true; }
            if (ti.filterMode != FilterMode.Trilinear) { ti.filterMode = FilterMode.Trilinear; changed = true; }
            if (ti.maxTextureSize != 2048) { ti.maxTextureSize = 2048; changed = true; }
            if (ti.textureCompression != TextureImporterCompression.Uncompressed) { ti.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
            if (ti.anisoLevel != 4) { ti.anisoLevel = 4; changed = true; }

            if (changed) ti.SaveAndReimport();
        }

        private static GameObject BuildMockIslandCluster(string rootName, IslandDefinition def, Vector3 centerWorld, int hexRadius, bool useLushMix)
        {
            GameObject root = new GameObject(rootName);
            root.transform.position = Vector3.zero;

            GameObject[] dry = def != null && def.dryTiles != null && def.dryTiles.Length > 0 ? def.dryTiles : null;
            GameObject[] lush = useLushMix && def != null && def.lushTiles != null && def.lushTiles.Length > 0 ? def.lushTiles : dry;

            for (int q = -hexRadius; q <= hexRadius; q++)
            {
                for (int r = -hexRadius; r <= hexRadius; r++)
                {
                    int s = -q - r;
                    int dist = Mathf.Max(Mathf.Abs(q), Mathf.Abs(r), Mathf.Abs(s));
                    if (dist > hexRadius) continue;

                    int elevIdx = dist == 0 ? 1 : ((Mathf.Abs(q * 3 + r * 5) % 5 == 0) ? 2 : (dist == hexRadius && (q + r) % 2 == 0 ? 0 : 1));
                    bool pickLush = useLushMix && (dist <= 1 || ((q - r) % 3 == 0));
                    GameObject[] pool = pickLush && lush != null && lush.Length > 0 ? lush : dry;

                    Vector3 localHex = HexMetrics.HexToWorldPosition(new HexCoordinates(q, r), -0.5f);
                    Vector3 worldPos = centerWorld + localHex;

                    if (pool != null && pool.Length > 0)
                    {
                        GameObject prefab = pool[Mathf.Clamp(elevIdx, 0, pool.Length - 1)];
                        if (prefab != null)
                        {
                            GameObject tile = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                            tile.name = $"{rootName}_Hex_{q}_{r}_T{elevIdx}";
                            tile.transform.position = worldPos;
                            tile.transform.rotation = Quaternion.Euler(0f, ((q * 2 + r) & 3) * 60f, 0f);
                            continue;
                        }
                    }

                    GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    fallback.name = $"{rootName}_Fallback_{q}_{r}";
                    fallback.transform.SetParent(root.transform, false);
                    fallback.transform.position = worldPos + Vector3.up * (0.5f + elevIdx * 0.45f);
                    fallback.transform.localScale = new Vector3(1.85f, 0.5f + elevIdx * 0.45f, 1.85f);
                }
            }

            return root;
        }

        private static GameObject BuildFootprintRing(Transform parent, string name, Vector3 center, float radius, float y, Color col)
        {
            GameObject ringGo = new GameObject(name, typeof(LineRenderer));
            ringGo.transform.SetParent(parent, false);
            ringGo.transform.position = center;
            LineRenderer lr = ringGo.GetComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.startWidth = 0.25f;
            lr.endWidth = 0.25f;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit != null)
            {
                Material m = CreateOrUpdateMaterial(
                    $"{MaterialsFolder}/M_Ring_{name}.mat",
                    unlit,
                    mat => mat.color = col);
                lr.sharedMaterial = m;
            }
            const int pts = 72;
            lr.positionCount = pts;
            for (int i = 0; i < pts; i++)
            {
                float a = (float)i / pts * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius));
            }
            return ringGo;
        }

        private static Material LoadMaterialByGuid(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static Material CreateOrUpdateMaterial(string assetPath, Shader shader, System.Action<Material> configure)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            bool isNew = mat == null;
            if (isNew) mat = new Material(shader);
            else mat.shader = shader;
            configure?.Invoke(mat);
            if (isNew) AssetDatabase.CreateAsset(mat, assetPath);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureFolder(string assetFolder)
        {
            string normalized = assetFolder.Replace('\\', '/').TrimEnd('/');
            string full = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, normalized);
            Directory.CreateDirectory(full);
        }
    }
}
