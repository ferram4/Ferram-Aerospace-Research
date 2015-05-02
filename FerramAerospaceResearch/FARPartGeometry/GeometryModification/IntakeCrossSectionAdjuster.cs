using System;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeometry.GeometryModification
{
    class IntakeCrossSectionAdjuster : ICrossSectionAdjuster
    {
        const double INTAKE_AREA_SCALAR = 75;

        Vector3 vehicleBasisForwardVector;
        double intakeArea;
        Matrix4x4 thisToWorldMatrix;

        ModuleResourceIntake intake;
        public ModuleResourceIntake IntakeModule
        {
            get { return intake; }
        }
        Part part;
        public Part GetPart()
        {
            return part;
        }

        public IntakeCrossSectionAdjuster(ModuleResourceIntake intake, Matrix4x4 worldToVesselMatrix)
        {
            Transform intakeTrans = intake.part.FindModelTransform(intake.intakeTransformName);
            if ((object)intakeTrans != null)
                vehicleBasisForwardVector = intakeTrans.forward;

            vehicleBasisForwardVector = worldToVesselMatrix.MultiplyVector(vehicleBasisForwardVector);

            intakeArea = INTAKE_AREA_SCALAR * intake.area;

            thisToWorldMatrix = worldToVesselMatrix.inverse;

            this.intake = intake;
            this.part = intake.part;
        }

        public double AreaRemovedFromCrossSection(Vector3 vehicleAxis)
        {
            double dot = Vector3.Dot(vehicleAxis, vehicleBasisForwardVector);
            if (dot > 0.9)
                return intakeArea;
            else
                return 0;
        }

        public double AreaRemovedFromCrossSection()
        {
            return intakeArea;
        }


        public void TransformBasis(Matrix4x4 matrix)
        {
            //Matrix4x4 tempMatrix = matrix * thisToWorldMatrix;

            Transform intakeTrans = intake.part.FindModelTransform(intake.intakeTransformName);
            if ((object)intakeTrans != null)
                vehicleBasisForwardVector = intakeTrans.forward;

            vehicleBasisForwardVector = matrix.MultiplyVector(vehicleBasisForwardVector);

            thisToWorldMatrix = matrix.inverse;
        }
    }
}
