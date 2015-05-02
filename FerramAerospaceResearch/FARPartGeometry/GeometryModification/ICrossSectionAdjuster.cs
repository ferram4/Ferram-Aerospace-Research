using System;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeometry.GeometryModification
{
    public interface ICrossSectionAdjuster
    {
        Part GetPart();

        double AreaRemovedFromCrossSection(Vector3 orientationVector);

        double AreaRemovedFromCrossSection();

        void TransformBasis(Matrix4x4 transform);
    }
}
