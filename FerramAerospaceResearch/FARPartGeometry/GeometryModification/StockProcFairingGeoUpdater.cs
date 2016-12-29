/*
Ferram Aerospace Research v0.15.7.2 "Lanchester"
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
using System.Reflection;
using UnityEngine;
using KSP;
using ProceduralFairings;

namespace FerramAerospaceResearch.FARPartGeometry.GeometryModification
{
    class StockProcFairingGeoUpdater : IGeometryUpdater
    {
        ModuleProceduralFairing fairing;
        GeometryPartModule geoModule;
        List<Bounds> prevPanelBounds;
        KFSMEvent deployEvent;
        KFSMEvent breakEvent;

        public StockProcFairingGeoUpdater(ModuleProceduralFairing fairing, GeometryPartModule geoModule)
        {
            this.fairing = fairing;
            this.geoModule = geoModule;

            if (HighLogic.LoadedSceneIsEditor)
                prevPanelBounds = new List<Bounds>();
        }

        public void EditorGeometryUpdate()
        {
            List<FairingPanel> panels = fairing.Panels;
            if (panels == null)
                return;

            bool rebuildMesh = prevPanelBounds.Count == panels.Count;        //if bounds count doesn't equal panels count, the number of panels changed

            if (rebuildMesh)
                prevPanelBounds.Clear();

            for (int i = 0; i < panels.Count; i++)      //set them back to where they started to prevent voxelization errors
            {
                FairingPanel p = panels[i];
                Bounds panelBounds = new Bounds();
                if (p != null)
                {
                    p.SetExplodedView(0);
                    p.SetOpacity(1);
                    p.SetTgtExplodedView(0);
                    p.SetTgtOpacity(1);
                    if(p.ColliderContainer)
                        p.ColliderContainer.SetActive(true);

                    panelBounds = p.GetBounds();
                }
                if(i >= prevPanelBounds.Count)      //set new panel bounds
                {
                    rebuildMesh = true;
                    prevPanelBounds.Add(panelBounds);
                }
                else if(panelBounds != prevPanelBounds[i])
                {
                    rebuildMesh = true;
                    prevPanelBounds[i] = (panelBounds);
                }
            }

            if (rebuildMesh)
                geoModule.RebuildAllMeshData();

        }

        public void FlightGeometryUpdate()
        {
            if (deployEvent == null)
            {
                Debug.Log("Update fairing event");
                FieldInfo[] fields = fairing.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
                bool deployBool = false, breakBool = false;

                for (int i = 0; i < fields.Length; ++i)
                {
                    FieldInfo field = fields[i];
                    if (field.Name.ToLowerInvariant() == "on_deploy")
                    {
                        deployEvent = (KFSMEvent)field.GetValue(fairing);
                        deployEvent.OnEvent += delegate { FairingDeployGeometryUpdate(); };
                        deployBool = true;
                    }
                    else if (field.Name.ToLowerInvariant() == "on_breakoff")
                    {
                        breakEvent = (KFSMEvent)field.GetValue(fairing);
                        breakEvent.OnEvent += delegate { FairingDeployGeometryUpdate(); };
                        breakBool = true;
                    }
                    if (deployBool && breakBool)
                        break;
                }
            }
        }

        private void FairingDeployGeometryUpdate()
        {
            Debug.Log("Fairing Geometry Update");
            geoModule.GeometryPartModuleRebuildMeshData();
        }
    }
}
