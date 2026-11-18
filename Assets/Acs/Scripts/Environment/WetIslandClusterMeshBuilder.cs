using System;
using System.Collections.Generic;
using UnityEngine;

namespace LumiWorld.Acs
{
    [Serializable]
    public struct WetIslandClusterSurfaceSettings
    {
        public float tileHeight;
        public float outerBevelWidth;
        public float outerBevelDrop;
        public float dryInteriorSeamDepth;
        public float dryInteriorSeamWidth;
        public float shoreStartMask;
        public float waterThresholdMask;
        public float maxBasinDepth;
        public float waterSurfaceDrop;
        public float streamSlopeDrop;
        public Vector2 streamSlopeDir;
        public int radialSubdivisions;

        public static WetIslandClusterSurfaceSettings CreateDefault()
        {
            return new WetIslandClusterSurfaceSettings
            {
                tileHeight = 1.055f,
                outerBevelWidth = 0.082f,
                outerBevelDrop = 0.052f,
                dryInteriorSeamDepth = 0.036f,
                dryInteriorSeamWidth = 0.072f,
                shoreStartMask = 0.04f,
                waterThresholdMask = 0.34f,
                maxBasinDepth = 0.42f,
                waterSurfaceDrop = 0.065f,
                streamSlopeDrop = 0.0f,
                streamSlopeDir = new Vector2(0.78f, -0.62f),
                radialSubdivisions = 22
            };
        }
    }

    /// <summary>
    /// Builds watertight 3/6/10-hex cluster ground meshes (with continuous sunken water basins and
    /// seam-free submerged beds) plus matching water sheet meshes and Streamstone rock/pebble features.
    /// </summary>
    public static class WetIslandClusterMeshBuilder
    {
        private static readonly Vector2Int[] ClockwiseNeighborOffsets = new Vector2Int[]
        {
            new Vector2Int(0, 1),   // 0: NE
            new Vector2Int(1, 0),   // 1: E
            new Vector2Int(1, -1),  // 2: SE
            new Vector2Int(0, -1),  // 3: SW
            new Vector2Int(-1, 0),  // 4: W
            new Vector2Int(-1, 1),  // 5: NW
        };

        private static readonly Vector3[] PointyHexCorners = new Vector3[]
        {
            new Vector3(0f, 0f, HexMetrics.OuterRadius),
            new Vector3(HexMetrics.InnerRadius, 0f, 0.5f * HexMetrics.OuterRadius),
            new Vector3(HexMetrics.InnerRadius, 0f, -0.5f * HexMetrics.OuterRadius),
            new Vector3(0f, 0f, -HexMetrics.OuterRadius),
            new Vector3(-HexMetrics.InnerRadius, 0f, -0.5f * HexMetrics.OuterRadius),
            new Vector3(-HexMetrics.InnerRadius, 0f, 0.5f * HexMetrics.OuterRadius),
        };

        /// <summary>
        /// Returns upright equilateral triangle hex coordinates (3, 6, or 10 hexes) centered cleanly around X=0.
        /// </summary>
        public static List<HexCoordinates> GetUprightTriangleHexes(int clusterSize)
        {
            int rows = clusterSize <= 3 ? 2 : (clusterSize <= 6 ? 3 : 4);
            var list = new List<HexCoordinates>(clusterSize);

            for (int r = 0; r < rows; r++)
            {
                int countInRow = rows - r;
                // Choose starting q so row is centered around X = 0
                // Since x = (2*q + r), to center countInRow tiles we want average (2*q + r) ~ 0
                int qStart = -Mathf.FloorToInt((countInRow - 1 + r) * 0.5f);
                for (int i = 0; i < countInRow; i++)
                {
                    list.Add(new HexCoordinates(qStart + i, r));
                }
            }
            return list;
        }

        public static void ComputeClusterBoundsXZ(
            IReadOnlyList<HexCoordinates> hexes,
            out Vector2 minXZ,
            out Vector2 maxXZ,
            out Vector3 centroidXZ)
        {
            minXZ = new Vector2(float.MaxValue, float.MaxValue);
            maxXZ = new Vector2(float.MinValue, float.MinValue);
            Vector3 sum = Vector3.zero;

            for (int i = 0; i < hexes.Count; i++)
            {
                Vector3 c = HexMetrics.HexToWorldPosition(hexes[i], 0f);
                sum += c;
                minXZ.x = Mathf.Min(minXZ.x, c.x - HexMetrics.InnerRadius);
                maxXZ.x = Mathf.Max(maxXZ.x, c.x + HexMetrics.InnerRadius);
                minXZ.y = Mathf.Min(minXZ.y, c.z - HexMetrics.OuterRadius);
                maxXZ.y = Mathf.Max(maxXZ.y, c.z + HexMetrics.OuterRadius);
            }

            centroidXZ = hexes.Count > 0 ? (sum / hexes.Count) : Vector3.zero;
        }

        /// <summary>
        /// Maps cluster local (x, z) to the upright triangle UV space [0.12..0.88, 0.16..0.88] used by our WaterMasks.
        /// </summary>
        public static Vector2 WorldXZToMaskUV(float x, float z, Vector2 minXZ, Vector2 maxXZ)
        {
            float tx = Mathf.InverseLerp(minXZ.x, maxXZ.x, x);
            float tz = Mathf.InverseLerp(minXZ.y, maxXZ.y, z);
            float u = Mathf.Lerp(0.12f, 0.88f, tx);
            float v = Mathf.Lerp(0.16f, 0.88f, tz);
            return new Vector2(u, v);
        }

        public static Vector3 MaskUVToLocalXZ(float u, float vPngTopDown, Vector2 minXZ, Vector2 maxXZ, Vector3 centroidXZ)
        {
            float vUnity = 1f - vPngTopDown;
            float tx = Mathf.InverseLerp(0.12f, 0.88f, u);
            float tz = Mathf.InverseLerp(0.16f, 0.88f, vUnity);
            float wx = Mathf.Lerp(minXZ.x, maxXZ.x, tx) - centroidXZ.x;
            float wz = Mathf.Lerp(minXZ.y, maxXZ.y, tz) - centroidXZ.z;
            return new Vector3(wx, 0f, wz);
        }

        public static float SampleWaterMaskBilinear(Texture2D maskTex, Vector2 uv)
        {
            if (maskTex == null) return 0f;
            float u = Mathf.Clamp01(uv.x);
            float v = Mathf.Clamp01(uv.y);
            const float d = 0.0065f;
            float c = maskTex.GetPixelBilinear(u, v).r;
            float n = maskTex.GetPixelBilinear(u, Mathf.Clamp01(v + d)).r;
            float s = maskTex.GetPixelBilinear(u, Mathf.Clamp01(v - d)).r;
            float e = maskTex.GetPixelBilinear(Mathf.Clamp01(u + d), v).r;
            float w = maskTex.GetPixelBilinear(Mathf.Clamp01(u - d), v).r;
            return c * 0.40f + (n + s + e + w) * 0.15f;
        }

        public static Mesh BuildGroundClusterMesh(
            string meshName,
            IReadOnlyList<HexCoordinates> hexes,
            Texture2D waterMask,
            WetIslandClusterSurfaceSettings settings)
        {
            ComputeClusterBoundsXZ(hexes, out Vector2 minXZ, out Vector2 maxXZ, out Vector3 centroid);

            var hexSet = new HashSet<HexCoordinates>(hexes);
            var hexCentersLocal = new Vector3[hexes.Count];
            for (int i = 0; i < hexes.Count; i++)
            {
                hexCentersLocal[i] = HexMetrics.HexToWorldPosition(hexes[i], 0f) - centroid;
            }

            // Collect all interior shared hex segments & outer perimeter segments
            var interiorSegments = new List<(Vector2 a, Vector2 b)>();
            var outerDirectedSegments = new List<(Vector2 a, Vector2 b, Vector2 outwardNormal)>();

            for (int h = 0; h < hexes.Count; h++)
            {
                HexCoordinates hc = hexes[h];
                Vector3 center = hexCentersLocal[h];

                for (int dir = 0; dir < 6; dir++)
                {
                    Vector3 cA = center + PointyHexCorners[dir];
                    Vector3 cB = center + PointyHexCorners[(dir + 1) % 6];
                    Vector2 pA = new Vector2(cA.x, cA.z);
                    Vector2 pB = new Vector2(cB.x, cB.z);

                    HexCoordinates nb = new HexCoordinates(
                        hc.Q + ClockwiseNeighborOffsets[dir].x,
                        hc.R + ClockwiseNeighborOffsets[dir].y);

                    if (hexSet.Contains(nb))
                    {
                        if (hc.Q < nb.Q || (hc.Q == nb.Q && hc.R < nb.R))
                        {
                            interiorSegments.Add((pA, pB));
                        }
                    }
                    else
                    {
                        Vector2 edge = pB - pA;
                        Vector2 outward = new Vector2(-edge.y, edge.x).normalized;
                        outerDirectedSegments.Add((pA, pB, outward));
                    }
                }
            }

            int N = Mathf.Clamp(settings.radialSubdivisions, 6, 32);
            var vertices = new List<Vector3>(4096);
            var uvTop = new List<Vector2>(4096);
            var uvMask = new List<Vector2>(4096);
            var colors = new List<Color>(4096);
            var triangles = new List<int>(12288);

            // Spatial weld dictionary for top surface so adjacent hexes share single watertight vertices
            var weldedTopLookup = new Dictionary<long, int>(4096);

            int GetOrAddTopVertex(Vector3 localFlatPos)
            {
                long qx = Mathf.RoundToInt(localFlatPos.x * 500f);
                long qz = Mathf.RoundToInt(localFlatPos.z * 500f);
                long key = (qx << 32) ^ (uint)qz;

                if (weldedTopLookup.TryGetValue(key, out int existingIdx))
                {
                    return existingIdx;
                }

                Vector2 p2 = new Vector2(localFlatPos.x, localFlatPos.z);
                float distOuter = MinDistanceToSegments(p2, outerDirectedSegments);
                float distInterior = interiorSegments.Count > 0 ? MinDistanceToSegments(p2, interiorSegments) : 999f;

                float worldX = localFlatPos.x + centroid.x;
                float worldZ = localFlatPos.z + centroid.z;
                Vector2 mUV = WorldXZToMaskUV(worldX, worldZ, minXZ, maxXZ);
                float maskVal = SampleWaterMaskBilinear(waterMask, mUV);

                // Dry factor is 1 on dry ground, fading to 0 as we enter the shore/water basin
                // so interior hex seams completely vanish inside the pond/stream!
                float dryFactor = 1f - Smoothstep(settings.shoreStartMask * 0.5f, settings.waterThresholdMask * 0.92f, maskVal);

                float y = settings.tileHeight;

                // 1) Concave water basin depression calibrated so y crosses (tileHeight - waterSurfaceDrop)
                // at maskVal == waterThresholdMask, giving UberStylizedWater a smooth 3D shoreline intersection!
                if (maskVal > settings.shoreStartMask && settings.maxBasinDepth > 0.0001f)
                {
                    if (maskVal <= settings.waterThresholdMask)
                    {
                        float shoreT = Smoothstep(settings.shoreStartMask, settings.waterThresholdMask, maskVal);
                        y -= shoreT * settings.waterSurfaceDrop;
                    }
                    else
                    {
                        float rawT = Mathf.Clamp01((maskVal - settings.waterThresholdMask) / Mathf.Max(0.01f, 0.90f - settings.waterThresholdMask));
                        // Steeper concave bowl curve (pow 0.62) so the pond drops into deep water quickly past the sandy rim
                        float waterCoreT = Mathf.Pow(rawT, 0.62f);
                        float extraDepth = Mathf.Max(0.02f, settings.maxBasinDepth - settings.waterSurfaceDrop);
                        y -= settings.waterSurfaceDrop + waterCoreT * extraDepth;

                        // Optional gentle stream slope from headwater to outflow inside the water channel
                        if (settings.streamSlopeDrop > 0.001f)
                        {
                            float along = Vector2.Dot(new Vector2(mUV.x - 0.5f, (1f - mUV.y) - 0.55f), settings.streamSlopeDir.normalized);
                            y -= Mathf.Clamp(along + 0.25f, 0f, 0.6f) * settings.streamSlopeDrop * waterCoreT;
                        }
                    }
                }

                // 2) Subtle dry interior hex seam groove (only on dry land!)
                float seamCrease = 0f;
                if (distInterior < settings.dryInteriorSeamWidth && dryFactor > 0.001f)
                {
                    float tSeam = 1f - Mathf.Clamp01(distInterior / settings.dryInteriorSeamWidth);
                    tSeam = tSeam * tSeam * (3f - 2f * tSeam);
                    seamCrease = tSeam * dryFactor;
                    y -= seamCrease * settings.dryInteriorSeamDepth;
                }

                // 3) Outer cluster perimeter bevel
                float bevelFactor = 0f;
                if (distOuter < settings.outerBevelWidth)
                {
                    float tBevel = 1f - Mathf.Clamp01(distOuter / settings.outerBevelWidth);
                    tBevel = tBevel * tBevel;
                    bevelFactor = tBevel * dryFactor;
                    y -= bevelFactor * settings.outerBevelDrop;
                }

                int newIdx = vertices.Count;
                vertices.Add(new Vector3(localFlatPos.x, y, localFlatPos.z));
                uvTop.Add(new Vector2(localFlatPos.x, localFlatPos.z));
                uvMask.Add(mUV);
                colors.Add(new Color(seamCrease, bevelFactor * 0.35f, 1f, maskVal));
                weldedTopLookup[key] = newIdx;
                return newIdx;
            }

            // Triangulate each hex's 6 sectors using N radial subdivisions
            var ringIndices = new int[N + 1][];
            for (int i = 0; i <= N; i++) ringIndices[i] = new int[i + 1];

            for (int h = 0; h < hexes.Count; h++)
            {
                Vector3 c = hexCentersLocal[h];
                for (int dir = 0; dir < 6; dir++)
                {
                    Vector3 cA = c + PointyHexCorners[dir];
                    Vector3 cB = c + PointyHexCorners[(dir + 1) % 6];

                    for (int i = 0; i <= N; i++)
                    {
                        for (int j = 0; j <= i; j++)
                        {
                            Vector3 pos = c;
                            if (i > 0)
                            {
                                float fa = (i - j) / (float)N;
                                float fb = j / (float)N;
                                pos = c + (cA - c) * fa + (cB - c) * fb;
                            }
                            ringIndices[i][j] = GetOrAddTopVertex(pos);
                        }
                    }

                    for (int i = 0; i < N; i++)
                    {
                        for (int j = 0; j <= i; j++)
                        {
                            int v0 = ringIndices[i][j];
                            int v1 = ringIndices[i + 1][j];
                            int v2 = ringIndices[i + 1][j + 1];
                            AddUpwardTriangle(vertices, triangles, v0, v1, v2);

                            if (j < i)
                            {
                                int v3 = ringIndices[i][j + 1];
                                AddUpwardTriangle(vertices, triangles, v0, v2, v3);
                            }
                        }
                    }
                }
            }

            // Build crisp vertical side walls along all outer perimeter segments
            for (int s = 0; s < outerDirectedSegments.Count; s++)
            {
                var seg = outerDirectedSegments[s];
                for (int k = 0; k < N; k++)
                {
                    float t0 = k / (float)N;
                    float t1 = (k + 1) / (float)N;
                    Vector2 p0 = Vector2.Lerp(seg.a, seg.b, t0);
                    Vector2 p1 = Vector2.Lerp(seg.a, seg.b, t1);

                    int top0 = GetOrAddTopVertex(new Vector3(p0.x, 0f, p0.y));
                    int top1 = GetOrAddTopVertex(new Vector3(p1.x, 0f, p1.y));

                    Vector3 topPos0 = vertices[top0];
                    Vector3 topPos1 = vertices[top1];
                    Vector2 mUV0 = uvMask[top0];
                    Vector2 mUV1 = uvMask[top1];

                    // Duplicate top rim vertices for crisp cliff wall shading + add bottom skirt at Y = 0
                    int wTop0 = vertices.Count;
                    vertices.Add(topPos0);
                    uvTop.Add(new Vector2(p0.x, topPos0.y));
                    uvMask.Add(mUV0);
                    colors.Add(new Color(0f, 1f, 1f, 0f));

                    int wTop1 = vertices.Count;
                    vertices.Add(topPos1);
                    uvTop.Add(new Vector2(p1.x, topPos1.y));
                    uvMask.Add(mUV1);
                    colors.Add(new Color(0f, 1f, 1f, 0f));

                    int wBot0 = vertices.Count;
                    vertices.Add(new Vector3(p0.x, 0f, p0.y));
                    uvTop.Add(new Vector2(p0.x, 0f));
                    uvMask.Add(mUV0);
                    colors.Add(new Color(0f, 1f, 0f, 0f));

                    int wBot1 = vertices.Count;
                    vertices.Add(new Vector3(p1.x, 0f, p1.y));
                    uvTop.Add(new Vector2(p1.x, 0f));
                    uvMask.Add(mUV1);
                    colors.Add(new Color(0f, 1f, 0f, 0f));

                    Vector3 expectedOut = new Vector3(seg.outwardNormal.x, 0f, seg.outwardNormal.y);
                    AddOrientedTriangle(vertices, triangles, wTop0, wBot0, wBot1, expectedOut);
                    AddOrientedTriangle(vertices, triangles, wTop0, wBot1, wTop1, expectedOut);
                }
            }

            var mesh = new Mesh { name = meshName };
            if (vertices.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvTop);
            mesh.SetUVs(1, uvMask);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh BuildWaterSurfaceMesh(
            string meshName,
            IReadOnlyList<HexCoordinates> hexes,
            Texture2D waterMask,
            WetIslandClusterSurfaceSettings settings)
        {
            ComputeClusterBoundsXZ(hexes, out Vector2 minXZ, out Vector2 maxXZ, out Vector3 centroid);

            int N = Mathf.Clamp(settings.radialSubdivisions, 6, 32);
            var vertices = new List<Vector3>(2048);
            var uvTop = new List<Vector2>(2048);
            var uvMask = new List<Vector2>(2048);
            var triangles = new List<int>(6144);
            var weldedLookup = new Dictionary<long, int>(2048);

            int GetOrAddWaterVertex(Vector3 localFlatPos)
            {
                long qx = Mathf.RoundToInt(localFlatPos.x * 500f);
                long qz = Mathf.RoundToInt(localFlatPos.z * 500f);
                long key = (qx << 32) ^ (uint)qz;

                if (weldedLookup.TryGetValue(key, out int existingIdx))
                {
                    return existingIdx;
                }

                float worldX = localFlatPos.x + centroid.x;
                float worldZ = localFlatPos.z + centroid.z;
                Vector2 mUV = WorldXZToMaskUV(worldX, worldZ, minXZ, maxXZ);

                float y = settings.tileHeight - settings.waterSurfaceDrop;
                if (settings.streamSlopeDrop > 0.001f)
                {
                    float along = Vector2.Dot(new Vector2(mUV.x - 0.5f, (1f - mUV.y) - 0.55f), settings.streamSlopeDir.normalized);
                    y -= Mathf.Clamp(along + 0.25f, 0f, 0.6f) * settings.streamSlopeDrop;
                }

                int idx = vertices.Count;
                vertices.Add(new Vector3(localFlatPos.x, y, localFlatPos.z));
                uvTop.Add(new Vector2(localFlatPos.x, localFlatPos.z));
                uvMask.Add(mUV);
                weldedLookup[key] = idx;
                return idx;
            }

            float minActiveMask = Mathf.Max(0.02f, settings.waterThresholdMask - 0.14f);
            var ringPos = new Vector3[N + 1][];
            var ringMask = new float[N + 1][];
            for (int i = 0; i <= N; i++)
            {
                ringPos[i] = new Vector3[i + 1];
                ringMask[i] = new float[i + 1];
            }

            for (int h = 0; h < hexes.Count; h++)
            {
                Vector3 c = HexMetrics.HexToWorldPosition(hexes[h], 0f) - centroid;
                for (int dir = 0; dir < 6; dir++)
                {
                    Vector3 cA = c + PointyHexCorners[dir];
                    Vector3 cB = c + PointyHexCorners[(dir + 1) % 6];

                    for (int i = 0; i <= N; i++)
                    {
                        for (int j = 0; j <= i; j++)
                        {
                            Vector3 pos = c;
                            if (i > 0)
                            {
                                float fa = (i - j) / (float)N;
                                float fb = j / (float)N;
                                pos = c + (cA - c) * fa + (cB - c) * fb;
                            }
                            ringPos[i][j] = pos;
                            Vector2 mUV = WorldXZToMaskUV(pos.x + centroid.x, pos.z + centroid.z, minXZ, maxXZ);
                            ringMask[i][j] = SampleWaterMaskBilinear(waterMask, mUV);
                        }
                    }

                    for (int i = 0; i < N; i++)
                    {
                        for (int j = 0; j <= i; j++)
                        {
                            if (ringMask[i][j] >= minActiveMask ||
                                ringMask[i + 1][j] >= minActiveMask ||
                                ringMask[i + 1][j + 1] >= minActiveMask)
                            {
                                int v0 = GetOrAddWaterVertex(ringPos[i][j]);
                                int v1 = GetOrAddWaterVertex(ringPos[i + 1][j]);
                                int v2 = GetOrAddWaterVertex(ringPos[i + 1][j + 1]);
                                AddUpwardTriangle(vertices, triangles, v0, v1, v2);
                            }

                            if (j < i)
                            {
                                if (ringMask[i][j] >= minActiveMask ||
                                    ringMask[i + 1][j + 1] >= minActiveMask ||
                                    ringMask[i][j + 1] >= minActiveMask)
                                {
                                    int v0 = GetOrAddWaterVertex(ringPos[i][j]);
                                    int v2 = GetOrAddWaterVertex(ringPos[i + 1][j + 1]);
                                    int v3 = GetOrAddWaterVertex(ringPos[i][j + 1]);
                                    AddUpwardTriangle(vertices, triangles, v0, v2, v3);
                                }
                            }
                        }
                    }
                }
            }

            var mesh = new Mesh { name = meshName };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvTop);
            mesh.SetUVs(1, uvMask);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Builds a stylized multi-tiered mossy limestone cliff + submerged river pebbles & stepping stones
        /// for Streamstone (using vertex colors compatible with WetIslandGround.shader).
        /// </summary>
        public static Mesh BuildStreamstoneRockAndPebblesMesh(
            string meshName,
            IReadOnlyList<HexCoordinates> hexes,
            Texture2D waterMask,
            WetIslandClusterSurfaceSettings settings)
        {
            ComputeClusterBoundsXZ(hexes, out Vector2 minXZ, out Vector2 maxXZ, out Vector3 centroid);

            var vertices = new List<Vector3>(2048);
            var uvTop = new List<Vector2>(2048);
            var uvMask = new List<Vector2>(2048);
            var colors = new List<Color>(2048);
            var triangles = new List<int>(6144);

            // 1. Left-flank headwater mossy limestone cliff tiers (around PNG u=0.28..0.35, v=0.47..0.55)
            Vector3 cliffAnchor = MaskUVToLocalXZ(0.31f, 0.51f, minXZ, maxXZ, centroid);
            cliffAnchor.y = settings.tileHeight - 0.04f;

            // Main tall limestone crag
            AppendSculptedRockMound(
                vertices, uvTop, uvMask, colors, triangles,
                cliffAnchor + new Vector3(-0.18f, 0f, 0.14f),
                new Vector3(0.62f, 1.15f, 0.54f),
                yawDeg: -32f, seed: 101, mossCapThreshold: 0.52f);

            // Left shoulder rock tier
            AppendSculptedRockMound(
                vertices, uvTop, uvMask, colors, triangles,
                cliffAnchor + new Vector3(-0.34f, 0f, -0.22f),
                new Vector3(0.46f, 0.78f, 0.42f),
                yawDeg: 18f, seed: 203, mossCapThreshold: 0.48f);

            // Upper-right spring ledge flanking the waterfall spout
            AppendSculptedRockMound(
                vertices, uvTop, uvMask, colors, triangles,
                cliffAnchor + new Vector3(0.16f, 0f, 0.36f),
                new Vector3(0.42f, 0.72f, 0.38f),
                yawDeg: -55f, seed: 307, mossCapThreshold: 0.48f);

            // Base boulders framing the plunge pool
            AppendSculptedRockMound(
                vertices, uvTop, uvMask, colors, triangles,
                cliffAnchor + new Vector3(-0.12f, -0.08f, -0.45f),
                new Vector3(0.28f, 0.34f, 0.25f),
                yawDeg: 40f, seed: 409, mossCapThreshold: 0.65f);

            AppendSculptedRockMound(
                vertices, uvTop, uvMask, colors, triangles,
                cliffAnchor + new Vector3(0.34f, -0.10f, 0.18f),
                new Vector3(0.24f, 0.28f, 0.22f),
                yawDeg: -15f, seed: 503, mossCapThreshold: 0.65f);

            // 2. Three stylized flat stepping stones along the stream (matching Streamstone v3 visual!)
            (float u, float vPng, Vector3 scale, float yaw)[] steppingStones = new[]
            {
                (0.43f, 0.51f, new Vector3(0.20f, 0.12f, 0.16f), 15f),
                (0.50f, 0.59f, new Vector3(0.27f, 0.14f, 0.21f), -24f),
                (0.60f, 0.65f, new Vector3(0.18f, 0.11f, 0.15f), 32f),
            };

            for (int i = 0; i < steppingStones.Length; i++)
            {
                var st = steppingStones[i];
                Vector3 pos = MaskUVToLocalXZ(st.u, st.vPng, minXZ, maxXZ, centroid);
                float waterY = settings.tileHeight - settings.waterSurfaceDrop;
                pos.y = waterY - st.scale.y * 0.42f; // top peeks just above water surface
                AppendSculptedRockMound(
                    vertices, uvTop, uvMask, colors, triangles,
                    pos, st.scale, st.yaw, seed: 600 + i * 37, mossCapThreshold: 0.92f, isSubmergedStone: true);
            }

            // 3. Smooth submerged 3D river pebbles scattered on the stream bed
            System.Random rng = new System.Random(1337);
            int pebbleCount = 0;
            int attempts = 0;
            while (pebbleCount < 32 && attempts < 300)
            {
                attempts++;
                float u = Mathf.Lerp(0.34f, 0.71f, (float)rng.NextDouble());
                float vPng = Mathf.Lerp(0.48f, 0.74f, (float)rng.NextDouble());
                Vector2 mUV = new Vector2(u, 1f - vPng);
                float mVal = SampleWaterMaskBilinear(waterMask, mUV);

                if (mVal < settings.waterThresholdMask + 0.04f) continue;

                Vector3 pos = MaskUVToLocalXZ(u, vPng, minXZ, maxXZ, centroid);
                float basinT = Smoothstep(settings.shoreStartMask, 0.96f, mVal);
                float waterCoreT = Smoothstep(settings.waterThresholdMask, 0.92f, mVal);
                float depth = Mathf.Lerp(basinT * 0.32f, 0.32f + waterCoreT * 0.68f, Smoothstep(settings.shoreStartMask, settings.waterThresholdMask + 0.1f, mVal)) * settings.maxBasinDepth;

                pos.y = settings.tileHeight - depth - 0.015f;
                float rx = Mathf.Lerp(0.065f, 0.145f, (float)rng.NextDouble());
                float rz = rx * Mathf.Lerp(0.72f, 1.18f, (float)rng.NextDouble());
                float ry = rx * Mathf.Lerp(0.42f, 0.62f, (float)rng.NextDouble());
                float yaw = (float)rng.NextDouble() * 360f;

                AppendSculptedRockMound(
                    vertices, uvTop, uvMask, colors, triangles,
                    pos, new Vector3(rx, ry, rz), yaw, seed: 800 + pebbleCount * 19, mossCapThreshold: 1.1f, isSubmergedStone: true);
                pebbleCount++;
            }

            var mesh = new Mesh { name = meshName };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvTop);
            mesh.SetUVs(1, uvMask);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Builds the curved waterfall cascade ribbon pouring from the left cliff spout into the plunge basin.
        /// </summary>
        public static Mesh BuildStreamstoneWaterfallMesh(
            string meshName,
            IReadOnlyList<HexCoordinates> hexes,
            WetIslandClusterSurfaceSettings settings)
        {
            ComputeClusterBoundsXZ(hexes, out Vector2 minXZ, out Vector2 maxXZ, out Vector3 centroid);

            Vector3 spoutTop = MaskUVToLocalXZ(0.32f, 0.515f, minXZ, maxXZ, centroid);
            spoutTop.y = settings.tileHeight + 0.56f;

            Vector3 plungeBottom = MaskUVToLocalXZ(0.368f, 0.538f, minXZ, maxXZ, centroid);
            plungeBottom.y = settings.tileHeight - settings.waterSurfaceDrop - 0.02f;

            Vector3 flowDir = (plungeBottom - spoutTop);
            flowDir.y = 0f;
            flowDir.Normalize();
            Vector3 right = new Vector3(-flowDir.z, 0f, flowDir.x);

            int segsY = 12;
            int segsX = 6;
            float widthTop = 0.24f;
            float widthBot = 0.36f;

            var vertices = new List<Vector3>((segsY + 1) * (segsX + 1));
            var uv0 = new List<Vector2>((segsY + 1) * (segsX + 1));
            var uv1 = new List<Vector2>((segsY + 1) * (segsX + 1));
            var triangles = new List<int>(segsY * segsX * 6);

            for (int iy = 0; iy <= segsY; iy++)
            {
                float ty = iy / (float)segsY;
                // Parabolic waterfall arc
                float horizT = Mathf.Sin(ty * Mathf.PI * 0.5f);
                float vertT = ty * ty;
                Vector3 center = new Vector3(
                    Mathf.Lerp(spoutTop.x, plungeBottom.x, horizT),
                    Mathf.Lerp(spoutTop.y, plungeBottom.y, vertT),
                    Mathf.Lerp(spoutTop.z, plungeBottom.z, horizT));

                float halfW = Mathf.Lerp(widthTop, widthBot, ty) * 0.5f;

                for (int ix = 0; ix <= segsX; ix++)
                {
                    float tx = (ix / (float)segsX) * 2f - 1f;
                    float arch = (1f - tx * tx) * 0.035f;
                    Vector3 pos = center + right * (tx * halfW) + flowDir * arch;

                    vertices.Add(pos);
                    uv0.Add(new Vector2(ix / (float)segsX, (1f - ty) * 2.2f));
                    uv1.Add(new Vector2(0.85f, 0.85f)); // High waterMask value so WetIslandWater renders opaque/foamy
                }
            }

            int stride = segsX + 1;
            for (int iy = 0; iy < segsY; iy++)
            {
                for (int ix = 0; ix < segsX; ix++)
                {
                    int a = iy * stride + ix;
                    int b = (iy + 1) * stride + ix;
                    int c = (iy + 1) * stride + (ix + 1);
                    int d = iy * stride + (ix + 1);

                    AddOrientedTriangle(vertices, triangles, a, b, c, flowDir + Vector3.up * 0.5f);
                    AddOrientedTriangle(vertices, triangles, a, c, d, flowDir + Vector3.up * 0.5f);
                }
            }

            var mesh = new Mesh { name = meshName };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AppendSculptedRockMound(
            List<Vector3> vertices,
            List<Vector2> uvTop,
            List<Vector2> uvMask,
            List<Color> colors,
            List<int> triangles,
            Vector3 baseCenter,
            Vector3 radii,
            float yawDeg,
            int seed,
            float mossCapThreshold,
            bool isSubmergedStone = false)
        {
            int latSegments = isSubmergedStone ? 6 : 9;
            int lonSegments = isSubmergedStone ? 10 : 14;
            int baseVert = vertices.Count;
            Quaternion rot = Quaternion.Euler(0f, yawDeg, 0f);

            for (int lat = 0; lat <= latSegments; lat++)
            {
                float v = lat / (float)latSegments;
                // Hemisphere from slightly below base (-0.15 pi) to apex (+0.5 pi)
                float phi = Mathf.Lerp(-0.12f * Mathf.PI, 0.5f * Mathf.PI, v);
                float cosPhi = Mathf.Cos(phi);
                float sinPhi = Mathf.Sin(phi);

                for (int lon = 0; lon <= lonSegments; lon++)
                {
                    float u = lon / (float)lonSegments;
                    float theta = u * Mathf.PI * 2f;
                    Vector3 unit = new Vector3(cosPhi * Mathf.Cos(theta), Mathf.Max(0f, sinPhi), cosPhi * Mathf.Sin(theta));

                    // Stylized faceted rock perturbation
                    float n1 = Mathf.Sin(unit.x * 4.3f + seed * 1.1f) * Mathf.Cos(unit.z * 3.9f + seed * 0.7f);
                    float n2 = Mathf.Cos(unit.y * 5.2f - unit.x * 3.1f + seed * 2.3f);
                    float perturb = isSubmergedStone ? (1f + n1 * 0.08f) : (1f + n1 * 0.16f + n2 * 0.08f);

                    // Flatten top slightly on stepping stones and cliff ledges
                    float yProfile = isSubmergedStone ? Mathf.Pow(unit.y, 0.68f) : Mathf.Pow(unit.y, 0.82f);
                    Vector3 local = new Vector3(
                        unit.x * radii.x * perturb,
                        yProfile * radii.y,
                        unit.z * radii.z * perturb);

                    Vector3 worldPos = baseCenter + rot * local;
                    vertices.Add(worldPos);
                    uvTop.Add(new Vector2(worldPos.x, worldPos.z));

                    // Force mask=0 on cliff rocks (so TopTex moss & SideColor limestone apply);
                    // On submerged pebbles/stepping stones, sample pocket-pool center (0.50, 0.41) where mask=1.0
                    uvMask.Add(isSubmergedStone ? new Vector2(0.50f, 0.41f) : Vector2.zero);

                    bool isTopSurface = isSubmergedStone ? (unit.y > 0.15f) : (unit.y > mossCapThreshold);
                    float wallFlag = isTopSurface ? 0f : 1f;
                    float heightGrad = Mathf.Clamp01(0.35f + unit.y * 0.65f);
                    colors.Add(new Color(0f, wallFlag, heightGrad, 0f));
                }
            }

            int ringStride = lonSegments + 1;
            for (int lat = 0; lat < latSegments; lat++)
            {
                for (int lon = 0; lon < lonSegments; lon++)
                {
                    int i0 = baseVert + lat * ringStride + lon;
                    int i1 = baseVert + (lat + 1) * ringStride + lon;
                    int i2 = baseVert + (lat + 1) * ringStride + (lon + 1);
                    int i3 = baseVert + lat * ringStride + (lon + 1);

                    Vector3 outward = (vertices[i0] - baseCenter).normalized + Vector3.up * 0.25f;
                    AddOrientedTriangle(vertices, triangles, i0, i1, i2, outward);
                    AddOrientedTriangle(vertices, triangles, i0, i2, i3, outward);
                }
            }
        }

        private static float Smoothstep(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / Mathf.Max(0.0001f, e1 - e0));
            return t * t * (3f - 2f * t);
        }

        private static float MinDistanceToSegments(Vector2 p, List<(Vector2 a, Vector2 b)> segs)
        {
            float best = float.MaxValue;
            for (int i = 0; i < segs.Count; i++)
            {
                float d = DistancePointToSegment(p, segs[i].a, segs[i].b);
                if (d < best) best = d;
            }
            return best;
        }

        private static float MinDistanceToSegments(Vector2 p, List<(Vector2 a, Vector2 b, Vector2 outwardNormal)> segs)
        {
            float best = float.MaxValue;
            for (int i = 0; i < segs.Count; i++)
            {
                float d = DistancePointToSegment(p, segs[i].a, segs[i].b);
                if (d < best) best = d;
            }
            return best;
        }

        private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            float t = lenSq > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        private static void AddUpwardTriangle(List<Vector3> verts, List<int> tris, int a, int b, int c)
        {
            Vector3 n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (n.y < 0f)
            {
                int tmp = b; b = c; c = tmp;
            }
            tris.Add(a);
            tris.Add(b);
            tris.Add(c);
        }

        private static void AddOrientedTriangle(List<Vector3> verts, List<int> tris, int a, int b, int c, Vector3 desiredNormal)
        {
            Vector3 n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (Vector3.Dot(n, desiredNormal) < 0f)
            {
                int tmp = b; b = c; c = tmp;
            }
            tris.Add(a);
            tris.Add(b);
            tris.Add(c);
        }
    }
}
