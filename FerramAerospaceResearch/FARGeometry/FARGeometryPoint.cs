using System;
using System.Collections.Generic;
using UnityEngine;
using KSP;

namespace FerramAerospaceResearch.FARGeometry
{
    public class FARGeometryPoint
    {
        public Vector3d point;
        public List<FARGeometryLineSegment> connectedLines;
        public Transform parentTransform;

        public FARGeometryPoint(Vector3d thisPoint)
        {
            connectedLines = new List<FARGeometryLineSegment>();
            point = thisPoint;
        }

        public FARGeometryPoint(Vector3d thisPoint, Transform transform)
        {
            connectedLines = new List<FARGeometryLineSegment>();
            point = thisPoint;
            parentTransform = transform;
        }

        public void TransformToLocalSpace(Transform transform)
        {
            Matrix4x4 transformMatrix = parentTransform.localToWorldMatrix;
            transformMatrix = transform.worldToLocalMatrix * transformMatrix;
            TransformToLocalSpace(transformMatrix, transform);
        }

        private void TransformToLocalSpace(Matrix4x4 transformMatrix, Transform transform)
        {
            if (parentTransform == transform)
                return;

            point = transformMatrix.MultiplyPoint(point);
        }
    }
}
