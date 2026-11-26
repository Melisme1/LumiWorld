using System;
using System.Collections.Generic;
using UnityEngine;

namespace LumiWorld.Acs
{
    [Serializable]
    public struct BiomeActivitySample
    {
        public Vector3 localPosition;
        public float surfaceY;
        public float waterDepth;
        public float clearanceRadius;
    }

    [Serializable]
    public struct BiomeObstacleCircle
    {
        public Vector2 localXZ;
        public float radius;
        public float topY;
        public string label;
    }

    /// <summary>
    /// Pure runtime spatial activity zone data for a biome cluster (Walkable dry ground,
    /// Swimmable water with depth, and HardObstacles). Decoupled from animal AI so Ground,
    /// Flying, Swimming, Amphibian, and future Ocean/Sea biome navigators can read it directly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BiomeActivityZones : MonoBehaviour
    {
        [Header("Cluster Metadata")]
        [SerializeField] private int clusterSize = 6;
        [SerializeField] private float waterSurfaceY = 1.0f;

        [Header("Generated Spatial Data")]
        [SerializeField] private List<BiomeActivitySample> walkableSpots = new List<BiomeActivitySample>();
        [SerializeField] private List<BiomeActivitySample> swimmableSpots = new List<BiomeActivitySample>();
        [SerializeField] private List<BiomeObstacleCircle> hardObstacles = new List<BiomeObstacleCircle>();

        [Header("Scene Gizmos")]
        [SerializeField] private bool drawGizmosAlways = false;
        [SerializeField] private bool hideGizmos = false;

        public int ClusterSize => clusterSize;
        public float WaterSurfaceY => waterSurfaceY;
        public IReadOnlyList<BiomeActivitySample> WalkableSpots => walkableSpots;
        public IReadOnlyList<BiomeActivitySample> SwimmableSpots => swimmableSpots;
        public IReadOnlyList<BiomeObstacleCircle> HardObstacles => hardObstacles;
        public bool DrawGizmosAlways
        {
            get => drawGizmosAlways;
            set => drawGizmosAlways = value;
        }
        public bool HideGizmos
        {
            get => hideGizmos;
            set => hideGizmos = value;
        }

        public void SetZoneData(
            int size,
            float localWaterSurfaceY,
            List<BiomeActivitySample> walkable,
            List<BiomeActivitySample> swimmable,
            List<BiomeObstacleCircle> obstacles)
        {
            clusterSize = size;
            waterSurfaceY = localWaterSurfaceY;
            walkableSpots = walkable ?? new List<BiomeActivitySample>();
            swimmableSpots = swimmable ?? new List<BiomeActivitySample>();
            hardObstacles = obstacles ?? new List<BiomeObstacleCircle>();
        }

        public bool TryGetRandomWalkableWorld(System.Random rng, out Vector3 worldPos)
        {
            if (walkableSpots == null || walkableSpots.Count == 0)
            {
                worldPos = transform.position;
                return false;
            }

            int idx = rng != null ? rng.Next(walkableSpots.Count) : UnityEngine.Random.Range(0, walkableSpots.Count);
            worldPos = transform.TransformPoint(walkableSpots[idx].localPosition);
            return true;
        }

        public bool TryGetRandomSwimmableWorld(
            System.Random rng,
            float minWaterDepth,
            out Vector3 worldPos,
            out float worldWaterSurfaceY,
            out float depth)
        {
            if (swimmableSpots == null || swimmableSpots.Count == 0)
            {
                worldPos = transform.position;
                worldWaterSurfaceY = transform.TransformPoint(new Vector3(0f, waterSurfaceY, 0f)).y;
                depth = 0f;
                return false;
            }

            int matchCount = 0;
            int chosenIndex = -1;
            for (int i = 0; i < swimmableSpots.Count; i++)
            {
                if (swimmableSpots[i].waterDepth >= minWaterDepth)
                {
                    matchCount++;
                    int pick = rng != null ? rng.Next(matchCount) : UnityEngine.Random.Range(0, matchCount);
                    if (pick == 0)
                    {
                        chosenIndex = i;
                    }
                }
            }

            if (chosenIndex < 0)
            {
                worldPos = transform.position;
                worldWaterSurfaceY = transform.TransformPoint(new Vector3(0f, waterSurfaceY, 0f)).y;
                depth = 0f;
                return false;
            }

            BiomeActivitySample s = swimmableSpots[chosenIndex];
            worldPos = transform.TransformPoint(s.localPosition);
            worldWaterSurfaceY = transform.TransformPoint(new Vector3(s.localPosition.x, s.surfaceY, s.localPosition.z)).y;
            depth = s.waterDepth;
            return true;
        }

        public bool IsBlockedLocalXZ(Vector2 localXZ, float agentRadius)
        {
            if (hardObstacles == null) return false;
            for (int i = 0; i < hardObstacles.Count; i++)
            {
                float r = hardObstacles[i].radius + agentRadius;
                Vector2 delta = localXZ - hardObstacles[i].localXZ;
                if (delta.sqrMagnitude < r * r)
                {
                    return true;
                }
            }
            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!hideGizmos && drawGizmosAlways)
            {
                DrawActivityGizmos();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!hideGizmos && !drawGizmosAlways)
            {
                DrawActivityGizmos();
            }
        }

        private void DrawActivityGizmos()
        {
            Matrix4x4 prev = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            // 1. Walkable dry spots (green)
            Gizmos.color = new Color(0.22f, 0.92f, 0.35f, 0.72f);
            if (walkableSpots != null)
            {
                for (int i = 0; i < walkableSpots.Count; i++)
                {
                    BiomeActivitySample s = walkableSpots[i];
                    DrawWireDisc(s.localPosition + Vector3.up * 0.03f, Mathf.Clamp(s.clearanceRadius * 0.45f, 0.12f, 0.28f), 12);
                }
            }

            // 2. Swimmable water spots (cyan + vertical depth tick)
            Gizmos.color = new Color(0.15f, 0.85f, 1.0f, 0.85f);
            if (swimmableSpots != null)
            {
                for (int i = 0; i < swimmableSpots.Count; i++)
                {
                    BiomeActivitySample s = swimmableSpots[i];
                    Vector3 top = new Vector3(s.localPosition.x, s.surfaceY + 0.02f, s.localPosition.z);
                    DrawWireDisc(top, Mathf.Clamp(s.clearanceRadius * 0.45f, 0.12f, 0.28f), 12);
                    Gizmos.DrawLine(top, top - Vector3.up * Mathf.Max(0.05f, s.waterDepth));
                }
            }

            // 3. Hard obstacles (coral-red)
            Gizmos.color = new Color(1.0f, 0.28f, 0.22f, 0.85f);
            if (hardObstacles != null)
            {
                for (int i = 0; i < hardObstacles.Count; i++)
                {
                    BiomeObstacleCircle obs = hardObstacles[i];
                    Vector3 p = new Vector3(obs.localXZ.x, obs.topY + 0.04f, obs.localXZ.y);
                    DrawWireDisc(p, Mathf.Max(0.12f, obs.radius), 18);
                }
            }

            Gizmos.matrix = prev;
        }

        private static void DrawWireDisc(Vector3 center, float radius, int segments)
        {
            float step = Mathf.PI * 2f / segments;
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i * step;
                Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
#endif
    }
}
