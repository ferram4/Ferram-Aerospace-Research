/*
Ferram Aerospace Research v0.15.5.5 "Hugoniot"
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
using System.Threading;
using System.Linq;
using UnityEngine;
using FerramAerospaceResearch.FARPartGeometry;
using ferram4;

namespace FerramAerospaceResearch.FARAeroComponents
{
    public class FARVesselAero : VesselModule
    {
        Vessel _vessel;
        FerramAerospaceResearch.FARGUI.FARFlightGUI.FlightGUI _flightGUI;
        int _voxelCount;

        public double Length
        {
            get { return _vehicleAero.Length; }
        }

		public bool isValid
		{
			get { return enabled && _vehicleAero != null; }
		}

        public double MaxCrossSectionArea
        {
            get { return _vehicleAero.MaxCrossSectionArea; }
        }

        double machNumber;
        public double MachNumber
        {
            get { return machNumber; }
        }
        double reynoldsNumber;
        public double ReynoldsNumber
        {
            get { return reynoldsNumber; }
        }

        List<GeometryPartModule> _currentGeoModules;
        int geoModulesReady = 0;

        List<FARAeroPartModule> _currentAeroModules;
        List<FARAeroPartModule> _unusedAeroModules;
        List<FARAeroSection> _currentAeroSections;
        List<FARWingAerodynamicModel> _legacyWingModels;

        int _updateRateLimiter = 20;
        bool _updateQueued = true;
        bool _recalcGeoModules = false;
        bool setup = false;

        VehicleAerodynamics _vehicleAero;
        VesselIntakeRamDrag _vesselIntakeRamDrag;
        

        private void Start()
        {
            if (!CompatibilityChecker.IsAllCompatible())
            {
                this.enabled = false;
                return;
            }

            _vessel = gameObject.GetComponent<Vessel>();
            this.enabled = true;

            if (_vessel.rootPart.Modules.Contains("MissileLauncher") && _vessel.parts.Count == 1)
            {
                _vessel.rootPart.dragModel = Part.DragModel.CUBE;
                this.enabled = false;
                return;
            }

            _currentGeoModules = new List<GeometryPartModule>();
            for (int i = 0; i < _vessel.parts.Count; i++)
            {
                Part p = _vessel.parts[i];
                p.maximum_drag = 0;
                p.minimum_drag = 0;
                p.angularDrag = 0;

                /*p.dragModel = Part.DragModel.NONE;
                p.dragReferenceVector = Vector3.zero;
                p.dragScalar = 0;
                p.dragVector = Vector3.zero;
                p.dragVectorDir = Vector3.zero;
                p.dragVectorDirLocal = Vector3.zero;
                p.dragVectorMag = 0;
                p.dragVectorSqrMag = 0;

                p.bodyLiftMultiplier = 0;
                p.bodyLiftScalar = 0;*/

                GeometryPartModule g = p.GetComponent<GeometryPartModule>();
                if((object)g != null)
                {
                    _currentGeoModules.Add(g);
                    if (g.Ready)
                        geoModulesReady++;
                }
                if(p.Modules.Contains("KerbalEVA"))
                {
                    p.AddModule("GeometryPartModule");
                    g = p.GetComponent<GeometryPartModule>();
                    p.AddModule("FARAeroPartModule");
                    _currentGeoModules.Add(g);
                }
            }

            GameEvents.onVesselGoOffRails.Add(VesselUpdateEvent);
            //GameEvents.onVesselChange.Add(VesselUpdateEvent);
            //GameEvents.onVesselLoaded.Add(VesselUpdate);
            //GameEvents.onVesselCreate.Add(VesselUpdateEvent);
            GameEvents.onVesselWasModified.Add(VesselUpdateEvent);
            RequestUpdateVoxel(false);

            if (_vessel == null)
            {
                _vessel = gameObject.GetComponent<Vessel>();
                if (_vessel == null)
                {
                    return;
                }
            }

            if (_vehicleAero == null)
            {
                _vehicleAero = new VehicleAerodynamics();
                _vesselIntakeRamDrag = new VesselIntakeRamDrag();
            }
            //Debug.Log("Starting " + _vessel.vesselName + " aero properties");
        }

        private void FixedUpdate()
        {
            if (_vehicleAero == null)
                return;
            if (_vehicleAero.CalculationCompleted)
            {
                _vehicleAero.GetNewAeroData(out _currentAeroModules, out _unusedAeroModules, out _currentAeroSections, out _legacyWingModels);

                if ((object)_flightGUI == null)
                    _flightGUI = _vessel.GetComponent<FerramAerospaceResearch.FARGUI.FARFlightGUI.FlightGUI>();

                _flightGUI.UpdateAeroModules(_currentAeroModules, _legacyWingModels);
                //Debug.Log("Updating " + _vessel.vesselName + " aero properties\n\rCross-Sectional Area: " + _vehicleAero.MaxCrossSectionArea + " Crit Mach: " + _vehicleAero.CriticalMach + "\n\rUnusedAeroCount: " + _unusedAeroModules.Count + " UsedAeroCount: " + _currentAeroModules.Count + " sectCount: " + _currentAeroSections.Count);

                for (int i = 0; i < _unusedAeroModules.Count; i++)
                {
                    FARAeroPartModule a = _unusedAeroModules[i];
                    a.SetShielded(true);
                    a.ForceLegacyAeroUpdates();
                    //Debug.Log(a.part.partInfo.title + " shielded, area: " + a.ProjectedAreas.totalArea);
                }

                for (int i = 0; i < _currentAeroModules.Count; i++)
                {
                    FARAeroPartModule a = _currentAeroModules[i];
                    a.SetShielded(false);
                    a.ForceLegacyAeroUpdates();
                    //Debug.Log(a.part.partInfo.title + " unshielded, area: " + a.ProjectedAreas.totalArea);
                }
                
                _vesselIntakeRamDrag.UpdateAeroData(_currentAeroModules, _unusedAeroModules);
            }

            if (FlightGlobals.ready && _currentAeroSections != null)                
                CalculateAndApplyVesselAeroProperties();
            

            if (_currentGeoModules.Count > geoModulesReady)
            {
                CheckGeoModulesReady();
            }
            if (_updateRateLimiter < FARSettingsScenarioModule.VoxelSettings.minPhysTicksPerUpdate)
            {
                _updateRateLimiter++;
            }
            else if (_updateQueued)
                VesselUpdate(_recalcGeoModules);
        }

        private void CalculateAndApplyVesselAeroProperties()
        {
            float atmDensity = (float)_vessel.atmDensity;

            if (atmDensity <= 0)
            {
                machNumber = 0;
                reynoldsNumber = 0;
                return;
            }

            machNumber = _vessel.mach;
            reynoldsNumber = FARAeroUtil.CalculateReynoldsNumber(_vessel.atmDensity, Length, _vessel.srfSpeed, machNumber, FlightGlobals.getExternalTemperature((float)_vessel.altitude, _vessel.mainBody), _vessel.mainBody.atmosphereAdiabaticIndex);
            float skinFrictionDragCoefficient = (float)FARAeroUtil.SkinFrictionDrag(reynoldsNumber, machNumber);

            Vector3 frameVel = Krakensbane.GetFrameVelocityV3f();

            //if(_updateQueued)       //only happens if we have an voxelization scheduled, then we need to check for null
            for (int i = _currentAeroModules.Count - 1; i >= 0; i--)        //start from the top and come down to improve performance if it needs to remove anything
            {
                FARAeroPartModule m = _currentAeroModules[i];
                if (m != null && m.part != null && m.part.partTransform != null)
                    m.UpdateVelocityAndAngVelocity(frameVel);
                else
                {
                    _currentAeroModules.RemoveAt(i);
                    //i++;
                }
            }
            /*else                    //otherwise, we don't need to do Unity's expensive "is this part dead" null-check
                for (int i = _currentAeroModules.Count - 1; i >= 0; i--)        //start from the top and come down to improve performance if it needs to remove anything
                {
                    FARAeroPartModule m = _currentAeroModules[i];
                    m.UpdateVelocityAndAngVelocity(frameVel);
                }*/
            
            for (int i = 0; i < _currentAeroSections.Count; i++)
                _currentAeroSections[i].FlightCalculateAeroForces(atmDensity, (float)machNumber, (float)(reynoldsNumber / Length), skinFrictionDragCoefficient);

            _vesselIntakeRamDrag.ApplyIntakeRamDrag((float)machNumber, _vessel.srf_velocity.normalized, (float)_vessel.dynamicPressurekPa);

            for (int i = 0; i < _currentAeroModules.Count; i++)
            {
                FARAeroPartModule m = _currentAeroModules[i];
                m.ApplyForces();
            }
        }

        public void SimulateAeroProperties(out Vector3 aeroForce, out Vector3 aeroTorque, Vector3 velocityWorldVector, double altitude)
        {
            FARCenterQuery center = new FARCenterQuery();

            float pressure;
            float density;
            float temperature;
            float speedOfSound;

            CelestialBody body = _vessel.mainBody;      //Calculate main gas properties
            pressure = (float)body.GetPressure(altitude);
            temperature = (float)body.GetTemperature(altitude);
            density = (float)body.GetDensity(pressure, temperature);
            speedOfSound = (float)body.GetSpeedOfSound(pressure, density);

            float velocityMag = velocityWorldVector.magnitude;
            float machNumber = velocityMag / speedOfSound;
            float reynoldsNumber = (float)FARAeroUtil.CalculateReynoldsNumber(density, Length, velocityMag, machNumber, temperature, body.atmosphereAdiabaticIndex);

            float reynoldsPerLength = reynoldsNumber / (float)Length;
            float skinFriction = (float)FARAeroUtil.SkinFrictionDrag(reynoldsNumber, machNumber);

            for(int i = 0; i < _currentAeroSections.Count; i++)
                _currentAeroSections[i].PredictionCalculateAeroForces(density, machNumber, reynoldsPerLength, skinFriction, velocityWorldVector, center);

            for (int i = 0; i < _legacyWingModels.Count; i++)
                _legacyWingModels[i].PrecomputeCenterOfLift(velocityWorldVector, machNumber, density, center);

            aeroForce = center.force;
            aeroTorque = center.TorqueAt(_vessel.CoM);
        }


        private void TriggerIGeometryUpdaters()
        {
            for (int i = 0; i < _currentGeoModules.Count; i++)
                _currentGeoModules[i].RunIGeometryUpdaters();
        }

        private void CheckGeoModulesReady()
        {
            geoModulesReady = 0;
            for(int i = 0; i < _currentGeoModules.Count; i++)
            {
                GeometryPartModule g = _currentGeoModules[i];
                if(g == null)
                {
                    _currentGeoModules.RemoveAt(i);
                    i--;
                }
                else
                {
                    geoModulesReady++;
                }
            }
        }

        public void AnimationVoxelUpdate()
        {
            if (_updateRateLimiter == FARSettingsScenarioModule.VoxelSettings.minPhysTicksPerUpdate)
                _updateRateLimiter = FARSettingsScenarioModule.VoxelSettings.minPhysTicksPerUpdate - 2;
            RequestUpdateVoxel(false);
        }

        public void VesselUpdateEvent(Vessel v)
        {
            if (v == _vessel)
                RequestUpdateVoxel(true);
        }

        public void RequestUpdateVoxel(bool recalcGeoModules)
        {
            if (_updateRateLimiter > FARSettingsScenarioModule.VoxelSettings.minPhysTicksPerUpdate)
                _updateRateLimiter = FARSettingsScenarioModule.VoxelSettings.minPhysTicksPerUpdate - 2;
            _updateQueued = true;
            _recalcGeoModules |= recalcGeoModules;
        }

         public void VesselUpdate(bool recalcGeoModules)
         {
             if (_vessel == null)
             {
                 _vessel = gameObject.GetComponent<Vessel>();
                 if (_vessel == null || _vessel.vesselTransform == null)
                 {
                     return;
                 }
             }

             if (_vehicleAero == null)
             {
                 _vehicleAero = new VehicleAerodynamics();
                 _vesselIntakeRamDrag = new VesselIntakeRamDrag();
             }

             if (_updateRateLimiter < FARSettingsScenarioModule.VoxelSettings.minPhysTicksPerUpdate)        //this has been updated recently in the past; queue an update and return
             {
                 _updateQueued = true;
                 return;
             }
             else                                //last update was far enough in the past to run; reset rate limit counter and clear the queued flag
             {
                 _updateRateLimiter = 0;
                 _updateQueued = false;
             }
             if (_vessel.rootPart.Modules.Contains("LaunchClamp"))// || _vessel.rootPart.Modules.Contains("KerbalEVA"))
             {
                 DisableModule();
                 return;
             }
             if (recalcGeoModules)
             {
                 _currentGeoModules.Clear();
                 geoModulesReady = 0;
                 for (int i = 0; i < _vessel.Parts.Count; i++)
                 {
                     Part p = _vessel.Parts[i];
                     GeometryPartModule g = p.GetComponent<GeometryPartModule>();
                     if ((object)g != null)
                     {
                         _currentGeoModules.Add(g);
                         if (g.Ready)
                             geoModulesReady++;
                     }
                 }
             }
             if (_currentGeoModules.Count > geoModulesReady)
             {
                 _updateRateLimiter = FARSettingsScenarioModule.VoxelSettings.minPhysTicksPerUpdate - 2;
                 _updateQueued = true;
                 return;
             }

             if(_currentGeoModules.Count == 0)
             {
                 DisableModule();
             }

             TriggerIGeometryUpdaters();

             _voxelCount = VoxelCountFromType();
             if (!_vehicleAero.TryVoxelUpdate(_vessel.vesselTransform.worldToLocalMatrix, _vessel.vesselTransform.localToWorldMatrix, _voxelCount, _vessel.Parts, _currentGeoModules, !setup))
             {
                 _updateRateLimiter = FARSettingsScenarioModule.VoxelSettings.minPhysTicksPerUpdate - 2;
                 _updateQueued = true;
             }
             if(!_updateQueued)
                setup = true;

             Debug.Log("Updating vessel voxel for " + _vessel.vesselName);
         }

        //TODO: have this grab from a config file
        private int VoxelCountFromType()
        {
            if (!_vessel.isCommandable)
            {
                if (_vessel.parts.Count >= 2)
                    return FARSettingsScenarioModule.VoxelSettings.numVoxelsDebrisVessel;
                else
                    return 200;
            }
            else
                return FARSettingsScenarioModule.VoxelSettings.numVoxelsControllableVessel;
        }

        private void OnDestroy()
        {
            DisableModule();
        }

        private void DisableModule()
        {
            this.enabled = false;
            GameEvents.onVesselGoOffRails.Remove(VesselUpdateEvent);
            //GameEvents.onVesselChange.Remove(VesselUpdateEvent);
            //GameEvents.onVesselLoaded.Add(VesselUpdate);
            //GameEvents.onVesselCreate.Remove(VesselUpdateEvent);
            GameEvents.onVesselWasModified.Remove(VesselUpdateEvent);
        }
    }
}
