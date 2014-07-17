/*
Neophyte's Elementary Aerodynamics Replacement v1.0
Copyright 2014, Michael Ferrara, aka Ferram4

    This file is part of Neophyte's Elementary Aerodynamics Replacement.

    Neophyte's Elementary Aerodynamics Replacement is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Kerbal Joint Reinforcement is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Neophyte's Elementary Aerodynamics Replacement.  If not, see <http://www.gnu.org/licenses/>.

    Serious thanks:		a.g., for tons of bugfixes and code-refactorings
            			Taverius, for correcting a ton of incorrect values
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager 1.5 updates
            			ialdabaoth (who is awesome), who originally created Module Manager
            			Duxwing, for copy editing the readme
 *
 * Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/55219
 *
 */

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This calculates the lift and drag on a wing in the atmosphere
/// 
/// It uses Prandtl lifting line theory to calculate the basic lift and drag coefficients and includes compressibility corrections for subsonic and supersonic flows; transsonic regime has placeholder
/// </summary>

namespace NEAR
{

    public class FARWingAerodynamicModel : FARBaseAerodynamics
    {
        public double AoAmax = 15;

        [KSPField(isPersistant = false)]
        public double MAC;

        [KSPField(isPersistant = false)]
        public double nonSideAttach;           //This is for ailerons and the small ctrl surf


        [KSPField(isPersistant = false)]
        public double TaperRatio;

        [KSPField(isPersistant = false, guiActive = true, guiName = "Stalled %", guiFormat = "N2")]
        protected double stall = 0;

        private double minStall = 0;

        private const double twopi = Mathf.PI * 2;   //lift slope
        private double piARe = 1;    //induced drag factor

        [KSPField(isPersistant = false)]
        public double b_2;        //span


        [KSPField(isPersistant = false)]
        public double MidChordSweep;

        [KSPField(isPersistant = false, guiActive = false, guiName = "Current lift", guiUnits = "kN", guiFormat = "F3")]
        protected float currentLift = 0.0f;

        [KSPField(isPersistant = false, guiActive = false, guiName = "Current drag", guiUnits = "kN", guiFormat = "F3")]
        protected float currentDrag = 0.0f;

        private double liftslope = 0;
        protected double ClCdInterference = 1;
        protected double WingtipExposure = 0;
        protected double WingrootExposure = 0;

        protected double zeroLiftCdIncrement = 0;

        protected double criticalCl = 1.6;


        public Vector3d AerodynamicCenter = Vector3.zero;
        private Vector3d CurWingCentroid = Vector3.zero;
        private Vector3d ParallelInPlane = Vector3.zero;
        private Vector3d perp = Vector3.zero;
        private Vector3d liftDirection = Vector3.zero;

        // in local coordinates
        private Vector3d localWingCentroid = Vector3.zero;
        private Vector3d ParallelInPlaneLocal = Vector3.zero;

        private Int16 srfAttachNegative = 1;

        private Vector3d LastInFrontRaycast = Vector3.zero;

        protected double ClIncrementFromRear = 0f;

        private double rho = 1;

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
            Vector3d WC = Vector3d.zero;
            if (nonSideAttach <= 0)
            {
                WC = -b_2 * 0.5 * (Vector3d.right * srfAttachNegative + Vector3d.up * Math.Tan(MidChordSweep * FARMathUtil.deg2rad));
            }
            else
                WC = (-MAC * 0.7) * Vector3d.up;

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

        private Vector3d CalculateAerodynamicCenter(double AoA, Vector3d WC)
        {
            Vector3d AC_offset = Vector3d.zero;

            WC += AC_offset;

            return WC;      //WC updated to AC
        }

        public void ComputeClCdEditor(Vector3d velocityVector)
        {
            velocityEditor = velocityVector;

            rho = 1;

            double AoA = CalculateAoA(velocityVector);
            CalculateForces(velocityVector, AoA);
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

        protected override Vector3d PrecomputeCenterOfLift(Vector3d velocity, FARCenterQuery center)
        {
            double AoA = CalculateAoA(velocity);

            Vector3d force = CalculateForces(velocity, AoA);
            center.AddForce(AerodynamicCenter, force);

            return force;
        }

        #endregion

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

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
        }

        public void MathAndFunctionInitialization()
        {
            double lengthScale = Math.Sqrt(FARAeroUtil.areaFactor);

            b_2 *= lengthScale;
            MAC *= lengthScale;

            S = b_2 * MAC;

            if (part.srfAttachNode.originalOrientation.x < 0)
                srfAttachNegative = -1;

            PrecomputeCentroid();
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
                    double v_scalar = velocity.magnitude;

                    rho = vessel.atmDensity;
                    if (rho > 0 && v_scalar > 0.1)
                    {
                        double AoA = CalculateAoA(velocity);
                        Vector3d force = DoCalculateForces(velocity, AoA);

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

        public Vector3d CalculateForces(Vector3d velocity, double AoA)
        {
            CurWingCentroid = WingCentroid();

            return DoCalculateForces(velocity, AoA);
        }

        private Vector3d DoCalculateForces(Vector3d velocity, double AoA)
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
            AerodynamicCenter = CalculateAerodynamicCenter(AoA, CurWingCentroid);

            //Throw AoA into lifting line theory and adjust for part exposure and compressibility effects
            CalculateCoefficients(AoA);



            //lift and drag vectors
            Vector3d L = liftDirection * (q * Cl * S);    //lift;
            Vector3d D = velocity_normalized * (-q * Cd * S);                         //drag is parallel to velocity vector
            currentLift = (float)(L.magnitude * 0.001);
            currentDrag = (float)(D.magnitude * 0.001);
            Vector3d force = (L + D) * 0.001;
            if (double.IsNaN(force.sqrMagnitude) || double.IsNaN(AerodynamicCenter.sqrMagnitude))// || float.IsNaN(moment.magnitude))
            {
                Debug.LogWarning("NEAR Error: Aerodynamic force = " + force.magnitude + " AC Loc = " + AerodynamicCenter.magnitude + " AoA = " + AoA + "\n\rMAC = " + MAC + " B_2 = " + b_2 + "\n\rMidChordSweep = " + MidChordSweep + "\n\r at " + part.name);
                force = AerodynamicCenter = Vector3d.zero;
            }

            return force;

        }

        protected virtual double CalculateAoA(Vector3d velocity)
        {
            double PerpVelocity = Vector3d.Dot(part_transform.forward, velocity.normalized);
            return Math.Asin(FARMathUtil.Clamp(PerpVelocity, -1, 1));
        }


        private void DetermineStall(double AoA)
        {
            double lastStall = stall;
            double tmp = 0;
            stall = 0;

            AoAmax = GetAoAmax();

            double absAoA = Math.Abs(AoA);

            if (absAoA > AoAmax)
            {
                stall = FARMathUtil.Clamp((absAoA - AoAmax) * 10, 0, 1);
                stall += tmp;
                stall = Math.Max(stall, lastStall);
            }
            else
            {
                stall = 1 - FARMathUtil.Clamp((AoAmax - absAoA) * 10, 0, 1);
                stall += tmp;
                stall = Math.Min(stall, lastStall);
            }

            if (HighLogic.LoadedSceneIsFlight)
                stall = FARMathUtil.Clamp(stall, lastStall - 2 * TimeWarp.fixedDeltaTime, lastStall + 2 * TimeWarp.fixedDeltaTime);     //Limits stall to increasing at a rate of 2/s

            stall = FARMathUtil.Clamp(stall, 0, 1);
        }


        /// <summary>
        /// This calculates the lift and drag coefficients
        /// </summary>
        private void CalculateCoefficients(double AoA)
        {

            minStall = 0;

            liftslope = 4; //Quite a bit below ideal 2pi, but better for flying around like mad

            DetermineStall(AoA);

            piARe = 4 * Mathf.PI;   //Uses induced drag of a AR 4 wing with an elliptical distribution

            double Cn = liftslope;
            Cl = Cn * Math.Sin(2 * AoA) * 0.5;
            Cd = (0.006 + Cl * Cl / piARe);     //Drag due to 3D effects on wing and base constant

            //AC shift due to flaps
            Vector3d ACShiftVec = Vector3d.zero;

            //Stalling effects
            stall = FARMathUtil.Clamp(stall, minStall, 1);

            //AC shift due to stall
            if (stall > 0)
                ACShiftVec -= 0.75 / criticalCl * MAC * Math.Abs(Cl) * stall * ParallelInPlane;

            Cl -= Cl * stall * 0.5;
            Cd += Cd * stall * 1.5;


            AerodynamicCenter = AerodynamicCenter + ACShiftVec;
        }


        protected double GetAoAmax()
        {


            double StallAngle;
            StallAngle = criticalCl / liftslope;          //Simpler version of above commented out section; just limit the lift coefficient to ~1.6

            return StallAngle;
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (node.HasValue("b_2"))
                double.TryParse(node.GetValue("b_2"), out b_2);
            if (node.HasValue("MAC"))
                double.TryParse(node.GetValue("MAC"), out MAC);
            if (node.HasValue("TaperRatio"))
                double.TryParse(node.GetValue("TaperRatio"), out TaperRatio);
            if (node.HasValue("nonSideAttach"))
                double.TryParse(node.GetValue("nonSideAttach"), out nonSideAttach);
            if (node.HasValue("MidChordSweep"))
                double.TryParse(node.GetValue("MidChordSweep"), out MidChordSweep);
        }
    }


}