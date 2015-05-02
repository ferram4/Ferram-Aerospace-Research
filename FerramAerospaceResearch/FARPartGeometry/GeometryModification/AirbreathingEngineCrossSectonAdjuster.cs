using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeometry.GeometryModification
{
    class AirbreathingEngineCrossSectonAdjuster : ICrossSectionAdjuster
    {
        Vector3 vehicleBasisForwardVector;
        double exitArea;

        public AirbreathingEngineCrossSectonAdjuster(ModuleEngines engine, Matrix4x4 worldToVesselMatrix)
        {
            for (int i = 0; i < engine.thrustTransforms.Count; i++)
                vehicleBasisForwardVector = engine.thrustTransforms[i].forward;

            vehicleBasisForwardVector = worldToVesselMatrix.MultiplyVector(vehicleBasisForwardVector);

            vehicleBasisForwardVector.Normalize();
        }

        public void SetExitArea(double area)
        {
            exitArea = area;
        }

        public double AreaRemovedFromCrossSection(Vector3 vehicleAxis)
        {
            double dot = Vector3.Dot(vehicleAxis, vehicleBasisForwardVector);
            if (dot > 0.9)
                return exitArea;
            else
                return 0;
        }

        public void TransformBasis(Matrix4x4 matrix)
        {
            vehicleBasisForwardVector = matrix.MultiplyVector(vehicleBasisForwardVector);
        }
    }
}
