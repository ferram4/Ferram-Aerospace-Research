using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FerramAerospaceResearch.FARAeroComponents
{
    class FARAeroSection
    {
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
        }

        public FARAeroSection(FloatCurve xForcePressureAoA0, FloatCurve xForcePressureAoA180, FloatCurve xForceSkinFriction, 
            float areaChange, float viscCrossflowDrag, float flatnessRatio, float hypersonicMomentForward, float hypersonicMomentBackward,
            Vector3 centroidWorldSpace, Vector3 xRefVectorWorldSpace, Vector3 nRefVectorWorldSpace, List<FARAeroPartModule> moduleList, List<float> dragFactor)
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
            for(int i = 0; i < moduleList.Count; i++)
            {
                PartData data = new PartData();
                data.aeroModule = moduleList[i];
                Matrix4x4 transformMatrix = data.aeroModule.transform.worldToLocalMatrix;
                data.centroidPartSpace = transformMatrix.MultiplyPoint3x4(centroidWorldSpace);
                data.xRefVectorPartSpace = transformMatrix.MultiplyVector(xRefVectorWorldSpace);
                data.nRefVectorPartSpace = transformMatrix.MultiplyVector(nRefVectorWorldSpace);
                data.dragFactor = dragFactor[i];
                partsIncluded.Add(data);
            }
        }

        public void CalculateAeroForces(float atmDensity, float machNumber, float skinFrictionDrag)
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

                nForce += viscCrossflowDrag * sinSqrAoA;            //viscous crossflow normal force

                float normalForceFactor = Math.Abs(Vector3.Dot(localNormalForceVec, nRefVector));
                normalForceFactor *= normalForceFactor;

                normalForceFactor = invFlatnessRatio * (1 - normalForceFactor) + flatnessRatio * normalForceFactor;     //accounts for changes in relative flatness of shape

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
    }
}
