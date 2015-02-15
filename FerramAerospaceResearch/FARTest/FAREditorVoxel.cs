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
using System.Threading;
using System.Diagnostics;
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
        string voxelCount = "25000";
        string timeToGenerate = "";
        bool multiThreaded = true;
        bool solidify = true;
        bool visualize = false;

        Stopwatch watch = new Stopwatch();

        string x = "0", y = "1", z = "0";
        bool toggleCrossSectionStuff = false;

        void OnGUI()
        {
            if (EditorLogic.RootPart)
            {
                windowPos = GUILayout.Window(this.GetHashCode(), windowPos, TestGUI, "FARTest");
            }
        }

        void TestGUI(int id)
        {

            voxelCount = GUILayout.TextField(voxelCount);
            multiThreaded = GUILayout.Toggle(multiThreaded, "Multithread Voxelization");
            solidify = GUILayout.Toggle(solidify, "Toggle Solidification");
            if (GUILayout.Button("Voxelize Vessel"))
            {
                if (voxel != null)
                {
                    if (visualize)
                    {
                        voxel.ClearVisualVoxels();
                        visualize = false;
                    }
                    voxel = null;
                }
                ThreadPool.QueueUserWorkItem(CreateVoxel, EditorLogic.SortedShipList);
            }
            if (voxel != null)
            {
                GUILayout.Label(timeToGenerate);
                if (GUILayout.Button("CrossSection Calc"))
                {
                    toggleCrossSectionStuff = !toggleCrossSectionStuff;
                    GUILayout.Height(0);
                }
                if (toggleCrossSectionStuff)
                {
                    GUILayout.Label("Velocity components");
                    GUILayout.BeginHorizontal();
                    x = GUILayout.TextField(x);
                    y = GUILayout.TextField(y);
                    z = GUILayout.TextField(z);
                    GUILayout.EndHorizontal();

                    if (GUILayout.Button("Dump Voxel Data"))
                        DumpVoxelData();
                }
                if (visualize)
                {
                    if (GUILayout.Button("Clear Visualization"))
                    {
                        voxel.ClearVisualVoxels();
                        visualize = false;
                    }
                }
                else
                    if (GUILayout.Button("Visualize Voxel"))
                    {
                        voxel.VisualizeVoxel(EditorLogic.RootPart.transform.position);
                        visualize = true;
                    }
            }

            GUI.DragWindow();
        }

        void CreateVoxel(object partList)
        {
            voxel = null;
            watch.Reset();
            watch.Start();
            int count;
            if(int.TryParse(voxelCount, out count))
            {
                VehicleVoxel newvoxel = new VehicleVoxel((List<Part>)partList, count, multiThreaded, solidify);
                voxel = newvoxel;
            }
            watch.Stop();
            timeToGenerate = watch.ElapsedMilliseconds.ToString() + " ms";
        }

        void DumpVoxelData()
        {
            VoxelCrossSection[] crossSections = new VoxelCrossSection[voxel.MaxArrayLength];
            Vector3 vel = new Vector3(float.Parse(x), float.Parse(y), float.Parse(z));

            watch.Reset();
            watch.Start();
            int frontIndex, backIndex;
            float sectionThickness;
            voxel.CrossSectionData(crossSections, vel, out frontIndex, out backIndex, out sectionThickness);
            watch.Stop();

            string initialCost = watch.ElapsedMilliseconds.ToString();

            watch.Reset();

            watch.Start();
            for(int i = 0; i < 50; i++)
                voxel.CrossSectionData(crossSections, vel, out frontIndex, out backIndex, out sectionThickness);
            watch.Stop();

            ConfigNode node = new ConfigNode("Cross Section Dump");
            for (int i = 0; i < crossSections.Length; i++)
                node.AddValue(i.ToString(), crossSections[i].area.ToString() + " " + crossSections[i].area_deriv1 + " " + crossSections[i].area_deriv2);

            node.AddValue("frontIndex", frontIndex);
            node.AddValue("backIndex", backIndex);
            node.AddValue("sectionThickness", sectionThickness);

            node.AddValue("initial time", initialCost + " ms");
            node.AddValue("repeated time avg", (float)watch.ElapsedMilliseconds / 50f + " ms");

            string savePath = KSPUtil.ApplicationRootPath.Replace("\\", "/") + "GameData/FerramAerospaceResearch/CrossSectionTest.cfg";
            node.Save(savePath);
        }
    }
}
