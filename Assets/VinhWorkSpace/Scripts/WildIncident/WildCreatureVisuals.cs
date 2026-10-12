using System;
using UnityEngine;

/// <summary>
/// Quản lý nhận diện thị giác độc quyền cho Thú Hoang (Wild Creatures):
/// 1. Vòng tròn đỏ phát sáng dưới chân (Red Ground Aura Ring):
///    - Ôm sát mặt đất dưới chân thú, di chuyển theo từng bước chân.
///    - Nhịp thở phát quang nhấp nhô nhẹ nhàng (Pulsating breathing effect).
///    - Giúp người chơi phân biệt tức thì từ góc nhìn Top-Down giữa thú hoang và thú nuôi.
/// 2. Texture & Sắc đỏ nổi bật (Fierce Crimson Tint & Red Rim Glow):
///    - Biến đổi chất liệu lông/da của thú hoang sang tông đỏ dữ dội.
///    - Viền sáng HDR Red bao quanh dáng hình (Silhouette) qua shader Custom/AnimalRimGlow.
/// 3. Loại bỏ hoàn toàn các icon/huy hiệu rườm rà trên đầu, giữ tính thẩm mỹ điện ảnh cho thế giới.
/// </summary>
public class WildCreatureVisuals : MonoBehaviour
{
    [Header("1. Vòng Tròn Đỏ Dưới Chân (Ground Ring)")]
    [SerializeField] private float baseRingScale = 1.0f;
    [SerializeField] private Color ringColor = new Color(1.0f, 0.15f, 0.12f, 0.95f);

    [Header("2. Sắc Đỏ Nổi Bật (Red Fur Tint & Rim)")]
    [SerializeField] private Color baseTextureTint = new Color(1.25f, 0.38f, 0.35f, 1f);
    [SerializeField] private Color rimGlowColor = new Color(3.2f, 0.3f, 0.15f, 1f);
    [SerializeField] private float rimIntensity = 3.0f;

    private GameObject groundRingObj;
    private SpriteRenderer ringRenderer;
    private Material instantiatedRimMaterial;
    private Material instantiatedRingMaterial;
    private bool isAlpha = false;
    private float cachedSurfaceY = float.MinValue;

    private static Sprite cachedRedRingSprite;

    public void Initialize(bool isAlphaCreature, float surfaceY = float.MinValue)
    {
        this.isAlpha = isAlphaCreature;
        this.cachedSurfaceY = surfaceY;

        if (isAlpha)
        {
            baseRingScale = 1.45f;
            rimIntensity = 3.6f;
            rimGlowColor = new Color(3.8f, 0.28f, 0.12f, 1f);
            baseTextureTint = new Color(1.30f, 0.35f, 0.30f, 1f);
        }
        else
        {
            baseRingScale = 0.85f;
            rimIntensity = 2.4f;
            rimGlowColor = new Color(2.5f, 0.20f, 0.10f, 1f);
            baseTextureTint = new Color(1.20f, 0.40f, 0.35f, 1f);
        }

        ApplyRedCreatureMaterials();
        CreateGroundRedRing();
    }

    private void Start()
    {
        if (groundRingObj == null)
        {
            CreateGroundRedRing();
        }
    }

    private void LateUpdate()
    {
        // Nhịp thở phát quang và xoay nhẹ nhàng cho vòng tròn đỏ dưới chân
        if (groundRingObj != null)
        {
            float pulse = baseRingScale * (1.0f + 0.06f * Mathf.Sin(Time.time * 3.2f));
            groundRingObj.transform.localScale = new Vector3(pulse, pulse, 1f);

            // Xác định độ cao bề mặt đất chuẩn dưới bàn chân con thú
            float surfaceY = cachedSurfaceY > -900f ? cachedSurfaceY : transform.position.y;

            // Bắn raycast từ trên cao xuống để bám sát địa hình nếu có thay đổi
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(transform.position.x, transform.position.y + 2f, transform.position.z), Vector3.down, 6f);
            float highestTerrainY = float.MinValue;
            foreach (var hit in hits)
            {
                if (hit.collider != null && !hit.collider.transform.IsChildOf(transform) && hit.point.y > highestTerrainY)
                {
                    highestTerrainY = hit.point.y;
                }
            }
            if (highestTerrainY > float.MinValue)
            {
                surfaceY = highestTerrainY;
                cachedSurfaceY = surfaceY;
            }

            // Giữ vòng tròn luôn nằm sát phẳng trên mặt đất, chỉ xoay yaw tự nhiên
            groundRingObj.transform.position = new Vector3(transform.position.x, surfaceY + 0.035f, transform.position.z);
            groundRingObj.transform.rotation = Quaternion.AngleAxis(Time.time * 25f, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
        }
    }

    /// <summary>
    /// Tạo Vòng Tròn Đỏ Phát Sáng phẳng dưới chân con thú
    /// </summary>
    private void CreateGroundRedRing()
    {
        if (groundRingObj != null) Destroy(groundRingObj);

        groundRingObj = new GameObject("Wild_GroundRedRing");
        groundRingObj.transform.SetParent(this.transform, false);
        groundRingObj.transform.localPosition = new Vector3(0f, 0.035f, 0f);
        groundRingObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        groundRingObj.transform.localScale = new Vector3(baseRingScale, baseRingScale, 1f);

        ringRenderer = groundRingObj.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = GetOrCreateRedRingSprite();

        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader == null) spriteShader = Shader.Find("Universal Render Pipeline/Unlit");

        if (spriteShader != null)
        {
            instantiatedRingMaterial = new Material(spriteShader);
            ringRenderer.material = instantiatedRingMaterial;
        }

        ringRenderer.color = ringColor;
        ringRenderer.sortingOrder = 5;
    }

    /// <summary>
    /// Biến đổi toàn bộ Texture/Material của thú sang tông đỏ hung dữ và phủ thêm viền sáng HDR
    /// </summary>
    private void ApplyRedCreatureMaterials()
    {
        // 1. Tắt và gỡ bỏ hoàn toàn các AI nội địa & enhancer của thú nuôi để thú hoang đứng uy nghi tại chỗ
        FarmAnimalAI farmAI = GetComponent<FarmAnimalAI>();
        if (farmAI != null)
        {
            farmAI.enabled = false;
            Destroy(farmAI);
        }

        AnimalMovementAI moveAI = GetComponent<AnimalMovementAI>();
        if (moveAI != null)
        {
            moveAI.enabled = false;
            Destroy(moveAI);
        }

        AnimalIndividual individual = GetComponent<AnimalIndividual>();
        if (individual != null)
        {
            individual.enabled = false;
            Destroy(individual);
        }

        AnimalVisualEnhancer enhancer = GetComponent<AnimalVisualEnhancer>();
        if (enhancer != null)
        {
            enhancer.enabled = false;
            Destroy(enhancer);
        }
        Transform crest = transform.Find("Runtime_OverheadCrest");
        if (crest != null) Destroy(crest.gameObject);

        // Phát animation Idle tự nhiên nếu có Animation component
        Animation anim = GetComponentInChildren<Animation>();
        if (anim != null)
        {
            string idleClip = "";
            foreach (AnimationState state in anim)
            {
                if (state.name.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    idleClip = state.name;
                    break;
                }
            }
            if (string.IsNullOrEmpty(idleClip))
            {
                foreach (AnimationState state in anim)
                {
                    if (state.name.IndexOf("Rest", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        idleClip = state.name;
                        break;
                    }
                }
            }
            if (!string.IsNullOrEmpty(idleClip))
            {
                anim.CrossFade(idleClip, 0.2f);
            }
        }

        // 2. Nhuộm đỏ chất liệu lông/da gốc của thú (Base Texture Red Tint)
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            if (r == null || (groundRingObj != null && r.gameObject == groundRingObj)) continue;

            Material[] mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;

                if (m.HasProperty("_BaseColor"))
                {
                    Color orig = m.GetColor("_BaseColor");
                    m.SetColor("_BaseColor", new Color(
                        Mathf.Min(1.0f, orig.r * baseTextureTint.r),
                        orig.g * baseTextureTint.g,
                        orig.b * baseTextureTint.b,
                        orig.a
                    ));
                }
                else if (m.HasProperty("_Color"))
                {
                    Color orig = m.GetColor("_Color");
                    m.SetColor("_Color", new Color(
                        Mathf.Min(1.0f, orig.r * baseTextureTint.r),
                        orig.g * baseTextureTint.g,
                        orig.b * baseTextureTint.b,
                        orig.a
                    ));
                }

                if (m.HasProperty("_EmissionColor"))
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color(0.7f, 0.08f, 0.04f, 1f));
                }
            }
        }

        // 3. Phủ thêm lớp Viền Đỏ Rực Rỡ HDR (Red Rim Glow Overlay)
        Shader rimShader = Shader.Find("Custom/AnimalRimGlow");
        if (rimShader != null)
        {
            instantiatedRimMaterial = new Material(rimShader);
            instantiatedRimMaterial.name = "Runtime_WildRedRimGlow";
            instantiatedRimMaterial.SetColor("_RimColor", rimGlowColor);
            instantiatedRimMaterial.SetFloat("_RimPower", 2.0f);
            instantiatedRimMaterial.SetFloat("_RimIntensity", rimIntensity);

            foreach (Renderer r in renderers)
            {
                if (r == null || (groundRingObj != null && r.gameObject == groundRingObj)) continue;

                Material[] origMats = r.materials;
                Material[] newMats = new Material[origMats.Length + 1];
                for (int j = 0; j < origMats.Length; j++)
                {
                    newMats[j] = origMats[j];
                }
                newMats[origMats.Length] = instantiatedRimMaterial;
                r.materials = newMats;
            }
        }
    }

    /// <summary>
    /// Sinh Sprite vòng tròn đỏ phát quang ôm trọn mặt đất
    /// </summary>
    private static Sprite GetOrCreateRedRingSprite()
    {
        if (cachedRedRingSprite != null) return cachedRedRingSprite;

        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = size * 0.5f;
        Color[] pixels = new Color[size * size];

        float outerR = size * 0.46f;
        float innerR = size * 0.32f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pt = new Vector2(x + 0.5f, y + 0.5f);
                float dist = Vector2.Distance(pt, new Vector2(center, center));

                float alpha = 0f;
                if (dist >= innerR && dist <= outerR)
                {
                    // Vành đai phát quang rực rỡ
                    float mid = (innerR + outerR) * 0.5f;
                    float halfSpan = (outerR - innerR) * 0.5f;
                    alpha = 1f - Mathf.Abs(dist - mid) / halfSpan;
                    alpha = Mathf.Pow(alpha, 0.75f);
                }
                else if (dist < innerR)
                {
                    // Đĩa nền mờ ảo bên trong lòng vòng tròn
                    float innerRatio = dist / innerR;
                    alpha = 0.22f * innerRatio;
                }
                else if (dist > outerR && dist <= outerR + 4f)
                {
                    // Viền tan mờ chống răng cưa
                    alpha = Mathf.Clamp01((outerR + 4f - dist) / 4f) * 0.5f;
                }

                Color c = new Color(1.0f, 0.15f, 0.12f, alpha);
                pixels[y * size + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        cachedRedRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return cachedRedRingSprite;
    }

    private void OnDestroy()
    {
        if (groundRingObj != null) Destroy(groundRingObj);
        if (instantiatedRimMaterial != null) Destroy(instantiatedRimMaterial);
        if (instantiatedRingMaterial != null) Destroy(instantiatedRingMaterial);
    }
}
