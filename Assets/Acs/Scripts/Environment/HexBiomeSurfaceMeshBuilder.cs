using System;
using System.Collections.Generic;
using UnityEngine;

namespace LumiWorld.Acs
{
    [Serializable]
    public struct HexBiomeSurfaceSettings
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

        public static HexBiomeSurfaceSettings CreateDefault()
        {
            return new HexBiomeSurfaceSettings
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
    /// Builds watertight multi-hex cluster ground meshes (with optional continuous sunken water basins
    /// and seam-free submerged beds) plus matching water sheet meshes and shared surface elevation queries.
    /// Island-agnostic: reusable across Starter Island, Wet Island, coastal/ocean biomes, and future islands.
    /// </summary>
    public static class HexBiomeSurfaceMeshBuilder
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
        /// Returns a single center hex (clusterSize &lt;= 1) or upright equilateral triangle hex coordinates
        /// (3, 6, or 10 hexes) centered cleanly around X = 0.
        /// </summary>
        public static List<HexCoordinates> GetUprightTriangleHexes(int clusterSize)
        {
            if (clusterSize <= 1)
            {
                return new List<HexCoordinates> { new HexCoordinates(0, 0) };
            }

            int rows = clusterSize <= 3 ? 2 : (clusterSize <= 6 ? 3 : 4);
            var list = new List<HexCoordinates>(clusterSize);

            for (int r = 0; r < rows; r++)
            {
                int countInRow = rows - r;
                // Center countInRow tiles around X = 0 where x = (2*q + r)
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
        /// Maps cluster world (x, z) to the upright triangle UV space [0.12..0.88, 0.16..0.88] used by cluster water masks.
        /// </summary>
        public static Vector2 WorldXZToMaskUV(float x, float z, Vector2 minXZ, Vector2 maxXZ)
        {
            float tx = Mathf.InverseLerp(minXZ.x, maxXZ.x, x);
            float tz = Mathf.InverseLerp(minXZ.y, maxXZ.y, z);
            float u = Mathf.Lerp(0.12f, 0.88f, tx);
            float v = Mathf.Lerp(0.16f, 0.88f, tz);
            return new Vector2(u, v);
        }

        /// <summary>
        /// Maps a top-down PNG UV coordinate back to cluster-local (x, 0, z).
        /// </summary>
        public static Vector3 MaskUVToLocalXZ(float u, float vPngTopDown, Vector2 minXZ, Vector2 maxXZ, Vector3 centroidXZ)
        {
            float vUnity = 1f - vPngTopDown;
            float tx = Mathf.InverseLerp(0.12f, 0.88f, u);
            float tz = Mathf.InverseLerp(0.16f, 0.88f, vUnity);
            float wx = Mathf.Lerp(minXZ.x, maxXZ.x, tx) - centroidXZ.x;
            float wz = Mathf.Lerp(minXZ.y, maxXZ.y, tz) - centroidXZ.z;
            return new Vector3(wx, 0f, wz);
        }

        /// <summary>
        /// Samples a 5-tap softened bilinear value from the R channel of a readable water mask texture.
        /// </summary>
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

        /// <summary>
        /// Evaluates the local Y elevation of the ground basin surface (excluding interior seam/bevel micro-relief).
        /// Shared source of truth for mesh generation, layout placement, and activity zone sampling.
        /// </summary>
        public static float EvaluateGroundBasinY(float maskVal, Vector2 maskUV, in HexBiomeSurfaceSettings settings)
        {
            float y = settings.tileHeight;
            if (maskVal <= settings.shoreStartMask || settings.maxBasinDepth <= 0.0001f)
            {
                return y;
            }

            if (maskVal <= settings.waterThresholdMask)
            {
                float shoreT = Smoothstep(settings.shoreStartMask, settings.waterThresholdMask, maskVal);
                y -= shoreT * settings.waterSurfaceDrop;
                return y;
            }

            float rawT = Mathf.Clamp01((maskVal - settings.waterThresholdMask) / Mathf.Max(0.01f, 0.90f - settings.waterThresholdMask));
            float waterCoreT = Mathf.Pow(rawT, 0.62f);
            float extraDepth = Mathf.Max(0.02f, settings.maxBasinDepth - settings.waterSurfaceDrop);
            y -= settings.waterSurfaceDrop + waterCoreT * extraDepth;

            if (settings.streamSlopeDrop > 0.001f)
            {
                float along = Vector2.Dot(new Vector2(maskUV.x - 0.5f, (1f - maskUV.y) - 0.55f), settings.streamSlopeDir.normalized);
                y -= Mathf.Clamp(along + 0.25f, 0f, 0.6f) * settings.streamSlopeDrop * waterCoreT;
            }

            return y;
        }

        /// <summary>
        /// Evaluates the local Y elevation of the water surface at a given mask UV.
        /// </summary>
        public static float EvaluateWaterSurfaceY(Vector2 maskUV, in HexBiomeSurfaceSettings settings)
        {
            float y = settings.tileHeight - settings.waterSurfaceDrop;
            if (settings.streamSlopeDrop > 0.001f)
            {
                float along = Vector2.Dot(new Vector2(maskUV.x - 0.5f, (1f - maskUV.y) - 0.55f), settings.streamSlopeDir.normalized);
                y -= Mathf.Clamp(along + 0.25f, 0f, 0.6f) * settings.streamSlopeDrop;
            }
            return y;
        }

        /// <summary>
        /// Evaluates vertical water column depth (in meters) at a mask value and UV. Returns 0 on dry land.
        /// </summary>
        public static float EvaluateWaterDepth(float maskVal, Vector2 maskUV, in HexBiomeSurfaceSettings settings)
        {
            if (maskVal <= settings.waterThresholdMask || settings.maxBasinDepth <= 0.0001f)
            {
                return 0f;
            }

            float surfaceY = EvaluateWaterSurfaceY(maskUV, in settings);
            float bedY = EvaluateGroundBasinY(maskVal, maskUV, in settings);
            return Mathf.Max(0f, surfaceY - bedY);
        }

        public static Mesh BuildGroundClusterMesh(
            string meshName,
            IReadOnlyList<HexCoordinates> hexes,
            Texture2D waterMask,
            HexBiomeSurfaceSettings settings)
        {
            ComputeClusterBoundsXZ(hexes, out Vector2 minXZ, out Vector2 maxXZ, out Vector3 centroid);

            var hexSet = new HashSet<HexCoordinates>(hexes);
            var hexCentersLocal = new Vector3[hexes.Count];
            for (int i = 0; i < hexes.Count; i++)
            {
                hexCentersLocal[i] = HexMetrics.HexToWorldPosition(hexes[i], 0f) - centroid;
            }

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

                // Dry factor is 1 on dry land and fades to 0 inside shore/basin so interior hex seams disappear underwater.
                float dryFactor = 1f - Smoothstep(settings.shoreStartMask * 0.5f, settings.waterThresholdMask * 0.92f, maskVal);

                // 1) Concave water basin depression calibrated to intersect the water plane smoothly at waterThresholdMask.
                float y = EvaluateGroundBasinY(maskVal, mUV, in settings);

                // 2) Subtle dry interior hex seam groove (only on dry land).
                float seamCrease = 0f;
                if (distInterior < settings.dryInteriorSeamWidth && dryFactor > 0.001f)
                {
                    float tSeam = 1f - Mathf.Clamp01(distInterior / settings.dryInteriorSeamWidth);
                    tSeam = tSeam * tSeam * (3f - 2f * tSeam);
                    seamCrease = tSeam * dryFactor;
                    y -= seamCrease * settings.dryInteriorSeamDepth;
                }

                // 3) Outer cluster perimeter bevel.
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

            // Build vertical side walls along all outer perimeter segments
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

                    // Duplicate top rim vertices for crisp wall shading + add bottom skirt at Y = 0
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
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

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
            HexBiomeSurfaceSettings settings)
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
                float y = EvaluateWaterSurfaceY(mUV, in settings);

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
