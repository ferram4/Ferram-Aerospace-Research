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
        VesselType _vType;
        int _voxelCount;

        public double Length
        {
            get { return _vehicleAero.Length; }
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
        List<FARAeroSection> _currentAeroSections;

        int _updateRateLimiter = 20;
        bool _updateQueued = true;
        bool setup = false;

        VehicleAerodynamics _vehicleAero;
        

        private void Start()
        {
            _vessel = gameObject.GetComponent<Vessel>();
            this.enabled = true;

            _currentGeoModules = new List<GeometryPartModule>();
            for (int i = 0; i < _vessel.parts.Count; i++)
            {
                Part p = _vessel.parts[i];
                p.maximum_drag = 0;
                p.minimum_drag = 0;
                p.angularDrag = 0;

                p.dragModel = Part.DragModel.NONE;
                p.dragReferenceVector = Vector3.zero;
                p.dragScalar = 0;
                p.dragVector = Vector3.zero;
                p.dragVectorDir = Vector3.zero;
                p.dragVectorDirLocal = Vector3.zero;
                p.dragVectorMag = 0;
                p.dragVectorSqrMag = 0;

                p.bodyLiftMultiplier = 0;
                p.bodyLiftScalar = 0;

                GeometryPartModule g = p.GetComponent<GeometryPartModule>();
                if((object)g != null)
                {
                    _currentGeoModules.Add(g);
                }
            }

            GameEvents.onVesselGoOffRails.Add(VesselUpdateEvent);
            GameEvents.onVesselChange.Add(VesselUpdateEvent);
            //GameEvents.onVesselLoaded.Add(VesselUpdate);
            GameEvents.onVesselCreate.Add(VesselUpdateEvent);
            GameEvents.onVesselWasModified.Add(VesselUpdateEvent);
            VesselUpdate(false);
        }

        private void FixedUpdate()
        {
            if (_vehicleAero.CalculationCompleted)
            {
                _vehicleAero.GetNewAeroData(out _currentAeroModules, out _currentAeroSections);

                _vessel.SendMessage("UpdateAeroModules", _currentAeroModules);
            } 
            
            if (FlightGlobals.ready && _currentAeroSections != null)
            {
                float atmDensity = (float)_vessel.atmDensity;

                if (atmDensity <= 0)
                    return;

                machNumber = _vessel.mach;
                reynoldsNumber = FARAeroUtil.CalculateReynoldsNumber(_vessel.atmDensity, Length, _vessel.srfSpeed, machNumber, FlightGlobals.getExternalTemperature((float)_vessel.altitude, _vessel.mainBody) + 273.15f);
                float skinFrictionDragCoefficient = (float)FARAeroUtil.SkinFrictionDrag(reynoldsNumber, machNumber);

                Vector3 frameVel = Krakensbane.GetFrameVelocityV3f();

                for (int i = 0; i < _currentAeroModules.Count; i++)
                {
                    FARAeroPartModule m = _currentAeroModules[i];
                    if (m != null)
                        m.UpdateVelocityAndAngVelocity(frameVel);
                    else
                    {
                        _currentAeroModules.RemoveAt(i);
                        i--;
                    }
                }
                
                for (int i = 0; i < _currentAeroSections.Count; i++)
                    _currentAeroSections[i].FlightCalculateAeroForces(atmDensity, (float)machNumber, (float)(reynoldsNumber / Length), skinFrictionDragCoefficient);

                for (int i = 0; i < _currentAeroModules.Count; i++)
                {
                    FARAeroPartModule m = _currentAeroModules[i];
                    if ((object)m != null)
                        m.ApplyForces();
                }
            }

            if (_currentGeoModules.Count > geoModulesReady)
            {
                CheckGeoModulesReady();
            }
            if (_updateRateLimiter < 20)
            {
                _updateRateLimiter++;
            }
            else if (_updateQueued)
                VesselUpdate(true);
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
            if (_updateRateLimiter == 20)
                _updateRateLimiter = 18;
            VesselUpdate(false);
        }

        public void VesselUpdateEvent(Vessel v)
        {
            if (v == _vessel)
                VesselUpdate(true);
        }

         public void VesselUpdate(bool recalcGeoModules)
         {
             if (_vessel == null)
                 _vessel = gameObject.GetComponent<Vessel>();
             if (_vehicleAero == null)
                 _vehicleAero = new VehicleAerodynamics();

             if (_currentGeoModules.Count > geoModulesReady)
             {
                 _updateQueued = true;
                 return;
             }

             if (_updateRateLimiter < 20)        //this has been updated recently in the past; queue an update and return
             {
                 _updateQueued = true;
                 return;
             }
             else                                //last update was far enough in the past to run; reset rate limit counter and clear the queued flag
             {
                 _updateRateLimiter = 0;
                 _updateQueued = false;
             }

             if (recalcGeoModules)
             {
                 _currentGeoModules = new List<GeometryPartModule>();
                 geoModulesReady = 0;
                 for (int i = 0; i < _vessel.parts.Count; i++)
                 {
                     Part p = _vessel.parts[i];
                     GeometryPartModule g = p.GetComponent<GeometryPartModule>();
                     if ((object)g != null)
                     {
                         _currentGeoModules.Add(g);
                         if (g.Ready)
                             geoModulesReady++;
                     }
                 }
             }

             if(_currentGeoModules.Count > geoModulesReady)
             {
                 _updateQueued = true;
                 return;
             }

             if(_currentGeoModules.Count == 0)
             {
                 DisableModule();
             }

             TriggerIGeometryUpdaters();

             _vType = _vessel.vesselType;

             _voxelCount = VoxelCountFromType();

             _vehicleAero.VoxelUpdate(_vessel.rootPart.partTransform.worldToLocalMatrix, _vessel.rootPart.partTransform.localToWorldMatrix, _voxelCount, _vessel.parts, _currentGeoModules, !setup);

             setup = true;

             Debug.Log("Updating vessel voxel for " + _vessel.vesselName);
         }

        //TODO: have this grab from a config file
        private int VoxelCountFromType()
        {
            if (_vType == VesselType.Debris || _vType == VesselType.Unknown)
                return 20000;
            else
                return 125000;
        }

        private void OnDestroy()
        {
            DisableModule();
        }

        private void DisableModule()
        {
            this.enabled = false;
            GameEvents.onVesselGoOffRails.Remove(VesselUpdateEvent);
            GameEvents.onVesselChange.Remove(VesselUpdateEvent);
            //GameEvents.onVesselLoaded.Add(VesselUpdate);
            GameEvents.onVesselCreate.Remove(VesselUpdateEvent);
            GameEvents.onVesselWasModified.Remove(VesselUpdateEvent);
        }
    }
}
