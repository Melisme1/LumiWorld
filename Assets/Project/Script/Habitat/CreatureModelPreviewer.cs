using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Trình chiếu 3D Model Sinh Vật trong giao diện UI (Render-to-Texture preview).
/// Tạo sân khấu biệt lập và Camera URP render với nền trong suốt (alpha = 0).
/// Hỗ trợ đồng thời nhiều ô preview (Slot 0: Offspring/Main, Slot 1: Parent A, Slot 2: Parent B).
/// Tự động vô hiệu hóa các script AI di chuyển, căn giữa model, chiếu sáng studio và xoay nhẹ nhàng.
/// </summary>
public class CreatureModelPreviewer : MonoBehaviour
{
    public const int SlotMain = 0;
    public const int SlotParentA = 1;
    public const int SlotParentB = 2;
    private const int MaxSlots = 3;

    private class PreviewSlot
    {
        public int index;
        public Transform slotRoot;
        public Transform stageRoot;
        public Camera cam;
        public RenderTexture texture;
        public GameObject sourcePrefab;
        public GameObject modelInstance;
        public float manualYaw;
        public bool isDragging;
        public PlayableGraph playableGraph;
        public AnimationClipPlayable clipPlayable;
        public float clipLength;
    }

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

    private readonly PreviewSlot[] slots = new PreviewSlot[MaxSlots];
    private Light keyLight;
    private Light fillLight;

    public RenderTexture PreviewTexture => GetSlotTexture(SlotMain);

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

        for (int i = 0; i < MaxSlots; i++)
        {
            EnsureSlotInitialized(i);
        }

        // Đèn chiếu sáng studio dùng chung cho các sân khấu
        if (keyLight == null && slots[0] != null && slots[0].cam != null)
        {
            keyLight = slots[0].cam.gameObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.6f;
            keyLight.color = new Color(1f, 0.98f, 0.94f, 1f);
        }

        if (fillLight == null)
        {
            var topLightObj = new GameObject("FillLight", typeof(Light));
            topLightObj.transform.SetParent(transform, false);
            topLightObj.transform.localPosition = new Vector3(1f, 2f, 1f);
            topLightObj.transform.LookAt(transform.position);

            fillLight = topLightObj.GetComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 1.1f;
            fillLight.color = new Color(0.9f, 0.95f, 1f, 1f);
        }
    }

    private PreviewSlot EnsureSlotInitialized(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxSlots) slotIndex = 0;
        if (slots[slotIndex] != null && slots[slotIndex].texture != null && slots[slotIndex].cam != null)
        {
            return slots[slotIndex];
        }

        var slot = new PreviewSlot { index = slotIndex };

        // Mỗi slot cách nhau 60m theo trục X để camera không nhìn thấy nhau
        var slotRootGo = new GameObject($"PreviewSlot_{slotIndex}");
        slotRootGo.transform.SetParent(transform, false);
        slotRootGo.transform.localPosition = new Vector3(slotIndex * 60f, 0f, 0f);
        slot.slotRoot = slotRootGo.transform;

        var stageRootGo = new GameObject($"StageRoot_{slotIndex}");
        stageRootGo.transform.SetParent(slot.slotRoot, false);
        stageRootGo.transform.localPosition = Vector3.zero;
        slot.stageRoot = stageRootGo.transform;

        int texSize = (slotIndex == SlotMain) ? 512 : 384;
        slot.texture = new RenderTexture(texSize, texSize, 24, RenderTextureFormat.ARGB32);
        slot.texture.antiAliasing = 4;
        slot.texture.wrapMode = TextureWrapMode.Clamp;
        slot.texture.filterMode = FilterMode.Bilinear;
        slot.texture.useMipMap = false;
        slot.texture.autoGenerateMips = false;
        slot.texture.Create();

        var camObj = new GameObject($"PreviewCamera_{slotIndex}", typeof(Camera));
        camObj.transform.SetParent(slot.slotRoot, false);
        camObj.transform.localPosition = new Vector3(0f, 0.05f, 2.25f);
        camObj.transform.LookAt(slot.slotRoot.position);

        slot.cam = camObj.GetComponent<Camera>();
        slot.cam.clearFlags = CameraClearFlags.SolidColor;
        slot.cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        slot.cam.nearClipPlane = 0.05f;
        slot.cam.farClipPlane = 25f;
        slot.cam.fieldOfView = 24f;
        slot.cam.cullingMask = -1;
        slot.cam.targetTexture = slot.texture;
        slot.cam.enabled = true;

        var urpCam = camObj.GetComponent<UniversalAdditionalCameraData>();
        if (urpCam == null) urpCam = camObj.AddComponent<UniversalAdditionalCameraData>();
        urpCam.renderType = CameraRenderType.Base;
        urpCam.renderPostProcessing = false;
        urpCam.renderShadows = false;

        slots[slotIndex] = slot;
        return slot;
    }

    public RenderTexture GetSlotTexture(int slotIndex)
    {
        var slot = EnsureSlotInitialized(slotIndex);
        return slot.texture;
    }

    private void Update()
    {
        for (int i = 0; i < MaxSlots; i++)
        {
            var slot = slots[i];
            if (slot == null || slot.modelInstance == null || slot.stageRoot == null) continue;

            if (!slot.isDragging)
            {
                float phase = i * 0.9f;
                float idleSway = Mathf.Sin(Time.unscaledTime * 0.85f + phase) * 12f;
                slot.stageRoot.localRotation = Quaternion.Euler(0f, slot.manualYaw + idleSway, 0f);
            }
            else
            {
                slot.stageRoot.localRotation = Quaternion.Euler(0f, slot.manualYaw, 0f);
            }

            // Loop PlayableGraph clip if used for Mecanim/GLB without controller
            if (slot.playableGraph.IsValid() && slot.clipPlayable.IsValid() && slot.clipLength > 0.05f)
            {
                double t = slot.clipPlayable.GetTime();
                if (t >= slot.clipLength)
                {
                    slot.clipPlayable.SetTime(t % slot.clipLength);
                }
            }
        }
    }

    public void OnDragRotate(float deltaX) => OnDragRotateSlot(SlotMain, deltaX);
    public void OnEndDragRotate() => OnEndDragRotateSlot(SlotMain);

    public void OnDragRotateSlot(int slotIndex, float deltaX)
    {
        var slot = EnsureSlotInitialized(slotIndex);
        slot.isDragging = true;
        slot.manualYaw -= deltaX * 0.45f;
    }

    public void OnEndDragRotateSlot(int slotIndex)
    {
        var slot = EnsureSlotInitialized(slotIndex);
        slot.isDragging = false;
    }

    /// <summary>
    /// Hiển thị 3D Prefab hoặc GameObject lên sân khấu chính (Slot 0)
    /// </summary>
    public bool ShowModel(GameObject prefabOrInstance)
    {
        return ShowModelInSlot(SlotMain, prefabOrInstance, null);
    }

    /// <summary>
    /// Hiển thị 3D Prefab hoặc GameObject lên ô preview chỉ định (0 = Offspring, 1 = Parent A, 2 = Parent B)
    /// </summary>
    public bool ShowModelInSlot(int slotIndex, GameObject prefabOrInstance, AnimationClip fallbackClip = null)
    {
        var slot = EnsureSlotInitialized(slotIndex);
        if (prefabOrInstance == null)
        {
            ClearSlot(slotIndex);
            return false;
        }

        // Reuse existing live instance if the same prefab is already displayed in this slot
        if (slot.modelInstance != null && slot.sourcePrefab == prefabOrInstance)
        {
            return true;
        }

        ClearSlot(slotIndex);
        slot.sourcePrefab = prefabOrInstance;
        slot.manualYaw = 0f;
        slot.isDragging = false;

        GameObject inst = Instantiate(prefabOrInstance, slot.stageRoot);
        inst.SetActive(true);
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = prefabOrInstance.transform.localRotation;
        slot.modelInstance = inst;
        slot.stageRoot.localRotation = Quaternion.identity;

        // Vô hiệu hóa toàn bộ script AI di chuyển / Rigidbody để tránh rơi hoặc chạy mất
        foreach (var mb in inst.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb != null) mb.enabled = false;
        }
        foreach (var col in inst.GetComponentsInChildren<Collider>(true))
        {
            if (col != null) col.enabled = false;
        }
        foreach (var rb in inst.GetComponentsInChildren<Rigidbody>(true))
        {
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }
        }

        // Đảm bảo SkinnedMeshRenderer luôn cập nhật khi render qua camera phụ
        foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr != null) smr.updateWhenOffscreen = true;
        }

        // 1. Thử phát Legacy Animation (chuẩn của các prefab *_anm và GLB Legacy)
        bool playedAnim = false;
        var anim = inst.GetComponentInChildren<Animation>(true);
        if (anim != null)
        {
            anim.enabled = true;
            anim.cullingType = AnimationCullingType.AlwaysAnimate;
            anim.wrapMode = WrapMode.Loop;

            string bestIdle = null;
            string[] preferred = { "Idle", "idle", "Rest_Pose", "rest_pose", "Stand", "stand", "Trot", "Walk", "walk" };
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
                    if (state == null) continue;
                    string n = state.name.ToLowerInvariant();
                    if (n.Contains("death") || n.Contains("die") || n.Contains("fall"))
                        continue;
                    bestIdle = state.name;
                    break;
                }
            }

            if (string.IsNullOrEmpty(bestIdle) && anim.clip != null)
            {
                bestIdle = anim.clip.name;
            }

            if (string.IsNullOrEmpty(bestIdle) && fallbackClip != null && fallbackClip.legacy)
            {
                anim.AddClip(fallbackClip, fallbackClip.name);
                bestIdle = fallbackClip.name;
            }

            if (!string.IsNullOrEmpty(bestIdle))
            {
                if (anim[bestIdle] != null) anim[bestIdle].wrapMode = WrapMode.Loop;
                anim.Play(bestIdle);
                playedAnim = true;
            }
        }

        // 2. Hỗ trợ Animator (Mecanim hoặc GLB import với animationMethod = Mecanim)
        var animator = inst.GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            animator.enabled = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            if (!playedAnim && animator.runtimeAnimatorController == null && fallbackClip != null)
            {
                try
                {
                    slot.playableGraph = PlayableGraph.Create($"PreviewGraph_{slotIndex}");
                    slot.playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                    var output = AnimationPlayableOutput.Create(slot.playableGraph, "Animation", animator);
                    slot.clipPlayable = AnimationClipPlayable.Create(slot.playableGraph, fallbackClip);
                    slot.clipLength = fallbackClip.length;
                    output.SetSourcePlayable(slot.clipPlayable);
                    slot.playableGraph.Play();
                }
                catch (System.Exception)
                {
                    // Fallback an toàn nếu clip là legacy
                }
            }
        }
        else if (!playedAnim && fallbackClip != null)
        {
            // Nếu prefab không gắn sẵn Animation hay Animator nhưng có fallbackClip
            if (fallbackClip.legacy)
            {
                var newAnim = inst.AddComponent<Animation>();
                newAnim.cullingType = AnimationCullingType.AlwaysAnimate;
                newAnim.wrapMode = WrapMode.Loop;
                newAnim.AddClip(fallbackClip, fallbackClip.name);
                if (newAnim[fallbackClip.name] != null) newAnim[fallbackClip.name].wrapMode = WrapMode.Loop;
                newAnim.Play(fallbackClip.name);
            }
            else
            {
                var newAnimator = inst.AddComponent<Animator>();
                newAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                try
                {
                    slot.playableGraph = PlayableGraph.Create($"PreviewGraph_{slotIndex}");
                    slot.playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                    var output = AnimationPlayableOutput.Create(slot.playableGraph, "Animation", newAnimator);
                    slot.clipPlayable = AnimationClipPlayable.Create(slot.playableGraph, fallbackClip);
                    slot.clipLength = fallbackClip.length;
                    output.SetSourcePlayable(slot.clipPlayable);
                    slot.playableGraph.Play();
                }
                catch (System.Exception) { }
            }
        }

        // 3. Căn giữa bounds của tất cả renderers và điều chỉnh kích cỡ vừa vặn
        var renderers = inst.GetComponentsInChildren<Renderer>(true);
        if (renderers != null && renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                b.Encapsulate(renderers[i].bounds);
            }

            float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z);
            float scaleFactor = 1f;
            if (maxDim > 0.01f)
            {
                string pNameLower = prefabOrInstance.name != null ? prefabOrInstance.name.ToLowerInvariant() : "";
                bool isCaterpillar = pNameLower.Contains("saubuom") || pNameLower.Contains("petaluff") || pNameLower.Contains("caterpillar");
                bool isBraluff = pNameLower.Contains("braluff");
                float targetSize = isCaterpillar ? 0.60f : (isBraluff ? 0.52f : 0.74f);
                scaleFactor = targetSize / maxDim;
                inst.transform.localScale *= scaleFactor;
            }

            Vector3 localCenterBeforeScale = b.center - slot.stageRoot.position;
            Vector3 scaledOffset = -localCenterBeforeScale * scaleFactor;
            inst.transform.position = slot.stageRoot.position + scaledOffset;
        }

        if (slot.cam != null)
        {
            slot.cam.enabled = true;
            slot.cam.Render();
        }

        return true;
    }

    public void ClearModel()
    {
        ClearSlot(SlotMain);
    }

    public void ClearSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxSlots) return;
        var slot = slots[slotIndex];
        if (slot == null) return;

        if (slot.playableGraph.IsValid())
        {
            slot.playableGraph.Destroy();
        }

        if (slot.modelInstance != null)
        {
            Destroy(slot.modelInstance);
            slot.modelInstance = null;
        }
        slot.sourcePrefab = null;
    }

    public void ClearAllSlots()
    {
        for (int i = 0; i < MaxSlots; i++)
        {
            ClearSlot(i);
        }
    }

    private void OnDestroy()
    {
        ClearAllSlots();
        for (int i = 0; i < MaxSlots; i++)
        {
            if (slots[i] != null && slots[i].texture != null)
            {
                slots[i].texture.Release();
                Destroy(slots[i].texture);
                slots[i].texture = null;
            }
        }
    }
}
