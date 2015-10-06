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
            ModularFI.ModularFlightIntegrator.RegisterUpdateAerodynamicsOverride(UpdateAerodynamics);
            ModularFI.ModularFlightIntegrator.RegisterUpdateThermodynamicsPre(UpdateThermodynamicsPre);
            Debug.Log("FAR Modular Flight Integrator function registration complete");
            GameObject.Destroy(this);
        }

        void UpdateThermodynamicsPre(ModularFI.ModularFlightIntegrator fi)
        {
            for (int i = 0; i < fi.PartThermalDataCount; i++)
            {
                Part part = fi.partThermalDataList[i].part;
                if (!part.Modules.Contains("FARAeroPartModule"))
                    continue;

                PartModule module = part.Modules["FARAeroPartModule"];

                FARAeroPartModule aeroModule = (FARAeroPartModule)module;

                double stockRadArea = fi.BaseFICalculateAreaRadiative(part);

                part.radiativeArea = CalculateAreaRadiative(fi, part, aeroModule);
                part.exposedArea = part.machNumber > 0 ? CalculateAreaExposed(fi, part, aeroModule) : part.radiativeArea;

                if (part.radiativeArea < stockRadArea)
                    SkinThermalMassShenanigansForShieldedParts(fi, part, stockRadArea, part.radiativeArea);     //very hacky method to deal with the fact that stock assumes that radiative area is also the skin area for the part.  This causes issues for parts that cannot radiate to the environment because they are completely enclosed

                if (part.exposedArea > part.radiativeArea)
                    part.exposedArea = part.radiativeArea;      //sanity check just in case
            }
            //Debug.Log("MFI: " + fi.CoM + " " + Planetarium.GetUniversalTime());
        }

        void SkinThermalMassShenanigansForShieldedParts(ModularFI.ModularFlightIntegrator fi, Part part, double stockRadArea, double calculatedArea)
        {
            part.thermalMass += part.skinThermalMass;   //reset overall thermalmass

            part.radiativeArea = stockRadArea;          //set rad area to stock values
            fi.SetSkinThermalMass(part);                //re-run setting skin thermal mass

            part.thermalMass -= part.skinThermalMass;   //re-subtract skin thermal mass
            part.thermalMassReciprocal = 1.0 / Math.Max(part.thermalMass, 0.001);   //reset thermalMassRecip

            //part.radiativeArea = calculatedArea;      //I doubt that this ever caused a problem, but let's be sure
        }

        void UpdateAerodynamics(ModularFI.ModularFlightIntegrator fi, Part part)
        {
            if (part.dragModel != Part.DragModel.CYLINDRICAL || part.vessel.isEVA)     //FIXME Proper model for airbrakes
            {
                fi.BaseFIUpdateAerodynamics(part);
                return;
            }
            else
            {
                Rigidbody rb = part.Rigidbody;
                if (rb)
                {
                    part.dragVectorDir = (-rb.velocity - Krakensbane.GetFrameVelocityV3f()).normalized;
                    part.dragVectorDirLocal = part.partTransform.worldToLocalMatrix.MultiplyVector(part.dragVectorDir);
                    if (!part.DragCubes.None) 
                        part.DragCubes.SetDrag(part.dragVectorDirLocal, (float)part.machNumber);
                }
            }

        }

        double CalculateAreaRadiative(ModularFI.ModularFlightIntegrator fi, Part part, FARAeroPartModule aeroModule)
        {
            //double dragCubeExposed = fi.BaseFICalculateAreaExposed(part);
            if ((object)aeroModule == null)
                return fi.BaseFICalculateAreaRadiative(part);
            else
            {
                return aeroModule.ProjectedAreas.totalArea;
            }
        }

        double CalculateAreaExposed(ModularFI.ModularFlightIntegrator fi, Part part, FARAeroPartModule aeroModule)
        {
            if ((object)aeroModule == null)
                return fi.BaseFICalculateAreaExposed(part);
            else
                return aeroModule.ProjectedAreaLocal(-part.dragVectorDirLocal);
            /*else
            {
                if (stockRadArea > 0)
                    return aeroModule.ProjectedAreas.totalArea * dragCubeExposed / stockRadArea;
                else
                    return aeroModule.ProjectedAreas.totalArea;
            }*/
        }
    }
}
