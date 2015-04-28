using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FerramAerospaceResearch.FARAeroComponents
{
    class FARAeroSection
    {
        static FloatCurve crossFlowDragMachCurve;
        static FloatCurve crossFlowDragReynoldsCurve;

        FloatCurve xForcePressureAoA0;
        FloatCurve xForcePressureAoA180;
        FloatCurve xForceSkinFriction;
        float potentialFlowNormalForce;
        float viscCrossflowDrag;
        float flatnessRatio;
        float invFlatnessRatio;
        float hypersonicMomentForward;
        float hypersonicMomentBackward;
        float diameter;

        List<PartData> partsIncluded;

        public struct PartData
        {
            public FARAeroPartModule aeroModule;
            public Vector3 centroidPartSpace;
            public Vector3 xRefVectorPartSpace;
            public Vector3 nRefVectorPartSpace;
            public float dragFactor;    //sum of these should add up to 1
            public float iP, iN, jP, jN, kP, kN;    //part local x, y, and z areas for heating
        }

        public FARAeroSection(FloatCurve xForcePressureAoA0, FloatCurve xForcePressureAoA180, FloatCurve xForceSkinFriction,
            float potentialFlowNormalForce, float viscCrossflowDrag, float diameter, float flatnessRatio, float hypersonicMomentForward, float hypersonicMomentBackward,
            Vector3 centroidWorldSpace, Vector3 xRefVectorWorldSpace, Vector3 nRefVectorWorldSpace, Matrix4x4 vesselToWorldMatrix, Vector3 vehicleMainAxis, List<FARAeroPartModule> moduleList,
            Dictionary<Part, FARPartGeometry.VoxelCrossSection.SideAreaValues> sideAreaValues, List<float> dragFactor)
        {
            this.xForcePressureAoA0 = xForcePressureAoA0;       //copy references to floatcurves over
            this.xForcePressureAoA180 = xForcePressureAoA180;
            this.xForceSkinFriction = xForceSkinFriction;

            this.potentialFlowNormalForce = potentialFlowNormalForce;                   //copy lifting body info over
            this.viscCrossflowDrag = viscCrossflowDrag;
            this.flatnessRatio = flatnessRatio;
            invFlatnessRatio = 1 / flatnessRatio;
            this.hypersonicMomentForward = hypersonicMomentForward;
            this.hypersonicMomentBackward = hypersonicMomentBackward;
            this.diameter = diameter;

            partsIncluded = new List<PartData>();

            Vector3 worldVehicleAxis = vesselToWorldMatrix.MultiplyVector(vehicleMainAxis);

            Vector3 centroidLocationAlongxRef = Vector3.Project(centroidWorldSpace, worldVehicleAxis);
            Vector3 centroidSansxRef = Vector3.ProjectOnPlane(centroidWorldSpace, worldVehicleAxis);

            Vector3 worldSpaceAvgPos = Vector3.zero;
            float totalDragFactor = 0;
            for (int i = 0; i < moduleList.Count; i++)
            {
                Part p = moduleList[i].part;
                worldSpaceAvgPos += p.transform.position * dragFactor[i];
                totalDragFactor += dragFactor[i];
            }

            worldSpaceAvgPos /= totalDragFactor;

            worldSpaceAvgPos = Vector3.ProjectOnPlane(worldSpaceAvgPos, worldVehicleAxis);

            Vector3 avgPosDiffFromCentroid = centroidSansxRef - worldSpaceAvgPos;
            
            for (int i = 0; i < moduleList.Count; i++)
            {
                PartData data = new PartData();
                data.aeroModule = moduleList[i];
                Transform transform = data.aeroModule.part.partTransform;
                Matrix4x4 transformMatrix = transform.worldToLocalMatrix;

                Vector3 forceCenterWorldSpace = centroidLocationAlongxRef + Vector3.ProjectOnPlane(transform.position, worldVehicleAxis) + avgPosDiffFromCentroid;

                data.centroidPartSpace = transformMatrix.MultiplyPoint3x4(forceCenterWorldSpace);
                data.xRefVectorPartSpace = transformMatrix.MultiplyVector(xRefVectorWorldSpace);
                data.nRefVectorPartSpace = transformMatrix.MultiplyVector(nRefVectorWorldSpace);
                data.dragFactor = dragFactor[i];

                FARPartGeometry.VoxelCrossSection.SideAreaValues values = sideAreaValues[data.aeroModule.part];
                Vector3 posAreas = new Vector3((float)values.iP, (float)values.jP, (float)values.kP);
                Vector3 negAreas = new Vector3((float)values.iN, (float)values.jN, (float)values.kN);

                transformMatrix = transformMatrix * vesselToWorldMatrix;

                IncrementAreas(ref data, (float)values.iP * Vector3.right, transformMatrix);
                IncrementAreas(ref data, (float)values.iN * -Vector3.right, transformMatrix);
                IncrementAreas(ref data, (float)values.jP * Vector3.up, transformMatrix);
                IncrementAreas(ref data, (float)values.jN * -Vector3.up, transformMatrix);
                IncrementAreas(ref data, (float)values.kP * Vector3.forward, transformMatrix);
                IncrementAreas(ref data, (float)values.kN * -Vector3.forward, transformMatrix);
                
                partsIncluded.Add(data);
            }

            if (crossFlowDragMachCurve == null)
                GenerateCrossFlowDragCurve();
        }

        private void IncrementAreas(ref PartData data, Vector3 vector, Matrix4x4 transformMatrix)
        {
            vector = transformMatrix.MultiplyVector(vector);

            if (vector.x >= 0)
                data.iP += vector.x;
            else
                data.iN -= vector.x;

            if (vector.y >= 0)
                data.jP += vector.y;
            else
                data.jN -= vector.y;

            if (vector.z >= 0)
                data.kP += vector.z;
            else
                data.kN -= vector.z;
        }

        public void LEGACY_SetLiftForFARWingAerodynamicModel()
        {
            for(int i = 0; i < partsIncluded.Count; i++)
            {
                PartData data = partsIncluded[i];
                Part p = data.aeroModule.part;
                if (p == null)
                    continue;

                ferram4.FARWingAerodynamicModel w = p.GetComponent<ferram4.FARWingAerodynamicModel>();
                if (w == null)
                    continue;

                double minShownArea = Math.Min(data.kN, data.kP);
                w.NUFAR_IncrementAreaExposedFactor(minShownArea);
            }
        }

        public void EditorCalculateAeroForces(float atmDensity, float machNumber, float reynoldsPerUnitLength, float skinFrictionDrag, Vector3 vel, ferram4.FARCenterQuery center)
        {
            if (partsIncluded.Count == 0)
                return;

            double skinFrictionForce = skinFrictionDrag * xForceSkinFriction.Evaluate(machNumber);      //this will be the same for each part, so why recalc it multiple times?
            float xForceAoA0 = xForcePressureAoA0.Evaluate(machNumber);
            float xForceAoA180 = xForcePressureAoA180.Evaluate(machNumber);


            PartData data = partsIncluded[0];
            FARAeroPartModule aeroModule = data.aeroModule;

            Vector3 xRefVector = data.xRefVectorPartSpace;
            Vector3 nRefVector = data.nRefVectorPartSpace;

            Vector3 velLocal = aeroModule.part.partTransform.worldToLocalMatrix.MultiplyVector(vel);

            //Vector3 angVelLocal = aeroModule.partLocalAngVel;

            //velLocal += Vector3.Cross(angVelLocal, data.centroidPartSpace);       //some transform issue here, needs investigation
            Vector3 velLocalNorm = velLocal.normalized;

            Vector3 localNormalForceVec = Vector3.ProjectOnPlane(-velLocalNorm, xRefVector).normalized;

            double cosAoA = Vector3.Dot(xRefVector, velLocalNorm);
            double cosSqrAoA = cosAoA * cosAoA;
            double sinSqrAoA = Math.Max(1 - cosSqrAoA, 0);
            double sinAoA = Math.Sqrt(sinSqrAoA);
            double sin2AoA = 2 * sinAoA * Math.Abs(cosAoA);
            double cosHalfAoA = Math.Sqrt(0.5 + 0.5 * Math.Abs(cosAoA));


            double nForce = 0;
            if (machNumber < 6)
                nForce = cosHalfAoA * sin2AoA * potentialFlowNormalForce * Math.Sign(cosAoA);  //potential flow normal force
            if (nForce < 0)     //potential flow is not significant over the rear face of things
                nForce = 0;
            //if (machNumber > 3)
            //    nForce *= 2d - machNumber * 0.3333333333333333d;

            float normalForceFactor = Math.Abs(Vector3.Dot(localNormalForceVec, nRefVector));
            normalForceFactor *= normalForceFactor;

            normalForceFactor = invFlatnessRatio * (1 - normalForceFactor) + flatnessRatio * normalForceFactor;     //accounts for changes in relative flatness of shape


            float crossFlowMach, crossFlowReynolds;
            crossFlowMach = machNumber * (float)sinAoA;
            crossFlowReynolds = reynoldsPerUnitLength * diameter * normalForceFactor * (float)sinAoA;

            nForce += viscCrossflowDrag * sinSqrAoA * CalculateCrossFlowDrag(crossFlowMach, crossFlowReynolds);            //viscous crossflow normal force

            nForce *= normalForceFactor;

            double xForce = -skinFrictionForce * Math.Sign(cosAoA) * cosSqrAoA;
            float moment = (float)(cosAoA * sinAoA);


            if (cosAoA > 0)
            {
                xForce += cosSqrAoA * xForceAoA0;
                float momentFactor;
                if (machNumber > 6)
                    momentFactor = hypersonicMomentForward * 0.4f;
                else if (machNumber < 0.6)
                    momentFactor = 0.6f * hypersonicMomentBackward;
                else
                {
                    float tmp = (-0.185185185f * machNumber + 1.11111111111f);
                    momentFactor = tmp * hypersonicMomentBackward * 0.6f + (1 - tmp) * hypersonicMomentForward * 0.4f;
                }
                //if (machNumber < 1.5)
                //    momentFactor += hypersonicMomentBackward * (0.5f - machNumber * 0.33333333333333333333333333333333f) * 0.2f;

                moment *= momentFactor;
            }
            else
            {
                xForce += cosSqrAoA * xForceAoA180;
                float momentFactor;     //negative to deal with the ref vector facing the opposite direction, causing the moment vector to point in the opposite direction
                if (machNumber > 6)
                    momentFactor = hypersonicMomentBackward * 0.4f;
                else if (machNumber < 0.6)
                    momentFactor = 0.6f * hypersonicMomentForward;
                else
                {
                    float tmp = (-0.185185185f * machNumber + 1.11111111111f);
                    momentFactor = tmp * hypersonicMomentForward * 0.6f + (1 - tmp) * hypersonicMomentBackward * 0.4f;
                }
                //if (machNumber < 1.5)
                //    momentFactor += hypersonicMomentForward * (0.5f - machNumber * 0.33333333333333333333333333333333f) * 0.2f;

                moment *= momentFactor;
            }
            moment /= normalForceFactor;

            Vector3 forceVector = (float)xForce * xRefVector + (float)nForce * localNormalForceVec;
            Vector3 torqueVector = Vector3.Cross(xRefVector, localNormalForceVec) * moment;

            Matrix4x4 localToWorld = aeroModule.part.partTransform.localToWorldMatrix;

            float dynPresAndScaling = 0.0005f * atmDensity * velLocal.sqrMagnitude;        //dyn pres and N -> kN conversion

            forceVector *= dynPresAndScaling;
            torqueVector *= dynPresAndScaling;

            forceVector = localToWorld.MultiplyVector(forceVector);
            torqueVector = localToWorld.MultiplyVector(torqueVector);
            Vector3 centroid = Vector3.zero;

            for (int i = 0; i < partsIncluded.Count; i++)
            {
                PartData partData = partsIncluded[i];
                FARAeroPartModule module = partData.aeroModule;
                if ((object)aeroModule == null)
                    continue;

                centroid = module.part.partTransform.localToWorldMatrix.MultiplyPoint3x4(partData.centroidPartSpace);
                center.AddForce(centroid, forceVector * partData.dragFactor);
                center.AddTorque(torqueVector * partData.dragFactor);
            }
        }

        public void FlightCalculateAeroForces(float atmDensity, float machNumber, float reynoldsPerUnitLength, float skinFrictionDrag)
        {

            double skinFrictionForce = skinFrictionDrag * xForceSkinFriction.Evaluate(machNumber);      //this will be the same for each part, so why recalc it multiple times?
            float xForceAoA0 = xForcePressureAoA0.Evaluate(machNumber);
            float xForceAoA180 = xForcePressureAoA180.Evaluate(machNumber);

            for(int i = 0; i < partsIncluded.Count; i++)
            {
                PartData data = partsIncluded[i];
                FARAeroPartModule aeroModule = data.aeroModule;
                if ((object)aeroModule == null)
                {
                    continue;
                }

                Vector3 xRefVector = data.xRefVectorPartSpace;
                Vector3 nRefVector = data.nRefVectorPartSpace;

                Vector3 velLocal = aeroModule.partLocalVel;

                Vector3 angVelLocal = aeroModule.partLocalAngVel;

                //velLocal += Vector3.Cross(angVelLocal, data.centroidPartSpace);       //some transform issue here, needs investigation
                Vector3 velLocalNorm = velLocal.normalized;

                Vector3 localNormalForceVec = Vector3.ProjectOnPlane(-velLocalNorm, xRefVector).normalized;

                double cosAoA = Vector3.Dot(xRefVector, velLocalNorm);
                double cosSqrAoA = cosAoA * cosAoA;
                double sinSqrAoA = Math.Max(1 - cosSqrAoA, 0);
                double sinAoA = Math.Sqrt(sinSqrAoA);
                double sin2AoA = 2 * sinAoA * Math.Abs(cosAoA);
                double cosHalfAoA = Math.Sqrt(0.5 + 0.5 * Math.Abs(cosAoA));


                double nForce = 0;
                if(machNumber < 6)
                    nForce = potentialFlowNormalForce * Math.Sign(cosAoA) * cosHalfAoA * sin2AoA;  //potential flow normal force
                if (nForce < 0)     //potential flow is not significant over the rear face of things
                    nForce = 0;

                //if (machNumber > 3)
                //    nForce *= 2d - machNumber * 0.3333333333333333d;

                float normalForceFactor = Math.Abs(Vector3.Dot(localNormalForceVec, nRefVector));
                normalForceFactor *= normalForceFactor;

                normalForceFactor = invFlatnessRatio * (1 - normalForceFactor) + flatnessRatio * normalForceFactor;     //accounts for changes in relative flatness of shape

                
                float crossFlowMach, crossFlowReynolds;
                crossFlowMach = machNumber * (float)sinAoA;
                crossFlowReynolds = reynoldsPerUnitLength * diameter * (float)sinAoA / normalForceFactor;

                nForce += viscCrossflowDrag * sinSqrAoA * CalculateCrossFlowDrag(crossFlowMach, crossFlowReynolds);            //viscous crossflow normal force

                nForce *= normalForceFactor;

                double xForce = -skinFrictionForce * Math.Sign(cosAoA) * cosSqrAoA;
                float moment = (float)(cosAoA * sinAoA);
                float dampingMoment = 0.25f * moment;

                if (cosAoA > 0)
                {
                    xForce += cosSqrAoA * xForceAoA0;
                    float momentFactor;
                    if (machNumber > 6)
                        momentFactor = hypersonicMomentForward * 0.4f;
                    else if (machNumber < 0.6)
                        momentFactor = 0.6f * hypersonicMomentBackward;
                    else
                    {
                        float tmp = (-0.185185185f * machNumber + 1.11111111111f);
                        momentFactor = tmp * hypersonicMomentBackward * 0.6f + (1 - tmp) * hypersonicMomentForward * 0.4f;
                    }
                    //if (machNumber < 1.5)
                    //    momentFactor += hypersonicMomentBackward * (0.5f - machNumber * 0.33333333333333333333333333333333f) * 0.2f;

                    moment *= momentFactor;
                    dampingMoment *= momentFactor;
                }
                else
                {
                    xForce += cosSqrAoA * xForceAoA180;
                    float momentFactor;     //negative to deal with the ref vector facing the opposite direction, causing the moment vector to point in the opposite direction
                    if (machNumber > 6)
                        momentFactor = hypersonicMomentBackward * 0.4f;
                    else if (machNumber < 0.6)
                        momentFactor = 0.6f * hypersonicMomentForward;
                    else
                    {
                        float tmp = (-0.185185185f * machNumber + 1.11111111111f);
                        momentFactor = tmp * hypersonicMomentForward * 0.6f + (1 - tmp) * hypersonicMomentBackward * 0.4f;
                    }
                    //if (machNumber < 1.5)
                    //    momentFactor += hypersonicMomentForward * (0.5f - machNumber * 0.33333333333333333333333333333333f) * 0.2f;

                    moment *= momentFactor;
                    dampingMoment *= momentFactor;
                }
                moment /= normalForceFactor;
                dampingMoment = Math.Abs(dampingMoment);
                dampingMoment += (float)Math.Abs(skinFrictionForce) * 0.1f;

                Vector3 forceVector = (float)xForce * xRefVector + (float)nForce * localNormalForceVec;
                Vector3 torqueVector = Vector3.Cross(xRefVector, localNormalForceVec) * moment;
                torqueVector -= dampingMoment * angVelLocal;

                float dynPresAndScaling = 0.0005f * atmDensity * velLocal.sqrMagnitude * data.dragFactor;        //dyn pres and N -> kN conversion

                forceVector *= dynPresAndScaling;
                torqueVector *= dynPresAndScaling;

                aeroModule.AddLocalForceAndTorque(forceVector, torqueVector, data.centroidPartSpace);
            }
        }

        private void GenerateCrossFlowDragCurve()
        {
            crossFlowDragMachCurve = new FloatCurve();
            crossFlowDragMachCurve.Add(0, 1.2f, 0, 0);
            crossFlowDragMachCurve.Add(0.3f, 1.2f, 0, 0);
            crossFlowDragMachCurve.Add(0.7f, 1.5f, 0, 0);
            crossFlowDragMachCurve.Add(0.85f, 1.41f, 0, 0);
            crossFlowDragMachCurve.Add(0.95f, 2.1f, 0, 0);
            crossFlowDragMachCurve.Add(1f, 2f, -2f, -2f);
            crossFlowDragMachCurve.Add(1.3f, 1.6f, -0.5f, -0.5f);
            crossFlowDragMachCurve.Add(2f, 1.4f, -0.1f, -0.1f);
            crossFlowDragMachCurve.Add(5f, 1.25f, -0.02f, -0.02f);
            crossFlowDragMachCurve.Add(10f, 1.2f, 0, 0);

            crossFlowDragReynoldsCurve = new FloatCurve();
            crossFlowDragReynoldsCurve.Add(10000, 1f, 0, 0);
            crossFlowDragReynoldsCurve.Add(100000, 1.0083333333333333333333333333333f, 0, 0);
            crossFlowDragReynoldsCurve.Add(180000, 1.0083333333333333333333333333333f, 0, 0);
            crossFlowDragReynoldsCurve.Add(250000, 0.66666666666666666666666666666667f);
            crossFlowDragReynoldsCurve.Add(300000, 0.25f, -5E-07f, -5E-07f);
            crossFlowDragReynoldsCurve.Add(500000, 0.20833333333333333333333333333333f, 0, 0);
            crossFlowDragReynoldsCurve.Add(1000000, 0.33333333333333333333333333333333f, 7E-8f, 7E-8f);
            crossFlowDragReynoldsCurve.Add(10000000, 0.58333333333333333333333333333333f, 0, 0);
        }

        private float CalculateCrossFlowDrag(float crossFlowMach, float crossFlowReynolds)
        {
            if (crossFlowMach > 0.5f)
                return crossFlowDragMachCurve.Evaluate(crossFlowMach);
            float reynoldsInfluenceFactor = 1;
            if (crossFlowMach > 0.4f)
                reynoldsInfluenceFactor -= (crossFlowMach - 0.4f) * 10;

            float crossFlowDrag = crossFlowDragReynoldsCurve.Evaluate(crossFlowReynolds);
            crossFlowDrag = (crossFlowDrag - 1) * reynoldsInfluenceFactor + 1;
            crossFlowDrag *= crossFlowDragMachCurve.Evaluate(crossFlowMach);

            return crossFlowDrag;
        }
    }
}
