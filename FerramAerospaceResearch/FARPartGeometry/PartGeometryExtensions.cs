using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeometry
{
    public static class PartGeometryExtensions
    {
        public static Bounds GetPartOverallMeshBoundsInBasis(this Part part, Matrix4x4 worldToBasisMatrix, int excessiveVerts = 2500)
        {
            Transform[] transforms = part.FindModelComponents<Transform>();
            Bounds bounds = new Bounds();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];

                MeshFilter mf = t.GetComponent<MeshFilter>();
                if (mf == null)
                    continue;
                Mesh m = mf.sharedMesh;

                if (m == null)
                    continue;
                Matrix4x4 matrix = worldToBasisMatrix * t.localToWorldMatrix;

                if (m.vertices.Length < excessiveVerts)
                    for (int j = 0; j < m.vertices.Length; j++)
                    {
                        bounds.Encapsulate(matrix.MultiplyPoint(m.vertices[j]));
                    }
                else
                {
                    bounds.Encapsulate(matrix.MultiplyPoint(m.bounds.min));
                    bounds.Encapsulate(matrix.MultiplyPoint(m.bounds.max));
                }
            }
            return bounds;
        }
        
        public static Bounds[] GetPartMeshBoundsInPartSpace(this Part part, int excessiveVerts = 2500)
        {
            Transform[] transforms = part.FindModelComponents<Transform>();
            Bounds[] bounds = new Bounds[transforms.Length];
            Matrix4x4 partMatrix = part.transform.worldToLocalMatrix;
            for (int i = 0; i < transforms.Length; i++)
            {
                Bounds newBounds = new Bounds();
                Transform t = transforms[i];

                MeshFilter mf = t.GetComponent<MeshFilter>();
                if (mf == null)
                    continue;
                Mesh m = mf.sharedMesh;

                if (m == null)
                    continue;
                Matrix4x4 matrix = partMatrix * t.localToWorldMatrix;

                if (m.vertices.Length < excessiveVerts)
                    for (int j = 0; j < m.vertices.Length; j++)
                    {
                        newBounds.Encapsulate(matrix.MultiplyPoint(m.vertices[j]));
                    }
                else
                {
                    newBounds.SetMinMax(matrix.MultiplyPoint(m.bounds.min), matrix.MultiplyPoint(m.bounds.max));
                }

                bounds[i] = newBounds;
            }
            return bounds;
        }

        public static Bounds[] GetPartMeshBoundsListInBasis(this Part part, Transform basis, int excessiveVerts = 2500)
        {
            Transform[] transforms = part.FindModelComponents<Transform>();
            Bounds[] bounds = new Bounds[transforms.Length];
            Matrix4x4 partMatrix = basis.worldToLocalMatrix;
            for (int i = 0; i < transforms.Length; i++)
            {
                Bounds newBounds = new Bounds();
                Transform t = transforms[i];

                MeshFilter mf = t.GetComponent<MeshFilter>();
                if (mf == null)
                    continue;
                Mesh m = mf.sharedMesh;

                if (m == null)
                    continue;
                Matrix4x4 matrix = partMatrix * t.localToWorldMatrix;

                if (m.vertices.Length < excessiveVerts)
                    for (int j = 0; j < m.vertices.Length; j++)
                    {
                        newBounds.Encapsulate(matrix.MultiplyPoint(m.vertices[j]));
                    }
                else
                {
                    newBounds.SetMinMax(matrix.MultiplyPoint(m.bounds.min), matrix.MultiplyPoint(m.bounds.max));
                }

                bounds[i] = newBounds;
            }
            return bounds;
        }
    }
}
