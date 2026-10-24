using UnityEngine;

/// <summary>
/// Script chẩn đoán kích thước, bounds và hướng xoay của các model sông hồ trong runtime
/// </summary>
public class RiverModelDiagnostic : MonoBehaviour
{
    private void Start()
    {
        LogModels();
    }

    [ContextMenu("Log Models Info")]
    public void LogModels()
    {
        RiverConfig config = RiverNetworkManager.Instance != null ? RiverNetworkManager.Instance.Config : null;
        if (config == null)
        {
            config = Resources.Load<RiverConfig>("RiverConfig");
        }

        if (config == null)
        {
            Debug.LogError("[RiverDiag] Không tìm thấy RiverConfig!");
            return;
        }

        Debug.Log($"[RiverDiag] --- BẮT ĐẦU CHẨN ĐOÁN MODEL SÔNG HỒ (PREFABS & OFFSETS) ---");
        CheckPrefab("SpringHead (Thượng nguồn)", config.springHeadPrefab);
        CheckPrefab("PondBasin (Hồ đọng)", config.pondBasinPrefab);
        CheckPrefab("Straight (Suối thẳng)", config.straightPrefab);
        CheckPrefab("Curve60 (Suối cua 60)", config.curve60Prefab);
        CheckPrefab("Curve120 (Suối cua 120)", config.curve120Prefab);
        CheckPrefab("Fork3Way (Ngã ba)", config.fork3WayPrefab);
        CheckPrefab("Waterfall (Thác nước)", config.waterfallPrefab);
    }

    private void CheckPrefab(string label, GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[RiverDiag] {label}: NULL!");
            return;
        }

        MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>();
        MeshRenderer mr = prefab.GetComponentInChildren<MeshRenderer>();
        string meshName = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "NO_MESH";
        Vector3 boundsSize = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size : Vector3.zero;
        Vector3 boundsCenter = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.center : Vector3.zero;
        string matName = mr != null && mr.sharedMaterial != null ? mr.sharedMaterial.name : "NO_MAT";

        Debug.Log($"[RiverDiag] {label} | Prefab: '{prefab.name}' | Mesh: '{meshName}' (size: {boundsSize}, center: {boundsCenter}) | Mat: '{matName}' | LocalRot: {prefab.transform.localEulerAngles} | LocalScale: {prefab.transform.localScale}");
    }
}
