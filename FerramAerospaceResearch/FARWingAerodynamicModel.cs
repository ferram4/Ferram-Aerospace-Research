/*
Ferram Aerospace Research v0.14.3
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

        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true)]
        public float curWingMass = 1;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Mass/Strength Multiplier", guiFormat = "0.##"), UI_FloatRange(minValue = 0.1f, maxValue = 2.0f, stepIncrement = 0.01f)]
        public float massMultiplier = 1.0f;

        public float oldMassMultiplier = -1f;

        [KSPField(isPersistant = false, guiActive = true)]
        protected double inFrontStall = 0;

        [KSPField(isPersistant = false)]
        public double MAC;

        [KSPField(isPersistant = false, guiActive = true)]
        public double e;

        [KSPField(isPersistant = false)]
        public int nonSideAttach;           //This is for ailerons and the small ctrl surf

        [KSPField(isPersistant = false)]
        public double TaperRatio;

        [KSPField(isPersistant = false, guiActive = true, guiName = "Stalled %")]
        protected double stall = 0;

        private double minStall = 0;

        private const double twopi = Math.PI * 2;   //lift slope
        private double piARe = 1;    //induced drag factor

        [KSPField(isPersistant = false)]
        public double b_2;        //span

        [KSPField(isPersistant = false)]
        public double MidChordSweep;
        private double MidChordSweepSideways = 0;

        private double cosSweepAngle = 0;

        private double effective_b_2 = 1;
        private double effective_MAC = 1;

        protected double effective_AR = 4;
        protected double transformed_AR = 4;

        [KSPField(isPersistant = false, guiActive = false, guiName = "Current lift", guiUnits = "kN", guiFormat = "F3")]
        protected float currentLift = 0.0f;

        [KSPField(isPersistant = false, guiActive = false, guiName = "Current drag", guiUnits = "kN", guiFormat = "F3")]
        protected float currentDrag = 0.0f;

        private double liftslope = 0;
        protected double zeroLiftCdIncrement = 0;

        protected double criticalCl = 1.6;

        private double refAreaChildren = 0;

        public Vector3d AerodynamicCenter = Vector3d.zero;
        private Vector3d CurWingCentroid = Vector3d.zero;
        private Vector3d ParallelInPlane = Vector3d.zero;
        private Vector3d perp = Vector3d.zero;
        private Vector3d liftDirection = Vector3d.zero;

        [KSPField(isPersistant = false)]
        public Vector3 rootMidChordOffsetFromOrig;

        // in local coordinates
        private Vector3d localWingCentroid = Vector3d.zero;
        private Vector3d sweepPerpLocal, sweepPerp2Local;

        private Vector3d ParallelInPlaneLocal = Vector3d.zero;

        FARWingInteraction wingInteraction;

        private short srfAttachNegative = 1;

        private FARWingAerodynamicModel parentWing = null;
        private bool updateMassNextFrame = false;

        protected double ClIncrementFromRear = 0;

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

        public Vector3d GetAerodynamicCenter()
        {
            return AerodynamicCenter;
        }

        public double GetMAC()
        {
            return effective_MAC;
        }
        public double Getb_2()
        {
            return effective_b_2;
        }

        public Vector3d GetLiftDirection()
        {
            return liftDirection;
        }

        public double GetLiftSlope()
        {
            return liftslope;
        }

        public double GetCosSweepAngle()
        {
            return cosSweepAngle;
        }

        public double GetCd0()
        {
            return zeroLiftCdIncrement;
        }

        #endregion

        #region Editor Functions

        public void ComputeClCdEditor(Vector3d velocityVector, double M)
        {
            velocityEditor = velocityVector;

            rho = 1;

            double AoA = CalculateAoA(velocityVector);
            CalculateForces(velocityVector, M, AoA);
        }

        protected override void ResetCenterOfLift()
        {
            rho = 1;
            stall = 0;
        }

        protected override Vector3d PrecomputeCenterOfLift(Vector3d velocity, double MachNumber, FARCenterQuery center)
        {
            try
            {
                double AoA = CalculateAoA(velocity);

                Vector3d force = CalculateForces(velocity, MachNumber, AoA);
                center.AddForce(AerodynamicCenter, force);

                return force;
            }
            catch       //FIX ME!!!
            {           //Yell at KSP devs so that I don't have to engage in bad code practice
                //Debug.Log("The expected exception from the symmetry counterpart part transform internals was caught and suppressed");
                return Vector3.zero;
            }
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

        #endregion

        #region Wing Centroid and Aerodynamic Center

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
                    double sweepFactor = cosSweepAngle * cosSweepAngle * tmp;
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

        #endregion

        #region Initialization

        public override void Start()
        {
            base.Start();
            StartInitialization();
            if(HighLogic.LoadedSceneIsEditor)
            {
                part.OnEditorAttach += OnWingAttach;
                part.OnEditorDetach += OnWingDetach;
            }

            OnVesselPartsChange += UpdateWingInteractions;
        }

        public void StartInitialization()
        {
            MathAndFunctionInitialization();

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
            Fields["currentLift"].guiActive = FARDebugValues.displayForces;
            Fields["currentDrag"].guiActive = FARDebugValues.displayForces;

            OnWingAttach();

            wingInteraction = new FARWingInteraction(this, this.part, rootMidChordOffsetFromOrig, srfAttachNegative);

            UpdateWingInteractions();
        }

        public void MathAndFunctionInitialization()
        {
            S = b_2 * MAC;

            S *= FARAeroUtil.areaFactor;

            if (part.srfAttachNode.originalOrientation.x < 0)
                srfAttachNegative = -1;

            transformed_AR = 2 * b_2 / MAC;

            MidChordSweepSideways = (1 - TaperRatio) / (1 + TaperRatio);

            MidChordSweepSideways = (Math.PI * 0.5 - Math.Atan(Math.Tan(MidChordSweep * FARMathUtil.deg2rad) + MidChordSweepSideways * 2 / transformed_AR)) * MidChordSweepSideways * 0.5;

            double sweepHalfChord = MidChordSweep * FARMathUtil.deg2rad;

            sweepPerpLocal = Vector3d.up * Math.Cos(sweepHalfChord) + Vector3d.right * Math.Sin(sweepHalfChord) * srfAttachNegative; //Vector perpendicular to midChord line
            sweepPerp2Local = Vector3d.up * Math.Sin(MidChordSweepSideways) - Vector3d.right * Math.Cos(MidChordSweepSideways) * srfAttachNegative; //Vector perpendicular to midChord line2

            PrecomputeCentroid();

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
                double maxForceMult = Math.Pow(massMultiplier, FARAeroUtil.massStressPower);
                YmaxForce *= maxForceMult;
                XZmaxForce *= maxForceMult;
            }
        }

        private void UpdateWingInteractions()
        {
            if(VesselPartList == null)
                VesselPartList = GetShipPartList();

            wingInteraction.UpdateWingInteraction(VesselPartList, nonSideAttach == 1);
        }

        #endregion

        #region Physics Frame

        public virtual void FixedUpdate()
        {
            currentLift = currentDrag = 0;

            // With unity objects, "foo" or "foo != null" calls a method to check if
            // it's destroyed. (object)foo != null just checks if it is actually null.
            if (HighLogic.LoadedSceneIsFlight && FlightGlobals.ready && !isShielded && (object)part != null)
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

                    Vector3d velocity = rb.GetPointVelocity(CurWingCentroid) + Krakensbane.GetFrameVelocity()
                        + FARWind.GetWind(FlightGlobals.currentMainBody, part, rb.position);

                    double soundspeed, v_scalar = velocity.magnitude;

                    rho = FARAeroUtil.GetCurrentDensity(vessel, out soundspeed);
                    if (rho > 0 && v_scalar > 0.1)
                    {
                        double MachNumber = v_scalar / soundspeed;
                        double AoA = CalculateAoA(velocity);
                        Vector3d force = DoCalculateForces(velocity, MachNumber, AoA);

                        rb.AddForceAtPosition(force, AerodynamicCenter);            //and apply force
                    }
                    else
                    {
                        stall = 0;
                        wingInteraction.ResetWingInteractions();
                    }
                }
                else
                {
                    stall = 0;
                    wingInteraction.ResetWingInteractions();
                }

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
                Debug.LogWarning("FAR Error: Aerodynamic force = " + force.magnitude + " AC Loc = " + AerodynamicCenter.magnitude + " AoA = " + AoA + "\n\rMAC = " + effective_MAC + " B_2 = " + effective_b_2 + " sweepAngle = " + cosSweepAngle + "\n\rMidChordSweep = " + MidChordSweep + " MidChordSweepSideways = " + MidChordSweepSideways + "\n\r at " + part.name);
                force = AerodynamicCenter = Vector3d.zero;
            }

            Vector3d scaledForce = force;
            //This accounts for the effect of flap effects only being handled by the rearward surface
            scaledForce *= S / (S + wingInteraction.EffectiveUpstreamArea);

            if (Math.Abs(Vector3d.Dot(scaledForce, forward)) > YmaxForce || Vector3d.Exclude(forward, scaledForce).magnitude > XZmaxForce)
                if (part.parent && !vessel.packed)
                {
                    part.SendEvent("AerodynamicFailureStatus");
                    FlightLogger.eventLog.Add("[" + FARMathUtil.FormatTime(vessel.missionTime) + "] Joint between " + part.partInfo.title + " and " + part.parent.partInfo.title + " failed due to aerodynamic stresses.");
                    part.decouple(25);
                }

            return force;

        }

        #endregion

        #region Wing Mass For Structure

        private void Update()
        {
            if(updateMassNextFrame)
            {
                GetRefAreaChildren();
                UpdateMassToAccountForArea();
                updateMassNextFrame = false;
            }
            else if (HighLogic.LoadedSceneIsEditor && massMultiplier != oldMassMultiplier)
            {
                GetRefAreaChildren();
                UpdateMassToAccountForArea();
            }

        }

        private void OnWingAttach()
        {
            if(part.parent)
                parentWing = part.parent.GetComponent<FARWingAerodynamicModel>();

            GetRefAreaChildren();

            UpdateMassToAccountForArea();
        }

        private void OnWingDetach()
        {
            if ((object)parentWing != null)
                parentWing.updateMassNextFrame = true;
        }

        private void UpdateMassToAccountForArea()
        {
            float supportedArea = (float)(refAreaChildren + S);
            part.mass = supportedArea * (float)FARAeroUtil.massPerWingAreaSupported * massMultiplier;
            curWingMass = part.mass;
            oldMassMultiplier = massMultiplier;
        }

        private void GetRefAreaChildren()
        {
            refAreaChildren = 0;

            for(int i = 0; i < part.children.Count; i++)
            {
                Part p = part.children[i];
                FARWingAerodynamicModel childWing = p.GetComponent<FARWingAerodynamicModel>();
                if ((object)childWing == null)
                    continue;

                refAreaChildren += childWing.refAreaChildren + childWing.S;
            }

            if ((object)parentWing != null)
            {
                parentWing.GetRefAreaChildren();
                parentWing.UpdateMassToAccountForArea();
            }
        }

        #endregion

        public virtual double CalculateAoA(Vector3d velocity)
        {
            double PerpVelocity = Vector3d.Dot(part_transform.forward, velocity.normalized);
            return Math.Asin(FARMathUtil.Clamp(PerpVelocity, -1, 1));
        }

        protected override Color AeroVisualizationTintingCalculation()
        {
            if(FARControlSys.tintForStall)
            {
                return new Color((float)stall * 10, 0, 0, 1);
            }
            else
                return base.AeroVisualizationTintingCalculation();
        }

        #region Interactive Effects

        //Calculates camber and flap effects due to wing interactions
        private void CalculateWingCamberInteractions(double MachNumber, double AoA, out double ACshift, out double ACweight)
        {
            ACshift = 0;
            ACweight = 0;
            ClIncrementFromRear = 0;

            AoAmax = 0;
            double effectiveUpstreamInfluence = 0;


            wingInteraction.UpdateOrientationForInteraction(ParallelInPlaneLocal);
            wingInteraction.CalculateEffectsOfUpstreamWing(AoA, MachNumber, ParallelInPlaneLocal, ref ACweight, ref ACshift, ref ClIncrementFromRear);
            effectiveUpstreamInfluence = wingInteraction.EffectiveUpstreamInfluence;

            if (effectiveUpstreamInfluence > 0)
            {
                effectiveUpstreamInfluence = wingInteraction.EffectiveUpstreamInfluence;

                AoAmax = wingInteraction.EffectiveUpstreamAoAMax;
                liftslope *= (1 - effectiveUpstreamInfluence);
                liftslope += wingInteraction.EffectiveUpstreamLiftSlope;

                cosSweepAngle *= (1 - effectiveUpstreamInfluence);
                cosSweepAngle += wingInteraction.EffectiveUpstreamCosSweepAngle;
            }
            AoAmax += CalculateAoAmax(MachNumber);
        }

        #endregion

        //Calculates current stall fraction based on previous stall fraction and current data.
        private void DetermineStall(double MachNumber, double AoA, out double ACshift, out double ACweight)
        {
            double lastStall = stall;
            double effectiveUpstreamStall = wingInteraction.EffectiveUpstreamStall;
            inFrontStall = effectiveUpstreamStall;

            stall = 0;

            CalculateWingCamberInteractions(MachNumber, AoA, out ACshift, out ACweight);

            double absAoA = Math.Abs(AoA);

            if (absAoA > AoAmax)
            {
                stall = FARMathUtil.Clamp((absAoA - AoAmax) * 10, 0, 1);
                stall = Math.Max(stall, lastStall);
                stall += effectiveUpstreamStall;
            }
            else if (absAoA < AoAmax * 0.8)
            {
                stall = 1 - FARMathUtil.Clamp((AoAmax * 0.75 - absAoA) * 20, 0, 1);
                stall = Math.Min(stall, lastStall);
                stall += effectiveUpstreamStall;
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

            liftslope = CalculateSubsonicLiftSlope(MachNumber);// / AoA;     //Prandtl lifting Line


            double ACshift = 0, ACweight = 0;
            DetermineStall(MachNumber, AoA, out ACshift, out ACweight);

            double beta = Math.Sqrt(MachNumber * MachNumber - 1);
            double TanSweep = Math.Tan(FARMathUtil.Clamp(Math.Acos(cosSweepAngle), 0, Math.PI * 0.5));
            double beta_TanSweep = beta / TanSweep;

            if (double.IsNaN(beta_TanSweep))
                beta_TanSweep = 0;

            double Cd0 = CdCompressibilityZeroLiftIncrement(MachNumber, cosSweepAngle, TanSweep, beta_TanSweep, beta) + 0.006;
            e = FARAeroUtil.CalculateOswaldsEfficiency(effective_AR, cosSweepAngle, Cd0);
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
                double coefMult = 1 / (FARAeroUtil.currentBodyAtm.y * MachNumber * MachNumber);

                double supersonicLENormalForceFactor = CalculateSupersonicLEFactor(beta, TanSweep, beta_TanSweep);

                double normalForce;
                if (FARDebugValues.useSplinesForSupersonicMath)
                    normalForce = GetSupersonicPressureDifference(MachNumber, AoA);
                else
                    normalForce = GetSupersonicPressureDifferenceNoSpline(MachNumber, AoA);
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
                
                Cl *= subScale;

                double M = FARMathUtil.Clamp(MachNumber, 1.2, double.PositiveInfinity);

                if (double.IsNaN(beta) || beta < 0.66332495807107996982298654733414)
                    beta = 0.66332495807107996982298654733414;

                TanSweep = Math.Tan(FARMathUtil.Clamp(Math.Acos(cosSweepAngle), 0, Math.PI * 0.5));
                beta_TanSweep = beta / TanSweep;
                if (double.IsNaN(beta_TanSweep))
                    beta_TanSweep = 0;
                
                double coefMult = 1 / (FARAeroUtil.currentBodyAtm.y * M * M);

                double supersonicLENormalForceFactor = CalculateSupersonicLEFactor(beta, TanSweep, beta_TanSweep);

                subScale = 1 - subScale; //Adjust for supersonic code
                double normalForce;
                if (FARDebugValues.useSplinesForSupersonicMath)
                    normalForce = GetSupersonicPressureDifference(M, AoA);
                else
                    normalForce = GetSupersonicPressureDifferenceNoSpline(M, AoA);
                double CosAoA = Math.Cos(AoA);

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

            Cl *= wingInteraction.ClInterferenceFactor;

            ClIncrementFromRear = 0;
        }


        #region Supersonic Calculations

        //Calculates effect of the Mach cone being in front of, along, or behind the leading edge of the wing
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

        //This models the wing using a symmetric diamond airfoil

        private double GetSupersonicPressureDifferenceNoSpline(double M, double AoA)
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


            pRatio = (p3 + p4) - (p1 + p2);

            return pRatio;
        }

        //Calculates pressure ratio of turning a supersonic flow through a particular angle using a shockwave
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

        //Calculates pressure ratio due to turning a supersonic flow through a Prandtl-Meyer Expansion
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

        //Calculates pressure ratio due to turning a supersonic flow through a Prandtl-Meyer Expansion
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

        
        private double GetSupersonicPressureDifference(double M, double AoA)
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

            pRatio = (p3 + p4) - (p1 + p2);

            return pRatio;
        }


        //Calculates pressure ratio of turning a supersonic flow through a particular angle using a shockwave
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

        //Calculates pressure ratio due to turning a supersonic flow through a Prandtl-Meyer Expansion
        private double PMExpansionCalculation(double angle, double inM, out double outM, double maxBeta, double minBeta)
        {
            inM = FARMathUtil.Clamp(inM, 1, double.PositiveInfinity);
            double nu1 = FARAeroUtil.PrandtlMeyerMach.Evaluate((float)inM);
            double theta = angle * FARMathUtil.rad2deg;
            double nu2 = nu1 + theta;
            if (nu2 >= FARAeroUtil.maxPrandtlMeyerTurnAngle)
            {
                nu2 = FARAeroUtil.maxPrandtlMeyerTurnAngle;
            }
            outM = FARAeroUtil.PrandtlMeyerAngle.Evaluate((float)nu2);

            double ratio;

            ratio = FARAeroUtil.StagnationPressure.Evaluate((float)inM) / FARAeroUtil.StagnationPressure.Evaluate((float)outM);
            return ratio;
        }

        //Calculates pressure ratio due to turning a supersonic flow through a Prandtl-Meyer Expansion
        private double PMExpansionCalculation(double angle, double inM, double maxBeta, double minBeta)
        {
            inM = FARMathUtil.Clamp(inM, 1, double.PositiveInfinity);
            double nu1 = FARAeroUtil.PrandtlMeyerMach.Evaluate((float)inM);
            double theta = angle * FARMathUtil.rad2deg;
            double nu2 = nu1 + theta;
            if (nu2 >= FARAeroUtil.maxPrandtlMeyerTurnAngle)
            {
                nu2 = FARAeroUtil.maxPrandtlMeyerTurnAngle;
            }
            float outM = FARAeroUtil.PrandtlMeyerAngle.Evaluate((float)nu2);

            double ratio;

            ratio = FARAeroUtil.StagnationPressure.Evaluate((float)inM) / FARAeroUtil.StagnationPressure.Evaluate(outM);
            return ratio;
        }

        #endregion

        //Short calculation for peak AoA for stalling
        protected double CalculateAoAmax(double MachNumber)
        {
            double StallAngle;
            if (MachNumber < 0.8)
                StallAngle = criticalCl / liftslope;
            else if (MachNumber > 1.4)
                StallAngle = 1.0471975511965977461542144610932;     //60 degrees in radians
            else
            {
                double tmp = criticalCl / liftslope;
                StallAngle = (MachNumber - 0.8) * (1.0471975511965977461542144610932 - tmp) * 1.6666666666666666666666666666667 + tmp;
            }

            return StallAngle;

        }

        //Calculates subsonic liftslope
        private double CalculateSubsonicLiftSlope(double MachNumber)
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

            effective_AR = transformed_AR * wingInteraction.ARFactor;

            effective_AR = FARMathUtil.Clamp(effective_AR, 0.25, 30);   //Even this range of effective ARs is large, but it keeps the Oswald's Efficiency numbers in check

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

        //Transforms sweep of the midchord to cosine(sweep of the leading edge)
        private void SetSweepAngle(double sweepHalfChord)
        {
            cosSweepAngle = sweepHalfChord;
            cosSweepAngle = Math.Tan(cosSweepAngle);
            double tmp = (1 - TaperRatio) / (1 + TaperRatio);
            tmp *= 2 / transformed_AR;
            cosSweepAngle += tmp;
            cosSweepAngle = Math.Cos(Math.Atan(cosSweepAngle));
        }


        #region Compressibility

        /// <summary>
        /// This modifies the Cd to account for compressibility effects due to increasing Mach number
        /// </summary>
        private double CdCompressibilityZeroLiftIncrement(double M, double SweepAngle, double TanSweep, double beta_TanSweep, double beta)
        {

            if (wingInteraction.HasWingsUpstream)
            {
                zeroLiftCdIncrement = wingInteraction.EffectiveUpstreamCd0;
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
                int.TryParse(node.GetValue("nonSideAttach"), out nonSideAttach);
            if (node.HasValue("MidChordSweep"))
                double.TryParse(node.GetValue("MidChordSweep"), out MidChordSweep);
        }
    }
}