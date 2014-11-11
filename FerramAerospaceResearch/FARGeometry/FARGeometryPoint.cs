using System;
using System.Collections.Generic;
using UnityEngine;
using KSP;

namespace FerramAerospaceResearch.FARGeometry
{
    public class FARGeometryPoint
    {
        public Vector3d point;
        public List<FARGeometryPoint> connectedPoints;
        public Transform parentTransform;
        public FARGeometryPartPolygon parentPoly;

        public FARGeometryPoint(Vector3d thisPoint)
        {
            connectedPoints = new List<FARGeometryPoint>();
            point = thisPoint;
        }

        public FARGeometryPoint(Vector3d thisPoint, FARGeometryPartPolygon poly, Transform transform)
        {
            connectedPoints = new List<FARGeometryPoint>();
            point = thisPoint;
            parentPoly = poly;
            parentTransform = transform;
        }

        public void TransformToLocalSpace(Transform transform)
        {
            Matrix4x4 transformMatrix = parentTransform.localToWorldMatrix;
            transformMatrix = transform.worldToLocalMatrix * transformMatrix;

            for(int i = 0; i < connectedPoints.Count; i++)
            {
                connectedPoints[i].TransformToLocalSpace(transformMatrix, transform);
            }
        }

        private void TransformToLocalSpace(Matrix4x4 transformMatrix, Transform transform)
        {
            if (parentTransform == transform)
                return;

            point = transformMatrix.MultiplyPoint(point);

            for (int i = 0; i < connectedPoints.Count; i++)
            {
                connectedPoints[i].TransformToLocalSpace(transformMatrix, transform);
            }
        }
    }
}
