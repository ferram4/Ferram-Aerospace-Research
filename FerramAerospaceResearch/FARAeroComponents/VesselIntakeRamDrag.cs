/*
Ferram Aerospace Research v0.15.5.2 "Helmbold"
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

namespace FerramAerospaceResearch.FARAeroComponents
{
    //Engines handle ram drag at full throttle, but as throttle drops so does ram drag
    //This attempts some manner of handling ram drag at various speeds
    class VesselIntakeRamDrag
    {
        const float AVG_NOZZLE_VEL_RELATIVE_TO_FREESTREAM = 0.25f;       //assume value approximately for turbojets
        const float AVG_NOZZLE_VEL_FACTOR = AVG_NOZZLE_VEL_RELATIVE_TO_FREESTREAM * (1 - AVG_NOZZLE_VEL_RELATIVE_TO_FREESTREAM);

        static int AJE_JET_CLASS_ID = "ModuleEnginesAJEJet".GetHashCode();
        static int AJE_PROP_CLASS_ID = "ModuleEnginesAJEPropeller".GetHashCode();


        List<FARAeroPartModule> _aeroModulesWithIntakes = new List<FARAeroPartModule>();
        List<ModuleResourceIntake> _intakeModules = new List<ModuleResourceIntake>();
        List<Transform> _intakeTransforms = new List<Transform>();
        List<ModuleEngines> _airBreathingEngines = new List<ModuleEngines>();

        public void UpdateAeroData(List<FARAeroPartModule> allUsedAeroModules, List<FARAeroPartModule> allUnusedAeroModules)
        {
            _aeroModulesWithIntakes.Clear();
            _intakeModules.Clear();
            _intakeTransforms.Clear();
            _airBreathingEngines.Clear();

            HashSet<string> intakeResourceNames = new HashSet<string>();


            for (int i = 0; i < allUsedAeroModules.Count; i++)       //get all exposed intakes
            {
                FARAeroPartModule aeroModule = allUsedAeroModules[i];
                if (aeroModule == null)
                    continue;
                Part p = aeroModule.part;

                for (int j = 0; j < p.Modules.Count; j++)
                {
                    PartModule m = p.Modules[j];
                    if (m is ModuleResourceIntake)
                    {
                        ModuleResourceIntake intake = (ModuleResourceIntake)m;

                        if (intake.node != null && intake.node.attachedPart != null)
                            continue;

                        _aeroModulesWithIntakes.Add(aeroModule);
                        _intakeModules.Add(intake);
                        _intakeTransforms.Add(p.FindModelTransform(intake.intakeTransformName));
                        if (!intakeResourceNames.Contains(intake.resourceName))
                            intakeResourceNames.Add(intake.resourceName);
                    }
                }
            }


            for (int i = 0; i < allUsedAeroModules.Count; i++)       //get all exposed engines
            {
                FARAeroPartModule aeroModule = allUsedAeroModules[i];
                if (aeroModule == null)
                    continue;
                Part p = aeroModule.part;

                for (int j = 0; j < p.Modules.Count; j++)
                {
                    PartModule m = p.Modules[j];

                    if (m is ModuleEngines)
                    {
                        ModuleEngines e = (ModuleEngines)m;
                        if (FARAeroUtil.AJELoaded)
                            if (m.ClassID == AJE_JET_CLASS_ID || m.ClassID == AJE_PROP_CLASS_ID)
                            {
                                _airBreathingEngines.Add(e);
                                continue;
                            }

                        for (int k = 0; k < e.propellants.Count; k++)
                        {
                            Propellant prop = e.propellants[k];
                            if (intakeResourceNames.Contains(prop.name))
                            {
                                _airBreathingEngines.Add(e);
                                break;
                            }
                        }
                    }
                }
            }
        }

        public void ApplyIntakeRamDrag(float machNumber, Vector3 vesselVelNorm, float dynPres)
        {
            float currentRamDrag = CalculateRamDrag(machNumber);
            ApplyIntakeDrag(currentRamDrag, vesselVelNorm, dynPres);
        }

        private float CalculateRamDrag(float machNumber)
        {
            float currentThrottle = 0;

            for (int i = 0; i < _airBreathingEngines.Count; i++)
            {
                currentThrottle += _airBreathingEngines[i].currentThrottle;
            }
            currentThrottle /= Math.Max((float)_airBreathingEngines.Count, 1);

            if (currentThrottle > 0.5)
                return 0;

            float currentRamDrag = RamDragPerArea(machNumber);
            currentRamDrag *= 1f - 2f * currentThrottle;

            return currentRamDrag;
        }

        private void ApplyIntakeDrag(float currentRamDrag, Vector3 vesselVelNorm, float dynPres)
        {
            for (int i = _intakeTransforms.Count - 1; i >= 0; i--)
            {
                ModuleResourceIntake intake = _intakeModules[i];
                if (!intake.intakeEnabled)
                    continue;

                Transform transform = _intakeTransforms[i];
                if (transform == null)
                {
                    _intakeModules.RemoveAt(i);
                    _intakeTransforms.RemoveAt(i);
                    _aeroModulesWithIntakes.RemoveAt(i);
                    ++i;
                    continue;
                }

                float cosAoA = Vector3.Dot(_intakeTransforms[i].forward, vesselVelNorm);
                if (cosAoA < 0)
                    cosAoA = 0;

                if (cosAoA <= 0)
                    continue;

                FARAeroPartModule aeroModule = _aeroModulesWithIntakes[i];


                Vector3 force = -aeroModule.partLocalVelNorm * cosAoA * currentRamDrag * (float)intake.area * 100f;
                //if(float.IsNaN(force.sqrMagnitude))
                //    force = Vector3.zero;
                aeroModule.AddLocalForce(force, Vector3.zero);
            }
        }

        private float RamDragPerArea(float machNumber)
        {
            float drag = machNumber * machNumber;
            ++drag;
            drag = 2f / drag;
            drag *= AVG_NOZZLE_VEL_FACTOR;  //drag based on the nozzle

            drag += 0.1f;           //drag based on inlet
                                    //assuming inlet and nozzle area are equal
            
            return drag;
        }
    }
}
