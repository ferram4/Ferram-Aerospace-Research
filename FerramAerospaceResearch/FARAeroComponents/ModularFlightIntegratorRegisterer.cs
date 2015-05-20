/*
Ferram Aerospace Research v0.15.2 "Ferri"
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
using ModularFI;
using UnityEngine;

namespace FerramAerospaceResearch.FARAeroComponents
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    public class ModularFlightIntegratorRegisterer : MonoBehaviour
    {
        void Start()
        {
            Debug.Log("FAR Modular Flight Integrator function registration started");
            ModularFlightIntegrator.RegisterUpdateAerodynamicsOverride(UpdateAerodynamics);
            Debug.Log("FAR Modular Flight Integrator function registration complete");
            GameObject.Destroy(this);
        }

        void UpdateAerodynamics(ModularFlightIntegrator fi, Part part)
        {            
            if (part.Modules.Contains("ModuleAeroSurface") || part.Modules.Contains("KerbalEVA"))     //FIXME Proper model for airbrakes
            {
                fi.BaseFIUpdateAerodynamics(part);
                return;
            }
            else if (!part.DragCubes.None)
            {
                Rigidbody rb = part.Rigidbody;
                if (rb)
                    part.DragCubes.SetDrag(-part.partTransform.worldToLocalMatrix.MultiplyVector(rb.velocity + Krakensbane.GetFrameVelocityV3f()).normalized, (float)part.machNumber);
            }

            FARAeroPartModule aeroModule = part.GetComponent<FARAeroPartModule>();

            part.radiativeArea = CalculateAreaRadiative(fi, part, aeroModule);
            part.exposedArea = CalculateAreaExposed(fi, part, aeroModule);
        }

        double CalculateAreaRadiative(ModularFlightIntegrator fi, Part part, FARAeroPartModule aeroModule)
        {
            //double dragCubeExposed = fi.BaseFICalculateAreaExposed(part);
            if ((object)aeroModule == null)
                return fi.BaseFICalculateAreaRadiative(part);
            else
            {
                return aeroModule.ProjectedAreas.totalArea;
            }
        }

        double CalculateAreaExposed(ModularFlightIntegrator fi, Part part, FARAeroPartModule aeroModule)
        {
            double dragCubeExposed = fi.BaseFICalculateAreaExposed(part);
            if (aeroModule == null)
                return dragCubeExposed;
            else
            {
                double cubeRadiative = fi.BaseFICalculateAreaRadiative(part);
                if (cubeRadiative > 0)
                    return aeroModule.ProjectedAreas.totalArea * dragCubeExposed / cubeRadiative;
                else
                    return aeroModule.ProjectedAreas.totalArea;
            }
        }
    }
}
