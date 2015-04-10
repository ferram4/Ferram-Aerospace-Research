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

        List<PartData> partsIncluded;

        public struct PartData
        {
            public FARAeroPartModule aeroModule;
            public Vector3 centroidPartSpace;
            public Vector3 xRefVectorPartSpace;
            public Vector3 nRefVectorPartSpace;
            public float dragFactor;    //sum of these should add up to 1
        }

        public FARAeroSection(FloatCurve xForcePressureAoA0, FloatCurve xForcePressureAoA180, FloatCurve xForceSkinFriction, float areaChange, float viscCrossflowDrag,
            Vector3 centroidWorldSpace, Vector3 xRefVectorWorldSpace, Vector3 nRefVectorWorldSpace, List<FARAeroPartModule> moduleList, List<float> dragFactor)
        {
            this.xForcePressureAoA0 = xForcePressureAoA0;       //copy references to floatcurves over
            this.xForcePressureAoA180 = xForcePressureAoA180;
            this.xForceSkinFriction = xForceSkinFriction;

            this.areaChange = areaChange;                   //copy lifting body info over
            this.viscCrossflowDrag = viscCrossflowDrag;

            partsIncluded = new List<PartData>();
            for(int i = 0; i < moduleList.Count; i++)
            {
                PartData data = new PartData();
                data.aeroModule = moduleList[i];
                data.centroidPartSpace = data.aeroModule.transform.worldToLocalMatrix.MultiplyPoint3x4(centroidWorldSpace);
                data.xRefVectorPartSpace = data.aeroModule.transform.worldToLocalMatrix.MultiplyVector(xRefVectorWorldSpace);
                data.nRefVectorPartSpace = data.aeroModule.transform.worldToLocalMatrix.MultiplyVector(nRefVectorWorldSpace);
                data.dragFactor = dragFactor[i];
                partsIncluded.Add(data);
            }
        }

        public void CalculateAeroForces(float atmDensity, float machNumber, float skinFrictionDrag)
        {

            double skinFrictionForce = skinFrictionDrag * xForceSkinFriction.Evaluate(machNumber);      //this will be the same for each part, so why recalc it multiple times?

            for(int i = 0; i < partsIncluded.Count; i++)
            {
                PartData data = partsIncluded[i];
                FARAeroPartModule aeroModule = data.aeroModule;
                if (aeroModule == null)
                    continue;

                Vector3 velLocal = aeroModule.partLocalVel;

                Vector3 velLocalNorm = velLocal.normalized;
                Vector3 localLiftVec = Vector3.Exclude(data.xRefVectorPartSpace, -velLocalNorm).normalized;

                double cosAoA = Vector3.Dot(data.xRefVectorPartSpace, velLocalNorm);
                double cosSqrAoA = cosAoA * cosAoA;
                double sinSqrAoA = 1 - cosSqrAoA;
                double sin2AoA = 2 * Math.Sqrt(Math.Max(1 - cosSqrAoA, 0)) * Math.Abs(cosAoA);
                double cosHalfAoA = Math.Sqrt(0.5 + 0.5 * Math.Abs(cosAoA));

                double nForce = 0;
                if(machNumber < 6)
                    nForce = cosHalfAoA * sin2AoA * areaChange * Math.Sign(cosAoA);  //potential flow normal force
                if (nForce < 0)     //potential flow is not significant over the rear face of things
                    nForce = 0;
                if (machNumber > 3)
                    nForce *= 2 - machNumber / 3;

                nForce += viscCrossflowDrag * sinSqrAoA;            //viscous crossflow normal force

                double xForce = -skinFrictionForce * Math.Sign(cosAoA) * cosSqrAoA;
                if (cosAoA > 0)
                    xForce += cosSqrAoA * xForcePressureAoA0.Evaluate(machNumber);
                else
                    xForce += cosSqrAoA * xForcePressureAoA180.Evaluate(machNumber);

                Vector3 forceVector = (float)xForce * data.xRefVectorPartSpace + (float)nForce * localLiftVec;
                forceVector *= 0.0005f * atmDensity * velLocal.sqrMagnitude;        //dyn pres and N -> kN conversion
                forceVector *= data.dragFactor;

                aeroModule.AddLocalForce(forceVector, data.centroidPartSpace);
            }
        }
    }
}
