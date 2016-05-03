/*
Ferram Aerospace Research v0.15.6.3 "Kindelberger"
=========================
Aerodynamics model for Kerbal Space Program

Copyright 2015, Michael Ferrara, aka Ferram4

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
				stupid_chris, for the RealChuteLite implementation
            			Taverius, for correcting a ton of incorrect values  
				Tetryds, for finding lots of bugs and issues and not letting me get away with them, and work on example crafts
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager updates  
            			ialdabaoth (who is awesome), who originally created Module Manager  
                        	Regex, for adding RPM support  
				DaMichel, for some ferramGraph updates and some control surface-related features  
            			Duxwing, for copy editing the readme  
   
   CompatibilityChecker by Majiir, BSD 2-clause http://opensource.org/licenses/BSD-2-Clause

   Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission  
	http://forum.kerbalspaceprogram.com/threads/55219

   ModularFLightIntegrator by Sarbian, Starwaster and Ferram4, MIT: http://opensource.org/licenses/MIT
	http://forum.kerbalspaceprogram.com/threads/118088

   Toolbar integration powered by blizzy78's Toolbar plugin; used with permission  
	http://forum.kerbalspaceprogram.com/threads/60863
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using FerramAerospaceResearch;
using FerramAerospaceResearch.FARAeroComponents;

/// <summary>
/// This calculates the lift and drag on a wing in the atmosphere
/// 
/// It uses Prandtl lifting line theory to calculate the basic lift and drag coefficients and includes compressibility corrections for subsonic and supersonic flows; transsonic regime has placeholder
/// </summary>

namespace ferram4
{
    public class FARWingAerodynamicModel : FARBaseAerodynamics, TweakScale.IRescalable<FARWingAerodynamicModel>, ILiftProvider, IPartMassModifier
    {
        public double rawAoAmax = 15;
        private double AoAmax = 15;

        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = false)]
        public float wingBaseMassMultiplier = 1f;

        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true)]
        public float curWingMass = 1;
        private float desiredMass = 0f;
        private float baseMass = 0f;

        [KSPField(guiName = "Mass-Strength Multiplier %", isPersistant = true, guiActiveEditor = true, guiActive = false), UI_FloatRange(maxValue = 4.0f, minValue = 0.05f, scene = UI_Scene.Editor, stepIncrement = 0.05f)]
        public float massMultiplier = 1.0f;

        public float oldMassMultiplier = -1f;

        [KSPField(isPersistant = false)]
        public double MAC;

        public double MAC_actual;

        [KSPField(isPersistant = false)]
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

        public double b_2_actual;        //span

        [KSPField(isPersistant = false)]
        public double MidChordSweep;
        private double MidChordSweepSideways = 0;

        private double cosSweepAngle = 0;

        private double effective_b_2 = 1;
        private double effective_MAC = 1;

        protected double effective_AR = 4;
        protected double transformed_AR = 4;

        private ArrowPointer liftArrow;
        private ArrowPointer dragArrow;

        bool fieldsVisible = false;

        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = false, guiFormat = "F3", guiUnits = "kN")]
        public float dragForceWing;

        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = false, guiFormat = "F3", guiUnits = "kN")]
        public float liftForceWing;

        private double rawLiftSlope = 0;
        private double liftslope = 0;
        private double finalLiftSlope = 0;
        public double LiftSlope
        {
            get { return finalLiftSlope; }
        }
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
        FARAeroPartModule aeroModule;

        public short srfAttachNegative = 1;

        private FARWingAerodynamicModel parentWing = null;
        private bool updateMassNextFrame = false;

        protected double ClIncrementFromRear = 0;

        public double YmaxForce = double.MaxValue;
        public double XZmaxForce = double.MaxValue;

        public Vector3 worldSpaceForce;

        protected double NUFAR_areaExposedFactor = 0;
        protected double NUFAR_totalExposedAreaFactor = 0;

        public bool ready = false;
        bool massScaleReady = false;

        public void NUFAR_ClearExposedAreaFactor()
        {
            NUFAR_areaExposedFactor = 0;
            NUFAR_totalExposedAreaFactor = 0;
        }

        public void NUFAR_CalculateExposedAreaFactor()
        {
            FARAeroPartModule a = (FARAeroPartModule)part.Modules["FARAeroPartModule"];

            NUFAR_areaExposedFactor = Math.Min(a.ProjectedAreas.kN, a.ProjectedAreas.kP);
            NUFAR_totalExposedAreaFactor = Math.Max(a.ProjectedAreas.kN, a.ProjectedAreas.kP);

        }

        public void NUFAR_SetExposedAreaFactor()
        {
            List<Part> counterparts = part.symmetryCounterparts;
            double counterpartsCount = 1; 
            double sum = NUFAR_areaExposedFactor;
            double totalExposedSum = NUFAR_totalExposedAreaFactor;

            for (int i = 0; i < counterparts.Count; i++)
            {
                Part p = counterparts[i];
                if (p == null)
                    continue;
                FARWingAerodynamicModel model;
                if (this is FARControllableSurface)
                    model = (FARWingAerodynamicModel)p.Modules["FARControllableSurface"];
                else
                    model = (FARWingAerodynamicModel)p.Modules["FARWingAerodynamicModel"];

                ++counterpartsCount;
                sum += model.NUFAR_areaExposedFactor;
                totalExposedSum += model.NUFAR_totalExposedAreaFactor;
            }
            double tmp = 1 / (counterpartsCount);
            sum *= tmp;
            totalExposedSum *= tmp;

            NUFAR_areaExposedFactor = sum;
            NUFAR_totalExposedAreaFactor = totalExposedSum;

            for (int i = 0; i < counterparts.Count; i++)
            {
                Part p = counterparts[i];
                if (p == null)
                    continue;
                FARWingAerodynamicModel model;
                if (this is FARControllableSurface)
                    model = (FARWingAerodynamicModel)p.Modules["FARControllableSurface"];
                else
                    model = (FARWingAerodynamicModel)p.Modules["FARWingAerodynamicModel"];

                model.NUFAR_areaExposedFactor = sum;
                model.NUFAR_totalExposedAreaFactor = totalExposedSum;
            }

        }

        public void NUFAR_UpdateShieldingStateFromAreaFactor()
        {
            if (NUFAR_areaExposedFactor < 0.1 * S)
                isShielded = true;
            else
            {
                isShielded = false;
            }
        }

        #region GetFunctions

        public double GetStall()
        {
            return stall;
        }

        public double GetCl()
        {
        
            double ClUpwards = 1;
            if (HighLogic.LoadedSceneIsFlight)
                ClUpwards = Vector3.Dot(liftDirection, -vessel.vesselTransform.forward);
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

        public double GetRawLiftSlope()
        {
            return rawLiftSlope;
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

        public Vector3d ComputeForceEditor(Vector3d velocityVector, double M, double density)
        {
            velocityEditor = velocityVector;

            rho = density;

            double AoA = CalculateAoA(velocityVector);
            return CalculateForces(velocityVector, M, AoA, density);
        }
        
        public void ComputeClCdEditor(Vector3d velocityVector, double M, double density)
        {
            velocityEditor = velocityVector;

            rho = density;

            double AoA = CalculateAoA(velocityVector);
            CalculateForces(velocityVector, M, AoA, density);
        }

        protected override void ResetCenterOfLift()
        {
            rho = 1;
            stall = 0;
        }

        public override Vector3d PrecomputeCenterOfLift(Vector3d velocity, double MachNumber, double density, FARCenterQuery center)
        {
            try
            {
                double AoA = CalculateAoA(velocity);

                Vector3d force = CalculateForces(velocity, MachNumber, AoA, density, double.PositiveInfinity);
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
                WC += -b_2_actual / 3 * (1 + TaperRatio * 2) / (1 + TaperRatio) * (Vector3d.right * srfAttachNegative + Vector3d.up * Math.Tan(MidChordSweep * FARMathUtil.deg2rad));
            }
            else
                WC += (-MAC_actual * 0.7) * Vector3d.up;

            localWingCentroid = WC;
        }

        public Vector3 WingCentroid()
        {
            return part_transform.TransformDirection(localWingCentroid) + part.partTransform.position;
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

        public override void Initialization()
        {
            base.Initialization();
            b_2_actual = b_2;
            MAC_actual = MAC;
            baseMass = part.prefabMass;
            StartInitialization();
            if(HighLogic.LoadedSceneIsEditor)
            {
                part.OnEditorAttach += OnWingAttach;
                part.OnEditorDetach += OnWingDetach;
            }

            OnVesselPartsChange += UpdateThisWingInteractions;
            ready = true;
        }

        public void StartInitialization()
        {
            MathAndFunctionInitialization();
            aeroModule = part.GetComponent<FARAeroPartModule>();

            if (aeroModule == null)
                Debug.LogError("[FAR] Could not find FARAeroPartModule on same part as FARWingAerodynamicModel!");

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

            OnWingAttach();
            massScaleReady = true;
            wingInteraction = new FARWingInteraction(this, this.part, rootMidChordOffsetFromOrig, srfAttachNegative);
            UpdateThisWingInteractions();
        }

        public void MathAndFunctionInitialization()
        {
            S = b_2_actual * MAC_actual;

            if (part.srfAttachNode.originalOrientation.x < 0)
                srfAttachNegative = -1;

            transformed_AR = b_2_actual / MAC_actual;

            MidChordSweepSideways = (1 - TaperRatio) / (1 + TaperRatio);

            MidChordSweepSideways = (Math.PI * 0.5 - Math.Atan(Math.Tan(MidChordSweep * FARMathUtil.deg2rad) + MidChordSweepSideways * 4 / transformed_AR)) * MidChordSweepSideways * 0.5;

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

        public void EditorUpdateWingInteractions()
        {

            //HashSet<FARWingAerodynamicModel> wingsHandled = wingInteraction.UpdateNearbyWingInteractions();     //first update the old nearby wings
            UpdateThisWingInteractions();
            //wingInteraction.UpdateNearbyWingInteractions(wingsHandled);     //then update the new nearby wings, not doing the ones already handled
        }

        public void UpdateThisWingInteractions()
        {
            if(VesselPartList == null)
                VesselPartList = GetShipPartList();
            if (wingInteraction == null)
                wingInteraction = new FARWingInteraction(this, part, rootMidChordOffsetFromOrig, srfAttachNegative);

            wingInteraction.UpdateWingInteraction(VesselPartList, nonSideAttach == 1);
        }

        #endregion

        #region Physics Frame

        public virtual void FixedUpdate()
        {
            // With unity objects, "foo" or "foo != null" calls a method to check if
            // it's destroyed. (object)foo != null just checks if it is actually null.
            if (HighLogic.LoadedSceneIsFlight && FlightGlobals.ready && !isShielded)
            {
                Rigidbody rb = part.Rigidbody;
                Vessel vessel = part.vessel;

                if (!rb || !vessel || vessel.packed)
                    return;

                //bool set_vel = false;

                // Check that rb is not destroyed, but vessel is just not null
                if (vessel.atmDensity > 0)
                {
                    CurWingCentroid = WingCentroid();

                    Vector3d velocity = rb.GetPointVelocity(CurWingCentroid) + Krakensbane.GetFrameVelocity()
                        - FARWind.GetWind(FlightGlobals.currentMainBody, part, rb.position);

                    double machNumber, v_scalar = velocity.magnitude;

                    if (vessel.mainBody.ocean)
                        rho = (vessel.mainBody.oceanDensity * 1000 * part.submergedPortion + part.atmDensity * (1 - part.submergedPortion));
                    else
                        rho = part.atmDensity;

                    machNumber = vessel.mach;
                    if (rho > 0 && v_scalar > 0.1)
                    {
                        double AoA = CalculateAoA(velocity);
                        double failureForceScaling = FARAeroUtil.GetFailureForceScaling(vessel);
                        Vector3d force = DoCalculateForces(velocity, machNumber, AoA, rho, failureForceScaling);

                        worldSpaceForce = force;

                        if(part.submergedPortion > 0)
                        {
                            Vector3 velNorm = velocity / v_scalar;
                            Vector3 worldSpaceDragForce, worldSpaceLiftForce;
                            worldSpaceDragForce = Vector3.Dot(velNorm, force) * velNorm;
                            worldSpaceLiftForce = worldSpaceForce - worldSpaceDragForce;

                            Vector3 waterDragForce, waterLiftForce;
                            if (part.submergedPortion < 1)
                            {
                                float waterFraction = (float)(part.submergedDynamicPressurekPa * part.submergedPortion);
                                waterFraction /= (float)rho;

                                waterDragForce = worldSpaceDragForce * waterFraction;        //calculate areaDrag vector
                                waterLiftForce = worldSpaceLiftForce * waterFraction;

                                worldSpaceDragForce -= waterDragForce;
                                worldSpaceLiftForce -= waterLiftForce;

                                waterDragForce *= Math.Min((float)part.submergedDragScalar, 1);
                                waterLiftForce *= (float)part.submergedLiftScalar;
                            }
                            else
                            {
                                waterDragForce = worldSpaceDragForce * Math.Min((float)part.submergedDragScalar, 1);
                                waterLiftForce = worldSpaceLiftForce * (float)part.submergedLiftScalar;

                                worldSpaceDragForce = worldSpaceLiftForce = Vector3.zero;
                            }
                            aeroModule.hackWaterDragVal += Math.Abs(waterDragForce.magnitude / (rb.mass * rb.velocity.magnitude)) * 5;  //extra water drag factor for wings
                            //rb.drag += waterDragForce.magnitude / (rb.mass * rb.velocity.magnitude);


                            waterLiftForce *= (float)PhysicsGlobals.BuoyancyWaterLiftScalarEnd;
                            if (part.partBuoyancy.splashedCounter < PhysicsGlobals.BuoyancyWaterDragTimer)
                            {
                                waterLiftForce *= (float)(part.partBuoyancy.splashedCounter / PhysicsGlobals.BuoyancyWaterDragTimer);
                            }

                            double waterLiftScalar = 1;
                            //reduce lift drastically when wing is in water
                            if (part.submergedPortion < 0.05)
                            {
                                waterLiftScalar = 396.0 * part.submergedPortion;
                                waterLiftScalar -= 39.6;
                                waterLiftScalar *= part.submergedPortion;
                                waterLiftScalar++;
                            }
                            else if (part.submergedPortion > 0.95)
                            {
                                waterLiftScalar = 396.0 * part.submergedPortion;
                                waterLiftScalar -= 752.4;
                                waterLiftScalar *= part.submergedPortion;
                                waterLiftScalar += 357.4;
                            }
                            else
                            {
                                waterLiftScalar = 0.01;
                            }

                            waterLiftForce *= (float)waterLiftScalar;
                            worldSpaceLiftForce *= (float)waterLiftScalar;

                            force = worldSpaceDragForce + worldSpaceLiftForce + waterLiftForce;
                            worldSpaceForce = force + waterDragForce;
                        }

                        Vector3d scaledForce = worldSpaceForce;
                        //This accounts for the effect of flap effects only being handled by the rearward surface
                        scaledForce *= S / (S + wingInteraction.EffectiveUpstreamArea);

                        double forwardScaledForceMag = Vector3d.Dot(scaledForce, part_transform.forward);
                        Vector3d forwardScaledForce = forwardScaledForceMag * (Vector3d)part_transform.forward;

                        if (Math.Abs(forwardScaledForceMag) > YmaxForce * failureForceScaling * (1 + part.submergedPortion * 1000) || (scaledForce - forwardScaledForce).magnitude > XZmaxForce * failureForceScaling * (1 + part.submergedPortion * 1000))
                            if (part.parent && !vessel.packed)
                            {
                                vessel.SendMessage("AerodynamicFailureStatus");
                                string msg = String.Format("[{0}] Joint between {1} and {2} failed due to aerodynamic stresses on the wing structure.",
                                                           KSPUtil.PrintTimeStamp(FlightLogger.met), part.partInfo.title, part.parent.partInfo.title);
                                FlightLogger.eventLog.Add(msg);
                                part.decouple(25);
                                if (FARDebugValues.aeroFailureExplosions)
                                    FXMonger.Explode(part, AerodynamicCenter, 1);
                            }

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
            else
            {
                if (isShielded)
                {
                    Cl = Cd = Cm = stall = 0;
                }
                if ((object)liftArrow != null)
                {
                    UnityEngine.Object.Destroy(liftArrow);
                    liftArrow = null;
                }
                if ((object)dragArrow != null)
                {
                    UnityEngine.Object.Destroy(dragArrow);
                    dragArrow = null;
                }
            }
        }

        //This version also updates the wing centroid
        public Vector3d CalculateForces(Vector3d velocity, double MachNumber, double AoA, double rho)
        {
            CurWingCentroid = WingCentroid();

            return DoCalculateForces(velocity, MachNumber, AoA, rho, 1);
        }

        public Vector3d CalculateForces(Vector3d velocity, double MachNumber, double AoA, double rho, double failureForceScaling)
        {
            CurWingCentroid = WingCentroid();

            return DoCalculateForces(velocity, MachNumber, AoA, rho, failureForceScaling);
        }

        private Vector3d DoCalculateForces(Vector3d velocity, double MachNumber, double AoA, double rho, double failureForceScaling)
        {
            //This calculates the angle of attack, adjusting the part's orientation for any deflection
            //CalculateAoA();

            double v_scalar = velocity.magnitude;
            //if (v_scalar <= 0.1)
            //    return Vector3d.zero;

            Vector3 forward = part_transform.forward;
            Vector3d velocity_normalized = velocity / v_scalar;

            double q = rho * v_scalar * v_scalar * 0.0005;   //dynamic pressure, q

            ParallelInPlane = Vector3d.Exclude(forward, velocity).normalized;  //Projection of velocity vector onto the plane of the wing
            perp = Vector3d.Cross(forward, ParallelInPlane).normalized;       //This just gives the vector to cross with the velocity vector
            liftDirection = Vector3d.Cross(perp, velocity).normalized;

            ParallelInPlaneLocal = part_transform.InverseTransformDirection(ParallelInPlane);

            // Calculate the adjusted AC position (uses ParallelInPlane)
            AerodynamicCenter = CalculateAerodynamicCenter(MachNumber, AoA, CurWingCentroid);

            //Throw AoA into lifting line theory and adjust for part exposure and compressibility effects

            double skinFrictionDrag;
            if(HighLogic.LoadedSceneIsFlight)
                skinFrictionDrag = FARAeroUtil.SkinFrictionDrag(rho, effective_MAC, v_scalar, MachNumber, vessel.externalTemperature, vessel.mainBody.atmosphereAdiabaticIndex);
            else
                skinFrictionDrag = 0.005;


            skinFrictionDrag *= 1.1;    //account for thickness

            CalculateCoefficients(MachNumber, AoA, skinFrictionDrag);


            //lift and drag vectors
            Vector3d L, D;
            if (failureForceScaling >= 1 && part.submergedPortion > 0)
            {
                L = liftDirection * (Cl * S) * q * (part.submergedPortion * part.submergedLiftScalar + 1 - part.submergedPortion);    //lift; submergedDynPreskPa handles lift
                D = -velocity_normalized * (Cd * S) * q * (part.submergedPortion * part.submergedDragScalar + 1 - part.submergedPortion);                         //drag is parallel to velocity vector
            }
            else
            {
                L = liftDirection * (Cl * S) * q;    //lift; submergedDynPreskPa handles lift
                D = -velocity_normalized * (Cd * S) * q;                         //drag is parallel to velocity vector
            }

            UpdateAeroDisplay(L, D);
            Vector3d force = (L + D);
            if (double.IsNaN(force.sqrMagnitude) || double.IsNaN(AerodynamicCenter.sqrMagnitude))// || float.IsNaN(moment.magnitude))
            {
                Debug.LogWarning("FAR Error: Aerodynamic force = " + force.magnitude + " AC Loc = " + AerodynamicCenter.magnitude + " AoA = " + AoA + "\n\rMAC = " + effective_MAC + " B_2 = " + effective_b_2 + " sweepAngle = " + cosSweepAngle + "\n\rMidChordSweep = " + MidChordSweep + " MidChordSweepSideways = " + MidChordSweepSideways + "\n\r at " + part.name);
                force = AerodynamicCenter = Vector3d.zero;
            }

            double numericalControlFactor = (part.rb.mass * v_scalar * 0.67) / (force.magnitude * TimeWarp.fixedDeltaTime);
            force *= Math.Min(numericalControlFactor, 1);


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

        public void OnWingAttach()
        {
            if(part.parent)
                parentWing = part.parent.GetComponent<FARWingAerodynamicModel>();

            GetRefAreaChildren();

            UpdateMassToAccountForArea();
        }

        public void OnWingDetach()
        {
            if ((object)parentWing != null)
                parentWing.updateMassNextFrame = true;

        }

        private void UpdateMassToAccountForArea()
        {
            float supportedArea = (float)(refAreaChildren + S);
            if ((object)parentWing != null)
                supportedArea *= 0.66666667f;   //if any supported area has been transfered to another part, we must remove it from here
            curWingMass = supportedArea * (float)FARAeroUtil.massPerWingAreaSupported * massMultiplier;

            desiredMass = curWingMass * wingBaseMassMultiplier;

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

                refAreaChildren += (childWing.refAreaChildren + childWing.S) * 0.33333333333333333333;//Take 1/3 of the area of the child wings
                //refAreaChildren += childWing.refAreaChildren + childWing.S;
            }

            if ((object)parentWing != null)
            {
                parentWing.GetRefAreaChildren();
                parentWing.UpdateMassToAccountForArea();
            }
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            Debug.Log("massDelta " + desiredMass);

            if (massScaleReady)
                return desiredMass - baseMass;
            else
                return 0;
        }

        public ModifierChangeWhen GetModuleMassChangeWhen()
        {
            return ModifierChangeWhen.FIXED;
        }

        #endregion

        public virtual double CalculateAoA(Vector3d velocity)
        {
            double PerpVelocity = Vector3d.Dot(part_transform.forward, velocity.normalized);
            return Math.Asin(FARMathUtil.Clamp(PerpVelocity, -1, 1));
        }

        #region Interactive Effects

        //Calculates camber and flap effects due to wing interactions
        private void CalculateWingCamberInteractions(double MachNumber, double AoA, out double ACshift, out double ACweight)
        {
            ACshift = 0;
            ACweight = 0;
            ClIncrementFromRear = 0;

            rawAoAmax = CalculateAoAmax(MachNumber);
            double effectiveUpstreamInfluence = 0;

            liftslope = rawLiftSlope;
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
                cosSweepAngle = FARMathUtil.Clamp(cosSweepAngle, 0d, 1d);
            }
            else
            {
                liftslope = rawLiftSlope;
                AoAmax = 0;
            }
            AoAmax += rawAoAmax;
        }

        #endregion

        //Calculates current stall fraction based on previous stall fraction and current data.
        private void DetermineStall(double MachNumber, double AoA)
        {
            double lastStall = stall;
            double effectiveUpstreamStall = wingInteraction.EffectiveUpstreamStall;

            stall = 0;
            double absAoA = Math.Abs(AoA);

            if (absAoA > AoAmax)
            {
                stall = FARMathUtil.Clamp((absAoA - AoAmax) * 10, 0, 1);
                stall = Math.Max(stall, lastStall);
                stall += effectiveUpstreamStall;
            }
            else if (absAoA < AoAmax)
            {
                stall = 1 - FARMathUtil.Clamp((AoAmax - absAoA) * 25, 0, 1);
                stall = Math.Min(stall, lastStall);
                stall += effectiveUpstreamStall;
            }
            else
            {
                stall = lastStall;
            }

            //if (HighLogic.LoadedSceneIsFlight)
            //    stall = FARMathUtil.Clamp(stall, lastStall - 2 * TimeWarp.fixedDeltaTime, lastStall + 2 * TimeWarp.fixedDeltaTime);     //Limits stall to increasing at a rate of 2/s

            stall = FARMathUtil.Clamp(stall, 0, 1);
            if (stall < 1e-5)
                stall = 0;
        }


        /// <summary>
        /// This calculates the lift and drag coefficients
        /// </summary>
        private void CalculateCoefficients(double MachNumber, double AoA, double skinFrictionCoefficient)
        {

            minStall = 0;

            rawLiftSlope = CalculateSubsonicLiftSlope(MachNumber);// / AoA;     //Prandtl lifting Line


            double ACshift = 0, ACweight = 0;
            CalculateWingCamberInteractions(MachNumber, AoA, out ACshift, out ACweight);
            DetermineStall(MachNumber, AoA);

            double beta = Math.Sqrt(MachNumber * MachNumber - 1);
            if (double.IsNaN(beta) || beta < 0.66332495807107996982298654733414)
                beta = 0.66332495807107996982298654733414;

            double TanSweep = Math.Sqrt(FARMathUtil.Clamp(1 - cosSweepAngle * cosSweepAngle, 0, 1)) / cosSweepAngle;//Math.Tan(FARMathUtil.Clamp(Math.Acos(cosSweepAngle), 0, Math.PI * 0.5));
            double beta_TanSweep = beta / TanSweep;


            double Cd0 = CdCompressibilityZeroLiftIncrement(MachNumber, cosSweepAngle, TanSweep, beta_TanSweep, beta) + 2 * skinFrictionCoefficient;
            double CdMax = CdMaxFlatPlate(MachNumber, beta);
            e = FARAeroUtil.CalculateOswaldsEfficiencyNitaScholz(effective_AR, cosSweepAngle, Cd0, TaperRatio);
            piARe = effective_AR * e * Math.PI;

            double CosAoA = Math.Cos(AoA);
            //            Debug.Log("Part: " + part.partInfo.title + " AoA: " + AoA);
            if (MachNumber <= 0.8)
            {
                double Cn = liftslope;
                finalLiftSlope = liftslope;
                //Cl = Cn * Math.Sin(2 * AoA) * 0.5;
                double sinAoA = Math.Sqrt(FARMathUtil.Clamp(1 - CosAoA * CosAoA, 0, 1));
                Cl = Cn * CosAoA * Math.Sign(AoA);

                Cl += ClIncrementFromRear;
                Cl *= sinAoA;

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
                double coefMult = 2 / (FARAeroUtil.CurrentBody.atmosphereAdiabaticIndex * MachNumber * MachNumber);

                double supersonicLENormalForceFactor = CalculateSupersonicLEFactor(beta, TanSweep, beta_TanSweep);

                double normalForce;
                normalForce = GetSupersonicPressureDifference(MachNumber, AoA);
//                double SinAoA = Math.Sin(AoA);
                //Cl = coefMult * (normalForce * CosAoA * Math.Sign(AoA) * sonicLEFactor - axialForce * SinAoA);
                //Cd = coefMult * (Math.Abs(normalForce * SinAoA) * sonicLEFactor + axialForce * CosAoA);
                finalLiftSlope = coefMult * normalForce * supersonicLENormalForceFactor;

                Cl = finalLiftSlope * CosAoA * Math.Sign(AoA);
                Cd = beta * Cl * Cl / piARe;

                Cd += Cd0;
            }
            /*
             * Transonic nonlinear lift / drag code
             * This uses a blend of subsonic and supersonic aerodynamics to try and smooth the gap between the two regimes
             */
            else
            {
                //This determines the weight of supersonic flow; subsonic uses 1-this
                double supScale = 2 * MachNumber;
                supScale -= 6.6;
                supScale *= MachNumber;
                supScale += 6.72;
                supScale *= MachNumber;
                supScale += -2.176;
                supScale *= -4.6296296296296296296296296296296;

                double Cn = liftslope;
                //Cl = Cn * Math.Sin(2 * AoA) * 0.5;
                double sinAoA = Math.Sqrt(FARMathUtil.Clamp(1 - CosAoA * CosAoA, 0, 1));
                Cl = Cn * CosAoA * sinAoA * Math.Sign(AoA);

                if (MachNumber <= 1)
                {
                    Cl += ClIncrementFromRear * sinAoA;
                    if (Math.Abs(Cl) > Math.Abs(ACweight))
                        ACshift *= FARMathUtil.Clamp(Math.Abs(ACweight / Cl), 0, 1);
                }
                finalLiftSlope = Cn * (1 - supScale);
                Cl *= (1 - supScale);

                double M = FARMathUtil.Clamp(MachNumber, 1.2, double.PositiveInfinity);

                double coefMult = 2 / (FARAeroUtil.CurrentBody.atmosphereAdiabaticIndex * M * M);

                double supersonicLENormalForceFactor = CalculateSupersonicLEFactor(beta, TanSweep, beta_TanSweep);

                //supScale = 1 - supScale; //Adjust for supersonic code
                double normalForce;
                normalForce = GetSupersonicPressureDifference(M, AoA);

                double supersonicLiftSlope = coefMult * normalForce * supersonicLENormalForceFactor * supScale;
                finalLiftSlope += supersonicLiftSlope;


                Cl += CosAoA * Math.Sign(AoA) * supersonicLiftSlope;

                double effectiveBeta = beta * supScale + (1 - supScale);

                Cd = effectiveBeta * Cl * Cl / piARe;

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
                ACShiftVec -= 0.75 / criticalCl * MAC_actual * Math.Abs(Cl) * stall * ParallelInPlane * CosAoA;

            Cl -= Cl * stall * 0.769;
            Cd += Cd * stall * 3;
            //double SinAoA = Math.Sqrt(FARMathUtil.Clamp(1 - CosAoA * CosAoA, 0, 1));
            Cd = Math.Max(Cd, CdMax * (1 - CosAoA * CosAoA));


            AerodynamicCenter = AerodynamicCenter + ACShiftVec;

            Cl *= wingInteraction.ClInterferenceFactor;

            finalLiftSlope *= wingInteraction.ClInterferenceFactor;

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

        private double GetSupersonicPressureDifference(double M, double AoA)
        {
            double pRatio;

            double maxSinBeta = FARAeroUtil.CalculateSinMaxShockAngle(M, FARAeroUtil.CurrentBody.atmosphereAdiabaticIndex);//GetBetaMax(M) * FARMathUtil.deg2rad;
            double minSinBeta = 1 / M;

            double halfAngle = 0.05;            //In radians, Corresponds to ~2.8 degrees or approximately what you would get from a ~4.8% thick diamond airfoil

            double AbsAoA = Math.Abs(AoA);

            double angle1 = halfAngle - AbsAoA;                  //Region 1 is the upper surface ahead of the max thickness
            double M1;
            double p1;       //pressure ratio wrt to freestream pressure
            if (angle1 >= 0)
                p1 = ShockWaveCalculation(angle1, M, out M1, maxSinBeta, minSinBeta);
            else
                p1 = PMExpansionCalculation(Math.Abs(angle1), M, out M1);

            //Region 2 is the upper surface behind the max thickness
            double p2 = PMExpansionCalculation(2 * halfAngle, M1) * p1;

            double angle3 = halfAngle + AbsAoA;                  //Region 3 is the lower surface ahead of the max thickness
            double M3;
            double p3;       //pressure ratio wrt to freestream pressure
            p3 = ShockWaveCalculation(angle3, M, out M3, maxSinBeta, minSinBeta);

            //Region 4 is the lower surface behind the max thickness
            double p4 = PMExpansionCalculation(2 * halfAngle, M3) * p3;

            //Debug.Log(p1 + " " + p2 + " " + p3 + " " + p4);
            pRatio = ((p3 + p4) - (p1 + p2)) * 0.5;

            return pRatio;
        }

        //Calculates pressure ratio of turning a supersonic flow through a particular angle using a shockwave
        private double ShockWaveCalculation(double angle, double inM, out double outM, double maxSinBeta, double minSinBeta)
        {
            //float sinBeta = (maxBeta - minBeta) * angle / maxTheta + minBeta;
            double sinBeta = FARAeroUtil.CalculateSinWeakObliqueShockAngle(inM, FARAeroUtil.CurrentBody.atmosphereAdiabaticIndex, angle);
            if (double.IsNaN(sinBeta))
                sinBeta = maxSinBeta;

            FARMathUtil.Clamp(sinBeta, minSinBeta, maxSinBeta);

            double normalInM = sinBeta * inM;
            normalInM = FARMathUtil.Clamp(normalInM, 1, double.PositiveInfinity);

            double tanM = inM * Math.Sqrt(FARMathUtil.Clamp(1 - sinBeta * sinBeta, 0, 1));

            double normalOutM = FARAeroUtil.MachBehindShockCalc(normalInM);

            outM = Math.Sqrt(normalOutM * normalOutM + tanM * tanM);

            double pRatio = FARAeroUtil.PressureBehindShockCalc(normalInM);

            return pRatio;
        }

        //Calculates pressure ratio due to turning a supersonic flow through a Prandtl-Meyer Expansion
        private double PMExpansionCalculation(double angle, double inM, out double outM)
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
        private double PMExpansionCalculation(double angle, double inM)
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
                sweepHalfChord = CosPartAngle;//Math.Acos(CosPartAngle);       //keep as cos to make things right
            else
                sweepHalfChord = CosPartAngle;//Math.Acos(tmp);

            //if (sweepHalfChord > Math.PI * 0.5)
            //    sweepHalfChord -= Math.PI;

            CosPartAngle = FARMathUtil.Clamp(ParallelInPlaneLocal.y, -1, 1);

            CosPartAngle *= CosPartAngle;
            double SinPartAngle2 = FARMathUtil.Clamp(1d - CosPartAngle, 0, 1);               //Get the squared values for the angles

            effective_b_2 = Math.Max(b_2_actual * CosPartAngle, MAC_actual * SinPartAngle2);
            effective_MAC = MAC_actual * CosPartAngle + b_2_actual * SinPartAngle2;
            transformed_AR = effective_b_2 / effective_MAC;

            sweepHalfChord = Math.Sqrt(Math.Max(1 - sweepHalfChord * sweepHalfChord, 0)) / sweepHalfChord;  //convert to tangent

            SetSweepAngle(sweepHalfChord);

            effective_AR = transformed_AR * wingInteraction.ARFactor;

            effective_AR = FARMathUtil.Clamp(effective_AR, 0.25, 30d);   //Even this range of effective ARs is large, but it keeps the Oswald's Efficiency numbers in check

            /*if (MachNumber < 1)
                tmp = Mathf.Clamp(MachNumber, 0, 0.9f);
            else
                tmp = 1 / Mathf.Clamp(MachNumber, 1.09f, Mathf.Infinity);*/

            if (MachNumber < 0.9)
                tmp = 1d - MachNumber * MachNumber;
            else
                tmp = 0.19;

            double sweepTmp = sweepHalfChord;
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

        //Transforms cos sweep of the midchord to cosine(sweep of the leading edge)
        private void SetSweepAngle(double tanSweepHalfChord)
        {
            //cosSweepAngle = cosSweepHalfChord;
            //cosSweepAngle = Math.Tan(cosSweepAngle);
            double tmp = (1d - TaperRatio) / (1d + TaperRatio);
            tmp *= 2d / transformed_AR;
            tanSweepHalfChord += tmp;
            cosSweepAngle = 1d / Math.Sqrt(1d + tanSweepHalfChord * tanSweepHalfChord);
            if (cosSweepAngle > 1d)
                cosSweepAngle = 1d;
        }


        #region Compressibility

        /// <summary>
        /// Calculates Cd at 90 degrees AoA so that the numbers are done correctly
        /// </summary>
        private double CdMaxFlatPlate(double M, double beta)
        {
            if (M < 0.5)
                return 2;
            if (M > 1.2)
                return 0.4 / (beta * beta) + 1.75;
            if(M < 1)
            {
                double result = M - 0.5;
                result *= result;
                return result * 2 + 2;
            }
            return 3.39 - 0.609091 * M;

        }

        /// <summary>
        /// This modifies the Cd to account for compressibility effects due to increasing Mach number
        /// </summary>
        private double CdCompressibilityZeroLiftIncrement(double M, double SweepAngle, double TanSweep, double beta_TanSweep, double beta)
        {
            double thisInteractionFactor = 1;
            if (wingInteraction.HasWingsUpstream)
            {
                if (wingInteraction.EffectiveUpstreamInfluence > 0.99)
                {
                    zeroLiftCdIncrement = wingInteraction.EffectiveUpstreamCd0;
                    return zeroLiftCdIncrement;
                }
                else
                    thisInteractionFactor = (1 - wingInteraction.EffectiveUpstreamInfluence);
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
                zeroLiftCdIncrement *= thisInteractionFactor;
                zeroLiftCdIncrement += wingInteraction.EffectiveUpstreamCd0 * wingInteraction.EffectiveUpstreamInfluence;
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
            {
                zeroLiftCdIncrement *= thisInteractionFactor;
                zeroLiftCdIncrement += wingInteraction.EffectiveUpstreamCd0 * wingInteraction.EffectiveUpstreamInfluence;
                return zeroLiftCdIncrement;
            }

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
            zeroLiftCdIncrement *= thisInteractionFactor;
            zeroLiftCdIncrement += wingInteraction.EffectiveUpstreamCd0 * wingInteraction.EffectiveUpstreamInfluence;

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

        public void OnRescale(TweakScale.ScalingFactor factor)
        {
            b_2_actual = factor.absolute.linear * b_2;
            MAC_actual = factor.absolute.linear * MAC;
            if(part.Modules.Contains("TweakScale"))
            {
                PartModule m = part.Modules["TweakScale"];
                float massScale = (float)m.Fields.GetValue("MassScale");
                baseMass = part.prefabMass * massScale;
                Debug.Log("massScale " + massScale);
            }
            massScaleReady = false;

            StartInitialization();
        }

        private void UpdateAeroDisplay(Vector3 lift, Vector3 drag)
        {
            if (PhysicsGlobals.AeroForceDisplay)
            {
                if (liftArrow == null)
                    liftArrow = ArrowPointer.Create(part_transform, localWingCentroid, lift, lift.magnitude * PhysicsGlobals.AeroForceDisplayScale, FerramAerospaceResearch.FARGUI.GUIColors.GetColor(0), true);
                else
                {
                    liftArrow.Direction = lift;
                    liftArrow.Length = lift.magnitude * PhysicsGlobals.AeroForceDisplayScale;
                }

                if (dragArrow == null)
                    dragArrow = ArrowPointer.Create(part_transform, localWingCentroid, drag, drag.magnitude * PhysicsGlobals.AeroForceDisplayScale, FerramAerospaceResearch.FARGUI.GUIColors.GetColor(1), true);
                else
                {
                    dragArrow.Direction = drag;
                    dragArrow.Length = drag.magnitude * PhysicsGlobals.AeroForceDisplayScale;
                }
            }
            else
            {
                if ((object)liftArrow != null)
                {
                    UnityEngine.Object.Destroy(liftArrow);
                    liftArrow = null;
                }
                if ((object)dragArrow != null)
                {
                    UnityEngine.Object.Destroy(dragArrow);
                    dragArrow = null;
                }
            }

            if (PhysicsGlobals.AeroDataDisplay)
            {
                if (!fieldsVisible)
                {
                    Fields["dragForceWing"].guiActive = true;
                    Fields["liftForceWing"].guiActive = true;
                    fieldsVisible = true;
                }

                dragForceWing = drag.magnitude;
                liftForceWing = lift.magnitude;

            }
            else if (fieldsVisible)
            {
                Fields["dragForceWing"].guiActive = false;
                Fields["liftForceWing"].guiActive = false;
                fieldsVisible = false;
            }

        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (liftArrow != null)
            {
                UnityEngine.Object.Destroy(liftArrow);
                liftArrow = null;
            }
            if (dragArrow != null)
            {
                UnityEngine.Object.Destroy(dragArrow);
                dragArrow = null;
            }
            if (wingInteraction != null)
            {
                wingInteraction.Destroy();
                wingInteraction = null;
            }
        }

    }
}