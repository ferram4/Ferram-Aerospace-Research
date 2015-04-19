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
        float areaChange;
        float viscCrossflowDrag;
        float flatnessRatio;
        float invFlatnessRatio;
        float hypersonicMomentForward;
        float hypersonicMomentBackward;

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
            float areaChange, float viscCrossflowDrag, float flatnessRatio, float hypersonicMomentForward, float hypersonicMomentBackward,
            Vector3 centroidWorldSpace, Vector3 xRefVectorWorldSpace, Vector3 nRefVectorWorldSpace, List<FARAeroPartModule> moduleList,
            Dictionary<Part, FARPartGeometry.VoxelCrossSection.SideAreaValues> sideAreaValues, List<float> dragFactor)
        {
            this.xForcePressureAoA0 = xForcePressureAoA0;       //copy references to floatcurves over
            this.xForcePressureAoA180 = xForcePressureAoA180;
            this.xForceSkinFriction = xForceSkinFriction;

            this.areaChange = areaChange;                   //copy lifting body info over
            this.viscCrossflowDrag = viscCrossflowDrag;
            this.flatnessRatio = flatnessRatio;
            invFlatnessRatio = 1 / flatnessRatio;
            this.hypersonicMomentForward = hypersonicMomentForward;
            this.hypersonicMomentBackward = hypersonicMomentBackward;

            partsIncluded = new List<PartData>();

            Vector3 centroidLocationAlongxRef = Vector3.Project(centroidWorldSpace, xRefVectorWorldSpace);
            Vector3 centroidSansxRef = Vector3.Exclude(xRefVectorWorldSpace, centroidWorldSpace);

            Vector3 worldSpaceAvgPos = Vector3.zero;
            float totalDragFactor = 0;
            for (int i = 0; i < moduleList.Count; i++)
            {
                Part p = moduleList[i].part;
                worldSpaceAvgPos += p.transform.position * dragFactor[i];
                totalDragFactor += dragFactor[i];
            }

            worldSpaceAvgPos /= totalDragFactor;

            worldSpaceAvgPos = Vector3.Exclude(xRefVectorWorldSpace, worldSpaceAvgPos);

            Vector3 avgPosDiffFromCentroid = centroidSansxRef - worldSpaceAvgPos;

            for (int i = 0; i < moduleList.Count; i++)
            {
                PartData data = new PartData();
                data.aeroModule = moduleList[i];
                Transform transform = data.aeroModule.part.transform;
                Matrix4x4 transformMatrix = transform.worldToLocalMatrix;

                Vector3 forceCenterWorldSpace = centroidLocationAlongxRef + Vector3.Exclude(xRefVectorWorldSpace, transform.position) + avgPosDiffFromCentroid;

                data.centroidPartSpace = transformMatrix.MultiplyPoint3x4(forceCenterWorldSpace);
                data.xRefVectorPartSpace = transformMatrix.MultiplyVector(xRefVectorWorldSpace);
                data.nRefVectorPartSpace = transformMatrix.MultiplyVector(nRefVectorWorldSpace);
                data.dragFactor = dragFactor[i];

                FARPartGeometry.VoxelCrossSection.SideAreaValues values = sideAreaValues[data.aeroModule.part];
                Vector3 posAreas = new Vector3((float)values.iP, (float)values.jP, (float)values.kP);
                Vector3 negAreas = new Vector3((float)values.iN, (float)values.jN, (float)values.kN);

                posAreas = transformMatrix.MultiplyVector(posAreas);
                negAreas = transformMatrix.MultiplyVector(negAreas);

                if (posAreas.x >= 0)
                {
                    data.iP = posAreas.x;
                    data.iN = negAreas.x;
                }
                else
                {
                    data.iP = Math.Abs(negAreas.x);
                    data.iN = Math.Abs(posAreas.x);
                }

                if (posAreas.y >= 0)
                {
                    data.jP = posAreas.y;
                    data.jN = negAreas.y;
                }
                else
                {
                    data.jP = Math.Abs(negAreas.y);
                    data.jN = Math.Abs(posAreas.y);
                }

                if (posAreas.z >= 0)
                {
                    data.kP = posAreas.z;
                    data.kN = negAreas.z;
                }
                else
                {
                    data.kP = Math.Abs(negAreas.z);
                    data.kN = Math.Abs(posAreas.z);
                }
                partsIncluded.Add(data);
            }

            if (crossFlowDragMachCurve == null)
                GenerateCrossFlowDragCurve();
        }

        public void CalculateAeroForces(float atmDensity, float machNumber, float reynoldsPerUnitLength, float skinFrictionDrag)
        {

            double skinFrictionForce = skinFrictionDrag * xForceSkinFriction.Evaluate(machNumber);      //this will be the same for each part, so why recalc it multiple times?
            float xForceAoA0 = xForcePressureAoA0.Evaluate(machNumber);
            float xForceAoA180 = xForcePressureAoA180.Evaluate(machNumber);

            for(int i = 0; i < partsIncluded.Count; i++)
            {
                PartData data = partsIncluded[i];
                FARAeroPartModule aeroModule = data.aeroModule;
                if (aeroModule == null)
                    continue;

                Vector3 xRefVector = data.xRefVectorPartSpace;
                Vector3 nRefVector = data.nRefVectorPartSpace;

                Vector3 velLocal = aeroModule.partLocalVel;

                Vector3 angVelLocal = aeroModule.partLocalAngVel;

                //velLocal += Vector3.Cross(angVelLocal, data.centroidPartSpace);       //some transform issue here, needs investigation
                Vector3 velLocalNorm = velLocal.normalized;

                Vector3 localNormalForceVec = Vector3.Exclude(xRefVector, -velLocalNorm).normalized;

                double cosAoA = Vector3.Dot(xRefVector, velLocalNorm);
                double cosSqrAoA = cosAoA * cosAoA;
                double sinSqrAoA = Math.Max(1 - cosSqrAoA, 0);
                double sinAoA = Math.Sqrt(sinSqrAoA);
                double sin2AoA = 2 * sinAoA * Math.Abs(cosAoA);
                double cosHalfAoA = Math.Sqrt(0.5 + 0.5 * Math.Abs(cosAoA));


                double nForce = 0;
                if(machNumber < 6)
                    nForce = cosHalfAoA * sin2AoA * areaChange * Math.Sign(cosAoA);  //potential flow normal force
                if (nForce < 0)     //potential flow is not significant over the rear face of things
                    nForce = 0;
                if (machNumber > 3)
                    nForce *= 2d - machNumber * 0.3333333333333333d;

                float normalForceFactor = Math.Abs(Vector3.Dot(localNormalForceVec, nRefVector));
                normalForceFactor *= normalForceFactor;

                normalForceFactor = invFlatnessRatio * (1 - normalForceFactor) + flatnessRatio * normalForceFactor;     //accounts for changes in relative flatness of shape

                
                float crossFlowMach, crossFlowReynolds;
                crossFlowMach = machNumber * (float)sinAoA;
                crossFlowReynolds = reynoldsPerUnitLength * viscCrossflowDrag * normalForceFactor * (float)sinAoA;

                nForce += viscCrossflowDrag * sinSqrAoA * CalculateCrossFlowDrag(crossFlowMach, crossFlowReynolds);            //viscous crossflow normal force

                nForce *= normalForceFactor;

                double xForce = -skinFrictionForce * Math.Sign(cosAoA) * cosSqrAoA;
                float moment = (float)(cosAoA * sinAoA);
                float dampingMoment = 0.05f * moment;

                if (cosAoA > 0)
                {
                    xForce += cosSqrAoA * xForceAoA0;
                    moment *= -hypersonicMomentForward;
                    dampingMoment *= -hypersonicMomentForward;
                }
                else
                {
                    xForce += cosSqrAoA * xForceAoA180;
                    moment *= hypersonicMomentBackward;
                    dampingMoment *= hypersonicMomentBackward;
                }
                moment /= normalForceFactor;
                dampingMoment = Math.Abs(dampingMoment);

                if(double.IsNaN(xForce))
                {
                    Debug.Log("xForce is NaN");
                    xForce = 0;
                }
                if (double.IsNaN(nForce))
                {
                    Debug.Log("nForce is NaN");
                    nForce = 0;
                }
                if(double.IsNaN(moment))
                {
                    Debug.Log("moment is NaN");
                    moment = 0;
                    dampingMoment = 0;
                }

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
