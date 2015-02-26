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

        Vector3 referenceVelVector;           //The velocity vector (normalized) for liftForces; used to control addition of pertDelLift
        Vector3 newReferenceVelVector;

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
                Vector3 velNorm = velocity.normalized;
                float dynPres = velocity.sqrMagnitude * 0.0005f * (float)vessel.atmDensity;     //In kPa

                if (dynPres == 0)
                    return;

                if (updateForces)
                {
                    updateForces = false;

                    dragPerDynPres = newDragPerDynPres;
                    newDragPerDynPres = 0;

                    pertDelDragPerDynPres = newPertDelDragPerDynPres;
                    newPertDelDragPerDynPres = 0;

                    liftForcePerDynPres = vesselToLocalTransform.MultiplyVector(newLiftForcePerDynPres);
                    newLiftForcePerDynPres = Vector3.zero;

                    pertDelLiftPerDynPres = newPertDelLiftPerDynPres;
                    newPertDelLiftPerDynPres = 0;

                    referenceVelVector = newReferenceVelVector;
                    newReferenceVelVector = Vector3.zero;
                }
                Vector3 force = Vector3.zero;

                Vector3 pertLiftDir = Vector3.Exclude(part.transform.localToWorldMatrix.MultiplyVector(referenceVelVector), velNorm);      //A vector of only the difference between the nominal velocity and the current velocity

                force += pertLiftDir * pertDelLiftPerDynPres;       //perturbation lift
                force -= (newPertDelDragPerDynPres * pertLiftDir.magnitude + dragPerDynPres) * velNorm;    //perturbation drag and nominal drag; negative is to ensure that it goes counter to velocity
                force += Vector3.Cross(velNorm, part.transform.localToWorldMatrix.MultiplyVector(liftForcePerDynPres));

                force *= dynPres;

                part.Rigidbody.AddForce(force);
            }
        }

        public void IncrementNewDragPerDynPres(float dragIncrement)
        {
            newDragPerDynPres += dragIncrement;
        }

        public void IncrementPerturbationDragPerDynPres(float pertDragInc)
        {
            newPertDelDragPerDynPres += pertDragInc;
        }
        
        public void IncrementNominalLiftPerDynPres(Vector3 liftInc, Vector3 velNormVector)
        {
            newLiftForcePerDynPres += Vector3.Cross(liftInc, velNormVector);
        }

        public void IncrementPerturbationLiftPerDynPres(float pertLiftInc)
        {
            newPertDelLiftPerDynPres += pertLiftInc;
        }

        public void UpdateRefVector(Vector3 normalizedVesselReferenceVector)
        {
            newReferenceVelVector = vesselToLocalTransform.MultiplyVector(normalizedVesselReferenceVector);
        }
    }
}
