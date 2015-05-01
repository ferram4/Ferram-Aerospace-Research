using System;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeometry
{
    public struct PartTransformInfo
    {
        public Matrix4x4 worldToLocalMatrix;
        public Vector3 worldPosition;

        public PartTransformInfo(Transform t)
        {
            worldToLocalMatrix = t.worldToLocalMatrix;
            worldPosition = t.position;
        }
    }
}
