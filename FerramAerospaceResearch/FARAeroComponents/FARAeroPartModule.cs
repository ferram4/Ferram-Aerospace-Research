using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;

namespace FerramAerospaceResearch.FARAeroComponents
{
    //Used to apply forces to vessel based on data from FARVesselAero
    public class FARAeroPartModule : PartModule
    {
        float dragPerDynPres = 0;
        float newDragPerDynPres = 0;

        float pertDelDragPerDynPres;   //change in drag per AoA from reference orientation
        float newPertDelDragPerDynPres;

        public bool updateForces = false;

        Vector3 liftForcePerDynPres;       //The lift vector calculated by the FARVesselAero
        Vector3 newLiftForcePerDynPres;

        float pertDelLiftPerDynPres;   //change in lift per AoA from reference orientation
        float newPertDelLiftPerDynPres;

        Vector3 momentPerDynPres;
        Vector3 newMomentPerDynPres;        //moment created about force center due to off-center lift and drag forces

        Vector3 referenceVelVector;           //The velocity vector (normalized) for liftForces; used to control addition of pertDelLift
        Vector3 newReferenceVelVector;

        Vector3 localForceCenterPosition;
        Vector3 newLocalForceCenterPosition;
        float forceCenterScaling = 0;

        public Matrix4x4 vesselToLocalTransform;

        [KSPField(isPersistant = false, guiActive = true)]
        public float dragCoeff;

        void Start()
        {
            part.maximum_drag = 0;
            part.minimum_drag = 0;
            part.angularDrag = 0;
            if (vessel)
                vesselToLocalTransform = part.transform.worldToLocalMatrix * vessel.transform.localToWorldMatrix;
        }

        void FixedUpdate()
        {
            if (FlightGlobals.ready && part && part.Rigidbody)
            {
                Vector3 velocity = part.Rigidbody.velocity + Krakensbane.GetFrameVelocityV3f();

                if (velocity.x == 0 && velocity.y == 0 && velocity.z == 0)
                    return;

                Vector3 worldVelNorm = velocity.normalized;
                float dynPres = velocity.sqrMagnitude * 0.0005f * (float)vessel.atmDensity;     //In kPa

                if (dynPres == 0)
                    return;

                if (updateForces)
                {
                    updateForces = false;

                    UpdateForces();
                }
                Vector3 force = Vector3.zero;
                Vector3 pertLiftDir = Vector3.zero;
                pertLiftDir = Vector3.Exclude(part.transform.localToWorldMatrix.MultiplyVector(referenceVelVector), worldVelNorm);      //A vector of only the difference between the nominal velocity and the current velocity
                float pertDragFactor = pertLiftDir.magnitude;
                force += pertLiftDir * pertDelLiftPerDynPres;       //perturbation lift
                force -= (pertDelDragPerDynPres * pertDragFactor + dragPerDynPres) * worldVelNorm;    //perturbation drag and nominal drag; negative is to ensure that it goes counter to velocity

                force += Vector3.Cross(worldVelNorm, part.transform.localToWorldMatrix.MultiplyVector(liftForcePerDynPres));
                force *= dynPres;

                part.Rigidbody.AddForceAtPosition(force, part.transform.localToWorldMatrix.MultiplyVector(localForceCenterPosition) + part.transform.position);
                //part.Rigidbody.AddTorque(part.transform.localToWorldMatrix.MultiplyVector(momentPerDynPres * dynPres));
            }
        }

        public void UpdateForces()
        {
            lock (vessel)
            {
                dragPerDynPres = newDragPerDynPres;
                newDragPerDynPres = 0;

                pertDelDragPerDynPres = newPertDelDragPerDynPres;
                newPertDelDragPerDynPres = 0;

                liftForcePerDynPres = newLiftForcePerDynPres;
                newLiftForcePerDynPres = Vector3.zero;

                pertDelLiftPerDynPres = newPertDelLiftPerDynPres;
                newPertDelLiftPerDynPres = 0;

                newMomentPerDynPres -= Vector3.Cross(localForceCenterPosition, dragPerDynPres * referenceVelVector + Vector3.Cross(referenceVelVector, liftForcePerDynPres));
                momentPerDynPres = newMomentPerDynPres;
                newMomentPerDynPres = Vector3.zero;

                referenceVelVector = newReferenceVelVector;
                newReferenceVelVector = Vector3.zero;

                if (forceCenterScaling != 0)
                    localForceCenterPosition = newLocalForceCenterPosition / (forceCenterScaling);
                else
                    localForceCenterPosition = Vector3.zero;
                newLocalForceCenterPosition = Vector3.zero;
                forceCenterScaling = 0;
            }
        }

        public void IncrementAeroForces(Vector3 velNormVector, Vector3 vesselForceCenter, float dragPerDynPres, Vector3 liftVecPerDynPres, float pertDragPerDynPres, float pertLiftPerDynPres)
        {
            Vector3 localCenter = PartLocalForceCenter(vesselForceCenter);
            Vector3 localNomLift = PartLocalForceVector(liftVecPerDynPres);
            Vector3 localVelNorm = PartLocalForceVector(velNormVector);

            IncrementNewDragPerDynPres(dragPerDynPres, localVelNorm, localCenter);
            IncrementPerturbationDragPerDynPres(pertDragPerDynPres);
            IncrementNominalLiftPerDynPres(localNomLift, localVelNorm, localCenter);
            IncrementPerturbationLiftPerDynPres(pertLiftPerDynPres);

            newReferenceVelVector = localVelNorm;
        }

        private void IncrementNewDragPerDynPres(float dragIncrement, Vector3 velNormVector, Vector3 localForceCenter)
        {
            if (dragIncrement != 0)
            {
                Vector3 leverVector = localForceCenter;  //vector from current center to part center
                newMomentPerDynPres += Vector3.Cross(leverVector, velNormVector * dragIncrement);

                newLocalForceCenterPosition += localForceCenter * dragIncrement;        //update numerator of force center position

                newDragPerDynPres += dragIncrement;     //finally, increment drag
                forceCenterScaling += dragIncrement;
            }
        }

        private void IncrementPerturbationDragPerDynPres(float pertDragInc)
        {
            newPertDelDragPerDynPres += pertDragInc;
        }

        private void IncrementNominalLiftPerDynPres(Vector3 liftInc, Vector3 velNormVector, Vector3 localForceCenter)
        {
            float liftIncFloat = liftInc.magnitude;
            if (liftIncFloat != 0)
            {
                Vector3 leverVector = localForceCenter;  //vector from current center to part center
                newMomentPerDynPres += Vector3.Cross(leverVector, liftInc);

                newLocalForceCenterPosition += localForceCenter * liftIncFloat;

                Vector3 liftTmp = Vector3.Cross(liftInc, velNormVector);
                newLiftForcePerDynPres += liftTmp;
                forceCenterScaling += liftIncFloat;
            }
        }

        private void IncrementPerturbationLiftPerDynPres(float pertLiftInc)
        {
            newPertDelLiftPerDynPres += pertLiftInc;
        }

        private Vector3 PartLocalForceCenter(Vector3 vesselLocalForceCenter)
        {
            return vesselToLocalTransform.MultiplyPoint3x4(vesselLocalForceCenter);
        }

        private Vector3 PartLocalForceVector(Vector3 vesselLocalForceVector)
        {
            return vesselToLocalTransform.MultiplyVector(vesselLocalForceVector);
        }
    }
}
