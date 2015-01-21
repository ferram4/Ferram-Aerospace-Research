using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using KSP;
using ferram4;

namespace FerramAerospaceResearch.FARPartGeoUtil
{
    class CrossSectionCurveGenerator
    {
        public void GetCrossSectionalAreaCurves(Part p, out FloatCurve xArea, out FloatCurve yArea, out FloatCurve zArea)
        {
            Transform partTransform = p.transform;
            xArea = new FloatCurve();
            yArea = new FloatCurve();
            zArea = new FloatCurve();

            Bounds colliderBounds, meshBounds;

            colliderBounds = PartGeometryUtil.MergeBounds(p.GetColliderBounds(), partTransform);
            meshBounds = PartGeometryUtil.MergeBounds(p.GetRendererBounds(), partTransform);

            List<Vector3> vertexList;
            List<int> triangleIndices;
            if(UseMeshBounds(colliderBounds, meshBounds, 0.05f))
            {
                Transform[] meshTransforms = FARGeoUtil.PartModelTransformArray(p);
                Mesh[] meshes = new Mesh[meshTransforms.Length];

                for(int i = 0; i < meshTransforms.Length; i++)
                {
                    MeshFilter mf = meshTransforms[i].GetComponent<MeshFilter>();
                    if (mf == null)
                        continue;
                    meshes[i] = mf.sharedMesh;
                }
                vertexList = GetVertexList(meshes, meshTransforms, partTransform);
                triangleIndices = GetTriangleVerts(meshes);
            }
            else
            {
                MeshCollider[] meshColliders = p.GetComponents<MeshCollider>();
                Transform[] meshTransforms = new Transform[meshColliders.Length];
                Mesh[] meshes = new Mesh[meshColliders.Length];

                for (int i = 0; i < meshColliders.Length; i++)
                {
                    MeshCollider mc = meshColliders[i];
                    meshTransforms[i] = mc.transform;
                    meshes[i] = mc.sharedMesh;
                }
                vertexList = GetVertexList(meshes, meshTransforms, partTransform);
                triangleIndices = GetTriangleVerts(meshes);
            }
        }

        private bool UseMeshBounds(Bounds colliderBounds, Bounds meshBounds, float relTolerance)
        {
            Vector3 absTolerance = (meshBounds.max - meshBounds.min) * relTolerance;

            Vector3 maxTest = meshBounds.max - colliderBounds.max;
            if (maxTest.x > absTolerance.x || maxTest.y > absTolerance.y || maxTest.z > absTolerance.z)
                return true;

            Vector3 minTest = meshBounds.min - colliderBounds.min;
            if (minTest.x > absTolerance.x || minTest.y > absTolerance.y || minTest.z > absTolerance.z)
                return true;

            return false;
        }

        private List<Vector3> GetVertexList(Mesh[] meshes, Transform[] meshTransforms, Transform partTransform)
        {
            List<Vector3> vertices = new List<Vector3>();

            for (int i = 0; i < meshes.Length; i++)
            {
                Mesh m = meshes[i];
                Matrix4x4 matrix = partTransform.worldToLocalMatrix * meshTransforms[i].localToWorldMatrix;

                for(int j = 0; j < m.vertices.Length; j++)
                {
                    Vector3 v = matrix.MultiplyPoint(m.vertices[j]);
                    vertices.Add(v);
                }
            }
            return vertices;
        }

        private List<int> GetTriangleVerts(Mesh[] meshes)
        {
            List<int> triIndices = new List<int>();
            for (int i = 0; i < meshes.Length; i++)
                triIndices.AddRange(meshes[i].triangles);

            return triIndices;
        }
    }
}
