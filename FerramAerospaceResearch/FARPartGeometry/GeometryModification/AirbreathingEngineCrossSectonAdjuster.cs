using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeometry.GeometryModification
{
    class AirbreathingEngineCrossSectonAdjuster : ICrossSectionAdjuster
    {
        Vector3 vehicleBasisForwardVector;
        double exitArea;
        Matrix4x4 thisToWorldMatrix;

        ModuleEngines engine;
        public ModuleEngines EngineModule
        {
            get { return engine; }
        }
        Part part;
        public Part GetPart()
        {
            return part;
        }

        public AirbreathingEngineCrossSectonAdjuster(ModuleEngines engine, Matrix4x4 worldToVesselMatrix)
        {
            vehicleBasisForwardVector = Vector3.zero;
            for (int i = 0; i < engine.thrustTransforms.Count; i++)
                vehicleBasisForwardVector += engine.thrustTransforms[i].forward;

            vehicleBasisForwardVector = worldToVesselMatrix.MultiplyVector(vehicleBasisForwardVector);

            vehicleBasisForwardVector.Normalize();
            vehicleBasisForwardVector *= -1f;

            thisToWorldMatrix = worldToVesselMatrix.inverse;

            this.engine = engine;
            this.part = engine.part;

            exitArea = -2;
        }

        public void CalculateExitArea(double areaPerUnitThrust)
        {
            exitArea = -areaPerUnitThrust * engine.maxThrust;       //we make this negative to account for it leaving through this direction
        }

        public double AreaRemovedFromCrossSection(Vector3 vehicleAxis)
        {
            double dot = Vector3.Dot(vehicleAxis, vehicleBasisForwardVector);
            if (dot > 0.9)
                return exitArea;
            else
                return 0;
        }

        public double AreaRemovedFromCrossSection()
        {
            return exitArea;
        }

        public void TransformBasis(Matrix4x4 matrix)
        {
            vehicleBasisForwardVector = Vector3.zero;
            for (int i = 0; i < engine.thrustTransforms.Count; i++)
                vehicleBasisForwardVector = engine.thrustTransforms[i].forward;

            vehicleBasisForwardVector = matrix.MultiplyVector(vehicleBasisForwardVector);

            thisToWorldMatrix = matrix.inverse;
        }
    }
}
