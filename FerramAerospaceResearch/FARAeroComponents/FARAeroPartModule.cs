/*
Ferram Aerospace Research v0.14.6
Copyright 2014, Michael Ferrara, aka Ferram4

    This file is part of Ferram Aerospace Research.

    Ferram Aerospace Research is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Ferram Aerospace Research is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Ferram Aerospace Research.  If not, see <http://www.gnu.org/licenses/>.

    Serious thanks:		a.g., for tons of bugfixes and code-refactorings
            			Taverius, for correcting a ton of incorrect values
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager 1.5 updates
            			ialdabaoth (who is awesome), who originally created Module Manager
                        Regex, for adding RPM support
            			Duxwing, for copy editing the readme
 * 
 * Kerbal Engineer Redux created by Cybutek, Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License
 *      Referenced for starting point for fixing the "editor click-through-GUI" bug
 *
 * Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/55219
 *
 * Toolbar integration powered by blizzy78's Toolbar plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/60863
 */

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
        double dragPerDynPres = 0;
        double newDragPerDynPres = 0;

        //float pertDelDragPerDynPres;   //change in drag per AoA from reference orientation
        //float newPertDelDragPerDynPres;

        public bool updateForces = false;

        Vector3 liftForcePerDynPres;       //The lift vector calculated by the FARVesselAero
        Vector3 newLiftForcePerDynPres;

        //float pertDelLiftPerDynPres;   //change in lift per AoA from reference orientation
        //float newPertDelLiftPerDynPres;

        Vector3 momentPerDynPres;
        Vector3 newMomentPerDynPres;        //moment created about force center due to off-center lift and drag forces

        //float pertDelMomentPerDynPres;      //moment created due to change in AoA
        //float newPertDelMomentPerDynPres;

        double momentDampingPerDynPres;    //moment magnitude created by angular velocity changes
        double newMomentDampingPerDynPres;

        //Vector3 referenceVelVector;           //The velocity vector (normalized) for liftForces; used to control addition of pertDelLift
        //Vector3 newReferenceVelVector;

        Vector3d localForceCenterPosition;
        Vector3d newLocalForceCenterPosition;
        double forceCenterScaling = 0;

        public Matrix4x4 vesselToLocalTransform;

        [KSPField(isPersistant = false, guiActive = true)]
        public float dragCoeff;

        void Start()
        {
            part.maximum_drag = 0;
            part.minimum_drag = 0;
            part.angularDrag = 0;
            if (vessel)
                vesselToLocalTransform = (part.transform.worldToLocalMatrix * vessel.ReferenceTransform.localToWorldMatrix);
        }


        public void AeroForceUpdate()
        {
            if (FlightGlobals.ready && part && part.Rigidbody)
            {
                if (vessel.atmDensity <= 0)
                {
                    dragPerDynPres = 0;
                    //pertDelDragPerDynPres = 0;
                    liftForcePerDynPres = Vector3d.zero;
                    //pertDelLiftPerDynPres = 0;
                    momentPerDynPres = Vector3d.zero;
                    //pertDelMomentPerDynPres = 0;
                    momentDampingPerDynPres = 0;
                    //referenceVelVector = Vector3.zero;
                    localForceCenterPosition = Vector3d.zero;

                    newDragPerDynPres = 0;
                    //newPertDelDragPerDynPres = 0;
                    newLiftForcePerDynPres = Vector3d.zero;
                    //newPertDelLiftPerDynPres = 0;
                    newMomentPerDynPres = Vector3d.zero;
                    //newPertDelMomentPerDynPres = 0;
                    newMomentDampingPerDynPres = 0;
                    //newReferenceVelVector = Vector3.zero;
                    newLocalForceCenterPosition = Vector3d.zero;

                    return;
                }

                lock (this)
                {
                    Vector3 velocity = part.Rigidbody.velocity + Krakensbane.GetFrameVelocityV3f();

                    if (velocity.x == 0 && velocity.y == 0 && velocity.z == 0)
                        return;

                    Vector3d worldVelNorm = velocity.normalized;
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
                    Vector3 nomLift = Vector3.Cross(worldVelNorm, part.transform.localToWorldMatrix.MultiplyVector(liftForcePerDynPres));

                    force += nomLift;

                    //pertLiftDir = Vector3.Exclude(worldVelNorm, part.transform.localToWorldMatrix.MultiplyVector(referenceVelVector)).normalized;      //A vector of only the difference between the nominal velocity and the current velocity
                    //float angleOfAttack = (float)Math.Acos(Mathf.Clamp01(Vector3.Dot(worldVelNorm, part.transform.localToWorldMatrix.MultiplyVector(referenceVelVector))));
                    //angleOfAttack *= Math.Sign(Vector3.Dot(nomLift, pertLiftDir));

                    //force += pertLiftDir * pertDelLiftPerDynPres * Math.Abs(angleOfAttack);       //perturbation lift
                    //force -= (Mathf.Clamp(pertDelDragPerDynPres * angleOfAttack, - dragPerDynPres * 0.75f, float.PositiveInfinity) + dragPerDynPres) * worldVelNorm;    //perturbation drag and nominal drag; negative is to ensure that it goes counter to velocity
                    force -= dragPerDynPres * worldVelNorm;    //perturbation drag and nominal drag; negative is to ensure that it goes counter to velocity

                    force *= dynPres;

                    part.Rigidbody.AddForce(force);//, part.transform.localToWorldMatrix.MultiplyVector(localForceCenterPosition) + part.transform.position);

                    Vector3 torque = part.transform.localToWorldMatrix.MultiplyVector(momentPerDynPres) * dynPres;
                    //torque += Vector3.Cross(pertLiftDir, worldVelNorm) * Math.Abs(angleOfAttack) * pertDelMomentPerDynPres;
                    if (dynPres > 5)
                        torque -= (float)momentDampingPerDynPres * part.Rigidbody.angularVelocity * velocity.magnitude * (float)vessel.atmDensity * 0.0005f;// / velocity.magnitude;

                    //torque *= dynPres;
                    part.Rigidbody.AddTorque(torque);
                }
            }
        }

        public void UpdateForces()
        {
            dragPerDynPres = newDragPerDynPres;
            newDragPerDynPres = 0;

            //pertDelDragPerDynPres = newPertDelDragPerDynPres;
            //newPertDelDragPerDynPres = 0;

            liftForcePerDynPres = newLiftForcePerDynPres;
            newLiftForcePerDynPres = Vector3.zero;

            //pertDelLiftPerDynPres = newPertDelLiftPerDynPres;
            //newPertDelLiftPerDynPres = 0;

            if (forceCenterScaling != 0)
                localForceCenterPosition = newLocalForceCenterPosition / (forceCenterScaling);
            else
                localForceCenterPosition = Vector3.zero;
            newLocalForceCenterPosition = Vector3.zero;

            forceCenterScaling = 0;

            //referenceVelVector = newReferenceVelVector;
            //newReferenceVelVector = Vector3.zero;

            //Vector3 tmpLiftVec = Vector3.Cross(referenceVelVector, liftForcePerDynPres);

            //newMomentPerDynPres -= Vector3.Cross(localForceCenterPosition, -dragPerDynPres * referenceVelVector + tmpLiftVec);
            momentPerDynPres = newMomentPerDynPres;
            newMomentPerDynPres = Vector3.zero;

            //newMomentDampingPerDynPres -= Vector3.Cross((localForceCenterPosition) * localForceCenterPosition.magnitude, -pertDelDragPerDynPres * referenceVelVector).magnitude + localForceCenterPosition.sqrMagnitude * pertDelLiftPerDynPres;
            momentDampingPerDynPres = newMomentDampingPerDynPres;
            newMomentDampingPerDynPres = 0;

            //pertDelMomentPerDynPres = newPertDelMomentPerDynPres;
            //newPertDelMomentPerDynPres = 0;
        }

        public void IncrementAeroForces(Vector3 velNormVector, Vector3 vesselForceCenter, float dragPerDynPres, Vector3 liftVecPerDynPres, Vector3 momentPerDynPres, float momentDampingPerDynPres)//float pertDragPerDynPres, float pertLiftPerDynPres, float pertMomentPerDynPres, float momentDampingPerDynPres)
        {
            Vector3 localCenter = vesselToLocalTransform.MultiplyPoint3x4(vesselForceCenter);
            Vector3 localNomLift = vesselToLocalTransform.MultiplyVector(liftVecPerDynPres);
            Vector3 localVelNorm = vesselToLocalTransform.MultiplyVector(velNormVector);
            Vector3 localMoment = vesselToLocalTransform.MultiplyVector(momentPerDynPres);

            //localCenter -= part.CoMOffset;

            float localCenterMag = localCenter.magnitude;

            IncrementNewDragPerDynPres(dragPerDynPres, localVelNorm, localCenter);
            //IncrementPerturbationDragPerDynPres(pertDragPerDynPres, localVelNorm, localCenter, localCenterMag);
            IncrementNominalLiftPerDynPres(localNomLift, localVelNorm, localCenter);
            //IncrementPerturbationLiftPerDynPres(pertLiftPerDynPres, localCenterMag, localVelNorm, localCenter);
            IncrementMomentPerDynPres(localMoment, localVelNorm, localCenter);
            //IncrementPertMomentPerDynPres(pertMomentPerDynPres);
            IncrementMomentDampingPerDynPres(momentDampingPerDynPres);

            //newReferenceVelVector = localVelNorm;
        }

        private void IncrementNewDragPerDynPres(float dragIncrement, Vector3 velNormVector, Vector3 localForceCenter)
        {
            if (dragIncrement != 0)
            {
                newMomentPerDynPres += Vector3.Cross(localForceCenter, -velNormVector * dragIncrement);

                newLocalForceCenterPosition += localForceCenter * dragIncrement;        //update numerator of force center position

                newDragPerDynPres += dragIncrement;     //finally, increment drag
                forceCenterScaling += dragIncrement;
            }
        }

        /*private void IncrementPerturbationDragPerDynPres(float pertDragInc, Vector3 velNormVector, Vector3 localForceCenter, float localForceCenterMag)
        {
            newPertDelDragPerDynPres += pertDragInc;
            newPertDelMomentPerDynPres += Vector3.Cross(localForceCenter, -velNormVector * pertDragInc).magnitude;
            newMomentDampingPerDynPres += Vector3.Cross(localForceCenter * localForceCenterMag, -velNormVector * pertDragInc).magnitude;
        }*/

        private void IncrementNominalLiftPerDynPres(Vector3 liftInc, Vector3 velNormVector, Vector3 localForceCenter)
        {
            float liftIncFloat = liftInc.magnitude;
            if (liftIncFloat != 0)
            {
                newMomentPerDynPres += Vector3.Cross(localForceCenter, liftInc);

                newLocalForceCenterPosition += localForceCenter * liftIncFloat;

                Vector3 liftTmp = Vector3.Cross(liftInc, velNormVector);
                newLiftForcePerDynPres += liftTmp;
                forceCenterScaling += liftIncFloat;
            }
        }

        /*private void IncrementPerturbationLiftPerDynPres(float pertLiftInc, float localForceCenterMag, Vector3 velNormVector, Vector3 localForceCenter)
        {
            newPertDelLiftPerDynPres += pertLiftInc;
            newPertDelMomentPerDynPres += Vector3.Dot(localForceCenter, velNormVector) * pertLiftInc;
            newMomentDampingPerDynPres += Vector3.Dot(localForceCenter, velNormVector) * localForceCenterMag * pertLiftInc;
        }*/

        private void IncrementMomentPerDynPres(Vector3 momentPerDynPres, Vector3 velNormVector, Vector3 localForceCenter)
        {
            newMomentPerDynPres += momentPerDynPres;
            Vector3 liftInc = Vector3.Cross(localForceCenter, momentPerDynPres);
            newLiftForcePerDynPres += Vector3.Cross(liftInc, velNormVector);
        }

        /*private void IncrementPertMomentPerDynPres(float pertMomentPerDynPres)
        {
            newPertDelMomentPerDynPres += pertMomentPerDynPres;
        }*/

        private void IncrementMomentDampingPerDynPres(float momentDampingPerDynPres)
        {
            newMomentDampingPerDynPres += momentDampingPerDynPres;
        }

    }
}
