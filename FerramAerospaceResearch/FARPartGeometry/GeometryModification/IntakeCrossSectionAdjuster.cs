using System;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeometry.GeometryModification
{
    class IntakeCrossSectionAdjuster : ICrossSectionAdjuster
    {
        const double INTAKE_AREA_SCALAR = 75;

        Vector3 vehicleBasisForwardVector;
        double intakeArea;

        public IntakeCrossSectionAdjuster(ModuleResourceIntake intake)
        {
            Transform intakeTrans = intake.part.FindModelTransform(intake.intakeTransformName);
            if ((object)intakeTrans != null)
                vehicleBasisForwardVector = intakeTrans.forward;

            intakeArea = INTAKE_AREA_SCALAR * intake.area;
        }

        public double AreaRemovedFromCrossSection(Vector3 vehicleAxis)
        {
            double dot = Vector3.Dot(vehicleAxis, vehicleBasisForwardVector);
            if (dot > 0.9)
                return intakeArea;
            else
                return 0;
        }

        public void TransformBasis(Matrix4x4 matrix)
        {
            vehicleBasisForwardVector = matrix.MultiplyVector(vehicleBasisForwardVector);
        }
    }
}
