using System;
using UnityEngine;

namespace LumiWorld.Acs
{
    public static class BiomeSurfaceReader
    {
        public static DioramaPlan.Surface Read(Transform root)
        {
            return DioramaSurface.Sample(ReadGeometry(root, "Ground_Basin", true), ReadGeometry(root, "Water_Surface", false));
        }

        private static DioramaSurface.Geometry ReadGeometry(Transform root, string name, bool required)
        {
            Transform child = root.Find(name);
            MeshFilter filter = child == null ? null : child.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                if (required) throw new InvalidOperationException("Missing habitat surface: " + name);
                return null;
            }
            Matrix4x4 matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            return new DioramaSurface.Geometry
            {
                indices = filter.sharedMesh.triangles,
                vertices = Array.ConvertAll(filter.sharedMesh.vertices, vertex =>
                {
                    Vector3 point = matrix.MultiplyPoint3x4(vertex);
                    return new DioramaSurface.Vertex { x = point.x, y = point.y, z = point.z };
                })
            };
        }
    }
}
