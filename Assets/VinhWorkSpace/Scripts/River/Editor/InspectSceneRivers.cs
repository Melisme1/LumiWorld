#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class InspectSceneRivers
{
    // Chạy thủ công qua Menu Unity khi cần kiểm tra Spline/Prefab: Tools / Menu item
    [MenuItem("LumiWorld/Dump All Rivers")]
    public static void DumpRiversManual()
    {
        DumpAllRivers("Manual Menu Request");
    }

    [MenuItem("LumiWorld/Simulate River Placement")]
    public static void SimulateAndInspect()
    {
        RiverConfig config = Resources.Load<RiverConfig>("RiverConfig");
        if (config == null)
        {
            Debug.LogError("Could not load RiverConfig from Resources!");
            return;
        }

        Debug.Log("================= [SIMULATE 3-TILE STRAIGHT RIVER TEST] =================");
        GameObject nmObj = new GameObject("TestRiverNetworkManager");
        RiverNetworkManager nm = nmObj.AddComponent<RiverNetworkManager>();
        nm.Config = config;

        // Place 3 tiles in direction 0 (Purely horizontal +X):
        // Hex(0, 0) -> Hex(1, 0) -> Hex(2, 0)
        GameObject t0 = new GameObject("Tile_0_0");
        GameObject t1 = new GameObject("Tile_1_0");
        GameObject t2 = new GameObject("Tile_2_0");
        t0.transform.position = HexMetrics.HexToWorldPosition(new HexCoordinates(0, 0), 1.0f);
        t1.transform.position = HexMetrics.HexToWorldPosition(new HexCoordinates(1, 0), 1.0f);
        t2.transform.position = HexMetrics.HexToWorldPosition(new HexCoordinates(2, 0), 1.0f);

        CardData dummyCard = ScriptableObject.CreateInstance<CardData>();
        dummyCard.cardID = "River";
        dummyCard.cardName = "River";
        dummyCard.cardType = CardType.Special;
        dummyCard.isRiverCard = true;

        nm.PlaceRiver(new HexCoordinates(0, 0), t0, dummyCard, t0.transform.position);
        nm.PlaceRiver(new HexCoordinates(1, 0), t1, dummyCard, t1.transform.position);
        nm.PlaceRiver(new HexCoordinates(2, 0), t2, dummyCard, t2.transform.position);

        Debug.Log($"Placed 3 river tiles. ContinuousRiverObjects count: {nm.ContinuousRiverObjects.Count}");
        foreach (var kvp in nm.ContinuousRiverObjects)
        {
            if (kvp.Value == null) continue;
            RiverSplineExtruder ext = kvp.Value.GetComponent<RiverSplineExtruder>();
            if (ext != null && ext.Container != null)
            {
                var spl = ext.Container.Splines[0];
                Debug.Log($"Spline: Length={spl.GetLength():F3}m, Knots={spl.Count}");
                for (int k = 0; k < spl.Count; k++)
                {
                    var knot = spl[k];
                    Debug.Log($"  Knot[{k}]: pos={knot.Position:F3}, tanIn={knot.TangentIn:F3}, tanOut={knot.TangentOut:F3}, rot={((Quaternion)knot.Rotation).eulerAngles:F1}°");
                }

                MeshFilter mf = ext.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Mesh m = mf.sharedMesh;
                    Debug.Log($"Extruded Mesh: vertexCount={m.vertexCount}, bounds={m.bounds}");
                    var verts = m.vertices;
                    for (int vi = 0; vi < verts.Length; vi += 4)
                    {
                        int endVi = Mathf.Min(vi + 4, verts.Length);
                        string ringStr = $"  Ring at [{vi}]: ";
                        for (int r = vi; r < endVi; r++)
                        {
                            Vector3 wv = ext.transform.TransformPoint(verts[r]);
                            ringStr += $"v{r}=({wv.x:F3}, {wv.y:F3}, {wv.z:F3}) ";
                        }
                        Debug.Log(ringStr);
                    }
                }
            }

            Transform trenchChain = kvp.Value.transform.Find("TrenchChain");
            if (trenchChain != null)
            {
                Debug.Log($"TrenchChain children count: {trenchChain.childCount}");
                for (int c = 0; c < trenchChain.childCount; c++)
                {
                    Transform ch = trenchChain.GetChild(c);
                    Debug.Log($"  Trench[{c}] {ch.name}: pos={ch.position:F3}, rot={ch.eulerAngles:F1}°, scale={ch.localScale:F3}");
                }
            }
        }

        Object.DestroyImmediate(t0);
        Object.DestroyImmediate(t1);
        Object.DestroyImmediate(t2);
        Object.DestroyImmediate(nmObj);
        Object.DestroyImmediate(dummyCard);
        Debug.Log("=========================================================================");
    }

    private static void InspectSegmentDetails(RiverTileSegment seg)
    {
        Debug.Log($"--- DETAILED INSPECTION: {seg.name} ({seg.pieceType}) ---");
        if (seg.VisualInstance != null)
        {
            MeshFilter mf = seg.VisualInstance.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds wb = mf.GetComponent<Renderer>().bounds;
                Debug.Log($"  VisualInstance: {seg.VisualInstance.name} | LocalPos={seg.VisualInstance.transform.localPosition:F3} | WorldBounds={wb}");
            }
        }

        RiverSplineExtruder extruder = seg.GetComponentInChildren<RiverSplineExtruder>(true);
        if (extruder != null)
        {
            MeshFilter wMf = extruder.GetComponent<MeshFilter>();
            if (wMf != null && wMf.sharedMesh != null)
            {
                Bounds wb = extruder.GetComponent<Renderer>().bounds;
                Debug.Log($"  WaterMesh (SplineExtruder): Vertices={wMf.sharedMesh.vertexCount} | WorldBounds={wb}");
                var verts = wMf.sharedMesh.vertices;
                if (verts.Length >= 4)
                {
                    Vector3 w0 = extruder.transform.TransformPoint(verts[0]);
                    Vector3 wMid = extruder.transform.TransformPoint(verts[verts.Length / 2]);
                    Vector3 wEnd = extruder.transform.TransformPoint(verts[verts.Length - 1]);
                    Debug.Log($"    Water Vert[0]={w0:F3} | Vert[mid]={wMid:F3} | Vert[end]={wEnd:F3}");
                }
            }
            var container = extruder.GetComponent<UnityEngine.Splines.SplineContainer>();
            if (container != null && container.Spline != null)
            {
                var spl = container.Spline;
                Debug.Log($"    Spline Knots count={spl.Count}:");
                for (int k = 0; k < spl.Count; k += Mathf.Max(1, spl.Count / 4))
                {
                    Vector3 wk = extruder.transform.TransformPoint((Vector3)spl[k].Position);
                    Debug.Log($"      Knot[{k}]: local={spl[k].Position:F3} -> world={wk:F3}");
                }
            }
        }

        Transform wrObj = seg.transform.Find("WaterRibbon");
        if (wrObj != null)
        {
            MeshFilter wMf = wrObj.GetComponent<MeshFilter>();
            if (wMf != null && wMf.sharedMesh != null)
            {
                Bounds wb = wrObj.GetComponent<Renderer>().bounds;
                Debug.Log($"  WaterMesh (RiverMeshBuilder): Vertices={wMf.sharedMesh.vertexCount} | WorldBounds={wb}");
            }
        }
    }

    private static void DumpAllRivers(string source)
    {
        RiverTileSegment[] segments = Object.FindObjectsByType<RiverTileSegment>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log($"================= [RIVER SCENE DUMP ({source}) - Total Segments: {segments.Length}] =================");
        foreach (var seg in segments)
        {
            if (seg == null) continue;
            Transform tr = seg.transform;
            Debug.Log($"SEGMENT: {seg.name} | Hex={seg.placedHex} | Piece={seg.pieceType} | flowStep={seg.flowStep} | RotY={seg.currentRotationY:F1}° | PortsMask={seg.connectedPortsMask}");
            Debug.Log($"  Tr Pos={tr.position:F3} | Rot={tr.eulerAngles:F1}°");

            if (seg.VisualInstance != null)
            {
                Transform vtr = seg.VisualInstance.transform;
                Debug.Log($"  VisualInstance: {seg.VisualInstance.name} | LocalPos={vtr.localPosition:F3} | LocalRot={vtr.localEulerAngles:F1}° | WorldPos={vtr.position:F3}");
            }
            else
            {
                Debug.Log("  VisualInstance: NULL");
            }


            RiverSplineExtruder extruder = seg.GetComponentInChildren<RiverSplineExtruder>(true);
            if (extruder != null)
            {
                Debug.Log($"  SplineExtruder: active={extruder.gameObject.activeSelf} | LocalPos={extruder.transform.localPosition:F3} | WorldPos={extruder.transform.position:F3}");
                var container = extruder.GetComponent<UnityEngine.Splines.SplineContainer>();
                if (container != null && container.Spline != null)
                {
                    var spl = container.Spline;
                    Debug.Log($"    Spline Knots ({spl.Count}):");
                    for (int k = 0; k < spl.Count; k++)
                    {
                        var knot = spl[k];
                        Vector3 worldKnot = extruder.transform.TransformPoint((Vector3)knot.Position);
                        Debug.Log($"      [{k}] local={knot.Position} -> world={worldKnot:F3}");
                    }
                }
            }

            MeshRenderer[] renderers = seg.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                Debug.Log($"    Renderer: {r.gameObject.name} (activeInHierarchy={r.gameObject.activeInHierarchy}) | Bounds={r.bounds} | Mat={(r.sharedMaterial != null ? r.sharedMaterial.name : "null")}");
            }
        }

        if (RiverNetworkManager.Instance != null && RiverNetworkManager.Instance.ContinuousRiverObjects != null)
        {
            Debug.Log($"--- CONTINUOUS RIVER SPLINE NETWORKS (Total: {RiverNetworkManager.Instance.ContinuousRiverObjects.Count}) ---");
            foreach (var kvp in RiverNetworkManager.Instance.ContinuousRiverObjects)
            {
                if (kvp.Value == null) continue;
                RiverSplineExtruder ext = kvp.Value.GetComponent<RiverSplineExtruder>();
                if (ext != null && ext.Container != null)
                {
                    Debug.Log($"  Network at Root {kvp.Key}: Splines count = {ext.Container.Splines.Count}");
                    for (int s = 0; s < ext.Container.Splines.Count; s++)
                    {
                        var spl = ext.Container.Splines[s];
                        Debug.Log($"    Spline[{s}]: Length={spl.GetLength():F2}m | Knots={spl.Count}");
                        for (int k = 0; k < spl.Count; k++)
                        {
                            var knot = spl[k];
                            Debug.Log($"      Knot[{k}]: pos={knot.Position:F3}, tanIn={knot.TangentIn:F3}, tanOut={knot.TangentOut:F3}, rot={((Quaternion)knot.Rotation).eulerAngles:F1}°");
                        }
                    }
                }
            }
        }
        Debug.Log("=================================================================================================");
    }

    [MenuItem("LumiWorld/Test All Prefab Orientations")]
    public static void TestAllPrefabOrientations()
    {
        string[] prefabNames = {
            "LushTerminalPondBasin",
            "HexCornerTurnStreamTrench",
            "HexFork3WayCrossroad",
            "WaterfallCascadeRamp",
            "Lush3-TierSpringGrotto"
        };

        Debug.Log("=== [TEST ALL 5 WATERFALL PREFABS ORIENTATION & BOUNDS] ===");
        foreach (string name in prefabNames)
        {
            string path = $"Assets/VinhWorkSpace/Prefabs/Waterfall/{name}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"Could not find prefab at: {path}");
                continue;
            }

            GameObject instance = Object.Instantiate(prefab);
            instance.name = $"Test_{name}";

            MeshFilter mf = instance.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds b = mf.GetComponent<Renderer>().bounds;
                Vector3[] verts = mf.sharedMesh.vertices;
                Vector3 lowAvg = Vector3.zero, highAvg = Vector3.zero;
                int lowCount = 0, highCount = 0;
                float minY = float.MaxValue, maxY = float.MinValue;
                foreach (var v in verts)
                {
                    Vector3 wv = instance.transform.TransformPoint(v);
                    if (wv.y < minY) minY = wv.y;
                    if (wv.y > maxY) maxY = wv.y;
                }
                float midY = (minY + maxY) * 0.5f;
                List<Vector3> lowVerts = new List<Vector3>();
                foreach (var v in verts)
                {
                    Vector3 wv = instance.transform.TransformPoint(v);
                    if (wv.y < minY + (maxY - minY) * 0.3f) lowVerts.Add(wv);
                }
                Debug.Log($"PREFAB: {name}\n" +
                          $"  Instance LocalRot={instance.transform.localEulerAngles:F1}° | LocalScale={instance.transform.localScale:F2}\n" +
                          $"  Renderer Bounds Center={b.center:F3}, Size={b.size:F3}\n" +
                          $"  Y Range: [{minY:F3} .. {maxY:F3}], Low Verts Count={lowVerts.Count}");
                if (lowVerts.Count >= 2)
                {
                    // Find extreme low points along X and Z
                    Vector3 minX = lowVerts[0], maxX = lowVerts[0], minZ = lowVerts[0], maxZ = lowVerts[0];
                    foreach (var lv in lowVerts)
                    {
                        if (lv.x < minX.x) minX = lv;
                        if (lv.x > maxX.x) maxX = lv;
                        if (lv.z < minZ.z) minZ = lv;
                        if (lv.z > maxZ.z) maxZ = lv;
                    }
                    Debug.Log($"    Channel Low Verts extremes: minX={minX:F3}, maxX={maxX:F3}, minZ={minZ:F3}, maxZ={maxZ:F3}");
                }
            }

            Object.DestroyImmediate(instance);
        }
        Debug.Log("===========================================================");
    }
}
#endif
