/*
Ferram Aerospace Research v0.15.5.7 "Johnson"
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
            ModularFI.ModularFlightIntegrator.RegisterCalculateAreaExposedOverride(CalculateAreaRadiative);
            ModularFI.ModularFlightIntegrator.RegisterCalculateAreaRadiativeOverride(CalculateAreaRadiative);
            ModularFI.ModularFlightIntegrator.RegisterGetSunAreaOverride(CalculateSunArea);
            ModularFI.ModularFlightIntegrator.RegisterGetBodyAreaOverride(CalculateBodyArea);
            Debug.Log("FAR Modular Flight Integrator function registration complete");
            GameObject.Destroy(this);
        }

        void UpdateThermodynamicsPre(ModularFI.ModularFlightIntegrator fi)
        {
            for (int i = 0; i < fi.PartThermalDataCount; i++)
            {
                PartThermalData ptd = fi.partThermalDataList[i];
                Part part = ptd.part;
                if (!part.Modules.Contains("FARAeroPartModule"))
                    continue;

                PartModule module = part.Modules["FARAeroPartModule"];

                FARAeroPartModule aeroModule = (FARAeroPartModule)module;

                part.radiativeArea = CalculateAreaRadiative(fi, part, aeroModule);
                part.exposedArea = part.machNumber > 0 ? CalculateAreaExposed(fi, part, aeroModule) : part.radiativeArea;

                if (part.exposedArea > part.radiativeArea)
                    part.exposedArea = part.radiativeArea;      //sanity check just in case

                //fi.SetSkinProperties(ptd);
            }
            //fi.timeSinceLastUpdate = 0;
            //Debug.Log("MFI: " + fi.CoM + " " + Planetarium.GetUniversalTime());
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
                Rigidbody rb = part.rb;
                if (rb)
                {
                    part.dragVector = rb.velocity + Krakensbane.GetFrameVelocity() - FARWind.GetWind(FlightGlobals.currentMainBody, part, rb.position);
                    part.dragVectorSqrMag = part.dragVector.sqrMagnitude;
                    if (part.dragVectorSqrMag == 0f)
                    {
                        part.dragVectorMag = 0f;
                        part.dragVectorDir = Vector3.zero;
                        part.dragVectorDirLocal = Vector3.zero;
                        part.dragScalar = 0f;
                    }
                    else
                    {
                        part.dragVectorMag = (float)Math.Sqrt(part.dragVectorSqrMag);
                        part.dragVectorDir = part.dragVector / part.dragVectorMag;
                        part.dragVectorDirLocal = -part.partTransform.InverseTransformDirection(part.dragVectorDir);
                        CalculateLocalDynPresAndAngularDrag(fi, part);
                    }
                    if (!part.DragCubes.None) 
                        part.DragCubes.SetDrag(part.dragVectorDirLocal, (float)fi.mach);
                }
            }

        }

        void CalculateLocalDynPresAndAngularDrag(ModularFI.ModularFlightIntegrator fi, Part p)
        {
            if(fi.CurrentMainBody.ocean && p.submergedPortion > 0)
            {
                p.submergedDynamicPressurekPa = fi.CurrentMainBody.oceanDensity * 1000;
                p.dynamicPressurekPa = p.atmDensity;
            }
            else
            {
                p.submergedDynamicPressurekPa = 0;
                p.dynamicPressurekPa = p.atmDensity;
            }
            double tmp = 0.0005 * p.dragVectorSqrMag;

            p.submergedDynamicPressurekPa *= tmp;
            p.dynamicPressurekPa *= tmp;

            tmp = p.dynamicPressurekPa * (1.0 - p.submergedPortion);
            tmp += p.submergedDynamicPressurekPa * PhysicsGlobals.BuoyancyWaterAngularDragScalar * p.waterAngularDragMultiplier * p.submergedPortion;

            p.rb.angularDrag = (float)(p.angularDrag * tmp * PhysicsGlobals.AngularDragMultiplier);

            tmp = Math.Max(fi.pseudoReDragMult, 1);
            p.dragScalar = (float)((p.dynamicPressurekPa * (1.0 - p.submergedPortion) + p.submergedDynamicPressurekPa * p.submergedPortion) * tmp);       //dyn pres adjusted for submersion
            p.bodyLiftScalar = (float)(p.dynamicPressurekPa * (1.0 - p.submergedPortion) + p.submergedDynamicPressurekPa * p.submergedPortion);
        }

        double CalculateAreaRadiative(ModularFI.ModularFlightIntegrator fi, Part part)
        {
            FARAeroPartModule module = null;
            if (part.Modules.Contains("FARAeroPartModule"))
                module = (FARAeroPartModule)part.Modules["FARAeroPartModule"];

            return CalculateAreaRadiative(fi, part, module);
        }
        
        double CalculateAreaRadiative(ModularFI.ModularFlightIntegrator fi, Part part, FARAeroPartModule aeroModule)
        {
            //double dragCubeExposed = fi.BaseFICalculateAreaExposed(part);
            if ((object)aeroModule != null)
            {
                double radArea = aeroModule.ProjectedAreas.totalArea;

                if (radArea > 0)
                    return radArea;
                else
                    return fi.BaseFICalculateAreaRadiative(part);
            }
            else
            {
                return fi.BaseFICalculateAreaRadiative(part);
            }
        }

        double CalculateAreaExposed(ModularFI.ModularFlightIntegrator fi, Part part)
        {
            FARAeroPartModule module = null;
            if (part.Modules.Contains("FARAeroPartModule"))
                module = (FARAeroPartModule)part.Modules["FARAeroPartModule"];

            return CalculateAreaExposed(fi, part, module);
        }

        double CalculateAreaExposed(ModularFI.ModularFlightIntegrator fi, Part part, FARAeroPartModule aeroModule)
        {
            if ((object)aeroModule != null)
            {
                double exposedArea = aeroModule.ProjectedAreaLocal(-part.dragVectorDirLocal);

                if (exposedArea > 0)
                    return exposedArea;
                else
                    return fi.BaseFICalculateAreaExposed(part); 
            }
            else
                return fi.BaseFICalculateAreaExposed(part);
            /*else
            {
                if (stockRadArea > 0)
                    return aeroModule.ProjectedAreas.totalArea * dragCubeExposed / stockRadArea;
                else
                    return aeroModule.ProjectedAreas.totalArea;
            }*/
        }

        double CalculateSunArea(ModularFI.ModularFlightIntegrator fi, PartThermalData ptd)
        {
            FARAeroPartModule module = null;
            if (ptd.part.Modules.Contains("FARAeroPartModule"))
                module = (FARAeroPartModule)ptd.part.Modules["FARAeroPartModule"];

            if ((object)module != null)
            {
                double sunArea = module.ProjectedAreaWorld(fi.sunVector) * ptd.sunAreaMultiplier;

                if (sunArea > 0)
                    return sunArea;
                else
                    return fi.BaseFIGetSunArea(ptd);
            }
            else
                return fi.BaseFIGetSunArea(ptd);
        }

        double CalculateBodyArea(ModularFI.ModularFlightIntegrator fi, PartThermalData ptd)
        {
            FARAeroPartModule module = null;
            if (ptd.part.Modules.Contains("FARAeroPartModule"))
                module = (FARAeroPartModule)ptd.part.Modules["FARAeroPartModule"];

            if ((object)module != null)
            {
                double bodyArea = module.ProjectedAreaWorld(-fi.Vessel.upAxis) * ptd.bodyAreaMultiplier;

                if (bodyArea > 0)
                    return bodyArea;
                else
                    return fi.BaseFIBodyArea(ptd);
            }
            else
                return fi.BaseFIBodyArea(ptd);
        }
    }
}
