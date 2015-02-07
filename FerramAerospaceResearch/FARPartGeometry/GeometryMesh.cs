using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeometry
{
    public class GeometryMesh
    {
        public Vector3[] vertices;
        public int[] triangles;
        public Transform meshTransform;
        public Matrix4x4 thisToVesselMatrix;
        public Bounds bounds;

        public GeometryMesh(Vector3[] untransformedVerts, int[] triangles, Bounds meshBounds, Transform meshTransform, Matrix4x4 worldToVesselMatrix)
        {
            vertices = new Vector3[untransformedVerts.Length];
            this.thisToVesselMatrix = worldToVesselMatrix * meshTransform.localToWorldMatrix;

            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = thisToVesselMatrix.MultiplyPoint3x4(untransformedVerts[i]);
            
            this.triangles = triangles;
            this.meshTransform = meshTransform;

            bounds = new Bounds(thisToVesselMatrix.MultiplyPoint3x4(meshBounds.center), thisToVesselMatrix.MultiplyVector(meshBounds.size));
        }

        public void TransformBasis(Matrix4x4 newThisToVesselMatrix)
        {
            Matrix4x4 tempMatrix = thisToVesselMatrix.inverse;
            thisToVesselMatrix = newThisToVesselMatrix * meshTransform.localToWorldMatrix;

            tempMatrix = thisToVesselMatrix * tempMatrix;

            bounds = new Bounds(thisToVesselMatrix.MultiplyPoint3x4(bounds.center), thisToVesselMatrix.MultiplyVector(bounds.size));

            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = tempMatrix.MultiplyPoint3x4(vertices[i]);

        }
    }
}
