using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Hiệu ứng Popup số lượng tài nguyên nổi lên khi người chơi click thu hoạch (Preserve / Farm game style).
/// Tự động bay lên, nảy kích thước và mờ dần (Fade-out) rồi tự hủy.
/// </summary>
public class ResourceHarvestPopup : MonoBehaviour
{
    [Header("Motion & Lifetime")]
    [SerializeField] private float floatSpeed = 1.2f;
    [SerializeField] private float duration = 1.2f;
    [SerializeField] private Vector3 startScale = new Vector3(0.5f, 0.5f, 0.5f);
    [SerializeField] private Vector3 punchScale = new Vector3(1.3f, 1.3f, 1.3f);
    [SerializeField] private Vector3 endScale = new Vector3(1.0f, 1.0f, 1.0f);

    private TextMeshPro textMesh;
    private Camera cachedCam;

    /// <summary>
    /// Sinh một popup bay lên tại vị trí worldPosition
    /// </summary>
    public static void Spawn(Vector3 worldPosition, ResourceData resource, int amount)
    {
        if (resource == null && amount <= 0) return;

        GameObject go = new GameObject("ResourceHarvestPopup");
        go.transform.position = worldPosition;

        ResourceHarvestPopup popup = go.AddComponent<ResourceHarvestPopup>();
        popup.Initialize(resource, amount);
    }

    public void Initialize(ResourceData resource, int amount)
    {
        cachedCam = Camera.main;

        textMesh = gameObject.AddComponent<TextMeshPro>();
        string resName = resource != null ? resource.resourceName : "Tài nguyên";
        Color resColor = resource != null ? resource.resourceColor : Color.green;

        textMesh.text = $"+{amount} {resName}";
        textMesh.fontSize = 4.2f;
        textMesh.fontStyle = FontStyles.Bold;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.color = resColor;
        textMesh.sortingOrder = 500;

        StartCoroutine(AnimateRoutine(resColor));
    }

    private void LateUpdate()
    {
        // Luôn xoay mặt chữ về phía Camera (Billboard)
        if (cachedCam == null) cachedCam = Camera.main;
        if (cachedCam != null)
        {
            transform.rotation = cachedCam.transform.rotation;
        }
    }

    private IEnumerator AnimateRoutine(Color baseColor)
    {
        transform.localScale = startScale;
        Vector3 startPos = transform.position;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // 1. Bay lên cao dần
            transform.position = startPos + Vector3.up * (elapsed * floatSpeed);

            // 2. Nảy kích thước (Punch Scale trong 20% đầu thời gian)
            if (t < 0.25f)
            {
                float punchT = t / 0.25f;
                transform.localScale = Vector3.Lerp(startScale, punchScale, punchT);
            }
            else
            {
                float settleT = (t - 0.25f) / 0.75f;
                transform.localScale = Vector3.Lerp(punchScale, endScale, settleT);
            }

            // 3. Mờ dần ở 40% thời gian cuối
            if (t > 0.6f)
            {
                float fadeT = (t - 0.6f) / 0.4f;
                Color c = baseColor;
                c.a = Mathf.Lerp(1.0f, 0.0f, fadeT);
                textMesh.color = c;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
