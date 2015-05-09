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
using UnityEngine;

namespace FerramAerospaceResearch.FARAeroComponents
{
    //Engines handle ram drag at full throttle, but as throttle drops so does ram drag
    //This attempts some manner of handling ram drag at various speeds
    class VesselIntakeRamDrag
    {
        const float AVG_NOZZLE_VEL_RELATIVE_TO_FREESTREAM = 0.25f;       //assume value approximately for turbojets
        const float AVG_NOZZLE_VEL_FACTOR = AVG_NOZZLE_VEL_RELATIVE_TO_FREESTREAM * (1 - AVG_NOZZLE_VEL_RELATIVE_TO_FREESTREAM);

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

            for(int i = 0; i < allUsedAeroModules.Count; i++)       //get all exposed intakes and engines
            {
                FARAeroPartModule aeroModule = allUsedAeroModules[i];
                if (aeroModule == null)
                    continue;
                Part p = aeroModule.part;

                if(p.Modules.Contains("ModuleResourceIntake"))
                {
                    ModuleResourceIntake intake = (ModuleResourceIntake)p.Modules["ModuleResourceIntake"];
                    _aeroModulesWithIntakes.Add(aeroModule);
                    _intakeModules.Add(intake);
                    _intakeTransforms.Add(p.FindModelTransform(intake.intakeTransformName));
                }
                if(p.Modules.Contains("ModuleEngines"))
                {
                    ModuleEngines engines = (ModuleEngines)p.Modules["ModuleEngines"];
                    for (int j = 0; j < engines.propellants.Count; j++)
                    {
                        Propellant prop = engines.propellants[j];
                        if (prop.name == "IntakeAir")
                        {
                            _airBreathingEngines.Add(engines);
                            break;
                        }
                    }
                }
                if (p.Modules.Contains("ModuleEnginesFX"))
                {
                    ModuleEnginesFX engines = (ModuleEnginesFX)p.Modules["ModuleEnginesFX"];
                    for (int j = 0; j < engines.propellants.Count; j++)
                    {
                        Propellant prop = engines.propellants[j];
                        if (prop.name == "IntakeAir")
                        {
                            _airBreathingEngines.Add(engines);
                            break;
                        }
                    }
                }
            }

            for(int i = 0; i < allUnusedAeroModules.Count; i++)     //get all covered airbreathing Engines
            {
                FARAeroPartModule aeroModule = allUnusedAeroModules[i];
                if (aeroModule == null)
                    continue;
                Part p = aeroModule.part;
                if (p.Modules.Contains("ModuleEngines"))
                {
                    ModuleEngines engines = (ModuleEngines)p.Modules["ModuleEngines"];
                    for (int j = 0; j < engines.propellants.Count; j++)
                    {
                        Propellant prop = engines.propellants[j];
                        if (prop.name == "IntakeAir")
                        {
                            _airBreathingEngines.Add(engines);
                            break;
                        }
                    }
                }
                if (p.Modules.Contains("ModuleEnginesFX"))
                {
                    ModuleEnginesFX engines = (ModuleEnginesFX)p.Modules["ModuleEnginesFX"];
                    for (int j = 0; j < engines.propellants.Count; j++)
                    {
                        Propellant prop = engines.propellants[j];
                        if (prop.name == "IntakeAir")
                        {
                            _airBreathingEngines.Add(engines);
                            break;
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
            currentThrottle /= (float)_airBreathingEngines.Count;

            float currentRamDrag = RamDragPerArea(machNumber);
            currentRamDrag *= 1f - currentThrottle;

            return currentRamDrag;
        }

        private void ApplyIntakeDrag(float currentRamDrag, Vector3 vesselVelNorm, float dynPres)
        {
            for(int i = 0; i < _intakeTransforms.Count; i++)
            {
                ModuleResourceIntake intake = _intakeModules[i];
                if (!intake.intakeEnabled)
                    continue;

                Transform transform = _intakeTransforms[i];
                if(transform == null)
                {
                    _intakeModules.RemoveAt(i);
                    _intakeTransforms.RemoveAt(i);
                    _aeroModulesWithIntakes.RemoveAt(i);
                    --i;
                    continue;
                }

                float cosAoA = Vector3.Dot(_intakeTransforms[i].forward, vesselVelNorm);
                if (cosAoA < 0)
                    cosAoA = 0;

                if (cosAoA <= intake.aoaThreshold)
                    continue;

                FARAeroPartModule aeroModule = _aeroModulesWithIntakes[i];

                aeroModule.AddLocalForce(-aeroModule.partLocalVelNorm * dynPres * cosAoA * currentRamDrag * intake.area * 100, Vector3.zero);
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
