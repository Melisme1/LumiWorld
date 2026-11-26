using System;
using System.Collections.Generic;
using UnityEngine;

namespace LumiWorld.Acs.Islands
{
    /// <summary>
    /// Pure procedural geometry builder for LumiWorld Cozy Cloud Cover:
    /// - Subdivided world plane for Layer 1 misty cloud sea
    /// - Centered unit quad for Layer 2 Painterly 2.5D cloud bank sprites
    /// - Seamless Smooth-Union SDF / Metaball 3D cloud meshes via Marching Tetrahedra
    ///   (zero sphere-intersection seams, analytical smooth normals)
    /// </summary>
    public static class CozyCloudSdfMeshBuilder
    {
        private struct EllipsoidLobe
        {
            public Vector3 Center;
            public Vector3 Radii;
        }

        // 6 tetrahedra partitioning a unit cube (corners 0..7)
        private static readonly int[,] CubeTets =
        {
            { 0, 5, 1, 6 },
            { 0, 1, 2, 6 },
            { 0, 2, 3, 6 },
            { 0, 3, 7, 6 },
            { 0, 7, 4, 6 },
            { 0, 4, 5, 6 }
        };

        private static readonly Vector3Int[] CubeCornerOffsets =
        {
            new Vector3Int(0, 0, 0), // 0
            new Vector3Int(1, 0, 0), // 1
            new Vector3Int(1, 0, 1), // 2
            new Vector3Int(0, 0, 1), // 3
            new Vector3Int(0, 1, 0), // 4
            new Vector3Int(1, 1, 0), // 5
            new Vector3Int(1, 1, 1), // 6
            new Vector3Int(0, 1, 1)  // 7
        };

        public static Mesh CreateBillboardQuadMesh()
        {
            var mesh = new Mesh { name = "CozyCloudSpriteQuad", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(new[]
            {
                new Vector3(-0.5f, -0.38f, 0f),
                new Vector3( 0.5f, -0.38f, 0f),
                new Vector3( 0.5f,  0.62f, 0f),
                new Vector3(-0.5f,  0.62f, 0f)
            });
            mesh.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetUVs(0, new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1.5f, 1.5f, 1.5f));
            return mesh;
        }

        public static Mesh CreateSubdividedPlaneMesh(float size, int segments)
        {
            segments = Mathf.Clamp(segments, 8, 128);
            int stride = segments + 1;
            int vertCount = stride * stride;
            var verts = new Vector3[vertCount];
            var norms = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var tris = new int[segments * segments * 6];

            float half = size * 0.5f;
            float invSeg = 1f / segments;

            for (int z = 0; z <= segments; z++)
            {
                float vz = z * invSeg;
                float pz = Mathf.Lerp(-half, half, vz);
                for (int x = 0; x <= segments; x++)
                {
                    float vx = x * invSeg;
                    float px = Mathf.Lerp(-half, half, vx);
                    int idx = z * stride + x;
                    verts[idx] = new Vector3(px, 0f, pz);
                    norms[idx] = Vector3.up;
                    uvs[idx] = new Vector2(vx, vz);
                }
            }

            int t = 0;
            for (int z = 0; z < segments; z++)
            {
                for (int x = 0; x < segments; x++)
                {
                    int a = z * stride + x;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;

                    tris[t++] = a; tris[t++] = c; tris[t++] = d;
                    tris[t++] = a; tris[t++] = d; tris[t++] = b;
                }
            }

            var mesh = new Mesh
            {
                name = "CozyWorldCloudSeaPlane",
                indexFormat = vertCount > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
                hideFlags = HideFlags.DontSave
            };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Generates a seamless Smooth-Union SDF 3D cloud mesh (variant 0 = wide cumulus bank, variant 1 = rounded cozy pillow).
        /// </summary>
        public static Mesh CreateSmoothSdfCloudMesh(int variant)
        {
            EllipsoidLobe[] lobes = variant == 0
                ? new[]
                {
                    new EllipsoidLobe { Center = new Vector3( 0.00f,  0.14f,  0.00f), Radii = new Vector3(0.68f, 0.44f, 0.56f) },
                    new EllipsoidLobe { Center = new Vector3(-0.54f,  0.02f,  0.08f), Radii = new Vector3(0.50f, 0.34f, 0.46f) },
                    new EllipsoidLobe { Center = new Vector3( 0.56f,  0.04f, -0.06f), Radii = new Vector3(0.48f, 0.34f, 0.44f) },
                    new EllipsoidLobe { Center = new Vector3(-0.18f,  0.00f,  0.42f), Radii = new Vector3(0.46f, 0.30f, 0.40f) },
                    new EllipsoidLobe { Center = new Vector3( 0.24f, -0.02f, -0.38f), Radii = new Vector3(0.44f, 0.30f, 0.38f) },
                    new EllipsoidLobe { Center = new Vector3(-0.34f, -0.04f, -0.32f), Radii = new Vector3(0.38f, 0.26f, 0.36f) },
                    new EllipsoidLobe { Center = new Vector3( 0.38f,  0.02f,  0.34f), Radii = new Vector3(0.38f, 0.28f, 0.36f) }
                }
                : new[]
                {
                    new EllipsoidLobe { Center = new Vector3(-0.12f,  0.16f,  0.02f), Radii = new Vector3(0.58f, 0.44f, 0.54f) },
                    new EllipsoidLobe { Center = new Vector3( 0.42f,  0.08f,  0.14f), Radii = new Vector3(0.50f, 0.36f, 0.46f) },
                    new EllipsoidLobe { Center = new Vector3(-0.56f,  0.00f, -0.10f), Radii = new Vector3(0.44f, 0.32f, 0.42f) },
                    new EllipsoidLobe { Center = new Vector3( 0.08f,  0.00f, -0.42f), Radii = new Vector3(0.46f, 0.30f, 0.40f) },
                    new EllipsoidLobe { Center = new Vector3(-0.18f, -0.02f,  0.44f), Radii = new Vector3(0.44f, 0.28f, 0.40f) },
                    new EllipsoidLobe { Center = new Vector3( 0.54f, -0.02f, -0.26f), Radii = new Vector3(0.36f, 0.26f, 0.34f) }
                };

            const int nx = 26;
            const int ny = 16;
            const int nz = 24;
            Vector3 minB = new Vector3(-1.32f, -0.58f, -1.18f);
            Vector3 maxB = new Vector3( 1.32f,  0.84f,  1.18f);

            var field = new float[nx * ny * nz];
            Vector3 step = new Vector3(
                (maxB.x - minB.x) / (nx - 1),
                (maxB.y - minB.y) / (ny - 1),
                (maxB.z - minB.z) / (nz - 1));

            for (int z = 0; z < nz; z++)
            {
                float pz = minB.z + z * step.z;
                for (int y = 0; y < ny; y++)
                {
                    float py = minB.y + y * step.y;
                    for (int x = 0; x < nx; x++)
                    {
                        float px = minB.x + x * step.x;
                        field[(z * ny + y) * nx + x] = SampleCloudSdf(new Vector3(px, py, pz), lobes);
                    }
                }
            }

            var verts = new List<Vector3>(3000);
            var norms = new List<Vector3>(3000);
            var tris = new List<int>(6000);
            var edgeCache = new Dictionary<long, int>(3000);

            int GridId(int gx, int gy, int gz) => (gz * ny + gy) * nx + gx;

            int GetOrCreateEdgeVertex(int idA, Vector3 pA, float vA, int idB, Vector3 pB, float vB)
            {
                long a = Math.Min(idA, idB);
                long b = Math.Max(idA, idB);
                long key = (a << 32) | b;
                if (edgeCache.TryGetValue(key, out int existing)) return existing;

                float denom = vB - vA;
                float t = Mathf.Abs(denom) > 1e-6f ? Mathf.Clamp01(-vA / denom) : 0.5f;
                Vector3 pos = Vector3.Lerp(pA, pB, t);

                // Analytical smooth SDF gradient normal blended slightly upward for soft Ghibli shading
                Vector3 grad = SampleCloudGradient(pos, lobes);
                Vector3 softNormal = Vector3.Slerp(grad, (grad + Vector3.up * 0.35f).normalized, 0.25f).normalized;

                int idx = verts.Count;
                verts.Add(pos);
                norms.Add(softNormal);
                edgeCache[key] = idx;
                return idx;
            }

            void EmitTri(int i0, int i1, int i2)
            {
                // Enforce outward winding consistent with analytical gradient normal
                Vector3 p0 = verts[i0];
                Vector3 p1 = verts[i1];
                Vector3 p2 = verts[i2];
                Vector3 faceN = Vector3.Cross(p1 - p0, p2 - p0);
                Vector3 avgN = norms[i0] + norms[i1] + norms[i2];
                if (Vector3.Dot(faceN, avgN) < 0f)
                {
                    tris.Add(i0); tris.Add(i2); tris.Add(i1);
                }
                else
                {
                    tris.Add(i0); tris.Add(i1); tris.Add(i2);
                }
            }

            int[] cIds = new int[8];
            Vector3[] cPos = new Vector3[8];
            float[] cVal = new float[8];
            int[] inList = new int[4];
            int[] outList = new int[4];

            for (int z = 0; z < nz - 1; z++)
            {
                for (int y = 0; y < ny - 1; y++)
                {
                    for (int x = 0; x < nx - 1; x++)
                    {
                        bool anyNeg = false, anyPos = false;
                        for (int c = 0; c < 8; c++)
                        {
                            Vector3Int o = CubeCornerOffsets[c];
                            int gx = x + o.x, gy = y + o.y, gz = z + o.z;
                            int id = GridId(gx, gy, gz);
                            float val = field[id];
                            cIds[c] = id;
                            cPos[c] = new Vector3(minB.x + gx * step.x, minB.y + gy * step.y, minB.z + gz * step.z);
                            cVal[c] = val;
                            if (val < 0f) anyNeg = true;
                            else anyPos = true;
                        }
                        if (!anyNeg || !anyPos) continue;

                        for (int tet = 0; tet < 6; tet++)
                        {
                            int inCount = 0, outCount = 0;
                            for (int k = 0; k < 4; k++)
                            {
                                int ci = CubeTets[tet, k];
                                if (cVal[ci] < 0f) inList[inCount++] = ci;
                                else outList[outCount++] = ci;
                            }

                            if (inCount == 1)
                            {
                                int a = inList[0];
                                int v0 = GetOrCreateEdgeVertex(cIds[a], cPos[a], cVal[a], cIds[outList[0]], cPos[outList[0]], cVal[outList[0]]);
                                int v1 = GetOrCreateEdgeVertex(cIds[a], cPos[a], cVal[a], cIds[outList[1]], cPos[outList[1]], cVal[outList[1]]);
                                int v2 = GetOrCreateEdgeVertex(cIds[a], cPos[a], cVal[a], cIds[outList[2]], cPos[outList[2]], cVal[outList[2]]);
                                EmitTri(v0, v1, v2);
                            }
                            else if (inCount == 3)
                            {
                                int a = outList[0];
                                int v0 = GetOrCreateEdgeVertex(cIds[a], cPos[a], cVal[a], cIds[inList[0]], cPos[inList[0]], cVal[inList[0]]);
                                int v1 = GetOrCreateEdgeVertex(cIds[a], cPos[a], cVal[a], cIds[inList[1]], cPos[inList[1]], cVal[inList[1]]);
                                int v2 = GetOrCreateEdgeVertex(cIds[a], cPos[a], cVal[a], cIds[inList[2]], cPos[inList[2]], cVal[inList[2]]);
                                EmitTri(v0, v1, v2);
                            }
                            else if (inCount == 2)
                            {
                                int i0 = inList[0], i1 = inList[1];
                                int o0 = outList[0], o1 = outList[1];
                                int e00 = GetOrCreateEdgeVertex(cIds[i0], cPos[i0], cVal[i0], cIds[o0], cPos[o0], cVal[o0]);
                                int e01 = GetOrCreateEdgeVertex(cIds[i0], cPos[i0], cVal[i0], cIds[o1], cPos[o1], cVal[o1]);
                                int e10 = GetOrCreateEdgeVertex(cIds[i1], cPos[i1], cVal[i1], cIds[o0], cPos[o0], cVal[o0]);
                                int e11 = GetOrCreateEdgeVertex(cIds[i1], cPos[i1], cVal[i1], cIds[o1], cPos[o1], cVal[o1]);
                                EmitTri(e00, e01, e11);
                                EmitTri(e00, e11, e10);
                            }
                        }
                    }
                }
            }

            // 2-pass Laplacian smoothing to remove any tiny Marching Tetrahedra facet creases at close-up zoom
            ApplyLaplacianSmoothing(verts, tris, iterations: 2, lambda: 0.45f);
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 grad = SampleCloudGradient(verts[i], lobes);
                norms[i] = Vector3.Slerp(grad, (grad + Vector3.up * 0.35f).normalized, 0.25f).normalized;
            }

            var mesh = new Mesh
            {
                name = $"CozySmoothSdfCloud_{variant}",
                hideFlags = HideFlags.DontSave
            };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void ApplyLaplacianSmoothing(List<Vector3> verts, List<int> tris, int iterations, float lambda)
        {
            int vCount = verts.Count;
            if (vCount == 0 || tris.Count == 0) return;

            var accum = new Vector3[vCount];
            var counts = new int[vCount];

            for (int iter = 0; iter < iterations; iter++)
            {
                Array.Clear(accum, 0, vCount);
                Array.Clear(counts, 0, vCount);

                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    Vector3 pa = verts[a], pb = verts[b], pc = verts[c];
                    accum[a] += pb + pc; counts[a] += 2;
                    accum[b] += pa + pc; counts[b] += 2;
                    accum[c] += pa + pb; counts[c] += 2;
                }

                for (int i = 0; i < vCount; i++)
                {
                    if (counts[i] > 0)
                    {
                        Vector3 avg = accum[i] / counts[i];
                        verts[i] = Vector3.Lerp(verts[i], avg, lambda);
                    }
                }
            }
        }

        private static float SampleCloudSdf(Vector3 p, EllipsoidLobe[] lobes)
        {
            const float smoothK = 0.42f;
            float d = 10f;
            for (int i = 0; i < lobes.Length; i++)
            {
                Vector3 q = p - lobes[i].Center;
                Vector3 r = lobes[i].Radii;
                float k0 = new Vector3(q.x / r.x, q.y / r.y, q.z / r.z).magnitude;
                float k1 = new Vector3(q.x / (r.x * r.x), q.y / (r.y * r.y), q.z / (r.z * r.z)).magnitude;
                float ell = k1 > 1e-5f ? k0 * (k0 - 1f) / k1 : -Mathf.Min(r.x, Mathf.Min(r.y, r.z));
                d = SmoothMin(d, ell, smoothK);
            }

            // Softly flatten underside like a real buoyant cumulus cloud resting on the mist sea
            float bottomPlane = -(p.y + 0.24f);
            d = SmoothMax(d, bottomPlane, 0.26f);
            return d;
        }

        private static Vector3 SampleCloudGradient(Vector3 p, EllipsoidLobe[] lobes)
        {
            const float eps = 0.025f;
            float gx = SampleCloudSdf(p + new Vector3(eps, 0f, 0f), lobes) - SampleCloudSdf(p - new Vector3(eps, 0f, 0f), lobes);
            float gy = SampleCloudSdf(p + new Vector3(0f, eps, 0f), lobes) - SampleCloudSdf(p - new Vector3(0f, eps, 0f), lobes);
            float gz = SampleCloudSdf(p + new Vector3(0f, 0f, eps), lobes) - SampleCloudSdf(p - new Vector3(0f, 0f, eps), lobes);
            Vector3 g = new Vector3(gx, gy, gz);
            return g.sqrMagnitude > 1e-8f ? g.normalized : Vector3.up;
        }

        private static float SmoothMin(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        private static float SmoothMax(float a, float b, float k)
        {
            return -SmoothMin(-a, -b, k);
        }
    }
}
