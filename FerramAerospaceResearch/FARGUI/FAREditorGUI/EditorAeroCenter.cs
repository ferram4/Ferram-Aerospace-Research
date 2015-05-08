/*
Ferram Aerospace Research v0.15 "Euler"
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
using System.Linq;
using UnityEngine;
using ferram4;
using FerramAerospaceResearch.FARAeroComponents;

namespace FerramAerospaceResearch.FARGUI.FAREditorGUI
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

            Transform rootPartTrans = EditorLogic.RootPart.partTransform;
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

            Vector3 vel = (vel_base - 2 * vel_fuzz).normalized;

            for(int i = 0; i < _currentAeroSections.Count; i++)
            {
                FARAeroSection section = _currentAeroSections[i];
                section.PredictionCalculateAeroForces(1, 3, 100000, 0.005f, vel, aeroSection);
            }

            FARBaseAerodynamics.PrecomputeGlobalCenterOfLift(aeroSection, dummy, vel, 1);

            Vector3 pos = rootPartTrans.position;

            Vector3 avgForcePos = Vector3.zero;

            Vector3 force0, moment0;
            force0 = aeroSection.force;
            moment0 = aeroSection.TorqueAt(pos);
            avgForcePos += aeroSection.GetPos();

            //aeroSection.force = -aeroSection.force;
            //aeroSection.torque = -aeroSection.torque;

            aeroSection.ClearAll();

            vel = (vel_base - vel_fuzz).normalized;

            for (int i = 0; i < _currentAeroSections.Count; i++)
            {
                FARAeroSection section = _currentAeroSections[i];
                section.PredictionCalculateAeroForces(1, 3, 100000, 0.005f, vel, aeroSection);
            }

            FARBaseAerodynamics.PrecomputeGlobalCenterOfLift(aeroSection, dummy, vel, 1);

            Vector3 force1, moment1;
            force1 = aeroSection.force;
            moment1 = aeroSection.TorqueAt(pos);
            avgForcePos += aeroSection.GetPos();

            aeroSection.ClearAll();

            vel = (vel_base + vel_fuzz).normalized;

            for (int i = 0; i < _currentAeroSections.Count; i++)
            {
                FARAeroSection section = _currentAeroSections[i];
                section.PredictionCalculateAeroForces(1, 3, 100000, 0.005f, vel, aeroSection);
            }

            FARBaseAerodynamics.PrecomputeGlobalCenterOfLift(aeroSection, dummy, vel, 1);

            Vector3 force2, moment2;
            force2 = aeroSection.force;
            moment2 = aeroSection.TorqueAt(pos);
            avgForcePos += aeroSection.GetPos();

            aeroSection.ClearAll();

            vel = (vel_base + 2 * vel_fuzz).normalized;

            for (int i = 0; i < _currentAeroSections.Count; i++)
            {
                FARAeroSection section = _currentAeroSections[i];
                section.PredictionCalculateAeroForces(1, 3, 100000, 0.005f, vel, aeroSection);
            }

            FARBaseAerodynamics.PrecomputeGlobalCenterOfLift(aeroSection, dummy, vel, 1);

            Vector3 force3, moment3;
            force3 = aeroSection.force;
            moment3 = aeroSection.TorqueAt(pos);
            avgForcePos += aeroSection.GetPos();

            aeroSection.ClearAll();
            
            /*vel = vel_base.normalized;

            for (int i = 0; i < _currentAeroSections.Count; i++)
            {
                FARAeroSection section = _currentAeroSections[i];
                section.EditorCalculateAeroForces(1, 3, 100000, 0.005f, vel, aeroSection);
            }


            FARBaseAerodynamics.PrecomputeGlobalCenterOfLift(aeroSection, dummy, vel);

            Vector3 force1, moment1;
            force1 = aeroSection.force;
            moment1 = aeroSection.TorqueAt(Vector3.zero);*/


            double N0, N1, N2, N3;
            double M0, M1, M2, M3;

            N0 = Vector3.Dot(-rootPartTrans.forward, force0);
            N1 = Vector3.Dot(-rootPartTrans.forward, force1);
            N2 = Vector3.Dot(-rootPartTrans.forward, force2);
            N3 = Vector3.Dot(-rootPartTrans.forward, force3);

            M0 = Vector3.Dot(-rootPartTrans.right, moment0);
            M1 = Vector3.Dot(-rootPartTrans.right, moment1);
            M2 = Vector3.Dot(-rootPartTrans.right, moment2);
            M3 = Vector3.Dot(-rootPartTrans.right, moment3);

            double x_ac = (M0 - M3 + 8 * (M2 - M1)) / (N0 - N3 + 8 * (N2 - N1));
            //double y_ac = pos.x;
            //double z_ac = (M2 - M0) / (X2 - X0);
            //Debug.Log(M2 + " " + M0 + " " + N2 + " " + N0);

            vesselRootLocalAeroCenter = Vector3.up * (float)x_ac;// +rootPartTrans.forward * (float)z_ac;
            avgForcePos *= 0.25f;
            avgForcePos = rootPartTrans.worldToLocalMatrix.MultiplyPoint3x4(avgForcePos);
            vesselRootLocalAeroCenter += Vector3.ProjectOnPlane(avgForcePos, Vector3.up);
            //vesselRootLocalAeroCenter = aeroSection.GetPos();
            //vesselRootLocalAeroCenter = rootPartTrans.worldToLocalMatrix.MultiplyPoint3x4(vesselRootLocalAeroCenter + rootPartTrans.position);
        }
    }
}
