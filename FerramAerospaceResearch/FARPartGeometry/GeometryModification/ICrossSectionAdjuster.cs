using System;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeometry.GeometryModification
{
    interface ICrossSectionAdjuster
    {
        double AreaRemovedFromCrossSection(Vector3 orientationVector);

        void TransformBasis(Matrix4x4 transform);
    }
}
