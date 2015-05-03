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
            FARCenterQuery aeroSection, dummy;
            aeroSection = new FARCenterQuery();
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

            FARBaseAerodynamics.PrecomputeGlobalCenterOfLift(aeroSection, dummy, vel);

            //Vector3 force0, moment0;
            //force0 = aeroSection.force;
            //moment0 = aeroSection.TorqueAt(Vector3.zero);

            aeroSection.force = -aeroSection.force;
            aeroSection.torque = -aeroSection.torque;

            //aeroSection.ClearAll();

            vel = (vel_base + vel_fuzz).normalized;

            for (int i = 0; i < _currentAeroSections.Count; i++)
            {
                FARAeroSection section = _currentAeroSections[i];
                section.EditorCalculateAeroForces(1, 3, 100000, 0.005f, vel, aeroSection);
            }

            FARBaseAerodynamics.PrecomputeGlobalCenterOfLift(aeroSection, dummy, vel);

            //Vector3 force2, moment2;
            //force2 = aeroSection.force;
            //moment2 = aeroSection.TorqueAt(Vector3.zero);


            //aeroSection.ClearAll();

            /*vel = vel_base.normalized;

            for (int i = 0; i < _currentAeroSections.Count; i++)
            {
                FARAeroSection section = _currentAeroSections[i];
                section.EditorCalculateAeroForces(1, 3, 100000, 0.005f, vel, aeroSection);
            }


            FARBaseAerodynamics.PrecomputeGlobalCenterOfLift(aeroSection, dummy, vel);

            Vector3 force1, moment1;
            force1 = aeroSection.force;
            moment1 = aeroSection.TorqueAt(Vector3.zero);

            vel_fuzz /= 0.02f;

            double L0, L1, L2;
            double D0, D1, D2;
            double M0, M1, M2;

            L0 = Vector3.Dot(vel_fuzz, force0);
            L1 = Vector3.Dot(vel_fuzz, force1);
            L2 = Vector3.Dot(vel_fuzz, force2);

            D0 = Vector3.Dot(vel_base, force0);
            D1 = Vector3.Dot(vel_base, force1);
            D2 = Vector3.Dot(vel_base, force2);

            M0 = moment0.magnitude;
            M1 = moment1.magnitude;
            M2 = moment2.magnitude;

            double dL_dalpha = (L2 - L0) * 0.5;
            double dD_dalpha = (D2 - D0) * 0.5;
            double dM_dalpha = (M2 - M0) * 0.5;

            double d2L_dalpha2 = L2 + L0 - 2 * L1;
            double d2D_dalpha2 = D2 + D0 - 2 * D1;
            double d2M_dalpha2 = M2 + M0 - 2 * M1;

            aeroForce = aeroSection.force;


            double x_ac = d2D_dalpha2 * dL_dalpha - dD_dalpha * d2L_dalpha2;
            x_ac = (-dM_dalpha * d2D_dalpha2 + dD_dalpha * d2M_dalpha2) / x_ac;

            double z_ac = (-d2M_dalpha2 - d2L_dalpha2 * x_ac) / d2D_dalpha2;

            vesselRootLocalAeroCenter = vel_base * (float)x_ac + vel_fuzz * (float)z_ac;*/
            vesselRootLocalAeroCenter = aeroSection.GetPos();
            vesselRootLocalAeroCenter = EditorLogic.RootPart.partTransform.worldToLocalMatrix.MultiplyPoint3x4(vesselRootLocalAeroCenter);
        }
    }
}
