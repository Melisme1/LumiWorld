using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thuật toán Thích ứng và Tái phân bố Props sinh thái (Flora/Props Harmonization) khi khơi thông Sông/Hồ trên ô Biome.
/// 
/// NGUYÊN LÝ HOẠT ĐỘNG:
/// 1. Vùng chiếm dụng mặt nước động (Dynamic Water Footprint): 
///    Xác định chính xác lòng suối, lòng hang grotto hoặc lòng hồ dựa theo RiverPieceType và góc xoay.
/// 2. Phân loại thực vật thông minh (Intelligent Flora Classification):
///    - Cây lớn (Heavy Obstacles): Thu nhỏ dần và dọn dẹp để không chắn ngang dòng nước.
///    - Cây nhỏ, bụi hoa, sỏi đá (Movable Flora): ĐẨY DẠT RA HAI BÊN BỜ SUỐI / VEN HỒ (Riverbank Repulsion),
///      tạo thảm thực vật ôm viền bờ nước tự nhiên như game Preserve, thay vì xóa trụi trần ô hex!
///    - Thảm cỏ lót (FilledGround): Dọn cỏ ngập dưới đáy nước sâu, giữ lại cỏ chen vào khe đá ven bờ.
/// </summary>
public static class RiverPropHarmonizer
{
    private const float HEX_INNER_RADIUS = 0.8660254f; // HexMetrics.InnerRadius (Pointed-top)

    /// <summary>
    /// Điều chỉnh và tái phân bố hệ thống props trên ô hex sao cho hòa quyện tự nhiên với dòng suối/hồ nước mới đặt
    /// </summary>
    public static void HarmonizePropsForRiver(
        GameObject tileObj, 
        RiverPieceType pieceType, 
        float rotationY, 
        bool isInitialClosedSpring, 
        MonoBehaviour coroutineRunner)
    {
        HarmonizePropsForRiver(tileObj, pieceType, rotationY, isInitialClosedSpring, coroutineRunner, null);
    }

    /// <summary>
    /// Điều chỉnh và tái phân bố hệ thống props trên ô hex sao cho hòa quyện tự nhiên với dải sông thủ tục liên tục
    /// </summary>
    public static void HarmonizePropsForRiver(
        GameObject tileObj, 
        RiverPieceType pieceType, 
        float rotationY, 
        bool isInitialClosedSpring, 
        MonoBehaviour coroutineRunner,
        RiverTileSegment riverSegment)
    {
        if (tileObj == null) return;

        Vector3 tileCenter = tileObj.transform.position;
        float rad = -rotationY * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        List<Transform> allProps = new List<Transform>();

        // 1. Quét toàn bộ props trên ô tileObj
        for (int i = 0; i < tileObj.transform.childCount; i++)
        {
            Transform container = tileObj.transform.GetChild(i);
            if (container == null) continue;

            string cName = container.name;
            if (cName.StartsWith("River_") || cName.StartsWith("Water_") || cName.StartsWith("Waterfall_")) continue;

            CollectLeafProps(container, allProps);
        }

        // 2. VÀ QUÉT TOÀN BỘ CÁC PROPS NỐI (CONNECTOR PROPS) TRONG BIOME CONNECTIONS CONTAINER GẦN Ô NÀY!
        Transform connRoot = HexBiomeClusterConnector.Instance != null ? HexBiomeClusterConnector.Instance.ConnectionsContainer : null;
        if (connRoot != null)
        {
            for (int c = 0; c < connRoot.childCount; c++)
            {
                Transform connChild = connRoot.GetChild(c);
                if (connChild == null) continue;
                // TUYỆT ĐỐI BẢO VỆ MỐI NỐI HÀN RÃNH MESH ĐẤT (SeamBridge, CornerFiller, EdgeBridge)
                if (connChild.name.StartsWith("SeamBridge_") || 
                    connChild.name.StartsWith("EdgeBridge_") || 
                    connChild.name.StartsWith("CornerFiller_")) 
                {
                    continue;
                }

                float dToTile = Vector2.Distance(
                    new Vector2(connChild.position.x, connChild.position.z),
                    new Vector2(tileCenter.x, tileCenter.z)
                );
                if (dToTile < 1.45f)
                {
                    CollectLeafProps(connChild, allProps);
                }
            }
        }

        List<Transform> propsToReposition = new List<Transform>();
        List<Vector3> targetWorldPositions = new List<Vector3>();
        List<Transform> propsToRemove = new List<Transform>();

        int connectionCount = riverSegment != null ? CountBits(riverSegment.connectedPortsMask) : (isInitialClosedSpring ? 0 : 2);

        foreach (Transform prop in allProps)
        {
            if (prop == null) continue;

            bool isGrass = IsGrassOrFoliage(prop);
            bool isHeavy = IsHeavyObstacle(prop);

            // Tọa độ thế giới sang 2D tương đối so với tâm ô
            Vector2 worldDelta = new Vector2(prop.position.x - tileCenter.x, prop.position.z - tileCenter.z);

            // Chuyển sang hệ tọa độ cục bộ theo hướng dòng chảy (Local Alignment)
            Vector2 localPos = new Vector2(
                cos * worldDelta.x - sin * worldDelta.y,
                sin * worldDelta.x + cos * worldDelta.y
            );

            bool isInWater;
            bool isDeepWater;
            Vector3 targetWorldPos;

            if (riverSegment != null && riverSegment.CachedPaths != null && riverSegment.CachedPaths.Count > 0)
            {
                EvaluateWaterZoneSpline(
                    prop.position,
                    tileCenter,
                    riverSegment,
                    pieceType,
                    isInitialClosedSpring,
                    isHeavy,
                    isGrass,
                    out isInWater,
                    out isDeepWater,
                    out targetWorldPos
                );
            }
            else
            {
                Vector2 suggestedShorelineLocal;
                EvaluateWaterZone(
                    localPos, 
                    pieceType, 
                    isInitialClosedSpring, 
                    connectionCount,
                    isHeavy,
                    isGrass,
                    out isInWater, 
                    out isDeepWater, 
                    out suggestedShorelineLocal
                );

                Vector2 suggestedShorelineWorld = new Vector2(
                    cos * suggestedShorelineLocal.x + sin * suggestedShorelineLocal.y,
                    -sin * suggestedShorelineLocal.x + cos * suggestedShorelineLocal.y
                );

                targetWorldPos = new Vector3(
                    tileCenter.x + suggestedShorelineWorld.x,
                    prop.position.y,
                    tileCenter.z + suggestedShorelineWorld.y
                );
            }

            if (!isInWater)
            {
                // Prop nằm hoàn toàn trên vùng đất khô ven bờ -> Giữ nguyên vị trí tuyệt đẹp của nó!
                continue;
            }

            // Các prop nối cạnh (Connector props từ Biome cluster) nếu nằm trong hành lang sông: XÓA BỎ NGAY
            // Cây lớn hoặc cỏ dại/lúa mọc ngập sâu trong lòng nước: XÓA BỎ để không che vòm trời và không mọc chắn giữa dòng suối
            bool isConnectorProp = (prop.name.StartsWith("Connector") || prop.name.StartsWith("ClusterEdge") || prop.name.StartsWith("BorderMicroGrass"));
            if (isConnectorProp || isHeavy || (isGrass && isDeepWater))
            {
                propsToRemove.Add(prop);
                continue;
            }

            // Bụi hoa dại, cây bụi con, sỏi đá ven bờ:
            // ĐẨY DẠT RA HAI BÊN BỜ SUỐI / ÔM VIỀN SƯỜN ĐÁ (Riverbank Repulsion)
            // Giữ lại tối đa hoa cỏ sinh thái trên ô, không xóa trụi!
            Vector2 delta2D = new Vector2(targetWorldPos.x - tileCenter.x, targetWorldPos.z - tileCenter.z);
            float maxR = HEX_INNER_RADIUS * 0.95f;
            if (delta2D.magnitude > maxR)
            {
                delta2D = delta2D.normalized * (HEX_INNER_RADIUS * 0.88f);
                targetWorldPos = new Vector3(tileCenter.x + delta2D.x, prop.position.y, tileCenter.z + delta2D.y);
            }

            propsToReposition.Add(prop);
            targetWorldPositions.Add(targetWorldPos);
        }

        // Thực thi di chuyển dạt bờ mềm mại
        for (int k = 0; k < propsToReposition.Count; k++)
        {
            Transform prop = propsToReposition[k];
            Vector3 targetPos = targetWorldPositions[k];

            if (coroutineRunner != null && Application.isPlaying)
            {
                coroutineRunner.StartCoroutine(AnimateReposition(prop, targetPos, 0.32f));
            }
            else if (prop != null)
            {
                prop.position = targetPos;
            }
        }

        // Thực thi xóa bỏ các vật cản nặng và cỏ mọc trong lòng nước/hang đá
        HashSet<GameObject> destroyedObjects = new HashSet<GameObject>();
        foreach (var prop in propsToRemove)
        {
            if (prop == null) continue;

            Transform toDestroy = prop;
            while (toDestroy.parent != null && 
                   toDestroy.parent != tileObj.transform && 
                   toDestroy.parent != connRoot &&
                   !toDestroy.parent.name.StartsWith("Habitat_") && 
                   !toDestroy.parent.name.StartsWith("FilledGround_") &&
                   !toDestroy.parent.name.StartsWith("Hex_"))
            {
                toDestroy = toDestroy.parent;
            }

            GameObject go = toDestroy.gameObject;
            if (destroyedObjects.Contains(go)) continue;
            destroyedObjects.Add(go);

            go.SetActive(false);
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }
    }

    /// <summary>
    /// Thu thập đệ quy tất cả các prop con thực tế có hiển thị hình ảnh (leaf nodes)
    /// </summary>
    private static void CollectLeafProps(Transform parent, List<Transform> result)
    {
        if (parent == null) return;

        // Nếu container này chứa danh sách con (như FilledGround_GrassCarpet, Habitat_...)
        if (parent.childCount > 0 && parent.GetComponent<MeshRenderer>() == null)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null)
                {
                    CollectLeafProps(child, result);
                }
            }
        }
        else
        {
            result.Add(parent);
        }
    }

    /// <summary>
    /// Kiểm tra xem một prop có phải là cỏ dại, thảm cỏ hay mạ con (FilledGround, Grass clump, Tussock, Reeds, Weed)
    /// </summary>
    private static bool IsGrassOrFoliage(Transform prop)
    {
        if (prop == null) return false;

        string name = prop.name.ToLowerInvariant();
        if (name.Contains("tussock") || name.Contains("grass") || name.Contains("tuft") || 
            name.Contains("clump") || name.Contains("reed") || name.Contains("weed") || 
            name.Contains("carpet") || name.Contains("pampas") || name.Contains("cattail") || 
            name.Contains("wheat") || name.Contains("undergrowth") || name.Contains("sedge") ||
            name.Contains("sprout") || name.Contains("seammicrograss"))
        {
            return true;
        }

        MeshFilter mf = prop.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            string mName = mf.sharedMesh.name.ToLowerInvariant();
            if (mName.Contains("tussock") || mName.Contains("reed") || mName.Contains("grass") || 
                mName.Contains("tuft") || mName.Contains("clump") || mName.Contains("wheat") ||
                mName.Contains("sprout"))
            {
                return true;
            }
        }

        Transform p = prop.parent;
        while (p != null)
        {
            string pName = p.name.ToLowerInvariant();
            if (pName.StartsWith("filledground") || pName.Contains("sprout") || pName.Contains("micrograss")) return true;
            if (pName.StartsWith("habitat_") || pName.StartsWith("tile_") || pName.StartsWith("biomeconnections")) break;
            p = p.parent;
        }

        return false;
    }

    /// <summary>
    /// Đánh giá vùng nước dựa trên danh sách Spline Path thực tế của dải sông thủ tục
    /// </summary>
    private static void EvaluateWaterZoneSpline(
        Vector3 propWorldPos,
        Vector3 tileCenter,
        RiverTileSegment segment,
        RiverPieceType pieceType,
        bool isInitialClosedSpring,
        bool isHeavy,
        bool isGrass,
        out bool isInWater,
        out bool isDeepWater,
        out Vector3 suggestedShorelineWorld)
    {
        isInWater = false;
        isDeepWater = false;
        suggestedShorelineWorld = propWorldPos;

        // 1. Kiểm tra Non bộ Grotto nếu là SpringHead (Scale ~0.68)
        if (pieceType == RiverPieceType.SpringHead)
        {
            Vector2 delta2D = new Vector2(propWorldPos.x - tileCenter.x, propWorldPos.z - tileCenter.z);
            float distToCenter = delta2D.magnitude;
            float clearRadius = isHeavy ? 0.48f : 0.35f;
            if (distToCenter < clearRadius)
            {
                isInWater = true;
                isDeepWater = distToCenter < 0.22f;
                Vector2 dir = (distToCenter > 0.01f) ? delta2D.normalized : Vector2.left;
                suggestedShorelineWorld = new Vector3(tileCenter.x + dir.x * 0.42f, propWorldPos.y, tileCenter.z + dir.y * 0.42f);
                return;
            }
        }
        else if (pieceType == RiverPieceType.PondBasin)
        {
            Vector3 pondCenterWorld = segment.transform.TransformPoint(segment.GetPondCenterLocal());
            Vector2 delta2D = new Vector2(propWorldPos.x - pondCenterWorld.x, propWorldPos.z - pondCenterWorld.z);
            float distToCenter = delta2D.magnitude;
            float clearRadius = isHeavy ? 0.45f : 0.35f;
            if (distToCenter < clearRadius)
            {
                isInWater = true;
                isDeepWater = distToCenter < 0.20f;
                Vector2 dir = (distToCenter > 0.01f) ? delta2D.normalized : Vector2.right;
                suggestedShorelineWorld = new Vector3(pondCenterWorld.x + dir.x * 0.40f, propWorldPos.y, pondCenterWorld.z + dir.y * 0.40f);
                return;
            }
        }

        // 2. Kiểm tra dọc theo tất cả các đường suối CachedPaths
        float minDistance = float.MaxValue;
        Vector3 bestNormal = Vector3.forward;
        Vector3 bestWaypointWorld = Vector3.zero;

        if (segment.CachedPaths != null)
        {
            Vector3 segPos = segment.transform.position;
            foreach (var path in segment.CachedPaths)
            {
                for (int i = 0; i < path.Count; i++)
                {
                    // path[i].position đã là offset không gian thế giới (unrotated) tương đối so với tâm ô hex
                    Vector3 wpWorld = segPos + path[i].position;
                    float d = Vector2.Distance(new Vector2(propWorldPos.x, propWorldPos.z), new Vector2(wpWorld.x, wpWorld.z));
                    if (d < minDistance)
                    {
                        minDistance = d;
                        bestNormal = path[i].normal;
                        bestWaypointWorld = wpWorld;
                    }
                }
            }
        }

        float halfWater = 0.10f;
        // Bán kính dọn dẹp cản trở: Cây lớn 0.38m, Cỏ và hoa 0.18m (đảm bảo hoa bụi không đè lên mép mương đá 0.14m)
        float bankOuter = isHeavy ? 0.38f : 0.18f;

        if (minDistance < bankOuter)
        {
            isInWater = true;
            isDeepWater = (minDistance < 0.12f);

            Vector3 toProp = propWorldPos - bestWaypointWorld;
            toProp.y = 0f;
            float sign = Vector3.Dot(toProp, bestNormal) >= 0f ? 1f : -1f;

            // Đẩy dạt bụi hoa ra ngoài mép bờ đá (0.22m) để hoa nằm xung quanh bờ chứ không đè lên suối
            Vector3 pushTarget = bestWaypointWorld + bestNormal * (sign * 0.22f);
            pushTarget.y = propWorldPos.y;
            suggestedShorelineWorld = pushTarget;
        }
    }

    /// <summary>
    /// Đánh giá vùng nước của mảnh suối:
    /// - isInWater: Có chạm vào lòng nước / vách đá của suối không
    /// - isDeepWater: Có nằm sâu dưới đáy nước không (dùng để lọc thảm cỏ ngập)
    /// - suggestedShorelineLocal: Vị trí bờ đất gần nhất theo hệ tọa độ cục bộ
    /// </summary>
    private static void EvaluateWaterZone(
        Vector2 localPos,
        RiverPieceType pieceType,
        bool isInitialClosedSpring,
        int connectionCount,
        bool isHeavy,
        bool isGrass,
        out bool isInWater,
        out bool isDeepWater,
        out Vector2 suggestedShorelineLocal)
    {
        isInWater = false;
        isDeepWater = false;
        suggestedShorelineLocal = localPos;

        // 1. THƯỢNG NGUỒN (SpringHead) - Grotto 3 Tầng Non Nước (scale ~0.68)
        if (pieceType == RiverPieceType.SpringHead)
        {
            float dist = localPos.magnitude;
            float clearR = isHeavy ? 0.52f : 0.40f;
            float corridorX = isHeavy ? 0.45f : 0.32f;
            bool inGrottoBody = dist < clearR;
            bool inFrontExitCorridor = (localPos.y > 0.00f && localPos.y < 0.75f && Mathf.Abs(localPos.x) < corridorX);

            if (inGrottoBody || inFrontExitCorridor)
            {
                isInWater = true;
                isDeepWater = dist < 0.25f;

                if (localPos.y < -0.05f)
                {
                    float targetX = Mathf.Clamp(localPos.x * 1.1f, -0.32f, 0.32f);
                    suggestedShorelineLocal = new Vector2(targetX, -0.46f);
                }
                else
                {
                    float signX = (localPos.x >= 0f) ? 1f : -1f;
                    float targetY = Mathf.Clamp(localPos.y, -0.05f, 0.30f);
                    suggestedShorelineLocal = new Vector2(signX * 0.44f, targetY);
                }
            }
            return;
        }

        // 2. HỒ ĐỌNG NƯỚC (PondBasin)
        if (pieceType == RiverPieceType.PondBasin)
        {
            float dist = localPos.magnitude;
            float pondRadius = isHeavy ? 0.45f : 0.34f;

            if (dist < pondRadius)
            {
                isInWater = true;
                isDeepWater = dist < 0.20f;

                Vector2 dir = (dist > 0.01f) ? localPos.normalized : Vector2.right;
                suggestedShorelineLocal = dir * 0.38f;
            }
            return;
        }

        // 3. SUỐI THẲNG (Straight - scale 0.28)
        if (pieceType == RiverPieceType.Straight)
        {
            float corridorHalfWidth = isHeavy ? 0.38f : 0.18f;
            bool isSingleEndInlet = connectionCount <= 1;
            bool inCorridorY = isSingleEndInlet ? (localPos.y > -0.05f && localPos.y < 1.05f) : (Mathf.Abs(localPos.y) < 1.05f);

            if (Mathf.Abs(localPos.x) < corridorHalfWidth && inCorridorY)
            {
                isInWater = true;
                isDeepWater = Mathf.Abs(localPos.x) < 0.12f;

                float signX = (localPos.x >= 0f) ? 1f : -1f;
                suggestedShorelineLocal = new Vector2(signX * 0.22f, localPos.y);
            }
            return;
        }

        // 4. SUỐI CUA (Curve60, Curve120) VÀ NGÃ BA (Fork3Way)
        {
            float centerDist = localPos.magnitude;
            float clearR = isHeavy ? 0.45f : 0.35f;
            if (centerDist < clearR)
            {
                isInWater = true;
                isDeepWater = centerDist < 0.18f;

                Vector2 dir = (centerDist > 0.01f) ? localPos.normalized : Vector2.up;
                suggestedShorelineLocal = dir * 0.38f;
            }
        }
    }

    private static int CountBits(int mask)
    {
        int count = 0;
        while (mask > 0)
        {
            count += (mask & 1);
            mask >>= 1;
        }
        return count;
    }

    /// <summary>
    /// Kiểm tra xem một prop có phải là cây gỗ lớn / đá tảng (không thể mọc trong lòng suối/hồ)
    /// </summary>
    private static bool IsHeavyObstacle(Transform prop)
    {
        if (prop == null) return false;

        string name = prop.name.ToLowerInvariant();
        if (name.Contains("tree") || name.Contains("oak") || name.Contains("birch") || 
            name.Contains("pine") || name.Contains("trunk") || name.Contains("boulder") || 
            name.Contains("rock_large") || name.Contains("anchor") || name.Contains("gnarled") || 
            name.Contains("windswept") || name.Contains("log") || name.Contains("stump") ||
            name.Contains("canopy") || name.Contains("branch") || name.Contains("clustercenter"))
        {
            return true;
        }

        // Kiểm tra toàn bộ phả hệ các node cha
        Transform p = prop.parent;
        while (p != null)
        {
            string pName = p.name.ToLowerInvariant();
            if (pName.Contains("tree") || pName.Contains("trunk") || pName.Contains("clustercenter") || pName.Contains("connectormedprop"))
                return true;
            if (pName.StartsWith("habitat_") || pName.StartsWith("tile_") || pName.StartsWith("biomeconnections")) break;
            p = p.parent;
        }

        // Chỉ những cây/khối đá cao thực sự (> 0.9m) mới là vật cản nặng chắn suối
        MeshFilter mf = prop.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            float height = mf.sharedMesh.bounds.size.y * prop.lossyScale.y;
            if (height > 0.90f) return true;
        }

        return false;
    }

    /// <summary>
    /// Diễn hoạt mượt mà dịch chuyển bụi hoa/cây con dạt bờ (Riverbank Repulsion)
    /// </summary>
    private static IEnumerator AnimateReposition(Transform prop, Vector3 targetPos, float duration)
    {
        if (prop == null) yield break;

        Vector3 startPos = prop.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (prop == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            prop.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        if (prop != null)
        {
            prop.position = targetPos;
        }
    }

    /// <summary>
    /// Diễn hoạt thu nhỏ dần (Pop-out scale down) và tiêu biến cây lớn trong lòng suối
    /// </summary>
    private static IEnumerator AnimateDisappearAndDestroy(Transform prop, float duration)
    {
        if (prop == null) yield break;

        Vector3 initialScale = prop.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (prop == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            prop.localScale = Vector3.Lerp(initialScale, Vector3.zero, t * t);
            yield return null;
        }

        if (prop != null)
        {
            Object.Destroy(prop.gameObject);
        }
    }
}
