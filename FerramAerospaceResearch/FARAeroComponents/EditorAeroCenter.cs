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
using UnityEngine;
using ferram4;

namespace FerramAerospaceResearch.FARAeroComponents
{
    class EditorAeroCenter
    {
        static EditorAeroCenter instance;
        public static EditorAeroCenter Instance
        {
            get { return instance; }
        }
        
        Vector3 vesselRootLocalAeroCenter;
        public static Vector3 VesselRootLocalAeroCenter
        {
            get { return instance.vesselRootLocalAeroCenter; }
        }
        Vector3 aeroForce;

        List<FARAeroPartModule> _currentAeroModules;
        List<FARAeroSection> _currentAeroSections;

        public EditorAeroCenter()
        {
            instance = this;
        }

        public void UpdateAeroData(VehicleAerodynamics vehicleAero)
        {
            vehicleAero.GetNewAeroData(out _currentAeroModules, out _currentAeroSections);
            UpdateAerodynamicCenter();
        }

        void UpdateAerodynamicCenter()
        {
            FARCenterQuery aeroSection, lift, dummy;
            aeroSection = new FARCenterQuery();
            lift = new FARCenterQuery();
            dummy = new FARCenterQuery();

            Vector3 vel_base, vel_fuzz;

            if (EditorDriver.editorFacility == EditorFacility.SPH)
            {
                vel_base = Vector3.forward;
                vel_fuzz = 0.02f * Vector3.up;
            }
            else
            {
                vel_base = Vector3.up;
                vel_fuzz = -0.02f * Vector3.forward;
            }

            Vector3 vel = (vel_base - vel_fuzz).normalized;

            for(int i = 0; i < _currentAeroSections.Count; i++)
            {
                FARAeroSection section = _currentAeroSections[i];
                section.EditorCalculateAeroForces(1, 3, 100000, 0.005f, vel, aeroSection);
            }
            
            aeroSection.force = -aeroSection.force;
            aeroSection.torque = -aeroSection.torque;

            vel = (vel_base + vel_fuzz).normalized;

            for (int i = 0; i < _currentAeroSections.Count; i++)
            {
                FARAeroSection section = _currentAeroSections[i];
                section.EditorCalculateAeroForces(1, 3, 100000, 0.005f, vel, aeroSection);
            }

            FARBaseAerodynamics.PrecomputeGlobalCenterOfLift(lift, dummy);
            //Debug.Log("Updated CoL");
            aeroSection.AddAll(lift);

            aeroForce = aeroSection.force;
            vesselRootLocalAeroCenter = aeroSection.GetPos();
            vesselRootLocalAeroCenter = EditorLogic.RootPart.transform.worldToLocalMatrix.MultiplyPoint3x4(vesselRootLocalAeroCenter);
        }
    }
}
