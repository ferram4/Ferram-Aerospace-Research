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
using System.Reflection;
using UnityEngine;
using KSP;
using ProceduralFairings;

namespace FerramAerospaceResearch.FARPartGeometry.GeometryUpdaters
{
    class StockProcFairingGeoUpdater : IGeometryUpdater
    {
        ModuleProceduralFairing fairing;
        GeometryPartModule geoModule;
        List<Bounds> prevPanelBounds;
        KFSMEvent deployEvent;

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
            bool rebuildMesh = false;

            rebuildMesh = prevPanelBounds.Count == panels.Count;        //if bounds count doesn't equal panels count, the number of panels changed

            if (rebuildMesh)
                prevPanelBounds.Clear();

            for (int i = 0; i < panels.Count; i++)      //set them back to where they started to prevent voxelization errors
            {
                panels[i].SetExplodedView(0);
                Bounds panelBounds = panels[i].GetBounds();

                if(i >= prevPanelBounds.Count)      //set new panel bounds
                {
                    rebuildMesh = true;
                    prevPanelBounds.Add(panelBounds);
                }
                else if(panelBounds != prevPanelBounds[i])
                {
                    rebuildMesh = true;
                    prevPanelBounds.Add(panelBounds);
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
                deployEvent = (KFSMEvent)fields[31].GetValue(fairing);
                deployEvent.OnEvent += delegate { FairingDeployGeometryUpdate(); };
            }
        }

        private void FairingDeployGeometryUpdate()
        {
            Debug.Log("Fairing Geometry Update");
            geoModule.GeometryPartModuleRebuildMeshData();
        }
    }
}
