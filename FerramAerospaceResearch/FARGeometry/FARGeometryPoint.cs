using System;
using System.Collections.Generic;
using UnityEngine;
using KSP;

namespace FerramAerospaceResearch.FARGeometry
{
    public class FARGeometryPoint : IComparable<FARGeometryPoint>
    {
        public Vector3d point;
        public Part associatedPart;
        public List<FARGeometryLineSegment> connectedLines;
        public Transform parentTransform;

        public FARGeometryPoint(Vector3d thisPoint, Part p)
        {
            connectedLines = new List<FARGeometryLineSegment>();
            point = thisPoint;
            associatedPart = p;
            parentTransform = p.transform;
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

        public int CompareTo(FARGeometryPoint other)
        {
            int tmp = this.point.x.CompareTo(other.point.x);   //Must use CompareTo, not < / >
            if (tmp == 0)
                return this.point.y.CompareTo(other.point.y);
            return tmp;
        }
        
    }
}
