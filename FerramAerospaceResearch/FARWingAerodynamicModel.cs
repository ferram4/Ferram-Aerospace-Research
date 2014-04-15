/*
Ferram Aerospace Research v0.13.1
Copyright 2013, Michael Ferrara, aka Ferram4

    This file is part of Ferram Aerospace Research.

    Ferram Aerospace Research is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Kerbal Joint Reinforcement is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Ferram Aerospace Research.  If not, see <http://www.gnu.org/licenses/>.

    Serious thanks:		a.g., for tons of bugfixes and code-refactorings
            			Taverius, for correcting a ton of incorrect values
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager 1.5 updates
            			ialdabaoth (who is awesome), who originally created Module Manager
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
using UnityEngine;

/// <summary>
/// This calculates the lift and drag on a wing in the atmosphere
/// 
/// It uses Prandtl lifting line theory to calculate the basic lift and drag coefficients and includes compressibility corrections for subsonic and supersonic flows; transsonic regime has placeholder
/// </summary>

namespace ferram4
{
    
    public class FARWingAerodynamicModel : FARBaseAerodynamics
    {
        
        /// <summary>
        /// S Planform Area
        /// AoAmax Stall Angle
        /// MAC (Mean Aerodynamic Chord)
        /// AR (aspect ratio)
        /// e (Oswald's efficiency)
        /// Control axis
        /// Maximum control deflection
        /// </summary>
        /// 

//        [KSPField(isPersistant = false)]
//        public float S;

        public float AoAmax = 15;

        [KSPField(isPersistant = false)]
        public float MAC;

        [KSPField(isPersistant = false)]
        public float e;

        [KSPField(isPersistant = false)]
        public float nonSideAttach;           //This is for ailerons and the small ctrl surf


        [KSPField(isPersistant = false)]
        public float TaperRatio;

        [KSPField(isPersistant = false, guiActive = true, guiName = "Stalled %", guiFormat = "F2")]
        protected float stall = 0;

        private float minStall = 0;

        private const float twopi = Mathf.PI * 2;   //lift slope
        private float piARe = 1;    //induced drag factor

        [KSPField(isPersistant = false)]
        public float b_2 = 4;        //span


        [KSPField(isPersistant = false)]
        public float MidChordSweep;

        private float MidChordSweepSideways = 0;

        //[KSPField(isPersistant = false, guiActive = true)]
        private float SweepAngle = 0;

//        [KSPField(isPersistant = false, guiActive = true)]
//        protected float AoA = 0;              //Angle of attack

        //[KSPField(guiActive = true, isPersistant = false)]
        private float effective_b_2 = 1f;
        //[KSPField(guiActive = true, isPersistant = false)]
        private float effective_MAC = 1f;

        //[KSPField(isPersistant = false, guiActive = true)]
        protected float effective_AR = 4;

        [KSPField(isPersistant = false, guiActive = false, guiName = "Current lift", guiUnits = "kN", guiFormat = "F3")]
        protected float currentLift = 0.0f;

        [KSPField(isPersistant = false, guiActive = false, guiName = "Current drag", guiUnits = "kN", guiFormat = "F3")]
        protected float currentDrag = 0.0f;

        private float liftslope = 0;
//        [KSPField(isPersistant = false, guiActive = true)]
        protected float ClCdInterference = 1;

        private float forwardexposure;
        private float backwardexposure;
        private float outwardexposure;
        private float inwardexposure;

//        protected float LeExposure = 0;
//        protected float TeExposure = 0;
        protected float WingtipExposure = 0;
        protected float WingrootExposure = 0;

        protected float zeroLiftCdIncrement = 0;


        public Vector3 AerodynamicCenter = Vector3.zero;
        private Vector3 CurWingCentroid = Vector3.zero;
        private Vector3 ParallelInPlane = Vector3.zero;
        private Vector3 perp = Vector3.zero;
        private Vector3 liftDirection = Vector3.zero;

        // in local coordinates
        private Vector3 localWingCentroid = Vector3.zero;
        private Vector3 sweepPerpLocal, sweepPerp2Local;
        private Vector3 ParallelInPlaneLocal = Vector3.zero;

        private Int16 srfAttachNegative = 1;

        private Vector3 LastInFrontRaycast = Vector3.zero;
        private int LastInFrontRaycastCount = 0;
        private Part PartInFrontOf = null;
        private FARWingAerodynamicModel WingInFrontOf = null;

        //[KSPField(guiActive = true, isPersistant = false)]
        protected float ClIncrementFromRear = 0f;

        /*[KSPField(guiActive = true, isPersistant = false)]
        protected float debug1 = 0;
        [KSPField(guiActive = true, isPersistant = false)]
        protected float debug2 = 0;*/

//        [KSPField(guiActive = true)]
//        private string PartInfrontOfName = "";

        private float rho = 1;

        #region GetFunctions

        public float GetStall()
        {
            return stall;
        }

        public float GetCl()
        {

            float ClUpwards = 1;
            if (start != StartState.Editor)
                ClUpwards = Vector3.Dot(liftDirection, -vessel.transform.forward);
            ClUpwards *= Cl;

            return ClUpwards;
        }

        public float GetCd()
        {
            return Cd;
        }

        public void EditorClClear(bool reset_stall)
        {
            Cl = 0;
            Cd = 0;
            if (reset_stall)
                stall = 0;
//            LastAoA = AoA;
//            LastAoADot = AoADot;
            //downWash = 0;
        }

        public Vector3 GetAerodynamicCenter()
        {
            return AerodynamicCenter;
        }

        private void PrecomputeCentroid()
        {
            Vector3 WC = Vector3.zero;
            if (nonSideAttach <= 0)
            {
                WC = - b_2 * 0.5f * (Vector3.right * srfAttachNegative + Vector3.up * Mathf.Tan(MidChordSweep * Mathf.Deg2Rad));
            }
            else
                WC = - MAC * 0.7f * Vector3.up;

            localWingCentroid = WC;
        }

        private Vector3 WingCentroid()
        {
            return part_transform.TransformPoint(localWingCentroid);
        }

        public override Vector3 GetVelocity()
        {
            if (start != StartState.Editor)
                return part.Rigidbody.GetPointVelocity(WingCentroid()) + Krakensbane.GetFrameVelocityV3f();
            else
                return velocityEditor;
        }

        private Vector3 CalculateAerodynamicCenter(float MachNumber, float AoA, Vector3 WC)
        {
            Vector3 AC_offset = Vector3.zero;
            if (nonSideAttach <= 0)
            {
                if (MachNumber < 0.85f)
                    AC_offset = effective_MAC * 0.25f * ParallelInPlane;
                else if (MachNumber > 1)
                    AC_offset = effective_MAC * 0.10f * ParallelInPlane;
                else
                {
                    if(MachNumber < 0.95)
                        AC_offset = effective_MAC * (0.5f * MachNumber - 0.175f) * ParallelInPlane;
                    else
                        AC_offset = effective_MAC * (-4f * MachNumber + 4.1f) * ParallelInPlane;
                }

                float tmp = FARMathUtil.FastCos(AoA);

                AC_offset *= tmp * tmp;

            }
            //if (stall > 0.5)
            //    AC_offset -= (stall - 0.5f) * effective_MAC * 2f * velocity.normalized;


            WC += AC_offset;

            return WC;      //WC updated to AC
        }

        public float GetMAC()
        {
            return effective_MAC;
        }
        public float Getb_2()
        {
            return effective_b_2;
        }

        public void ComputeClCdEditor(Vector3 velocityVector, float M)
        {
            velocityEditor = velocityVector;

            rho = 1;

            float AoA = CalculateAoA(velocityVector);
            CalculateForces(velocityVector, M, AoA);
        }

        public Vector3 GetLiftDirection()
        {
            return liftDirection;
        }

        protected override void ResetCenterOfLift()
        {
            rho = 1;
            stall = 0;
        }

        protected override Vector3 PrecomputeCenterOfLift(Vector3 velocity, float MachNumber, FARCenterQuery center)
        {
            float AoA = CalculateAoA(velocity);

            Vector3 force = CalculateForces(velocity, MachNumber, AoA);
            center.AddForce(AerodynamicCenter, force);

            return force;
        }

        #endregion

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            OnVesselPartsChange += RunExposureAndGetControlSys;

            if (part is ControlSurface)
            {
                ControlSurface w = part as ControlSurface;
                w.deflectionLiftCoeff = 0;
                w.dragCoeff = 0;
                w.ctrlSurfaceArea = 0;
                w.ctrlSurfaceRange = 0;
                w.angularDrag = 0;
                w.maximum_drag = 0;
                w.minimum_drag = 0;
            }
            if (part is Winglet)
            {
                Winglet w = part as Winglet;
                w.deflectionLiftCoeff = 0;
                w.dragCoeff = 0;
                w.angularDrag = 0;
                w.maximum_drag = 0;
                w.minimum_drag = 0;
            }           
            
            MathAndFunctionInitialization();
            Fields["currentLift"].guiActive = FARDebugValues.displayForces;
            Fields["currentDrag"].guiActive = FARDebugValues.displayForces;
        }

        public void MathAndFunctionInitialization()
        {
            float lengthScale = Mathf.Sqrt(FARAeroUtil.areaFactor);

            b_2 *= lengthScale;
            MAC *= lengthScale;

            S = b_2 * MAC;

            if (part.srfAttachNode.originalOrientation.x < 0)
                srfAttachNegative = -1;

            effective_AR = 2 * b_2 / MAC;

            MidChordSweepSideways = (1 - TaperRatio) / (1 + TaperRatio);

            MidChordSweepSideways = (Mathf.PI * 0.5f - Mathf.Atan(FARMathUtil.FastTan(MidChordSweep * Mathf.Deg2Rad) + MidChordSweepSideways * 2 / effective_AR)) * MidChordSweepSideways * 0.5f;

            float sweepHalfChord = MidChordSweep * Mathf.Deg2Rad;

            sweepPerpLocal = Vector3.up * FARMathUtil.FastCos(sweepHalfChord) + Vector3.right * FARMathUtil.FastSin(sweepHalfChord) * srfAttachNegative; //Vector perpendicular to midChord line
            sweepPerp2Local = Vector3.up * FARMathUtil.FastSin(MidChordSweepSideways) - Vector3.right * FARMathUtil.FastCos(MidChordSweepSideways) * srfAttachNegative; //Vector perpendicular to midChord line2

            PrecomputeCentroid();
            RunExposureAndGetControlSys();
        }

        private void RunExposureAndGetControlSys()
        {
            LastInFrontRaycastCount = 0;

            WingExposureFunction();
        }

        public override void FixedUpdate()
        {
            currentLift = currentDrag = 0;

            // With unity objects, "foo" or "foo != null" calls a method to check if
            // it's destroyed. (object)foo != null just checks if it is actually null.
            if (start != StartState.Editor && !isShielded && (object)part != null)
            {
                Rigidbody rb = part.Rigidbody;
                Vessel vessel = part.vessel;

                if (!rb || (object)vessel == null)
                    return;

                //bool set_vel = false;

                // Check that rb is not destroyed, but vessel is just not null
                if (vessel.atmDensity > 0)
                {
                    CurWingCentroid = WingCentroid();

                    Vector3 velocity = rb.GetPointVelocity(CurWingCentroid) + Krakensbane.GetFrameVelocityV3f();
                    float soundspeed, v_scalar = velocity.magnitude;

                    rho = FARAeroUtil.GetCurrentDensity(vessel, out soundspeed);
                    if (rho > 0f && v_scalar > 0.1f)
                    {
                        float MachNumber = v_scalar / soundspeed;
                        float AoA = CalculateAoA(velocity);
                        Vector3 force = DoCalculateForces(velocity, MachNumber, AoA);

/*                        if ((object)part.parent != null)
                        {
                            set_vel = true;
                            rb.maxAngularVelocity = ((object)WingInFrontOf != null ? 700 : 400);
                        }*/

                        rb.AddForceAtPosition(force, AerodynamicCenter);            //and apply force
                    }
                    else
                        stall = 0;
                }
                else
                    stall = 0;

                //if (!set_vel)
                //    rb.maxAngularVelocity = 7;
            }
        }

        public Vector3 CalculateForces(Vector3 velocity, float MachNumber, float AoA)
        {
            CurWingCentroid = WingCentroid();

            return DoCalculateForces(velocity,  MachNumber, AoA);
        }

        private Vector3 DoCalculateForces(Vector3 velocity, float MachNumber, float AoA)
        {
            //This calculates the angle of attack, adjusting the part's orientation for any deflection
            //CalculateAoA();

            if (isShielded)
            {
                Cl = Cd = Cm = stall = 0;
                return Vector3.zero;
            }

            float v_scalar = velocity.magnitude;
            if (v_scalar <= 0.1f)
                return Vector3.zero;

            Vector3 forward = part_transform.forward;
            Vector3 velocity_normalized = velocity / v_scalar;

            float q = rho * v_scalar * v_scalar * 0.5f;   //dynamic pressure, q

            ParallelInPlane = Vector3.Exclude(forward, velocity).normalized;  //Projection of velocity vector onto the plane of the wing
            perp = Vector3.Cross(forward, ParallelInPlane).normalized;       //This just gives the vector to cross with the velocity vector
            liftDirection = Vector3.Cross(perp, velocity).normalized;

            ParallelInPlaneLocal = part_transform.InverseTransformDirection(ParallelInPlane);

            // Calculate the adjusted AC position (uses ParallelInPlane)
            AerodynamicCenter = CalculateAerodynamicCenter(MachNumber, AoA, CurWingCentroid);

            //Throw AoA into lifting line theory and adjust for part exposure and compressibility effects
            CalculateCoefficients(MachNumber, AoA);



            //lift and drag vectors
            Vector3 L = liftDirection * (q * Cl * S);    //lift;
            Vector3 D = velocity_normalized * (-q * Cd * S) ;                         //drag is parallel to velocity vector
            currentLift = L.magnitude * 0.001f;
            currentDrag = D.magnitude * 0.001f;
            Vector3 force = (L + D) * 0.001f;
            if (float.IsNaN(force.sqrMagnitude) || float.IsNaN(AerodynamicCenter.sqrMagnitude))// || float.IsNaN(moment.magnitude))
            {
                Debug.LogWarning("FAR Error: Aerodynamic force = " + force.magnitude + " AC Loc = " + AerodynamicCenter.magnitude + " AoA = " + AoA + "\n\rMAC = " + effective_MAC + " B_2 = " + effective_b_2 + " sweepAngle = " + SweepAngle + "\n\rMidChordSweep = " + MidChordSweep + " MidChordSweepSideways = " + MidChordSweepSideways + "\n\r at " + part.name);
                force = AerodynamicCenter = Vector3.zero;
            }

            return force;
            
        }

        protected virtual float CalculateAoA(Vector3 velocity)
        {
            float PerpVelocity = Vector3.Dot(part_transform.forward, velocity.normalized);
            return Mathf.Asin(Mathf.Clamp(PerpVelocity, -1, 1));
        }

        #region Interactive Effects

        private void FindWingInFrontOf()
        {
            if (start != StartState.Editor)
            {
                // Don't repeat traces until count expires or > 5 degree angle change
                if (LastInFrontRaycastCount-- > 0 &&
                    Vector3.Dot(ParallelInPlaneLocal, LastInFrontRaycast) > 0.996f)
                    return;

                LastInFrontRaycastCount = 10;
                LastInFrontRaycast = ParallelInPlaneLocal;
            }

            PartInFrontOf = null;
            WingInFrontOf = null;
            //PartInfrontOfName = "";

            if (ParallelInPlane != Vector3.zero)
            {
                // Special case for control surfaces attached to a wing:
                // If the ray direction is within 60 degrees of up, just use parent.
                if (nonSideAttach > 0 && ParallelInPlaneLocal.y > 0.5f &&
                    (part.parent || start == StartState.Editor && part.potentialParent))
                {
                    Part parent = part.parent ?? part.potentialParent;
                    FARWingAerodynamicModel w = parent.GetComponent<FARWingAerodynamicModel>();

                    if (w != null)
                    {
                        PartInFrontOf = parent;
                        WingInFrontOf = w;
                        //PartInfrontOfName = PartInFrontOf.partInfo.title;
                        return;
                    }
                }

                RaycastHit hit = new RaycastHit();
                float distance = Mathf.Max(3 * effective_MAC, 0.1f);
                RaycastHit[] hits = Physics.RaycastAll(CurWingCentroid, ParallelInPlane, distance, FARAeroUtil.RaycastMask);

                foreach (RaycastHit h in hits)
                    if (h.collider == part.collider || h.rigidbody == part.rigidbody)
                        continue;
                    else if (h.distance < distance)
                    {
                        distance = h.distance;
                        hit = h;
                    }

                if (hit.distance != 0)
                {
                    if (hit.collider.attachedRigidbody || start == StartState.Editor)
                    {
                        Part p = null;
                        if (hit.collider.attachedRigidbody)
                            p = hit.collider.attachedRigidbody.GetComponent<Part>();
                        if (p == null && start == StartState.Editor)
                            foreach (Part q in VesselPartList)
                                if (q.collider == hit.collider)
                                {
                                    p = q;
                                    break;
                                }
                        if (p != null && p != this.part)
                        {
                            if (start != StartState.Editor && p.vessel != vessel)
                                return;

                            FARWingAerodynamicModel w = p.GetComponent<FARWingAerodynamicModel>();

                            if (w != null)
                            {
                                PartInFrontOf = p;
                                WingInFrontOf = w;
                                //PartInfrontOfName = PartInFrontOf.partInfo.title;
                            }
                        }
                    }
                }
            }
        }

        private void GetWingInFrontOf(float MachNumber, float AoA, out float ACshift, out float ACweight, out float wStall)
        {
            ACshift = 0;
            ACweight = 0;
            wStall = 0;

            FindWingInFrontOf();

            if (WingInFrontOf != null)
            {
                var w = WingInFrontOf;

                float flapRatio = Mathf.Clamp01(this.effective_MAC / (this.effective_MAC + w.effective_MAC));
                float flapFactor = FARAeroUtil.WingCamberFactor.Evaluate(flapRatio);        //Flap Effectiveness Factor
                float dCm_dCl = FARAeroUtil.WingCamberMoment.Evaluate(flapRatio);           //Change in moment due to change in lift from flap

                float angle = Vector3.Dot(w.liftDirection, this.liftDirection);        //This deals with the effect of a part being attached at a strange angle and reducing the effect, so that a vertical stabilizer placed on a wing doesn't affect the lift of the wing
                float liftDirVal = angle;
                float wAoA = w.CalculateAoA(w.GetVelocity()) * Mathf.Sign(liftDirVal);
                angle = (AoA - wAoA) * Mathf.Abs(angle);                //First, make sure that the AoA are wrt the same direction; then account for any strange angling of the part that shouldn't be there

                //This accounts for the wing possibly having a longer span than the flap
                float WingFraction = Mathf.Clamp01(this.effective_b_2 / w.effective_b_2);
                //This accounts for the flap possibly having a longer span than the wing it's attached to
                float FlapFraction = Mathf.Clamp01(w.effective_b_2 / this.effective_b_2);

                float ClIncrement = flapFactor * w.liftslope * FARMathUtil.FastSin(2 * angle) * 0.5f;   //Lift created by the flap interaction
                ClIncrement *= (this.S * FlapFraction + w.S * WingFraction) / this.S;                   //Increase the Cl so that even though we're working with the flap's area, it accounts for the added lift across the entire object

                float MachCoeff = Mathf.Sqrt(Mathf.Clamp01(1 - MachNumber * MachNumber));
                ACweight = ClIncrement * MachCoeff; // Total flap Cl for the purpose of applying ACshift, including the bit subtracted below

                ClIncrement -= FlapFraction * w.liftslope * FARMathUtil.FastSin(2 * angle) * 0.5f;        //Removing additional angle so that lift of the flap is calculated as lift at wing angle + lift due to flap interaction rather than being greater

                ACshift = (dCm_dCl + 0.75f * (1 - flapRatio)) * (effective_MAC + w.effective_MAC);      //Change in Cm with change in Cl

                this.ClIncrementFromRear = ClIncrement * MachCoeff;

                liftslope = w.liftslope;
                SweepAngle = w.SweepAngle;

                wStall = w.stall * Mathf.Abs(liftDirVal);
                if (Mathf.Abs(liftDirVal) > 0.5f)
                {
                    this.AoAmax = (w.AoAmax + GetAoAmax()) * 0.5f;
                    this.AoAmax += (AoA - wAoA) * Mathf.Sign(AoA); //  less efficient than before since we calculate AoA again
                }
                else
                    AoAmax = GetAoAmax();
            }
            else
                AoAmax = GetAoAmax();
        }


        #endregion

        private void DetermineStall(float MachNumber, float AoA, out float ACshift, out float ACweight)
        {
            float lastStall = stall;
            float tmp = 0;
            stall = 0;

            GetWingInFrontOf(MachNumber, AoA, out ACshift, out ACweight, out tmp);

            float absAoA = Mathf.Abs(AoA);

            if (absAoA > AoAmax)
            {
                stall = Mathf.Clamp01((absAoA - AoAmax) * 10);
                stall += tmp;
                stall = Mathf.Max(stall, lastStall);
            }
            else if (absAoA < AoAmax * 0.75f)
            {
                stall = 1 - Mathf.Clamp01((AoAmax * 0.75f - absAoA) * 10);
                stall += tmp;
                stall = Mathf.Min(stall, lastStall);
            }
            else
            {
                stall = lastStall;
            }

            if (start != StartState.Editor)
                stall = Mathf.Clamp(stall, lastStall - 2f * TimeWarp.fixedDeltaTime, lastStall + 2f * TimeWarp.fixedDeltaTime);     //Limits stall to increasing at a rate of 2/s
            
            stall = Mathf.Clamp01(stall);
        }


        /// <summary>
        /// This calculates the lift and drag coefficients
        /// </summary>
        private void CalculateCoefficients(float MachNumber, float AoA)
        {

            minStall = 0;// ShockStall(MachNumber);

            liftslope = GetLiftSlope(MachNumber);// / AoA;     //Prandtl lifting Line

            //float SweepOrMiddle = Mathf.Abs(SweepAngle);
            float sonicLe = 0;

            //if (PartInFrontOf)
            //    SweepOrMiddle = 0;

            float ACshift = 0, ACweight = 0;
            DetermineStall(MachNumber, AoA, out ACshift, out ACweight);

            if (MachNumber > 1)
            {
                sonicLe = Mathf.Sqrt(MachNumber * MachNumber - 1);
                sonicLe /= FARMathUtil.FastTan(Mathf.Clamp(Mathf.Abs(Mathf.Acos(SweepAngle) - Mathf.PI * 0.5f), -1.57f, 1.57f));
                //print(sonicLe);
            }

            //float CdMultiplier = CdCompressibilityMultiplier(MachNumber, SweepOrMiddle, sonicLe);

            //downWash = CalculateDownwash();
            //AoA -= downWash;
            /*
             * Subsonic nonlinear lift / drag code
             * 
             */
            if (MachNumber <= 0.8f)
            {
                float Cn = liftslope;
                Cl = Cn * FARMathUtil.FastSin(2 * AoA) * 0.5f;
                
                Cl += ClIncrementFromRear;
                if (Mathf.Abs(Cl) > Mathf.Abs(ACweight))
                    ACshift *= Mathf.Clamp01(Mathf.Abs(ACweight / Cl));
                //Cl += UnsteadyAerodynamicsCl();
                Cd = (0.006f + Cl * Cl / piARe);     //Drag due to 3D effects on wing and base constant
            }
            /*
             * Supersonic nonlinear lift / drag code
             * 
             */
            else if (MachNumber > 1.2f)
            {
                float axialForce = 0;
                float coefMult = 1 / (1.4f * MachNumber * MachNumber);

                float sonicLEFactor = 1;

                //This handles the effect of wings that have their leading edges inside their own Mach cone / ones outside their own Mach cone
                if (sonicLe < 1)
                    sonicLEFactor = liftslope * (Mathf.Clamp01(1 - sonicLe) + 1) * 0.125f;
                else
                    sonicLEFactor = (1 - 0.125f * liftslope) * Mathf.Clamp01(1 - 1 / sonicLe) + 0.125f * liftslope;

                float normalForce = GetSupersonicPressureDifference(MachNumber, AoA, out axialForce);        //get the pressure difference across the airfoil
                float CosAoA = FARMathUtil.FastCos(AoA);
                float SinAoA = FARMathUtil.FastSin(AoA);
                Cl = coefMult * (normalForce * CosAoA * Mathf.Sign(AoA) * sonicLEFactor - axialForce * SinAoA);
                Cd = coefMult * (Mathf.Abs(normalForce * SinAoA) * sonicLEFactor + axialForce * CosAoA);
                Cd += 0.006f;
                Cd += CdCompressibilityZeroLiftIncrement(MachNumber, SweepAngle);
            }
            /*
             * Transonic nonlinear lift / drag code
             * This uses a blend of subsonic and supersonic aerodynamics to try and smooth the gap between the two regimes
             */ 
            else
            {
                float subScale = 3f - 2.5f * MachNumber;            //This determines the weight of subsonic flow; supersonic uses 1-this
                float Cn = liftslope;
                Cl = Cn * FARMathUtil.FastSin(2 * AoA) * 0.5f;
                if (MachNumber < 1)
                {
                    Cl += ClIncrementFromRear;
                    if (Mathf.Abs(Cl) > Mathf.Abs(ACweight))
                        ACshift *= Mathf.Clamp01(Mathf.Abs(ACweight / Cl));
                }
                Cd = Cl * Cl / piARe;     //Drag due to 3D effects on wing and base constant
                Cl *= subScale;
                Cd *= subScale;



                float M = Mathf.Clamp(MachNumber, 1.1f, Mathf.Infinity);
                float axialForce = 0;
                float coefMult = 1 / (1.4f * M * M);
                float sonicLEFactor = 1;

                //This handles the effect of wings that have their leading edges inside their own Mach cone / ones outside their own Mach cone
                if (sonicLe < 1)
                    sonicLEFactor = liftslope * (Mathf.Clamp01(1 - sonicLe) + 1) * 0.125f;
                else
                    sonicLEFactor = (1 - 0.125f * liftslope) * Mathf.Clamp01(1 - 1 / sonicLe) + 0.125f * liftslope;

                subScale = 1 - subScale; //Adjust for supersonic code
                float normalForce = GetSupersonicPressureDifference(M, AoA, out axialForce);
                float CosAoA = FARMathUtil.FastCos(AoA);
                float SinAoA = FARMathUtil.FastSin(AoA);
                Cl += coefMult * (normalForce * CosAoA * Mathf.Sign(AoA) * sonicLEFactor - axialForce * SinAoA) * (subScale);
                Cd += coefMult * (Mathf.Abs(normalForce * SinAoA) * sonicLEFactor + axialForce * CosAoA) * (subScale);
                Cd += 0.006f;
                Cd += CdCompressibilityZeroLiftIncrement(MachNumber, SweepAngle);
            }

            Vector3 ACShiftVec;
            if (!float.IsNaN(ACshift))
                ACShiftVec = ACshift * ParallelInPlane;
            else
                ACShiftVec = Vector3.zero;
            AerodynamicCenter = AerodynamicCenter + ACShiftVec;

            stall = Mathf.Clamp(stall, minStall, 1);


            Cl -= Cl * stall * 0.769f;
            Cd += Cd * stall * 0.4f;

            Cl *= ClCdInterference;

            ClIncrementFromRear = 0;
        }


        #region Supersonic Calculations

 /*       //approximations of oblique shock relation; I can't run an iterative search multiple times every physics update :P
        //returns beta in degrees
        private float GetBetaMax(float M)
        {
            float beta;
            beta = 25 / M;
            beta += 65;

            return beta;
        }

        //returns theta in degrees
        private float GetThetaMax(float M)
        {
            float theta;
            theta = 45.5f;
            theta -= 40f / M;
            theta -= 5.5f / (M * M * M);

            return theta;
        }*/

        //This models the wing using a symmetric diamond airfoil

        private float GetSupersonicPressureDifference(float M, float AoA, out float axialForce)
        {
            float pRatio;

            float maxSinBeta = FARAeroUtil.CalculateSinMaxShockAngle(M, FARAeroUtil.currentBodyAtm.y);//GetBetaMax(M) * Mathf.Deg2Rad;
            float minSinBeta = 1 / M;


            float halfAngle = 0.05f;            //Corresponds to ~2.8 degrees or approximately what you would get from a ~4.8% thick diamond airfoil

            float AbsAoA = Mathf.Abs(AoA);

            float angle1 = halfAngle - AbsAoA;                  //Region 1 is the upper surface ahead of the max thickness
            float M1;
            float p1;       //pressure ratio wrt to freestream pressure
            if (angle1 >= 0)
                p1 = ShockWaveCalculation(angle1, M, out M1, maxSinBeta, minSinBeta);
            else
                p1 = PMExpansionCalculation(Mathf.Abs(angle1), M, out M1, maxSinBeta, minSinBeta);

                                                                //Region 2 is the upper surface behind the max thickness
            float p2 = PMExpansionCalculation(2 * halfAngle, M1, maxSinBeta, minSinBeta) * p1;

            float angle3 = halfAngle + AbsAoA;                  //Region 3 is the lower surface ahead of the max thickness
            float M3;
            float p3;       //pressure ratio wrt to freestream pressure
            p3 = ShockWaveCalculation(angle3, M, out M3, maxSinBeta, minSinBeta);

                                                                //Region 4 is the lower surface behind the max thickness
            float p4 = PMExpansionCalculation(2 * halfAngle, M3, maxSinBeta, minSinBeta) * p3;

            //float cosHalfAngle = Mathf.Cos(halfAngle);
            //float sinHalfAngle = halfAngle;

            pRatio = (p3 + p4) - (p1 + p2);

            axialForce = (p1 + p3) - (p2 + p4);
            axialForce *= 0.048f;               //Thickness of the airfoil

            return pRatio;
        }

        
        private float ShockWaveCalculation(float angle, float inM, out float outM, float maxSinBeta, float minSinBeta)
        {
            //float sinBeta = (maxBeta - minBeta) * angle / maxTheta + minBeta;
            float sinBeta = FARAeroUtil.CalculateSinWeakObliqueShockAngle(inM, FARAeroUtil.currentBodyAtm.y, angle);
            if (float.IsNaN(sinBeta))
                sinBeta = maxSinBeta;

            Mathf.Clamp(sinBeta, minSinBeta, maxSinBeta);

            float normalInM = sinBeta * inM;
            normalInM = Mathf.Clamp(normalInM, 1f, Mathf.Infinity);

            float tanM = inM * Mathf.Sqrt(Mathf.Clamp01(1 - sinBeta * sinBeta));

            float normalOutM = FARAeroUtil.MachBehindShock.Evaluate(normalInM);

            outM = Mathf.Sqrt(normalOutM * normalOutM + tanM * tanM);

            float pRatio = FARAeroUtil.PressureBehindShock.Evaluate(normalInM);

            return pRatio;
        }

        private float PMExpansionCalculation(float angle, float inM, out float outM, float maxBeta, float minBeta)
        {
            inM = Mathf.Clamp(inM, 1, Mathf.Infinity);
            float nu1 = FARAeroUtil.PrandtlMeyerMach.Evaluate(inM);
            float theta = angle * Mathf.Rad2Deg;
            float nu2 = nu1 + theta;
            if (nu2 >= FARAeroUtil.maxPrandtlMeyerTurnAngle)
            {
                //minStall += (nu2 - FARAeroUtil.maxPrandtlMeyerTurnAngle) * 0.066666667f;
                //minStall = Mathf.Clamp01(minStall);
                nu2 = FARAeroUtil.maxPrandtlMeyerTurnAngle;
            }
            outM = FARAeroUtil.PrandtlMeyerAngle.Evaluate(nu2);

            float ratio;

            ratio = FARAeroUtil.StagnationPressure.Evaluate(inM) / FARAeroUtil.StagnationPressure.Evaluate(outM);
            return ratio;
        }

        private float PMExpansionCalculation(float angle, float inM, float maxBeta, float minBeta)
        {
            inM = Mathf.Clamp(inM, 1, Mathf.Infinity);
            float nu1 = FARAeroUtil.PrandtlMeyerMach.Evaluate(inM);
            float theta = angle * Mathf.Rad2Deg;
            float nu2 = nu1 + theta;
            if (nu2 >= FARAeroUtil.maxPrandtlMeyerTurnAngle)
            {
//                minStall += (nu2 - FARAeroUtil.maxPrandtlMeyerTurnAngle) * 0.066666667f;
//                minStall = Mathf.Clamp01(minStall);
                nu2 = FARAeroUtil.maxPrandtlMeyerTurnAngle;
            }
            float outM = FARAeroUtil.PrandtlMeyerAngle.Evaluate(nu2);

            float ratio;

            ratio = FARAeroUtil.StagnationPressure.Evaluate(inM) / FARAeroUtil.StagnationPressure.Evaluate(outM);
            return ratio;
        }
        
        #endregion

        protected float GetAoAmax()
        {

            
            float StallAngle;
            /*
            if (MachNumber < 1.1)
            {
                float tmp = 2 / effective_AR;
                StallAngle = 16 * (Mathf.Sqrt(1 + Mathf.Pow(tmp, 2)) + 2 / (tmp));

                if (Mathf.Abs(SweepAngle) >= 0.4)
                    StallAngle *= 1 / Mathf.Abs(SweepAngle);
                else
                    StallAngle *= 2.5f;


                StallAngle = Mathf.Clamp(StallAngle, 15, 45) * Mathf.Deg2Rad;
            }
            else
                StallAngle = Mathf.PI / 3;*/

            StallAngle = 1.6f / liftslope;          //Simpler version of above commented out section; just limit the lift coefficient to ~1.6

            return StallAngle;

        }


        private float GetLiftSlope(float MachNumber)
        {
            float sweepHalfChord = MidChordSweep * Mathf.Deg2Rad;

            float CosPartAngle = Mathf.Clamp(Vector3.Dot(sweepPerpLocal, ParallelInPlaneLocal), -1, 1);
            float tmp = Mathf.Clamp(Vector3.Dot(sweepPerp2Local, ParallelInPlaneLocal), -1, 1);

            if(Mathf.Abs(CosPartAngle) > Mathf.Abs(tmp))                //Based on perp vector find which line is the right one
                sweepHalfChord = Mathf.Acos(CosPartAngle);
            else
                sweepHalfChord = Mathf.Acos(tmp);

            if (sweepHalfChord > Mathf.PI * 0.5f)
                sweepHalfChord -= Mathf.PI;

            CosPartAngle = Mathf.Clamp(ParallelInPlaneLocal.y, -1, 1);

            CosPartAngle *= CosPartAngle;
            float SinPartAngle2 = Mathf.Clamp01(1 - CosPartAngle);               //Get the squared values for the angles

            effective_b_2 = Mathf.Max(b_2 * CosPartAngle, MAC * SinPartAngle2);
            effective_MAC = MAC * CosPartAngle + b_2 * SinPartAngle2;
            effective_AR = 2 * effective_b_2 / effective_MAC;

            SetSweepAngle(sweepHalfChord);

            effective_AR = EffectOfExposure();

            piARe = Mathf.PI * effective_AR * e;

            /*if (MachNumber < 1)
                tmp = Mathf.Clamp(MachNumber, 0, 0.9f);
            else
                tmp = 1 / Mathf.Clamp(MachNumber, 1.09f, Mathf.Infinity);*/

            if(MachNumber < 1)
                tmp = 1 - MachNumber * MachNumber;
            else
                tmp = MachNumber * MachNumber - 1;

            float sweepTmp = FARMathUtil.FastTan(SweepAngle);
            sweepTmp *= sweepTmp;

            tmp += sweepTmp;
            tmp = Mathf.Sqrt(tmp) * effective_AR;

            float liftslope = FARAeroUtil.LiftSlope.Evaluate(tmp) * effective_AR;

            /*float liftslope = Mathf.Pow(effective_AR / FARAeroUtil.FastCos(sweepHalfChord), 2) + 4 - Mathf.Pow(effective_AR * tmp, 2);
            liftslope = 2 + Mathf.Sqrt(Mathf.Clamp(liftslope, 0, Mathf.Infinity));
            liftslope = twopi / liftslope * effective_AR;*/

            return liftslope;

        }


        private void SetSweepAngle(float sweepHalfChord)
        {
            SweepAngle = sweepHalfChord;
            SweepAngle = FARMathUtil.FastTan(SweepAngle);
            float tmp = (1 - TaperRatio) / (1 + TaperRatio);
            tmp *= 2 / effective_AR;
            SweepAngle += tmp;
            SweepAngle = FARMathUtil.FastCos(Mathf.Atan(SweepAngle));
        }

        
        #region Compressibility


        /// <summary>
        /// This models stall due to shockwaves appearing on the wings during transonic flight
        /// </summary>
/*        private float ShockStall(float M)
        {
            float flowSep = 0;
            if (Mathf.Abs(AoA) < Mathf.PI / 36)
                return 0;
            float CritMach = FARAeroUtil.CriticalMachNumber.Evaluate(Mathf.Abs(AoA));
            if(M > CritMach)
            {
                flowSep = Mathf.Min((M - CritMach) * 0.5f, (1.1f - M) * 0.5f);
                flowSep = Mathf.Clamp01(flowSep);
            }
            return flowSep;
        }*/



        /// <summary>
        /// This modifies the Cd to account for compressibility effects
        /// </summary>
        private float CdCompressibilityZeroLiftIncrement(float M, float SweepAngle)
        {

            if (PartInFrontOf != null)
            {
                zeroLiftCdIncrement = WingInFrontOf.zeroLiftCdIncrement;
                return zeroLiftCdIncrement;
            }

            float tmp = 1 / Mathf.Sqrt(SweepAngle);

            float dd_MachNumber = 0.85f * tmp;               //Find Drag Divergence Mach Number

            if(M < dd_MachNumber)                                               //If below this number, 
                return 0;

            float peak_MachNumber = 1.1f * tmp;

            float peak_Increment = 0.025f * Mathf.Pow(SweepAngle, 2.5f);

            if (M > peak_MachNumber)
                return peak_Increment;

            float CdIncrement = (M - dd_MachNumber) / (peak_MachNumber - dd_MachNumber) * peak_Increment;

            zeroLiftCdIncrement = CdIncrement;

            return CdIncrement;
        }
        
        /*private float CdCompressibilityMultiplier(float M, float SweepOrMiddle, float sonicLE)
        {
            float CdMultiplier;

            float severityFactor = 1 - SweepOrMiddle;
            severityFactor *= severityFactor;

            float ExpSlope = 1.1f - severityFactor;                    //These make drag worse for unswept wings; add 1 to this to get multiplier at Mach 1
            float MinfAsym = 1.35f - severityFactor * 0.35f;
            float contfactor = (1 + ExpSlope - MinfAsym);                  //make continuous
            if (M <= 1)
            {
                CdMultiplier = 1f + ExpSlope * FARAeroUtil.ExponentialApproximation(10 * M - 10f);            //Exponentially increases, mostly from 0.8 to 1;  Models Drag divergence due to locally supersonic flow around object at flight Mach Numbers < 1
            }
            else
            {
                CdMultiplier = contfactor / M + MinfAsym;             //Cd drops after Mach 1 for wings

//                if (sonicLE > 1)
//                {
//                    CdMultiplier *= 5 * (5 * sonicLE / (sonicLE + 5) + sonicLE) + 1;
//                }
            }
            
            return CdMultiplier;
        }*/
        #endregion

        #region Exposure / Whole is greater than sum of parts

        ///<summary>
        /// This function calculates 4 Exposure values corresponding to the wing part's location wrt other wings and the fuselage
        /// These values describe forward, backward, outward and inward exposure for the wing and have no formal basis other than "things block air, therefore adjust Cl and Cd"
        /// </summary>
        /// 

        private void WingExposureFunction()
        {
            if (VesselPartList == null)
                VesselPartList = GetShipPartList();

            ray.direction = part.transform.up;
            forwardexposure = ExposureDirection(ray, hit, VesselPartList, MAC, true);

            ray.direction = -part.transform.up;
            backwardexposure = ExposureDirection(ray, hit, VesselPartList, MAC, true);

            ray.direction = part.transform.right;
            inwardexposure = ExposureDirection(ray, hit, VesselPartList, b_2, false);

            ray.direction = -part.transform.right;
            outwardexposure = ExposureDirection(ray, hit, VesselPartList, b_2, false);

            //This part handles effects of biplanes, triplanes, etc.
            ClCdInterference = 1;
            ray.direction = part.transform.forward;
            ClCdInterference *= WingInterference(ray, hit, VesselPartList, b_2);
            ray.direction = -part.transform.forward;
            ClCdInterference *= WingInterference(ray, hit, VesselPartList, b_2);
        }

        private float WingInterference(Ray ray, RaycastHit hit, List<Part> PartList, float dist)
        {
            float interferencevalue = 1;

            ray.origin = WingCentroid();

            hit.distance = 0;
            RaycastHit[] hits = Physics.RaycastAll(ray.origin, ray.direction, dist, FARAeroUtil.RaycastMask);
            foreach (RaycastHit h in hits)
            {
                if (h.collider != null)
                {
                    foreach (Part p in PartList)
                    {
                        if (p.Modules.Contains("FARWingAerodynamicModel"))
                        {
                            if (h.collider == p.collider && p != part)
                            {
                                if (h.distance > 0)
                                {
                                    float tmp = h.distance / b_2;
                                    tmp = Mathf.Clamp01(tmp);
                                    interferencevalue = Mathf.Min(tmp, interferencevalue);
                                }
                                break;
                            }
                        }
                    }
                }
            }
            return interferencevalue;
        }

        private float ExposureDirection(Ray ray, RaycastHit hit, List<Part> PartList, float dist, bool span)
        {
            float exposure = 1;
            if (nonSideAttach == 0)
                for (int i = 0; i < 5; i++)
                {
                    //Vector3 centroid = WingCentroid();
                    if (span)
                    {
                        ray.origin = part.transform.position - b_2 * (i * 0.2f + 0.1f) * part.transform.right.normalized * srfAttachNegative;
                    }
                    else
                    {
                        ray.origin = part.transform.position + (MAC * i * 0.25f - (MAC * 0.5f)) * part.transform.up.normalized * 0.8f;
//                        ray.origin = part.transform.position + (MAC * i / 4 - (MAC / 2)) * part.transform.up.normalized * 0.8f;
                        ray.origin -= b_2 / 2 * part.transform.right.normalized * srfAttachNegative;
                    }

                    if (dist <= 0)
                        dist = 1;

                    hit.distance = 0;
                    RaycastHit[] hits = Physics.RaycastAll(ray.origin, ray.direction, dist, FARAeroUtil.RaycastMask);
                    bool gotSomething = false;
                    foreach (RaycastHit h in hits)
                    {
                        if (h.collider != null)
                        {
                            foreach (Part p in PartList)
                            {
                                if (h.collider == p.collider && p != part)
                                {
                                    if (h.distance > 0)
                                    {
                                        exposure -= 0.2f;
                                        gotSomething = true;
                                    }
                                    break;
                                }
                            }
                        }
                        if (gotSomething)
                            break;
                    }
                }
            else
            {

                ray.origin = part.transform.position - MAC * 0.7f * part.transform.up.normalized;
                

                if (dist <= 0)
                    dist = 1;

                hit.distance = 0;
                RaycastHit[] hits = Physics.RaycastAll(ray.origin, ray.direction, dist, FARAeroUtil.RaycastMask);
                bool gotSomething = false;
                foreach (RaycastHit h in hits)
                {
                    if (h.collider != null)
                    {
                        foreach (Part p in PartList)
                        {
                            if (h.collider == p.collider && p != part)
                            {
                                if (h.distance > 0)
                                {
                                    exposure -= 1f;
                                    gotSomething = true;
                                }
                                break;
                            }
                        }
                    }
                    if (gotSomething)
                        break;
                }
            }
            return exposure;
        }
        /// <summary>
        /// This function adjusts the lift and drag coefficients based on the 4 Exposure values
        /// </summary>

        private float EffectOfExposure()
        {
            float forwardbackward = ParallelInPlaneLocal.y;
            float inwardoutward = ParallelInPlaneLocal.x * srfAttachNegative;
            //ClExposureModifier = 0;
            //CdExposureModifier = 0;
//            LeExposure = 0;
//            TeExposure = 0;
            WingtipExposure = 0;
            WingrootExposure = 0;

            //LeExposure = forwardexposure * Mathf.Pow(Mathf.Clamp01(forwardbackward), 2) + backwardexposure * Mathf.Pow(Mathf.Clamp01(-forwardbackward), 2) + outwardexposure * Mathf.Pow(Mathf.Clamp01(inwardoutward), 2) + inwardexposure * Mathf.Pow(Mathf.Clamp01(-inwardoutward), 2);
            //TeExposure = forwardexposure * Mathf.Pow(Mathf.Clamp01(-forwardbackward), 2) + backwardexposure * Mathf.Pow(Mathf.Clamp01(forwardbackward), 2) + outwardexposure * Mathf.Pow(Mathf.Clamp01(-inwardoutward), 2) + inwardexposure * Mathf.Pow(Mathf.Clamp01(inwardoutward), 2);
            //WingtipExposure = forwardexposure * Mathf.Pow(Mathf.Clamp01(-inwardoutward), 2) + backwardexposure * Mathf.Pow(Mathf.Clamp01(inwardoutward), 2) + outwardexposure * Mathf.Pow(Mathf.Clamp01(forwardbackward), 2) + inwardexposure * Mathf.Pow(Mathf.Clamp01(-forwardbackward), 2);
            //WingrootExposure = forwardexposure * Mathf.Pow(Mathf.Clamp01(inwardoutward), 2) + backwardexposure * Mathf.Pow(Mathf.Clamp01(-inwardoutward), 2) + outwardexposure * Mathf.Pow(Mathf.Clamp01(-forwardbackward), 2) + inwardexposure * Mathf.Pow(Mathf.Clamp01(forwardbackward), 2);

            if (forwardbackward > 0)
            {
                forwardbackward *= forwardbackward;
//                LeExposure += forwardexposure * forwardbackward;
//                TeExposure += backwardexposure * forwardbackward;
                WingtipExposure += outwardexposure * forwardbackward;
                WingrootExposure += inwardexposure * forwardbackward;
            }
            else
            {
                forwardbackward *= forwardbackward;
//                LeExposure += backwardexposure * forwardbackward;
//                TeExposure += forwardexposure * forwardbackward;
                WingtipExposure += inwardexposure * forwardbackward;
                WingrootExposure += outwardexposure * forwardbackward;
            }

            if (inwardoutward > 0)
            {
                inwardoutward *= inwardoutward;
//                LeExposure += outwardexposure * inwardoutward;
//                TeExposure += inwardexposure * inwardoutward;
                WingtipExposure += backwardexposure * inwardoutward;
                WingrootExposure += forwardexposure * inwardoutward;
            }
            else
            {
                inwardoutward *= inwardoutward;
//                LeExposure += inwardexposure * inwardoutward;
//                TeExposure += outwardexposure * inwardoutward;
                WingtipExposure += forwardexposure * inwardoutward;
                WingrootExposure += backwardexposure * inwardoutward;
            }

            WingtipExposure = 1-WingtipExposure;
            WingrootExposure = 1-WingrootExposure;


            float effective_AR_modifier = (WingrootExposure + WingtipExposure);

            

            float e_AR;

            if (effective_AR_modifier < 1)
                e_AR = effective_AR * (effective_AR_modifier + 1);
            else
                e_AR = effective_AR * 2 * (2 - effective_AR_modifier) + 30 * (effective_AR_modifier - 1);

//            print(forwardexposure + " " + backwardexposure + " " + outwardexposure + " " + inwardexposure + " " + e_AR);

            /*//This segment calculates Cl modifiers; uses the same variables for Cd later on
            float LeModifier = 0.2f * LeExposure + 0.8f;
            float TeModifier = 0.2f * TeExposure + 0.8f;

            ClExposureModifier = LeModifier * TeModifier;

            //This segment calculates Cd modifiers
            LeModifier = 0.2f * LeExposure + 0.8f;
            TeModifier = 0.2f * TeExposure + 0.8f;

            CdExposureModifier = LeModifier * TeModifier;*/

            return e_AR;

        }

        #endregion


    }


}