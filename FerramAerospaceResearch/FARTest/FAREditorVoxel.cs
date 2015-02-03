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
using UnityEngine;
using KSP;
using FerramAerospaceResearch.FARPartGeometry;

namespace FerramAerospaceResearch.FARTest
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    class FAREditorVoxel : MonoBehaviour
    {
        Rect windowPos;
        VehicleVoxel voxel;
        void OnGUI()
        {
            windowPos = GUILayout.Window(this.GetHashCode(), windowPos, TestGUI, "FARTest");
        }

        void TestGUI(int id)
        {
            if(EditorLogic.RootPart)
            {
                if(GUILayout.Button("Voxelize Vessel"))
                    CreateVoxel(EditorLogic.SortedShipList);
                if (GUILayout.Button("Visualize Voxel") && voxel != null)
                    voxel.VisualizeVoxel(EditorLogic.RootPart.transform.position);
            }
        }

        void CreateVoxel(List<Part> partList)
        {
            voxel = null;
            voxel = new VehicleVoxel(partList, 25000);
            float[] crossSectionArea = voxel.CrossSectionalArea(Vector3.up);

            ConfigNode node = new ConfigNode("Cross Section Dump");
            for (int i = 0; i < crossSectionArea.Length; i++)
                node.AddValue(i.ToString(), crossSectionArea[i].ToString());

            string savePath = KSPUtil.ApplicationRootPath.Replace("\\", "/") + "GameData/FerramAerospaceResearch/CrossSectionTest.cfg";
            node.Save(savePath);
        }
    }
}
