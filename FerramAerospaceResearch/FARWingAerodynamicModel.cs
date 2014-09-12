/*
Ferram Aerospace Research v0.14.1.2
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
        public double AoAmax = 15;

        [KSPField(isPersistant = false)]
        public double MAC;

        [KSPField(isPersistant = false)]
        public double e;

        [KSPField(isPersistant = false)]
        public double nonSideAttach;           //This is for ailerons and the small ctrl surf


        [KSPField(isPersistant = false)]
        public double TaperRatio;

        [KSPField(isPersistant = false, guiActive = true, guiName = "Stalled %", guiFormat = "F2")]
        protected double stall = 0;

        private double minStall = 0;

        private const double twopi = Math.PI * 2;   //lift slope
        private double piARe = 1;    //induced drag factor

        [KSPField(isPersistant = false)]
        public double b_2;        //span


        [KSPField(isPersistant = false)]
        public double MidChordSweep;
        private double MidChordSweepSideways = 0;

        private double SweepAngle = 0;

        private double effective_b_2 = 1;
        private double effective_MAC = 1;

        protected double effective_AR = 4;
        protected double transformed_AR = 4;

        [KSPField(isPersistant = false, guiActive = false, guiName = "Current lift", guiUnits = "kN", guiFormat = "F3")]
        protected float currentLift = 0.0f;

        [KSPField(isPersistant = false, guiActive = false, guiName = "Current drag", guiUnits = "kN", guiFormat = "F3")]
        protected float currentDrag = 0.0f;

        private double liftslope = 0;
        protected double ClCdInterference = 1;

        private double forwardexposure;
        private double backwardexposure;
        private double outwardexposure;
        private double inwardexposure;

        protected double WingtipExposure = 0;
        protected double WingrootExposure = 0;

        protected double zeroLiftCdIncrement = 0;

        protected double criticalCl = 1.6;


        public Vector3d AerodynamicCenter = Vector3d.zero;
        private Vector3d CurWingCentroid = Vector3d.zero;
        private Vector3d ParallelInPlane = Vector3d.zero;
        private Vector3d perp = Vector3d.zero;
        private Vector3d liftDirection = Vector3d.zero;

        [KSPField(isPersistant = false)]
        private Vector3d rootMidChordOffsetFromOrig = Vector3d.zero;

        // in local coordinates
        private Vector3d localWingCentroid = Vector3.zero;
        private Vector3d sweepPerpLocal, sweepPerp2Local;
        private Vector3d ParallelInPlaneLocal = Vector3.zero;

        private Int16 srfAttachNegative = 1;

        private Vector3d LastInFrontRaycast = Vector3.zero;
        private int LastInFrontRaycastCount = 0;
        private Part PartInFrontOf = null;
        private FARWingAerodynamicModel WingInFrontOf = null;

        protected double ClIncrementFromRear = 0f;

        private double rho = 1;

        public double YmaxForce = double.MaxValue;
        public double XZmaxForce = double.MaxValue;

        #region GetFunctions

        public double GetStall()
        {
            return stall;
        }

        public double GetCl()
        {

            double ClUpwards = 1;
            if (HighLogic.LoadedSceneIsFlight)
                ClUpwards = Vector3.Dot(liftDirection, -vessel.transform.forward);
            ClUpwards *= Cl;

            return ClUpwards;
        }

        public double GetCd()
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

        public Vector3d GetAerodynamicCenter()
        {
            return AerodynamicCenter;
        }

        private void PrecomputeCentroid()
        {
            Vector3d WC = rootMidChordOffsetFromOrig;
            if (nonSideAttach <= 0)
            {
                WC += -b_2 / 3 * (1 + TaperRatio * 2) / (1 + TaperRatio) * (Vector3d.right * srfAttachNegative + Vector3d.up * Math.Tan(MidChordSweep * FARMathUtil.deg2rad));
            }
            else
                WC += (-MAC * 0.7) * Vector3d.up;

            localWingCentroid = WC;
        }

        public Vector3 WingCentroid()
        {
            return part_transform.TransformDirection(localWingCentroid) + part.transform.position;
        }

        public override Vector3d GetVelocity()
        {
            if (HighLogic.LoadedSceneIsFlight)
                return part.Rigidbody.GetPointVelocity(WingCentroid()) + Krakensbane.GetFrameVelocityV3f();
            else
                return velocityEditor;
        }

        private Vector3d CalculateAerodynamicCenter(double MachNumber, double AoA, Vector3d WC)
        {
            Vector3d AC_offset = Vector3d.zero;
            if (nonSideAttach <= 0)
            {
                double tmp = Math.Cos(AoA);
                tmp *= tmp;
                if (MachNumber < 0.85)
                    AC_offset = effective_MAC * 0.25 * ParallelInPlane;
                else if (MachNumber > 1.4)
                    AC_offset = effective_MAC * 0.10 * ParallelInPlane;
                else if (MachNumber >= 1)
                    AC_offset = effective_MAC * (-0.375 * MachNumber + 0.625) * ParallelInPlane;
                    //This is for the transonic instability, which is lessened for highly swept wings
                else
                {
                    double sweepFactor = SweepAngle * SweepAngle * tmp;
                    if (MachNumber < 0.9)
                        AC_offset = effective_MAC * ((MachNumber - 0.85) * 2 * sweepFactor + 0.25) * ParallelInPlane;
                    else
                        AC_offset = effective_MAC * ((1 - MachNumber) * sweepFactor + 0.25) * ParallelInPlane;
                }

                AC_offset *= tmp;

            }

            WC += AC_offset;

            return WC;      //WC updated to AC
        }

        public double GetMAC()
        {
            return effective_MAC;
        }
        public double Getb_2()
        {
            return effective_b_2;
        }

        public void ComputeClCdEditor(Vector3d velocityVector, double M)
        {
            velocityEditor = velocityVector;

            rho = 1;

            double AoA = CalculateAoA(velocityVector);
            CalculateForces(velocityVector, M, AoA);
        }

        public Vector3d GetLiftDirection()
        {
            return liftDirection;
        }

        protected override void ResetCenterOfLift()
        {
            rho = 1;
            stall = 0;
        }

        protected override Vector3d PrecomputeCenterOfLift(Vector3d velocity, double MachNumber, FARCenterQuery center)
        {
            double AoA = CalculateAoA(velocity);

            Vector3d force = CalculateForces(velocity, MachNumber, AoA);
            center.AddForce(AerodynamicCenter, force);

            return force;
        }

        #endregion

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            OnVesselPartsChange += RunExposure;

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
            double lengthScale = Math.Sqrt(FARAeroUtil.areaFactor);

            b_2 *= lengthScale;
            MAC *= lengthScale;

            S = b_2 * MAC;

            if (part.srfAttachNode.originalOrientation.x < 0)
                srfAttachNegative = -1;

            transformed_AR = 2 * b_2 / MAC;

            MidChordSweepSideways = (1 - TaperRatio) / (1 + TaperRatio);

            MidChordSweepSideways = (Math.PI * 0.5 - Math.Atan(Math.Tan(MidChordSweep * FARMathUtil.deg2rad) + MidChordSweepSideways * 2 / transformed_AR)) * MidChordSweepSideways * 0.5;

            double sweepHalfChord = MidChordSweep * FARMathUtil.deg2rad;

            sweepPerpLocal = Vector3d.up * Math.Cos(sweepHalfChord) + Vector3d.right * Math.Sin(sweepHalfChord) * srfAttachNegative; //Vector perpendicular to midChord line
            sweepPerp2Local = Vector3d.up * Math.Sin(MidChordSweepSideways) - Vector3d.right * Math.Cos(MidChordSweepSideways) * srfAttachNegative; //Vector perpendicular to midChord line2

            PrecomputeCentroid();
            RunExposure();

            if (FARDebugValues.allowStructuralFailures)
            {
                FARPartStressTemplate template;
                foreach (FARPartStressTemplate temp in FARAeroStress.StressTemplates)
                    if (temp.name == "wingStress")
                    {
                        template = temp;

                        YmaxForce = template.YmaxStress;    //in MPa
                        YmaxForce *= S;

                        XZmaxForce = template.XZmaxStress;
                        XZmaxForce *= S;
                        break;
                    }
            }
        }

        private void RunExposure()
        {
            LastInFrontRaycastCount = 0;

            WingExposureFunction();
        }

        public virtual void FixedUpdate()
        {
            currentLift = currentDrag = 0;

            // With unity objects, "foo" or "foo != null" calls a method to check if
            // it's destroyed. (object)foo != null just checks if it is actually null.
            if (HighLogic.LoadedSceneIsFlight && !isShielded && (object)part != null)
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

                    Vector3d velocity = rb.GetPointVelocity(CurWingCentroid) + Krakensbane.GetFrameVelocity();
                    double soundspeed, v_scalar = velocity.magnitude;

                    rho = FARAeroUtil.GetCurrentDensity(vessel, out soundspeed);
                    if (rho > 0 && v_scalar > 0.1)
                    {
                        double MachNumber = v_scalar / soundspeed;
                        double AoA = CalculateAoA(velocity);
                        Vector3d force = DoCalculateForces(velocity, MachNumber, AoA);

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

        public Vector3d CalculateForces(Vector3d velocity, double MachNumber, double AoA)
        {
            CurWingCentroid = WingCentroid();

            return DoCalculateForces(velocity, MachNumber, AoA);
        }

        private Vector3d DoCalculateForces(Vector3d velocity, double MachNumber, double AoA)
        {
            //This calculates the angle of attack, adjusting the part's orientation for any deflection
            //CalculateAoA();

            if (isShielded)
            {
                Cl = Cd = Cm = stall = 0;
                return Vector3d.zero;
            }

            double v_scalar = velocity.magnitude;
            //if (v_scalar <= 0.1)
            //    return Vector3d.zero;

            Vector3 forward = part_transform.forward;
            Vector3d velocity_normalized = velocity / v_scalar;

            double q = rho * v_scalar * v_scalar * 0.5;   //dynamic pressure, q

            ParallelInPlane = Vector3d.Exclude(forward, velocity).normalized;  //Projection of velocity vector onto the plane of the wing
            perp = Vector3d.Cross(forward, ParallelInPlane).normalized;       //This just gives the vector to cross with the velocity vector
            liftDirection = Vector3d.Cross(perp, velocity).normalized;

            ParallelInPlaneLocal = part_transform.InverseTransformDirection(ParallelInPlane);

            // Calculate the adjusted AC position (uses ParallelInPlane)
            AerodynamicCenter = CalculateAerodynamicCenter(MachNumber, AoA, CurWingCentroid);

            //Throw AoA into lifting line theory and adjust for part exposure and compressibility effects
            CalculateCoefficients(MachNumber, AoA);



            //lift and drag vectors
            Vector3d L = liftDirection * (q * Cl * S);    //lift;
            Vector3d D = velocity_normalized * (-q * Cd * S);                         //drag is parallel to velocity vector
            currentLift = (float)(L.magnitude * 0.001);
            currentDrag = (float)(D.magnitude * 0.001);
            Vector3d force = (L + D) * 0.001;
            if (double.IsNaN(force.sqrMagnitude) || double.IsNaN(AerodynamicCenter.sqrMagnitude))// || float.IsNaN(moment.magnitude))
            {
                Debug.LogWarning("FAR Error: Aerodynamic force = " + force.magnitude + " AC Loc = " + AerodynamicCenter.magnitude + " AoA = " + AoA + "\n\rMAC = " + effective_MAC + " B_2 = " + effective_b_2 + " sweepAngle = " + SweepAngle + "\n\rMidChordSweep = " + MidChordSweep + " MidChordSweepSideways = " + MidChordSweepSideways + "\n\r at " + part.name);
                force = AerodynamicCenter = Vector3d.zero;
            }

            if (Math.Abs(Vector3d.Dot(force, forward)) > YmaxForce || Vector3d.Exclude(forward, force).magnitude > XZmaxForce)
                if (part.parent && !vessel.packed)
                {
                    part.SendEvent("AerodynamicFailureStatus");
                    FlightLogger.eventLog.Add("[" + FARMathUtil.FormatTime(vessel.missionTime) + "] Joint between " + part.partInfo.title + " and " + part.parent.partInfo.title + " failed due to aerodynamic stresses.");
                    part.decouple(25);
                }

            return force;

        }

        protected virtual double CalculateAoA(Vector3d velocity)
        {
            double PerpVelocity = Vector3d.Dot(part_transform.forward, velocity.normalized);
            return Math.Asin(FARMathUtil.Clamp(PerpVelocity, -1, 1));
        }

        #region Interactive Effects

        private void FindWingInFrontOf()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                // Don't repeat traces until count expires or > 5 degree angle change
                if (LastInFrontRaycastCount-- > 0 &&
                    Vector3d.Dot(ParallelInPlaneLocal, LastInFrontRaycast) > 0.996)
                    return;

                LastInFrontRaycastCount = 10;
                LastInFrontRaycast = ParallelInPlaneLocal;
            }

            PartInFrontOf = null;
            WingInFrontOf = null;
            //PartInfrontOfName = "";

            if (ParallelInPlane != Vector3d.zero)
            {
                // Special case for control surfaces attached to a wing:
                // If the ray direction is within 60 degrees of up, just use parent.
                if (nonSideAttach > 0 && ParallelInPlaneLocal.y > 0.5 &&
                    (part.parent || HighLogic.LoadedSceneIsEditor && part.potentialParent))
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
                float distance = Mathf.Max((float)(3 * effective_MAC), 0.1f);
                RaycastHit[] hits = Physics.RaycastAll(CurWingCentroid, ParallelInPlane, distance, FARAeroUtil.RaycastMask);

                for (int i = 0; i < hits.Length; i++)
                {
                    RaycastHit h = hits[i];
                    if (h.rigidbody == part.rigidbody)
                        continue;

                    Collider[] colliders;
                    try
                    {
                        colliders = part.GetComponents<Collider>();
                    }
                    catch (Exception e)
                    {
                        //Fail silently because it's the only way to avoid issues with pWings
                        //Debug.LogException(e);
                        colliders = new Collider[1] { part.collider };
                    }
                    for (int j = 0; j < colliders.Length; j++)
                        if (h.collider == colliders[j])
                            continue;
                        
                    else if (h.distance < distance)
                    {
                        distance = h.distance;
                        hit = h;
                    }
                }
                if (hit.distance != 0)
                {
                    if (hit.collider.attachedRigidbody || HighLogic.LoadedSceneIsEditor)
                    {
                        Part p = null;
                        if (hit.collider.attachedRigidbody)
                            p = hit.collider.attachedRigidbody.GetComponent<Part>();
                        if (p == null && HighLogic.LoadedSceneIsEditor)
                            for (int i = 0; i < VesselPartList.Count; i++)
                            {
                                Part q = VesselPartList[i];
                                bool breakBool = false;

                                Collider[] colliders;
                                try
                                {
                                    colliders = q.GetComponents<Collider>();
                                }
                                catch (Exception e)
                                {
                                    //Fail silently because it's the only way to avoid issues with pWings
                                    //Debug.LogException(e);
                                    colliders = new Collider[1] { q.collider };
                                }
                                for (int j = 0; j < colliders.Length; j++)
                                    if (hit.collider == colliders[j])
                                    {
                                        p = q;
                                        breakBool = true;
                                        break;
                                    }

                                if (breakBool)
                                    break;
                            }
                        if (p != null && p != this.part)
                        {
                            if (HighLogic.LoadedSceneIsFlight && p.vessel != vessel)
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

        private void GetWingInFrontOf(double MachNumber, double AoA, out double ACshift, out double ACweight, out double wStall)
        {
            ACshift = 0;
            ACweight = 0;
            wStall = 0;

            FindWingInFrontOf();

            if ((object)WingInFrontOf != null)
            {
                var w = WingInFrontOf;

                double angle = Vector3.Dot(w.liftDirection, this.liftDirection);        //This deals with the effect of a part being attached at a strange angle and reducing the effect, so that a vertical stabilizer placed on a wing doesn't affect the lift of the wing
                
                double flapRatio = FARMathUtil.Clamp(this.effective_MAC / (this.effective_MAC + w.effective_MAC), 0, 1);
                float flt_flapRatio = (float)flapRatio;
                double flapFactor = FARAeroUtil.WingCamberFactor.Evaluate(flt_flapRatio);        //Flap Effectiveness Factor
                double dCm_dCl = FARAeroUtil.WingCamberMoment.Evaluate(flt_flapRatio);           //Change in moment due to change in lift from flap

                double liftDirVal = angle;
                double wAoA = w.CalculateAoA(w.GetVelocity()) * Math.Sign(liftDirVal);
                angle = (AoA - wAoA) * Math.Abs(angle);                //First, make sure that the AoA are wrt the same direction; then account for any strange angling of the part that shouldn't be there

                //This accounts for the wing possibly having a longer span than the flap
                double WingFraction = FARMathUtil.Clamp(this.effective_b_2 / w.effective_b_2, 0, 1);
                //This accounts for the flap possibly having a longer span than the wing it's attached to
                double FlapFraction = FARMathUtil.Clamp(w.effective_b_2 / this.effective_b_2, 0, 1);

                double ClIncrement = flapFactor * w.liftslope * Math.Sin(2 * angle) * 0.5;   //Lift created by the flap interaction
                ClIncrement *= (this.S * FlapFraction + w.S * WingFraction) / this.S;                   //Increase the Cl so that even though we're working with the flap's area, it accounts for the added lift across the entire object

                double MachCoeff = FARMathUtil.Clamp(1 - MachNumber * MachNumber, 0, 1);

                ACweight = ClIncrement * MachCoeff; // Total flap Cl for the purpose of applying ACshift, including the bit subtracted below

                ClIncrement -= FlapFraction * w.liftslope * Math.Sin(2 * angle) * 0.5;        //Removing additional angle so that lift of the flap is calculated as lift at wing angle + lift due to flap interaction rather than being greater

                ACshift = (dCm_dCl + 0.75 * (1 - flapRatio)) * (effective_MAC + w.effective_MAC);      //Change in Cm with change in Cl

                this.ClIncrementFromRear = ClIncrement * MachCoeff;

                liftslope = w.liftslope;
                SweepAngle = w.SweepAngle;
                

                wStall = w.stall * Math.Abs(liftDirVal);
                if (Math.Abs(liftDirVal) > 0.5)
                {
                    this.AoAmax = (w.AoAmax + GetAoAmax()) * 0.5;
                    this.AoAmax += (AoA - wAoA) * Math.Sign(AoA); //  less efficient than before since we calculate AoA again
                }
                else
                    AoAmax = GetAoAmax();
            }
            else
                AoAmax = GetAoAmax();
        }

        #endregion

        private void DetermineStall(double MachNumber, double AoA, out double ACshift, out double ACweight)
        {
            double lastStall = stall;
            double tmp = 0;
            stall = 0;

            GetWingInFrontOf(MachNumber, AoA, out ACshift, out ACweight, out tmp);

            double absAoA = Math.Abs(AoA);

            if (absAoA > AoAmax)
            {
                stall = FARMathUtil.Clamp((absAoA - AoAmax) * 10, 0, 1);
                stall += tmp;
                stall = Math.Max(stall, lastStall);
            }
            else if (absAoA < AoAmax * 0.75)
            {
                stall = 1 - FARMathUtil.Clamp((AoAmax * 0.75 - absAoA) * 10, 0, 1);
                stall += tmp;
                stall = Math.Min(stall, lastStall);
            }
            else
            {
                stall = lastStall;
            }

            if (HighLogic.LoadedSceneIsFlight)
                stall = FARMathUtil.Clamp(stall, lastStall - 2 * TimeWarp.fixedDeltaTime, lastStall + 2 * TimeWarp.fixedDeltaTime);     //Limits stall to increasing at a rate of 2/s

            stall = FARMathUtil.Clamp(stall, 0, 1);
        }


        /// <summary>
        /// This calculates the lift and drag coefficients
        /// </summary>
        private void CalculateCoefficients(double MachNumber, double AoA)
        {

            minStall = 0;

            liftslope = GetLiftSlope(MachNumber);// / AoA;     //Prandtl lifting Line


            double ACshift = 0, ACweight = 0;
            DetermineStall(MachNumber, AoA, out ACshift, out ACweight);

            double beta = Math.Sqrt(MachNumber * MachNumber - 1);
            double TanSweep = Math.Tan(FARMathUtil.Clamp(Math.Acos(SweepAngle), 0, Math.PI * 0.5));
            double beta_TanSweep = beta / TanSweep;

            if (double.IsNaN(beta_TanSweep))
                beta_TanSweep = 0;

            double Cd0 = CdCompressibilityZeroLiftIncrement(MachNumber, SweepAngle, TanSweep, beta_TanSweep, beta) + 0.006;
            e = FARAeroUtil.CalculateOswaldsEfficiency(effective_AR, SweepAngle, Cd0);
            piARe = effective_AR * e * Math.PI;

//            Debug.Log("Part: " + part.partInfo.title + " AoA: " + AoA);

            if (MachNumber <= 0.6)
            {
                double Cn = liftslope;
                Cl = Cn * Math.Sin(2 * AoA) * 0.5;

                Cl += ClIncrementFromRear;
                if (Math.Abs(Cl) > Math.Abs(ACweight))
                    ACshift *= FARMathUtil.Clamp(Math.Abs(ACweight / Cl), 0, 1);
                Cd = (Cl * Cl / piARe);     //Drag due to 3D effects on wing and base constant
                Cd += Cd0;
            }
            /*
             * Supersonic nonlinear lift / drag code
             * 
             */
            else if (MachNumber > 1.4)
            {
                double axialForce = 0;

                //DATCOMSupersonicLiftAndDrag(MachNumber, SweepAngle, sonicLe, AoA, out Cl, out Cd);

                double coefMult = 1 / (FARAeroUtil.currentBodyAtm.y * MachNumber * MachNumber);

                /*double sonicLEFactor = 1;

                //This handles the effect of wings that have their leading edges inside their own Mach cone / ones outside their own Mach cone
                if (sonicLe < 1)
                    sonicLEFactor = liftslope * (FARMathUtil.Clamp(1 - sonicLe, 0, 1) + 1) * 0.125;
                else
                    sonicLEFactor = (1 - 0.125 * liftslope) * FARMathUtil.Clamp(1 - 1 / sonicLe, 0, 1) + 0.125 * liftslope;*/

                double supersonicLENormalForceFactor = CalculateSupersonicLEFactor(beta, TanSweep, beta_TanSweep);

                double normalForce;
                if (FARDebugValues.useSplinesForSupersonicMath)
                    normalForce = GetSupersonicPressureDifference(MachNumber, AoA, out axialForce);
                else
                    normalForce = GetSupersonicPressureDifferenceNoSpline(MachNumber, AoA, out axialForce);
                double CosAoA = Math.Cos(AoA);
                double SinAoA = Math.Sin(AoA);
                //Cl = coefMult * (normalForce * CosAoA * Math.Sign(AoA) * sonicLEFactor - axialForce * SinAoA);
                //Cd = coefMult * (Math.Abs(normalForce * SinAoA) * sonicLEFactor + axialForce * CosAoA);

                Cl = coefMult * normalForce * CosAoA * Math.Sign(AoA) * supersonicLENormalForceFactor;
                Cd = beta * Cl * Cl / piARe;

                Cd += Cd0;
            }
            /*
             * Transonic nonlinear lift / drag code
             * This uses a blend of subsonic and supersonic aerodynamics to try and smooth the gap between the two regimes
             */
            else
            {
                double subScale = 1.75 - 1.25 * MachNumber;            //This determines the weight of subsonic flow; supersonic uses 1-this
                double Cn = liftslope;
                Cl = Cn * Math.Sin(2 * AoA) * 0.5;

                if (MachNumber <= 1)
                {
                    Cl += ClIncrementFromRear;
                    if (Math.Abs(Cl) > Math.Abs(ACweight))
                        ACshift *= FARMathUtil.Clamp(Math.Abs(ACweight / Cl), 0, 1);
                }
                
                //Cd = Cl * Cl / piARe;     //Drag due to 3D effects on wing and base constant
                Cl *= subScale;
                //Cd *= subScale;



                double M = FARMathUtil.Clamp(MachNumber, 1.2, double.PositiveInfinity);

                if (double.IsNaN(beta) || beta < 0.66332495807107996982298654733414)
                    beta = 0.66332495807107996982298654733414;

                TanSweep = Math.Tan(FARMathUtil.Clamp(Math.Acos(SweepAngle), 0, Math.PI * 0.5));
                beta_TanSweep = beta / TanSweep;
                if (double.IsNaN(beta_TanSweep))
                    beta_TanSweep = 0;

                //double tmpCl, tmpCd;
                //DATCOMSupersonicLiftAndDrag(M, SweepAngle, sonicLe, AoA, out tmpCl, out tmpCd);

//                Cl += tmpCl * subScale;
//                Cd += tmpCd * subScale;
                
                double axialForce = 0;
                double coefMult = 1 / (FARAeroUtil.currentBodyAtm.y * M * M);

                double supersonicLENormalForceFactor = CalculateSupersonicLEFactor(beta, TanSweep, beta_TanSweep);

                subScale = 1 - subScale; //Adjust for supersonic code
                double normalForce;
                if (FARDebugValues.useSplinesForSupersonicMath)
                    normalForce = GetSupersonicPressureDifference(M, AoA, out axialForce);
                else
                    normalForce = GetSupersonicPressureDifferenceNoSpline(M, AoA, out axialForce);
                double CosAoA = Math.Cos(AoA);
                //double SinAoA = Math.Sin(AoA);
                //Cl += coefMult * (normalForce * CosAoA * Math.Sign(AoA) * sonicLEFactor - axialForce * SinAoA) * (subScale);
                //Cd += coefMult * (Math.Abs(normalForce * SinAoA) * sonicLEFactor + axialForce * CosAoA) * (subScale);
                Cl += coefMult * normalForce * CosAoA * Math.Sign(AoA) * supersonicLENormalForceFactor * subScale;
                Cd = Cl * Cl / piARe;

                Cd += Cd0;
            }

            //AC shift due to flaps
            Vector3d ACShiftVec;
            if (!double.IsNaN(ACshift) && MachNumber <= 1)
                ACShiftVec = ACshift * ParallelInPlane;
            else
                ACShiftVec = Vector3d.zero;

            //Stalling effects
            stall = FARMathUtil.Clamp(stall, minStall, 1);

            //AC shift due to stall
            if (stall > 0)
                ACShiftVec -= 0.75 / criticalCl * MAC * Math.Abs(Cl) * stall * ParallelInPlane;

            Cl -= Cl * stall * 0.769;
            Cd += Cd * stall * 3;


            AerodynamicCenter = AerodynamicCenter + ACShiftVec;

            Cl *= ClCdInterference;

            ClIncrementFromRear = 0;
        }


        #region Supersonic Calculations


        private double CalculateSupersonicLEFactor(double beta, double TanSweep, double beta_TanSweep)
        {
            double SupersonicLEFactor = 1;
            double ARTanSweep = effective_AR * TanSweep;

            if (beta_TanSweep < 1)   //"subsonic" leading edge, scales with Tan Sweep
            {
                if (beta_TanSweep < 0.5)
                {
                    SupersonicLEFactor = 1.57 * effective_AR;
                    SupersonicLEFactor /= ARTanSweep + 0.5;
                }
                else
                {
                    SupersonicLEFactor = (1.57 - 0.28 * (beta_TanSweep - 0.5)) * effective_AR;
                    SupersonicLEFactor /= ARTanSweep + 0.5 - (beta_TanSweep - 0.5) * 0.25;
                }
                SupersonicLEFactor *= beta;
            }
            else //"supersonic" leading edge, scales with beta
            {
                beta_TanSweep = 1 / beta_TanSweep;

                SupersonicLEFactor = 1.43 * ARTanSweep;
                SupersonicLEFactor /= ARTanSweep + 0.375;
                SupersonicLEFactor--;
                SupersonicLEFactor *= beta_TanSweep;
                SupersonicLEFactor++;
            }

            return SupersonicLEFactor;
        }

        /*private void DATCOMSupersonicLiftAndDrag(double M, double cosSweep, double sonicLE, double AoA, out double Cl, out double Cd)
        {
            double perpM = M * cosSweep;
            double B = Math.Sqrt(M * M - 1);
            double tanSweep = Math.Sqrt(Math.Abs(1 - cosSweep * cosSweep)) / cosSweep;

            bool subsonicLE = false;

            double Cn = 0;

            if ((object)PartInFrontOf == null)
                Cn = FARAeroUtil.SupersonicWingCna(transformed_AR, tanSweep, B, TaperRatio, out subsonicLE);
            else
                Cn = FARAeroUtil.SupersonicWingCna(transformed_AR, tanSweep, B, WingInFrontOf.TaperRatio, out subsonicLE);

            //And now for the nonlinear lift
            double Cnaa = 0;
            double slopeFactor = tanSweep * 0.52083333333333333333333333333333;

            double E;
            if (slopeFactor <= 1)
                E = Cn;
            else
            {
                E = Cn * (slopeFactor + 2.5 * (slopeFactor - 1));
            }

            if (subsonicLE)
            {
                Cnaa = FARAeroUtil.SubsonicLECnaa(E, B, tanSweep, AoA);



            }

            Cl = Cn * Math.Sin(2 * AoA) * 0.5;

            double slendernessParam = effective_b_2 * tanSweep + effective_MAC;
            double p = S / (effective_b_2 * slendernessParam);
            slendernessParam = effective_b_2 / slendernessParam;
            slendernessParam *= B;

            double dragParam;
            if (slendernessParam <= 0.4)
                dragParam = 0.55;
            else
                dragParam = (slendernessParam - 0.4) + 0.55;

            Cd = dragParam * (1 + p) / p;
            Cd /= Math.PI * transformed_AR;

            Cd *= Cl * Cl;
        }*/

        //This models the wing using a symmetric diamond airfoil

        private double GetSupersonicPressureDifferenceNoSpline(double M, double AoA, out double axialForce)
        {
            double pRatio;

            double maxSinBeta = FARAeroUtil.CalculateSinMaxShockAngle(M, FARAeroUtil.currentBodyAtm.y);//GetBetaMax(M) * FARMathUtil.deg2rad;
            double minSinBeta = 1 / M;


            double halfAngle = 0.05;            //Corresponds to ~2.8 degrees or approximately what you would get from a ~4.8% thick diamond airfoil

            double AbsAoA = Math.Abs(AoA);

            double angle1 = halfAngle - AbsAoA;                  //Region 1 is the upper surface ahead of the max thickness
            double M1;
            double p1;       //pressure ratio wrt to freestream pressure
            if (angle1 >= 0)
                p1 = ShockWaveCalculationNoSpline(angle1, M, out M1, maxSinBeta, minSinBeta);
            else
                p1 = PMExpansionCalculationNoSpline(Math.Abs(angle1), M, out M1, maxSinBeta, minSinBeta);

            //Region 2 is the upper surface behind the max thickness
            double p2 = PMExpansionCalculationNoSpline(2 * halfAngle, M1, maxSinBeta, minSinBeta) * p1;

            double angle3 = halfAngle + AbsAoA;                  //Region 3 is the lower surface ahead of the max thickness
            double M3;
            double p3;       //pressure ratio wrt to freestream pressure
            p3 = ShockWaveCalculationNoSpline(angle3, M, out M3, maxSinBeta, minSinBeta);

            //Region 4 is the lower surface behind the max thickness
            double p4 = PMExpansionCalculationNoSpline(2 * halfAngle, M3, maxSinBeta, minSinBeta) * p3;

            //float cosHalfAngle = Mathf.Cos(halfAngle);
            //float sinHalfAngle = halfAngle;

            pRatio = (p3 + p4) - (p1 + p2);

            //axialForce = (p1 + p3) - (p2 + p4);
            //axialForce *= 0.048;               //Thickness of the airfoil
            axialForce = 0;

            return pRatio;
        }


        private double ShockWaveCalculationNoSpline(double angle, double inM, out double outM, double maxSinBeta, double minSinBeta)
        {
            //float sinBeta = (maxBeta - minBeta) * angle / maxTheta + minBeta;
            double sinBeta = FARAeroUtil.CalculateSinWeakObliqueShockAngle(inM, FARAeroUtil.currentBodyAtm.y, angle);
            if (double.IsNaN(sinBeta))
                sinBeta = maxSinBeta;

            FARMathUtil.Clamp(sinBeta, minSinBeta, maxSinBeta);

            double normalInM = sinBeta * inM;
            normalInM = FARMathUtil.Clamp(normalInM, 1f, Mathf.Infinity);

            double tanM = inM * Math.Sqrt(FARMathUtil.Clamp(1 - sinBeta * sinBeta, 0, 1));

            double normalOutM = FARAeroUtil.MachBehindShockCalc(normalInM);

            outM = Math.Sqrt(normalOutM * normalOutM + tanM * tanM);

            double pRatio = FARAeroUtil.PressureBehindShockCalc(normalInM);

            return pRatio;
        }

        private double PMExpansionCalculationNoSpline(double angle, double inM, out double outM, double maxBeta, double minBeta)
        {
            inM = FARMathUtil.Clamp(inM, 1, double.PositiveInfinity);
            double nu1 = FARAeroUtil.PrandtlMeyerMach.Evaluate((float)inM);
            double theta = angle * FARMathUtil.rad2deg;
            double nu2 = nu1 + theta;
            if (nu2 >= FARAeroUtil.maxPrandtlMeyerTurnAngle)
            {
                //minStall += (nu2 - FARAeroUtil.maxPrandtlMeyerTurnAngle) * 0.066666667f;
                //minStall = Mathf.Clamp01(minStall);
                nu2 = FARAeroUtil.maxPrandtlMeyerTurnAngle;
            }
            outM = FARAeroUtil.PrandtlMeyerAngle.Evaluate((float)nu2);

            double ratio;

            ratio = FARAeroUtil.StagnationPressureCalc(inM) / FARAeroUtil.StagnationPressureCalc(outM);
            return ratio;
        }

        private double PMExpansionCalculationNoSpline(double angle, double inM, double maxBeta, double minBeta)
        {
            inM = FARMathUtil.Clamp(inM, 1, double.PositiveInfinity);
            double nu1 = FARAeroUtil.PrandtlMeyerMach.Evaluate((float)inM);
            double theta = angle * FARMathUtil.rad2deg;
            double nu2 = nu1 + theta;
            if (nu2 >= FARAeroUtil.maxPrandtlMeyerTurnAngle)
            {
                //minStall += (nu2 - FARAeroUtil.maxPrandtlMeyerTurnAngle) * 0.066666667f;
                //minStall = Mathf.Clamp01(minStall);
                nu2 = FARAeroUtil.maxPrandtlMeyerTurnAngle;
            }
            float outM = FARAeroUtil.PrandtlMeyerAngle.Evaluate((float)nu2);

            double ratio;

            ratio = FARAeroUtil.StagnationPressureCalc(inM) / FARAeroUtil.StagnationPressureCalc(outM);
            return ratio;
        }

        
        private double GetSupersonicPressureDifference(double M, double AoA, out double axialForce)
        {
            double pRatio;

            double maxSinBeta = FARAeroUtil.CalculateSinMaxShockAngle(M, FARAeroUtil.currentBodyAtm.y);//GetBetaMax(M) * FARMathUtil.deg2rad;
            double minSinBeta = 1 / M;


            double halfAngle = 0.05;            //Corresponds to ~2.8 degrees or approximately what you would get from a ~4.8% thick diamond airfoil

            double AbsAoA = Math.Abs(AoA);

            double angle1 = halfAngle - AbsAoA;                  //Region 1 is the upper surface ahead of the max thickness
            double M1;
            double p1;       //pressure ratio wrt to freestream pressure
            if (angle1 >= 0)
                p1 = ShockWaveCalculation(angle1, M, out M1, maxSinBeta, minSinBeta);
            else
                p1 = PMExpansionCalculation(Math.Abs(angle1), M, out M1, maxSinBeta, minSinBeta);

            //Region 2 is the upper surface behind the max thickness
            double p2 = PMExpansionCalculation(2 * halfAngle, M1, maxSinBeta, minSinBeta) * p1;

            double angle3 = halfAngle + AbsAoA;                  //Region 3 is the lower surface ahead of the max thickness
            double M3;
            double p3;       //pressure ratio wrt to freestream pressure
            p3 = ShockWaveCalculation(angle3, M, out M3, maxSinBeta, minSinBeta);

            //Region 4 is the lower surface behind the max thickness
            double p4 = PMExpansionCalculation(2 * halfAngle, M3, maxSinBeta, minSinBeta) * p3;

            //float cosHalfAngle = Mathf.Cos(halfAngle);
            //float sinHalfAngle = halfAngle;

            pRatio = (p3 + p4) - (p1 + p2);

            //axialForce = (p1 + p3) - (p2 + p4);
            //axialForce *= 0.048;               //Thickness of the airfoil
            axialForce = 0;

            return pRatio;
        }


        private double ShockWaveCalculation(double angle, double inM, out double outM, double maxSinBeta, double minSinBeta)
        {
            //float sinBeta = (maxBeta - minBeta) * angle / maxTheta + minBeta;
            double sinBeta = FARAeroUtil.CalculateSinWeakObliqueShockAngle(inM, FARAeroUtil.currentBodyAtm.y, angle);
            if (double.IsNaN(sinBeta))
                sinBeta = maxSinBeta;

            FARMathUtil.Clamp(sinBeta, minSinBeta, maxSinBeta);

            double normalInM = sinBeta * inM;
            normalInM = FARMathUtil.Clamp(normalInM, 1f, Mathf.Infinity);

            double tanM = inM * Math.Sqrt(FARMathUtil.Clamp(1 - sinBeta * sinBeta, 0, 1));

            double normalOutM = FARAeroUtil.MachBehindShock.Evaluate((float)normalInM);

            outM = Math.Sqrt(normalOutM * normalOutM + tanM * tanM);

            double pRatio = FARAeroUtil.PressureBehindShock.Evaluate((float)normalInM);

            return pRatio;
        }

        private double PMExpansionCalculation(double angle, double inM, out double outM, double maxBeta, double minBeta)
        {
            inM = FARMathUtil.Clamp(inM, 1, double.PositiveInfinity);
            double nu1 = FARAeroUtil.PrandtlMeyerMach.Evaluate((float)inM);
            double theta = angle * FARMathUtil.rad2deg;
            double nu2 = nu1 + theta;
            if (nu2 >= FARAeroUtil.maxPrandtlMeyerTurnAngle)
            {
                //minStall += (nu2 - FARAeroUtil.maxPrandtlMeyerTurnAngle) * 0.066666667f;
                //minStall = Mathf.Clamp01(minStall);
                nu2 = FARAeroUtil.maxPrandtlMeyerTurnAngle;
            }
            outM = FARAeroUtil.PrandtlMeyerAngle.Evaluate((float)nu2);

            double ratio;

            ratio = FARAeroUtil.StagnationPressure.Evaluate((float)inM) / FARAeroUtil.StagnationPressure.Evaluate((float)outM);
            return ratio;
        }

        private double PMExpansionCalculation(double angle, double inM, double maxBeta, double minBeta)
        {
            inM = FARMathUtil.Clamp(inM, 1, double.PositiveInfinity);
            double nu1 = FARAeroUtil.PrandtlMeyerMach.Evaluate((float)inM);
            double theta = angle * FARMathUtil.rad2deg;
            double nu2 = nu1 + theta;
            if (nu2 >= FARAeroUtil.maxPrandtlMeyerTurnAngle)
            {
                //minStall += (nu2 - FARAeroUtil.maxPrandtlMeyerTurnAngle) * 0.066666667f;
                //minStall = Mathf.Clamp01(minStall);
                nu2 = FARAeroUtil.maxPrandtlMeyerTurnAngle;
            }
            float outM = FARAeroUtil.PrandtlMeyerAngle.Evaluate((float)nu2);

            double ratio;

            ratio = FARAeroUtil.StagnationPressure.Evaluate((float)inM) / FARAeroUtil.StagnationPressure.Evaluate(outM);
            return ratio;
        }

        #endregion

        protected double GetAoAmax()
        {


            double StallAngle;
            /*
            if (MachNumber < 1.1)
            {
                float tmp = 2 / effective_AR;
                StallAngle = 16 * (Mathf.Sqrt(1 + Mathf.Pow(tmp, 2)) + 2 / (tmp));

                if (Mathf.Abs(SweepAngle) >= 0.4)
                    StallAngle *= 1 / Mathf.Abs(SweepAngle);
                else
                    StallAngle *= 2.5f;


                StallAngle = Mathf.Clamp(StallAngle, 15, 45) * FARMathUtil.deg2rad;
            }
            else
                StallAngle = Math.PI / 3;*/

            StallAngle = criticalCl / liftslope;          //Simpler version of above commented out section; just limit the lift coefficient to ~1.6

            return StallAngle;

        }


        private double GetLiftSlope(double MachNumber)
        {
            double sweepHalfChord = MidChordSweep * FARMathUtil.deg2rad;

            double CosPartAngle = FARMathUtil.Clamp(Vector3.Dot(sweepPerpLocal, ParallelInPlaneLocal), -1, 1);
            double tmp = FARMathUtil.Clamp(Vector3.Dot(sweepPerp2Local, ParallelInPlaneLocal), -1, 1);

            if (Math.Abs(CosPartAngle) > Math.Abs(tmp))                //Based on perp vector find which line is the right one
                sweepHalfChord = Math.Acos(CosPartAngle);
            else
                sweepHalfChord = Math.Acos(tmp);

            if (sweepHalfChord > Math.PI * 0.5)
                sweepHalfChord -= Math.PI;

            CosPartAngle = FARMathUtil.Clamp(ParallelInPlaneLocal.y, -1, 1);

            CosPartAngle *= CosPartAngle;
            double SinPartAngle2 = FARMathUtil.Clamp(1 - CosPartAngle, 0, 1);               //Get the squared values for the angles

            effective_b_2 = Math.Max(b_2 * CosPartAngle, MAC * SinPartAngle2);
            effective_MAC = MAC * CosPartAngle + b_2 * SinPartAngle2;
            transformed_AR = 2 * effective_b_2 / effective_MAC;

            SetSweepAngle(sweepHalfChord);

            effective_AR = EffectOfExposure();

            /*if (MachNumber < 1)
                tmp = Mathf.Clamp(MachNumber, 0, 0.9f);
            else
                tmp = 1 / Mathf.Clamp(MachNumber, 1.09f, Mathf.Infinity);*/

            if (MachNumber < 0.9)
                tmp = 1 - MachNumber * MachNumber;
            else
                tmp = 0.09;

            double sweepTmp = Math.Tan(sweepHalfChord);
            sweepTmp *= sweepTmp;

            tmp += sweepTmp;
            tmp = tmp * effective_AR * effective_AR;
            tmp += 4;
            tmp = Math.Sqrt(tmp);
            tmp += 2;
            tmp = 1 / tmp;
            tmp *= 2 * Math.PI;

            double liftslope = tmp * effective_AR;

            /*float liftslope = Mathf.Pow(effective_AR / FARAeroUtil.FastCos(sweepHalfChord), 2) + 4 - Mathf.Pow(effective_AR * tmp, 2);
            liftslope = 2 + Mathf.Sqrt(Mathf.Clamp(liftslope, 0, Mathf.Infinity));
            liftslope = twopi / liftslope * effective_AR;*/

            return liftslope;

        }


        private void SetSweepAngle(double sweepHalfChord)
        {
            SweepAngle = sweepHalfChord;
            SweepAngle = Math.Tan(SweepAngle);
            double tmp = (1 - TaperRatio) / (1 + TaperRatio);
            tmp *= 2 / transformed_AR;
            SweepAngle += tmp;
            SweepAngle = Math.Cos(Math.Atan(SweepAngle));
        }


        #region Compressibility

        /// <summary>
        /// This modifies the Cd to account for compressibility effects
        /// </summary>
        private double CdCompressibilityZeroLiftIncrement(double M, double SweepAngle, double TanSweep, double beta_TanSweep, double beta)
        {

            if ((object)WingInFrontOf != null)
            {
                zeroLiftCdIncrement = WingInFrontOf.zeroLiftCdIncrement;
                return zeroLiftCdIncrement;
            }

            //Based on the method of DATCOM Section 4.1.5.1-C
            if(M > 1.4)
            {
                //Subsonic leading edge
                if(beta_TanSweep < 1)
                {                           //This constant is due to airfoil shape and thickness
                    zeroLiftCdIncrement = 0.009216 / TanSweep;
                }
                //Supersonic leading edge
                else
                {
                    zeroLiftCdIncrement = 0.009216 / beta;
                }
                return zeroLiftCdIncrement;
            }


            //Based on the method of DATCOM Section 4.1.5.1-B
            double tmp = 1 / Math.Sqrt(SweepAngle);

            double dd_MachNumber = 0.8 * tmp;               //Find Drag Divergence Mach Number

            if (M < dd_MachNumber)      //If below this number, 
            {
                zeroLiftCdIncrement = 0;
                return 0;
            }

            double peak_MachNumber = 1.1 * tmp;

            double peak_Increment = 0.025 * FARMathUtil.PowApprox(SweepAngle, 2.5);

            if (M > peak_MachNumber)
            {
                zeroLiftCdIncrement = peak_Increment;
            }
            else
            {
                tmp = dd_MachNumber - peak_MachNumber;
                tmp = tmp * tmp * tmp;
                tmp = 1 / tmp;

                double CdIncrement = 2 * M;
                CdIncrement -= 3 * (dd_MachNumber + peak_MachNumber);
                CdIncrement *= M;
                CdIncrement += 6 * dd_MachNumber * peak_MachNumber;
                CdIncrement *= M;
                CdIncrement += dd_MachNumber * dd_MachNumber * (dd_MachNumber - 3 * peak_MachNumber);
                CdIncrement *= tmp;
                CdIncrement *= peak_Increment;

                zeroLiftCdIncrement = CdIncrement;
            }
           
            double scalingMachNumber = Math.Min(peak_MachNumber, 1.2);

            if (M < scalingMachNumber)
                return zeroLiftCdIncrement;

            double scale = (M - 1.4) / (scalingMachNumber - 1.4);
            zeroLiftCdIncrement *= scale;
            scale = 1 - scale;

            //Subsonic leading edge
            if (beta_TanSweep < 1)
            {                           //This constant is due to airfoil shape and thickness
                zeroLiftCdIncrement += 0.009216 / TanSweep * scale;
            }
            //Supersonic leading edge
            else
            {
                zeroLiftCdIncrement += 0.009216 / beta * scale;
            }
            return zeroLiftCdIncrement;
        }
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

            float flt_MAC = (float)MAC;
            float flt_b_2 = (float)b_2;

            ray.direction = part.transform.up;
            forwardexposure = ExposureDirection(ray, hit, VesselPartList, flt_MAC, true);

            ray.direction = -part.transform.up;
            backwardexposure = ExposureDirection(ray, hit, VesselPartList, flt_MAC, true);

            ray.direction = part.transform.right;
            inwardexposure = ExposureDirection(ray, hit, VesselPartList, flt_b_2, false);

            ray.direction = -part.transform.right;
            outwardexposure = ExposureDirection(ray, hit, VesselPartList, flt_b_2, false);

            //This part handles effects of biplanes, triplanes, etc.
            ClCdInterference = 1;
            ray.direction = part.transform.forward;
            ClCdInterference *= WingInterference(ray, hit, VesselPartList, flt_b_2);
            ray.direction = -part.transform.forward;
            ClCdInterference *= WingInterference(ray, hit, VesselPartList, flt_b_2);
        }

        private double WingInterference(Ray ray, RaycastHit hit, List<Part> PartList, float dist)
        {
            double interferencevalue = 1;

            ray.origin = WingCentroid();

            bool gotSomething = false;

            hit.distance = 0;
            RaycastHit[] hits = Physics.RaycastAll(ray.origin, ray.direction, dist, FARAeroUtil.RaycastMask);
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit h = hits[i];
                if (h.collider != null)
                {
                    for (int j = 0; j < PartList.Count; j++)
                    {
                        Part p = PartList[j];
                        Collider[] colliders;
                        try
                        {
                            colliders = p.GetComponents<Collider>();
                        }
                        catch (Exception e)
                        {
                            //Fail silently because it's the only way to avoid issues with pWings
                            //Debug.LogException(e);
                            colliders = new Collider[1] { p.collider };
                        }
                        if (p.Modules.Contains("FARWingAerodynamicModel"))
                        {
                            for (int k = 0; k < colliders.Length; k++)
                                if (h.collider == colliders[k] && p != part)
                                {
                                    if (h.distance > 0)
                                    {
                                        double tmp = h.distance / dist;
                                        tmp = FARMathUtil.Clamp(tmp, 0, 1);
                                        interferencevalue = Math.Min(tmp, interferencevalue);
                                        gotSomething = true;
                                    }
                                    break;
                                }
                        }
                        if (gotSomething)
                            break;
                    }
                }
            }
            return interferencevalue;
        }

        private double ExposureDirection(Ray ray, RaycastHit hit, List<Part> PartList, float dist, bool span)
        {
            double exposure = 1;
            if (nonSideAttach == 0)
                for (int i = 0; i < 5; i++)
                {
                    //Vector3 centroid = WingCentroid();
                    if (span)
                    {
                        ray.origin = part.transform.position - (float)(b_2 * (i * 0.2 + 0.1)) * part.transform.right.normalized * srfAttachNegative;
                    }
                    else
                    {
                        ray.origin = part.transform.position + (float)(MAC * i * 0.25 - (MAC * 0.5)) * part.transform.up.normalized * 0.8f;
                        //                        ray.origin = part.transform.position + (MAC * i / 4 - (MAC / 2)) * part.transform.up.normalized * 0.8f;
                        ray.origin -= (float)(b_2 * 0.5) * part.transform.right.normalized * srfAttachNegative;
                    }

                    if (dist <= 0)
                        dist = 1;

                    hit.distance = 0;
                    RaycastHit[] hits = Physics.RaycastAll(ray.origin, ray.direction, dist, FARAeroUtil.RaycastMask);
                    bool gotSomething = false;
                    for (int j = 0; j < hits.Length; j++)
                    {
                        RaycastHit h = hits[j];
                        if (h.collider != null)
                        {
                            for (int k = 0; k < PartList.Count; k++)
                            {
                                Part p = PartList[k];
                                Collider[] colliders;
                                try
                                {
                                    colliders = p.GetComponents<Collider>();
                                }
                                catch (Exception e)
                                {
                                    //Fail silently because it's the only way to avoid issues with pWings
                                    //Debug.LogException(e);
                                    colliders = new Collider[1] { p.collider };
                                }
                                for (int l = 0; l < colliders.Length; l++)
                                    if (h.collider == colliders[l] && p != part)
                                    {
                                        if (h.distance > 0)
                                        {
                                            exposure -= 0.2;
                                            gotSomething = true;
                                        }
                                        break;
                                    }
                                if (gotSomething)
                                    break;
                            }
                        }
                        if (gotSomething)
                            break;
                    }
                }
            else
            {
                ray.origin = part.transform.position - (float)(MAC * 0.7) * part.transform.up.normalized;


                if (dist <= 0)
                    dist = 1;

                hit.distance = 0;
                RaycastHit[] hits = Physics.RaycastAll(ray.origin, ray.direction, dist, FARAeroUtil.RaycastMask);
                bool gotSomething = false;
                for (int i = 0; i < hits.Length; i++)
                {
                    RaycastHit h = hits[i];
                    if (h.collider != null)
                    {
                        for (int j = 0; j < PartList.Count; j++)
                        {
                            Part p = PartList[j];
                            Collider[] colliders;
                            try
                            {
                                colliders = p.GetComponents<Collider>();
                            }
                            catch (Exception e)
                            {
                                //Fail silently because it's the only way to avoid issues with pWings
                                //Debug.LogException(e);
                                colliders = new Collider[1] { p.collider };
                            }
                            for (int k = 0; k < colliders.Length; k++)
                                if (h.collider == colliders[k] && p != part)
                                {
                                    if (h.distance > 0)
                                    {
                                        exposure -= 1;
                                        gotSomething = true;
                                    }
                                    break;
                                }
                            if (gotSomething)
                                break;
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

        private double EffectOfExposure()
        {
            double forwardbackward = ParallelInPlaneLocal.y;
            double inwardoutward = ParallelInPlaneLocal.x * srfAttachNegative;
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

            WingtipExposure = 1 - WingtipExposure;
            WingrootExposure = 1 - WingrootExposure;


            double effective_AR_modifier = (WingrootExposure + WingtipExposure);



            double e_AR;

            if (effective_AR_modifier < 1)
                e_AR = transformed_AR * (effective_AR_modifier + 1);
            else
                e_AR = transformed_AR * 2 * (2 - effective_AR_modifier) + 30 * (effective_AR_modifier - 1);

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

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (node.HasValue("b_2"))
                double.TryParse(node.GetValue("b_2"), out b_2);
            if (node.HasValue("MAC"))
                double.TryParse(node.GetValue("MAC"), out MAC);
            if (node.HasValue("e"))
                double.TryParse(node.GetValue("e"), out e);
            if (node.HasValue("TaperRatio"))
                double.TryParse(node.GetValue("TaperRatio"), out TaperRatio);
            if (node.HasValue("nonSideAttach"))
                double.TryParse(node.GetValue("nonSideAttach"), out nonSideAttach);
            if (node.HasValue("MidChordSweep"))
                double.TryParse(node.GetValue("MidChordSweep"), out MidChordSweep);
        }
    }


}