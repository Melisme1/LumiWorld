using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Trình chiếu 3D Model Sinh Vật trong giao diện UI (Render-to-Texture preview).
/// Tạo một sân khấu biệt lập và Camera URP render với nền trong suốt (alpha = 0).
/// Tự động vô hiệu hóa các script AI di chuyển, căn giữa model, chiếu sáng studio và xoay nhẹ nhàng.
/// </summary>
public class CreatureModelPreviewer : MonoBehaviour
{
    private static CreatureModelPreviewer instance;
    public static CreatureModelPreviewer Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("CreatureModelPreviewStage");
                instance = go.AddComponent<CreatureModelPreviewer>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    private Camera previewCam;
    private Light keyLight;
    private Light fillLight;
    private RenderTexture previewTexture;
    private GameObject currentModelInstance;
    private Transform stageRoot;

    public RenderTexture PreviewTexture => previewTexture;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        InitStage();
    }

    private void InitStage()
    {
        // Đặt vị trí sân khấu ngoài không gian bản đồ
        transform.position = new Vector3(3000f, 1000f, 3000f);

        stageRoot = new GameObject("StageRoot").transform;
        stageRoot.SetParent(transform, false);
        stageRoot.localPosition = Vector3.zero;

        // 1. Tạo RenderTexture ARGB32 hỗ trợ kênh Alpha trong suốt
        if (previewTexture == null)
        {
            previewTexture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
            previewTexture.antiAliasing = 4;
            previewTexture.wrapMode = TextureWrapMode.Clamp;
            previewTexture.filterMode = FilterMode.Bilinear;
            previewTexture.useMipMap = false;
            previewTexture.autoGenerateMips = false;
            previewTexture.Create();
        }

        // 2. Camera URP chuyên dụng
        var camObj = new GameObject("PreviewCamera", typeof(Camera));
        camObj.transform.SetParent(transform, false);
        camObj.transform.localPosition = new Vector3(0f, 0f, 2.25f);
        camObj.transform.LookAt(transform.position);

        previewCam = camObj.GetComponent<Camera>();
        previewCam.clearFlags = CameraClearFlags.SolidColor;
        previewCam.backgroundColor = new Color(0f, 0f, 0f, 0f); // 100% Transparent Alpha
        previewCam.nearClipPlane = 0.05f;
        previewCam.farClipPlane = 40f;
        previewCam.fieldOfView = 24f;
        previewCam.cullingMask = -1; // Nhìn thấy mọi layer
        previewCam.targetTexture = previewTexture;
        previewCam.enabled = true;

        // Cấu hình URP Camera Data
        var urpCam = camObj.GetComponent<UniversalAdditionalCameraData>();
        if (urpCam == null) urpCam = camObj.AddComponent<UniversalAdditionalCameraData>();
        urpCam.renderType = CameraRenderType.Base;
        urpCam.renderPostProcessing = false;
        urpCam.renderShadows = false;

        // 3. Đèn chiếu sáng studio (Key light từ Camera + Fill light từ trên xuống)
        keyLight = camObj.AddComponent<Light>();
        keyLight.type = LightType.Directional;
        keyLight.intensity = 1.6f;
        keyLight.color = new Color(1f, 0.98f, 0.94f, 1f);

        var topLightObj = new GameObject("FillLight", typeof(Light));
        topLightObj.transform.SetParent(transform, false);
        topLightObj.transform.localPosition = new Vector3(1f, 2f, 1f);
        topLightObj.transform.LookAt(transform.position);

        fillLight = topLightObj.GetComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.intensity = 1.1f;
        fillLight.color = new Color(0.9f, 0.95f, 1f, 1f);
    }

    private float manualYaw = 0f;
    private bool isDragging = false;

    private void Update()
    {
        if (currentModelInstance != null && stageRoot != null)
        {
            if (!isDragging)
            {
                // Dao động nhẹ nhàng quanh hướng nhìn trực diện của người chơi
                float idleSway = Mathf.Sin(Time.time * 0.8f) * 10f;
                stageRoot.localRotation = Quaternion.Euler(0f, manualYaw + idleSway, 0f);
            }
            else
            {
                stageRoot.localRotation = Quaternion.Euler(0f, manualYaw, 0f);
            }
        }
    }

    public void OnDragRotate(float deltaX)
    {
        isDragging = true;
        manualYaw -= deltaX * 0.45f;
    }

    public void OnEndDragRotate()
    {
        isDragging = false;
    }

    /// <summary>
    /// Hiển thị 3D Prefab hoặc GameObject lên sân khấu preview
    /// </summary>
    public bool ShowModel(GameObject prefabOrInstance)
    {
        ClearModel();

        if (prefabOrInstance == null) return false;

        manualYaw = 0f;
        isDragging = false;

        currentModelInstance = Instantiate(prefabOrInstance, stageRoot);
        currentModelInstance.transform.localPosition = Vector3.zero;
        // Mặt thú hướng trực diện về phía Camera (0 độ chuẩn)
        currentModelInstance.transform.localRotation = prefabOrInstance.transform.localRotation;

        stageRoot.localRotation = Quaternion.identity;

        // VÔ HIỆU HÓA TOÀN BỘ SCRIPT AI DI CHUYỂN / RIGIDBODY ĐỂ TRÁNH RƠI HOẶC CHẠY MẤT
        foreach (var mb in currentModelInstance.GetComponentsInChildren<MonoBehaviour>())
        {
            mb.enabled = false;
        }
        foreach (var col in currentModelInstance.GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }
        foreach (var rb in currentModelInstance.GetComponentsInChildren<Rigidbody>())
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        // Bật lại Animator hoặc Animation để thú có thể thở hoặc cử động idle
        var anim = currentModelInstance.GetComponentInChildren<Animation>();
        if (anim != null)
        {
            anim.enabled = true;
            string bestIdle = null;
            string[] preferred = { "Idle", "idle", "Rest_Pose", "rest_pose", "Stand", "stand", "Trot", "Walk" };
            foreach (var p in preferred)
            {
                if (anim.GetClip(p) != null)
                {
                    bestIdle = p;
                    break;
                }
            }

            if (string.IsNullOrEmpty(bestIdle))
            {
                foreach (AnimationState state in anim)
                {
                    string n = state.name.ToLower();
                    if (n.Contains("death") || n.Contains("die") || n.Contains("fall"))
                        continue;
                    bestIdle = state.name;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(bestIdle))
            {
                anim.Play(bestIdle);
            }
        }

        var animator = currentModelInstance.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.enabled = true;
        }

        // Căn giữa bounds của tất cả renderers và điều chỉnh kích cỡ vừa vặn
        var renderers = currentModelInstance.GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                b.Encapsulate(renderers[i].bounds);
            }

            float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (maxDim > 0.05f)
            {
                float targetSize = 0.78f;
                float scaleFactor = targetSize / maxDim;
                currentModelInstance.transform.localScale *= scaleFactor;
            }

            Bounds bScaled = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bScaled.Encapsulate(renderers[i].bounds);
            }

            Vector3 worldCenter = bScaled.center;
            Vector3 offset = stageRoot.position - worldCenter;
            currentModelInstance.transform.position += offset;
        }

        return true;
    }

    public void ClearModel()
    {
        if (currentModelInstance != null)
        {
            Destroy(currentModelInstance);
            currentModelInstance = null;
        }
    }

    private void OnDestroy()
    {
        ClearModel();
        if (previewTexture != null)
        {
            previewTexture.Release();
            Destroy(previewTexture);
        }
    }
}
