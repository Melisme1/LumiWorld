#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class InspectRiverModels
{
    [MenuItem("LumiWorld/Inspect River Models")]
    public static void Inspect()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/VinhWorkSpace/Props/Waterfall" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null)
            {
                MeshFilter mf = go.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Debug.Log($"[MODEL] {go.name} | Bounds: {mf.sharedMesh.bounds.size} | Center: {mf.sharedMesh.bounds.center}");
                }
            }
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/VinhWorkSpace/Prefabs/Waterfall" });
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null)
            {
                MeshFilter mf = go.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Mesh mesh = mf.sharedMesh;
                    Vector3[] verts = mesh.vertices;
                    Quaternion rot = go.transform.localRotation;
                    
                    // For Grotto: detailed profile
                    if (go.name.Contains("SpringGrotto"))
                    {
                        Quaternion finalRot = Quaternion.Euler(0f, 180f, 0f) * rot;
                        // Slices from Z = -0.35 to +0.25 (from cave back to pool front)
                        Debug.Log("--- GROTTO PROFILE (visualInstance local space) ---");
                        for (float z = -0.35f; z <= 0.35f; z += 0.05f)
                        {
                            float minY = float.MaxValue;
                            float maxY = float.MinValue;
                            float sumX = 0f; int count = 0;
                            for (int i = 0; i < verts.Length; i++)
                            {
                                Vector3 p = finalRot * verts[i];
                                if (Mathf.Abs(p.z - z) < 0.035f && Mathf.Abs(p.x) < 0.20f)
                                {
                                    if (p.y < minY) minY = p.y;
                                    if (p.y > maxY) maxY = p.y;
                                    sumX += p.x;
                                    count++;
                                }
                            }
                            if (count > 0)
                            {
                                float avgX = sumX / count;
                                Debug.Log($"[GROTTO TIER] Z={z:F2} | Y_range=[{minY:F3}, {maxY:F3}] | avgX={avgX:F3} | count={count}");
                            }
                        }
                    }
                    
                    // For Stream: detailed trench bed profile
                    if (go.name.Contains("Straight"))
                    {
                        Debug.Log("--- STRAIGHT STREAM PROFILE (prefab local space) ---");
                        for (float z = -0.50f; z <= 0.50f; z += 0.05f)
                        {
                            float minY = float.MaxValue;
                            float bestX = 0f;
                            for (int i = 0; i < verts.Length; i++)
                            {
                                Vector3 p = rot * verts[i];
                                if (Mathf.Abs(p.z - z) < 0.035f && Mathf.Abs(p.x) < 0.28f)
                                {
                                    if (p.y < minY)
                                    {
                                        minY = p.y;
                                        bestX = p.x;
                                    }
                                }
                            }
                            Debug.Log($"[STREAM TRENCH BED] Z={z:F2} | bedY={minY:F3} | bedX={bestX:F3}");
                        }
                    }

                    // For Corner Turn (Curve120): find trench bed along ports
                    if (go.name.Contains("CornerTurn"))
                    {
                        Debug.Log("--- CORNER TURN (CURVE) PROFILE (prefab local space) ---");
                        Vector3 minVert = Vector3.one * float.MaxValue;
                        Vector3 maxVert = Vector3.one * float.MinValue;
                        for (int i = 0; i < verts.Length; i++)
                        {
                            Vector3 p = rot * verts[i];
                            minVert = Vector3.Min(minVert, p);
                            maxVert = Vector3.Max(maxVert, p);
                        }
                        Debug.Log($"[CORNER TURN BOUNDS] min={minVert:F3} max={maxVert:F3}");
                        
                        // Check trench lowest points in a grid
                        for (float x = -0.5f; x <= 0.5f; x += 0.1f)
                        {
                            for (float z = -0.5f; z <= 0.5f; z += 0.1f)
                            {
                                float minY = float.MaxValue;
                                for (int i = 0; i < verts.Length; i++)
                                {
                                    Vector3 p = rot * verts[i];
                                    if (Mathf.Abs(p.x - x) < 0.06f && Mathf.Abs(p.z - z) < 0.06f)
                                    {
                                        if (p.y < minY) minY = p.y;
                                    }
                                }
                                if (minY < -0.05f && minY > -0.2f)
                                {
                                    Debug.Log($"[CORNER BED] ({x:F2}, {z:F2}) -> Y={minY:F3}");
                                }
                            }
                        }
                    }

                    // For Pond Basin: find pond center and inlet canal
                    if (go.name.Contains("PondBasin"))
                    {
                        Debug.Log("--- POND BASIN PROFILE (prefab local space) ---");
                        Vector3 minVert = Vector3.one * float.MaxValue;
                        Vector3 maxVert = Vector3.one * float.MinValue;
                        for (int i = 0; i < verts.Length; i++)
                        {
                            Vector3 p = rot * verts[i];
                            minVert = Vector3.Min(minVert, p);
                            maxVert = Vector3.Max(maxVert, p);
                        }
                        Debug.Log($"[POND BASIN BOUNDS] min={minVert:F3} max={maxVert:F3}");
                    }

                    // For Waterfall Ramp: find top and bottom
                    if (go.name.Contains("Waterfall"))
                    {
                        Debug.Log("--- WATERFALL RAMP PROFILE (prefab local space) ---");
                        Vector3 minVert = Vector3.one * float.MaxValue;
                        Vector3 maxVert = Vector3.one * float.MinValue;
                        for (int i = 0; i < verts.Length; i++)
                        {
                            Vector3 p = rot * verts[i];
                            minVert = Vector3.Min(minVert, p);
                            maxVert = Vector3.Max(maxVert, p);
                        }
                        Debug.Log($"[WATERFALL BOUNDS] min={minVert:F3} max={maxVert:F3}");
                        for (float z = -0.5f; z <= 0.5f; z += 0.1f)
                        {
                            float minY = float.MaxValue;
                            for (int i = 0; i < verts.Length; i++)
                            {
                                Vector3 p = rot * verts[i];
                                if (Mathf.Abs(p.z - z) < 0.06f && Mathf.Abs(p.x) < 0.25f)
                                {
                                    if (p.y < minY) minY = p.y;
                                }
                            }
                            Debug.Log($"[WATERFALL BED] Z={z:F2} | minY={minY:F3}");
                        }
                    }
                }
            }
        }

        // Test Scenario Hex(-2, 1, 1) to Hex(-2, 0, 2)
        HexCoordinates h1 = new HexCoordinates(-2, 1, 1);
        HexCoordinates h2 = new HexCoordinates(-2, 0, 2);
        Vector3 w1 = HexMetrics.HexToWorldPosition(h1, 0f);
        Vector3 w2 = HexMetrics.HexToWorldPosition(h2, 0f);
        Vector3 diff1to2 = w2 - w1;
        Vector3 diff2to1 = w1 - w2;
        float ang1to2 = Mathf.Atan2(diff1to2.x, diff1to2.z) * Mathf.Rad2Deg;
        float ang2to1 = Mathf.Atan2(diff2to1.x, diff2to1.z) * Mathf.Rad2Deg;
        Debug.Log($"[SCENARIO TEST] H1={h1} pos={w1:F3} -> H2={h2} pos={w2:F3} | Dist={diff1to2.magnitude:F3} | Angle 1->2={ang1to2:F1}° | Angle 2->1={ang2to1:F1}°");
    }
}
#endif
