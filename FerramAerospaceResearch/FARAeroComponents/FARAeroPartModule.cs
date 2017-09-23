/*
Ferram Aerospace Research v0.15.9 "Liebe"
=========================
Aerodynamics model for Kerbal Space Program

Copyright 2017, Michael Ferrara, aka Ferram4

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
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;
using KSP.Localization;
using FerramAerospaceResearch;
using FerramAerospaceResearch.FARGUI;
using FerramAerospaceResearch.FARGUI.FARFlightGUI;
using ferram4;

namespace FerramAerospaceResearch.FARAeroComponents
{
    //Used to hold relevant aero data for each part before applying it
    public class FARAeroPartModule : PartModule, ILiftProvider
    {
        public Vector3 partLocalVel;
        public Vector3 partLocalVelNorm;
        public Vector3 partLocalAngVel;

        private Vector3 worldSpaceVelNorm;
        public Vector3 worldSpaceAeroForce;
        public Vector3 worldSpaceTorque;

        public Vector3 totalWorldSpaceAeroForce;

        Vector3 partLocalForce;
        Vector3 partLocalTorque;

        public float hackWaterDragVal;
        public static float waterSlowDragNew = -1;
        public static float minVelVesselMultNew = 0;

        ProjectedArea projectedArea;

        private double partStressMaxY = double.MaxValue;
        private double partStressMaxXZ = double.MaxValue;
        private double partForceMaxY = double.MaxValue;
        private double partForceMaxXZ = double.MaxValue;

        private ArrowPointer liftArrow;
        private ArrowPointer dragArrow;
        private ArrowPointer momentArrow;

        bool fieldsVisible = false;

        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = false, guiFormat = "F3", guiUnits = "FARUnitKN")]
        public float dragForce;

        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = false, guiFormat = "F3", guiUnits = "FARUnitKN")]
        public float liftForce;

        //[KSPField(isPersistant = false, guiActive = true)]
        //public double expSkinArea;

        //[KSPField(isPersistant = false, guiActive = true)]
        //public double expSkinFrac;

        private Transform partTransform;

        private MaterialColorUpdater materialColorUpdater;
        private FARWingAerodynamicModel legacyWingModel;
        public FARWingAerodynamicModel LegacyWingModel
        {
            get { return legacyWingModel; }
        }
        private ModuleLiftingSurface stockAeroSurfaceModule;

        public ProjectedArea ProjectedAreas
        {
            get { return projectedArea; }
        }

        public struct ProjectedArea
        {
            public double iN, iP;  //area in x direction
            public double jN, jP;  //area in y direction
            public double kN, kP;  //area in z direction
            public double totalArea;


            public static ProjectedArea operator +(ProjectedArea a, ProjectedArea b)
            {
                a.iN += b.iN;
                a.iP += b.iP;
                a.jN += b.jN;
                a.jP += b.jP;
                a.kN += b.kN;
                a.kP += b.kP;
                return a;
            }

            public static ProjectedArea operator + (ProjectedArea a, FARPartGeometry.VoxelCrossSection.SideAreaValues b)
            {
                a.iN += b.iN;
                a.iP += b.iP;
                a.jN += b.jN;
                a.jP += b.jP;
                a.kN += b.kN;
                a.kP += b.kP;
                return a;
            }

            public static implicit operator ProjectedArea(FARPartGeometry.VoxelCrossSection.SideAreaValues b)
            {
                ProjectedArea a = new ProjectedArea();
                a.iN = b.iN;
                a.iP = b.iP;
                a.jN = b.jN;
                a.jP = b.jP;
                a.kN = b.kN;
                a.kP = b.kP;
                return a;
               
            }
        }

        public void SetShielded(bool value)
        {
            part.ShieldedFromAirstream = value;
            if (value)
            {
                worldSpaceAeroForce = Vector3.zero;
                worldSpaceTorque = Vector3.zero;

                totalWorldSpaceAeroForce = Vector3.zero;

                partLocalForce = Vector3.zero;
                partLocalTorque = Vector3.zero;

                partLocalAngVel = Vector3.zero;
                partLocalVel = Vector3.zero;
                partLocalVelNorm = Vector3.zero;

                part.dragVectorMag = 0f;
                part.dragVectorDir = Vector3.zero;
                part.dragVectorDirLocal = Vector3.zero;
                part.dragScalar = 0f;

                UpdateAeroDisplay();
            }
        }

        public void ForceLegacyAeroUpdates()
        {
            if (legacyWingModel != null)
                legacyWingModel.ForceOnVesselPartsChange();
        }


        public void SetProjectedArea(ProjectedArea areas, Matrix4x4 vesselToWorldMatrix)
        {
            ProjectedArea transformedArea = new ProjectedArea();
            if (!part)
                return;

            Matrix4x4 transformMatrix = part.partTransform.worldToLocalMatrix * vesselToWorldMatrix;

            IncrementAreas(ref transformedArea, (float)areas.iP * Vector3.right, transformMatrix);
            IncrementAreas(ref transformedArea, (float)areas.iN * -Vector3.right, transformMatrix);
            IncrementAreas(ref transformedArea, (float)areas.jP * Vector3.up, transformMatrix);
            IncrementAreas(ref transformedArea, (float)areas.jN * -Vector3.up, transformMatrix);
            IncrementAreas(ref transformedArea, (float)areas.kP * Vector3.forward, transformMatrix);
            IncrementAreas(ref transformedArea, (float)areas.kN * -Vector3.forward, transformMatrix);

            projectedArea = transformedArea;
            projectedArea.totalArea = projectedArea.iN + projectedArea.iP + projectedArea.jN + projectedArea.jP + projectedArea.kN + projectedArea.kP;

            if (projectedArea.totalArea <= 0)
            {
                part.ShieldedFromAirstream = true;
                if (fieldsVisible)
                {
                    Fields["dragForce"].guiActive = false;
                    Fields["liftForce"].guiActive = false;
                    fieldsVisible = false;
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
                if ((object)momentArrow != null)
                {
                    UnityEngine.Object.Destroy(momentArrow);
                    momentArrow = null;
                }
            }
            else
            {
                part.ShieldedFromAirstream = false;
            }

            double areaForStress = projectedArea.totalArea / 6;
            if (!FARDebugValues.allowStructuralFailures || areaForStress <= 0.1 || part.Modules.Contains<RealChuteLite.RealChuteFAR>() || part.Modules.Contains<ModuleAblator>())
            {
                partForceMaxY = double.MaxValue;
                partForceMaxXZ = double.MaxValue;
                return;
            }
            partForceMaxY = areaForStress * partStressMaxY;
            partForceMaxXZ = areaForStress * partStressMaxXZ;
        }

        private void IncrementAreas(ref ProjectedArea data, Vector3 vector, Matrix4x4 transformMatrix)
        {
            vector = transformMatrix.MultiplyVector(vector);

            if (vector.x >= 0)
                data.iP += vector.x;
            else
                data.iN -= vector.x;

            if (vector.y >= 0)
                data.jP += vector.y;
            else
                data.jN -= vector.y;

            if (vector.z >= 0)
                data.kP += vector.z;
            else
                data.kN -= vector.z;
        }

        void Start()
        {
            if (waterSlowDragNew < 0)
            {
                waterSlowDragNew = PhysicsGlobals.BuoyancyWaterDragSlow;
                minVelVesselMultNew = (float)PhysicsGlobals.BuoyancyWaterDragPartVelGreaterVesselMult;
                PhysicsGlobals.BuoyancyWaterDragPartVelGreaterVesselMult = 0;
            }

            part.maximum_drag = 0;
            part.minimum_drag = 0;
            part.angularDrag = 0;
            if (HighLogic.LoadedSceneIsFlight)
                this.enabled = true;
            else if (HighLogic.LoadedSceneIsEditor)
                this.enabled = false;

            partLocalVel = Vector3.zero;
            partLocalForce = Vector3.zero;
            partLocalTorque = Vector3.zero;

            //if (!part.Modules.Contains("ModuleAeroSurface"))
            //    part.dragModel = Part.DragModel.CYLINDRICAL;

            if(FARDebugValues.allowStructuralFailures)
            {
                FARPartStressTemplate template = FARAeroStress.DetermineStressTemplate(this.part);
                partStressMaxY = template.YmaxStress;
                partStressMaxXZ = template.XZmaxStress;
            }
            partTransform = part.partTransform;

            materialColorUpdater = new MaterialColorUpdater(partTransform, PhysicsGlobals.TemperaturePropertyID);
            if (part.Modules.Contains<FARWingAerodynamicModel>())
                legacyWingModel = part.Modules.GetModule<FARWingAerodynamicModel>();
            else if (part.Modules.Contains<FARControllableSurface>())
                legacyWingModel = part.Modules.GetModule<FARControllableSurface>();
            else
                legacyWingModel = null;

            // For handling airbrakes aero visualization
            if (part.Modules.Contains<ModuleAeroSurface>())
                stockAeroSurfaceModule = part.Modules.GetModule<ModuleAeroSurface>();
            else
                stockAeroSurfaceModule = null;
        }

        public double ProjectedAreaWorld(Vector3 normalizedDirectionVector)
        {
            return ProjectedAreaLocal(partTransform.worldToLocalMatrix.MultiplyVector(normalizedDirectionVector));
        }

        public double ProjectedAreaLocal(Vector3 normalizedDirectionVector)
        {
            double area = 0;
            if (normalizedDirectionVector.x > 0)
                area += normalizedDirectionVector.x * projectedArea.iP;
            else
                area -= normalizedDirectionVector.x * projectedArea.iN;

            if (normalizedDirectionVector.y > 0)
                area += normalizedDirectionVector.y * projectedArea.jP;
            else
                area -= normalizedDirectionVector.y * projectedArea.jN;

            if (normalizedDirectionVector.z > 0)
                area += normalizedDirectionVector.z * projectedArea.kP;
            else
                area -= normalizedDirectionVector.z * projectedArea.kN;

            return area;
        }

        public void Update()
        {
            CalculateTotalAeroForce();

            FlightGUI flightGUI;
            AeroVisualizationGUI aeroVizGUI = null;

            if (FlightGUI.vesselFlightGUI != null && vessel != null && FlightGUI.vesselFlightGUI.TryGetValue(vessel, out flightGUI))
                aeroVizGUI = flightGUI.AeroVizGUI;

            if (aeroVizGUI != null && aeroVizGUI.AnyVisualizationActive && HighLogic.LoadedSceneIsFlight && !PhysicsGlobals.ThermalColorsDebug)
            {
                Color tintColor = AeroVisualizationTintingCalculation(aeroVizGUI);
                materialColorUpdater.Update(tintColor);
            }
        }

        //Do this so FlightGUI can read off of the numbers from this
        private void CalculateTotalAeroForce()
        {
            if (projectedArea.totalArea > 0.0)
            {
                totalWorldSpaceAeroForce = worldSpaceAeroForce;

                // Combine forces from legacy wing model
                if (legacyWingModel != null)
                    totalWorldSpaceAeroForce += legacyWingModel.worldSpaceForce;

                // Combine forces from stock code
                //totalWorldSpaceAeroForce += -part.dragVectorDir * part.dragScalar; // dragVectorDir is actually the velocity vector direction

                // Handle airbrakes
                if (stockAeroSurfaceModule != null)
                    totalWorldSpaceAeroForce += stockAeroSurfaceModule.dragForce + stockAeroSurfaceModule.liftForce;
            }
        }

        //Returns the tinted color if active; else it returns an alpha 0 color
        private Color AeroVisualizationTintingCalculation(AeroVisualizationGUI aeroVizGUI)
        {
            // Disable tinting for low dynamic pressure to prevent flicker
            if (vessel.dynamicPressurekPa <= 0.00001)
                return new Color(0, 0, 0, 0);

            // Stall tinting overrides Cl / Cd tinting
            if (legacyWingModel != null && aeroVizGUI.TintForStall)
                return new Color((float)((legacyWingModel.GetStall() * 100.0) / aeroVizGUI.FullySaturatedStall), 0f, 0f, 0.5f);

            if (!aeroVizGUI.TintForCl && !aeroVizGUI.TintForCd)
                return new Color(0, 0, 0, 0);

            double visualizationCl = 0, visualizationCd = 0;

            if (projectedArea.totalArea > 0.0)
            {
                Vector3 worldVelNorm = partTransform.localToWorldMatrix.MultiplyVector(partLocalVelNorm);
                Vector3 worldDragArrow = Vector3.Dot(totalWorldSpaceAeroForce, worldVelNorm) * worldVelNorm;
                Vector3 worldLiftArrow = totalWorldSpaceAeroForce - worldDragArrow;

                double invAndDynPresArea = legacyWingModel != null ? legacyWingModel.S : projectedArea.totalArea;
                invAndDynPresArea *= vessel.dynamicPressurekPa;
                invAndDynPresArea = 1 / invAndDynPresArea;
                visualizationCl = worldLiftArrow.magnitude * invAndDynPresArea;
                visualizationCd = worldDragArrow.magnitude * invAndDynPresArea;
            }

            double fullSatCl = 0, satCl = 0, fullSatCd = 0, satCd = 0;

            if (legacyWingModel != null)
            {
                fullSatCl = aeroVizGUI.FullySaturatedCl;
                fullSatCd = aeroVizGUI.FullySaturatedCd;
            }
            else
            {
                fullSatCl = aeroVizGUI.FullySaturatedClBody;
                fullSatCd = aeroVizGUI.FullySaturatedCdBody;
            }

            if (aeroVizGUI.TintForCl)
                satCl = Math.Abs(visualizationCl / fullSatCl);
            if (aeroVizGUI.TintForCd)
                satCd = Math.Abs(visualizationCd / fullSatCd);

            return new Color((float)satCd, 0.5f * (float)(satCl + satCd), (float)satCl, 0.5f);
        }

        public void ApplyForces()
        {
            if (!part)
                return;

            if (float.IsNaN(partLocalForce.sqrMagnitude))
                partLocalForce = Vector3.zero;
            if (float.IsNaN(partLocalTorque.sqrMagnitude))
                partLocalTorque = Vector3.zero;

            Vector3 localForceTemp = Vector3.Dot(partLocalVelNorm, partLocalForce) * partLocalVelNorm;
            
            partLocalForce = (localForceTemp * (float)part.dragScalar + (partLocalForce - localForceTemp) * (float)part.bodyLiftScalar);
            partLocalTorque *= (float)part.dragScalar;

            part.dragScalar = 0;
            part.bodyLiftScalar = 0;

            if(!vessel.packed)
                CheckAeroStressFailure();

            //Matrix4x4 matrix = partTransform.localToWorldMatrix;
            Rigidbody rb = part.Rigidbody;

            if (!rb)
                return;

            worldSpaceAeroForce = partTransform.TransformDirection(partLocalForce);
            worldSpaceTorque = partTransform.TransformDirection(partLocalTorque);
            UpdateAeroDisplay();

            //worldSpaceAeroForce *= (float)part.dynamicPressurekPa;     //is now used as a multiplier, not a force itself, in kPa
            //worldSpaceTorque *= (float)part.dynamicPressurekPa;

            if (part.submergedPortion <= 0)
            {
                part.AddForce(worldSpaceAeroForce);
                //rb.AddForce(worldSpaceAeroForce);
            }
            else
            {
                Vector3 worldSpaceDragForce, worldSpaceLiftForce;
                worldSpaceDragForce = Vector3.Dot(worldSpaceVelNorm, worldSpaceAeroForce) * worldSpaceVelNorm;
                worldSpaceLiftForce = worldSpaceAeroForce - worldSpaceDragForce;

                Vector3 waterDragForce, waterLiftForce;
                if (part.submergedPortion < 1)
                {
                    float waterFraction = (float)(part.submergedDynamicPressurekPa * part.submergedPortion + part.dynamicPressurekPa * (1 - part.submergedPortion));
                    waterFraction = (float)(part.submergedDynamicPressurekPa * part.submergedPortion) / waterFraction;

                    waterDragForce = worldSpaceDragForce * waterFraction;        //calculate areaDrag vector
                    waterLiftForce = worldSpaceLiftForce * waterFraction;

                    worldSpaceDragForce -= waterDragForce;      //remove water drag from this
                    worldSpaceLiftForce -= waterLiftForce;

                    waterDragForce *= (float)Math.Min(part.submergedDragScalar, 1);
                    waterLiftForce *= (float)part.submergedLiftScalar;
                }
                else
                {
                    waterDragForce = worldSpaceDragForce * (float)Math.Min(part.submergedDragScalar, 1);
                    waterLiftForce = worldSpaceLiftForce * (float)part.submergedLiftScalar;
                    worldSpaceDragForce = Vector3.zero;
                }
                hackWaterDragVal += Math.Abs(waterDragForce.magnitude / (rb.mass * rb.velocity.magnitude));
                //rb.drag += waterDragForce.magnitude / (rb.mass * rb.velocity.magnitude);

                if(!float.IsNaN(worldSpaceDragForce.x))
                    part.AddForce(worldSpaceDragForce + worldSpaceLiftForce + waterLiftForce);
                    //rb.AddForce(worldSpaceDragForce + worldSpaceLiftForce + waterLiftForce);

                worldSpaceAeroForce = worldSpaceDragForce + worldSpaceLiftForce + waterDragForce + waterLiftForce;
            }
            part.AddTorque(worldSpaceTorque);
            //rb.AddTorque(worldSpaceTorque);

            partLocalForce = Vector3.zero;
            partLocalTorque = Vector3.zero;

            //expSkinArea = part.skinExposedArea;
            //expSkinFrac = part.skinExposedAreaFrac;
        }

        //just to make water drag work in some possibly sane way
        public void FixedUpdate()
        {
            if (waterSlowDragNew > 0 && vessel && part.submergedPortion > 0)
            {
                PhysicsGlobals.BuoyancyWaterDragSlow = Math.Max(hackWaterDragVal, 0f);
                hackWaterDragVal = 0;

                float vel = partLocalVel.magnitude;

                if (vel < PhysicsGlobals.BuoyancyWaterDragMinVel || vel > vessel.srfSpeed * minVelVesselMultNew)
                    PhysicsGlobals.BuoyancyWaterDragSlow += waterSlowDragNew;
            }
        }

        public void AddLocalForce(Vector3 partLocalForce, Vector3 partLocalLocation)
        {
            this.partLocalForce += partLocalForce;
            this.partLocalTorque += Vector3.Cross(partLocalLocation - part.CoMOffset, partLocalForce);
        }

        public void AddLocalForceAndTorque(Vector3 partLocalForce, Vector3 partLocalTorque, Vector3 partLocalLocation)
        {
            Vector3 localRadVector = partLocalLocation - part.CoMOffset;
            this.partLocalForce += partLocalForce;
            this.partLocalTorque += Vector3.Cross(localRadVector, partLocalForce);

            this.partLocalTorque += partLocalTorque;

        }

        public void UpdateVelocityAndAngVelocity(Vector3 frameVel)
        {
            if ((object)partTransform == null)
                if (part != null)
                    partTransform = part.partTransform;
                else
                    return;

            if (part == null)
                return;

            //Matrix4x4 matrix = partTransform.worldToLocalMatrix;
            Rigidbody rb = part.Rigidbody;

            if (rb == null)
                return;

            //rb.drag = 0;
            partLocalVel = rb.velocity + frameVel
                        - FARWind.GetWind(FARAeroUtil.CurrentBody, part, rb.position);      //world velocity

            worldSpaceVelNorm = partLocalVel.normalized;
            partLocalVel = partTransform.InverseTransformDirection(partLocalVel);

            partLocalVelNorm = partLocalVel.normalized;

            partLocalAngVel = rb.angularVelocity;
            partLocalAngVel = partTransform.InverseTransformDirection(partLocalAngVel);

        }

        // TODO 1.2: provide actual implementation of new 1.2 methods
        #region ILiftProvider
        public bool DisableBodyLift
        {
            get
            {
                return false;
            }
        }

        public bool IsLifting
        {
            get
            {
                return true;
            }
        }

        public void OnCenterOfLiftQuery(CenterOfLiftQuery CoLMarker)
        {
            // Compute the actual center ourselves once per frame
            // Feed the precomputed values to the vanilla indicator
            CoLMarker.pos = FerramAerospaceResearch.FARGUI.FAREditorGUI.EditorAeroCenter.VesselRootLocalAeroCenter;      //hacking the old stuff to work with the new
            CoLMarker.pos = EditorLogic.RootPart.partTransform.localToWorldMatrix.MultiplyPoint3x4(CoLMarker.pos);
            CoLMarker.dir = Vector3.zero;
            CoLMarker.lift = 1;
        }
        #endregion ILiftProvider

        private void CheckAeroStressFailure()
        {
            if (partForceMaxY * (1 + part.submergedPortion * 1000) < partLocalForce.y || Vector3.ProjectOnPlane(partLocalForce, Vector3.up).magnitude > partForceMaxXZ * (1 + part.submergedPortion * 1000))
                ApplyAeroStressFailure();
        }

        private void ApplyAeroStressFailure()
        {
            bool failureOccured = false;
            if (part.Modules.Contains<ModuleProceduralFairing>())
            {
                ModuleProceduralFairing fairing = part.Modules.GetModule<ModuleProceduralFairing>();
                fairing.ejectionForce = 0.5f;

                fairing.DeployFairing();
                failureOccured = true;
            }

            List<Part> children = part.children;
            for (int i = 0; i < children.Count; i++)
            {
                Part child = children[i];
                child.decouple(25);


                failureOccured = true;
            }
            if (part.parent)
            {
                part.decouple(25);
                failureOccured = true;
            }

            if (failureOccured)
            {
                if (vessel)
                {
                    vessel.SendMessage("AerodynamicFailureStatus");
                    string msg = Localizer.Format("FARFlightLogAeroFailure", KSPUtil.PrintTimeStamp(FlightLogger.met), part.partInfo.title);
                    FlightLogger.eventLog.Add(msg); 
                    if (FARDebugValues.aeroFailureExplosions)
                    {
                        FXMonger.Explode(part, partTransform.position, (float)projectedArea.totalArea * 0.0005f);
                    }
                }
            }
        }

        private void UpdateAeroDisplay()
        {
            Vector3 worldDragArrow = Vector3.zero;
            Vector3 worldLiftArrow = Vector3.zero;

            if ((PhysicsGlobals.AeroForceDisplay || PhysicsGlobals.AeroDataDisplay) && !part.ShieldedFromAirstream)
            {
                Vector3 worldVelNorm = partTransform.localToWorldMatrix.MultiplyVector(partLocalVelNorm);
                worldDragArrow = Vector3.Dot(worldSpaceAeroForce, worldVelNorm) * worldVelNorm;
                worldLiftArrow = worldSpaceAeroForce - worldDragArrow;
            }
            if (PhysicsGlobals.AeroForceDisplay && !part.ShieldedFromAirstream)
            {
                if (liftArrow == null)
                    liftArrow = ArrowPointer.Create(partTransform, Vector3.zero, worldLiftArrow, worldLiftArrow.magnitude * FARKSPAddonFlightScene.FARAeroForceDisplayScale, GUIColors.GetColor(0), true);
                else
                {
                    liftArrow.Direction = worldLiftArrow;
                    liftArrow.Length = worldLiftArrow.magnitude * FARKSPAddonFlightScene.FARAeroForceDisplayScale;
                }

                if (dragArrow == null)
                    dragArrow = ArrowPointer.Create(partTransform, Vector3.zero, worldDragArrow, worldDragArrow.magnitude * FARKSPAddonFlightScene.FARAeroForceDisplayScale, GUIColors.GetColor(1), true);
                else
                {
                    dragArrow.Direction = worldDragArrow;
                    dragArrow.Length = worldDragArrow.magnitude * FARKSPAddonFlightScene.FARAeroForceDisplayScale;
                }

                if (FARDebugValues.showMomentArrows)
                {
                    if (momentArrow == null)
                        momentArrow = ArrowPointer.Create(partTransform, Vector3.zero, worldSpaceTorque, worldSpaceTorque.magnitude * FARKSPAddonFlightScene.FARAeroForceDisplayScale, GUIColors.GetColor(2), true);
                    else
                    {
                        momentArrow.Direction = -worldSpaceTorque;
                        momentArrow.Length = worldSpaceTorque.magnitude * FARKSPAddonFlightScene.FARAeroForceDisplayScale;
                    }
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
                if ((object)momentArrow != null)
                {
                    UnityEngine.Object.Destroy(momentArrow);
                    momentArrow = null;
                }
            }

            if (PhysicsGlobals.AeroDataDisplay && !part.ShieldedFromAirstream)
            {
                if (!fieldsVisible)
                {
                    Fields["dragForce"].guiActive = true;
                    Fields["liftForce"].guiActive = true;
                    fieldsVisible = true;
                }

                dragForce = worldDragArrow.magnitude;
                liftForce = worldLiftArrow.magnitude;

            }
            else if (fieldsVisible)
            {
                Fields["dragForce"].guiActive = false;
                Fields["liftForce"].guiActive = false;
                fieldsVisible = false;
            }

        }

        private void OnDestroy()
        {
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
            if (momentArrow != null)
            {
                UnityEngine.Object.Destroy(momentArrow);
                momentArrow = null;
            }
            legacyWingModel = null;
            stockAeroSurfaceModule = null;
        }
    }
}
