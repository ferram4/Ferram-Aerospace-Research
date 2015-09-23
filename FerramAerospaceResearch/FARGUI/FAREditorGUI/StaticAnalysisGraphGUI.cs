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
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using FerramAerospaceResearch.FARGUI.FAREditorGUI.Simulation;
using FerramAerospaceResearch.FARAeroComponents;
using ferram4;

namespace FerramAerospaceResearch.FARGUI.FAREditorGUI
{
    class StaticAnalysisGraphGUI : IDisposable
    {
        ferramGraph _graph = new ferramGraph(400, 350);

        double lastMaxBounds, lastMinBounds;
        bool isMachMode = false;

        GraphInputs aoASweepInputs, machSweepInputs;
        GUIDropDown<int> flapSettingDropdown;
        GUIDropDown<CelestialBody> bodySettingDropdown;
        EditorSimManager simManager;

        public StaticAnalysisGraphGUI(EditorSimManager simManager, GUIDropDown<int> flapSettingDropDown, GUIDropDown<CelestialBody> bodySettingDropdown)
        {
            this.simManager = simManager;
            this.flapSettingDropdown = flapSettingDropDown;
            this.bodySettingDropdown = bodySettingDropdown;

            //Set up defaults for AoA Sweep
            aoASweepInputs = new GraphInputs();
            aoASweepInputs.lowerBound = "0";
            aoASweepInputs.upperBound = "25";
            aoASweepInputs.numPts = "100";
            aoASweepInputs.flapSetting = 0;
            aoASweepInputs.pitchSetting = "0";
            aoASweepInputs.otherInput = "0.2";

            //Set up defaults for Mach Sweep
            machSweepInputs = new GraphInputs();
            machSweepInputs.lowerBound = "0";
            machSweepInputs.upperBound = "3";
            machSweepInputs.numPts = "100";
            machSweepInputs.flapSetting = 0;
            machSweepInputs.pitchSetting = "0";
            machSweepInputs.otherInput = "2";

            _graph.SetBoundaries(0, 25, 0, 2);
            _graph.SetGridScaleUsingValues(5, 0.5);
            _graph.horizontalLabel = "Angle of Attack, degrees";
            _graph.verticalLabel = "Cl\nCd\nCm\nL/D / 10";
            _graph.Update();
        }

        public void Dispose()
        {
            aoASweepInputs = machSweepInputs = null;
            flapSettingDropdown = null;
            bodySettingDropdown = null;
            simManager = null;
            _graph = null;
        }

        public void Display()
        {
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label(isMachMode ? "Mach Number Sweep Analysis" : "Angle of Attack Sweep Analysis", GUILayout.Width(250));
            if (GUILayout.Button(isMachMode ? "Switch To AoA Sweep" : "Switch To Mach Sweep"))
                isMachMode = !isMachMode;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GraphDisplay();
            RightGraphInputsGUI(isMachMode ? machSweepInputs : aoASweepInputs);

            GUILayout.EndHorizontal();

            BelowGraphInputsGUI(isMachMode ? machSweepInputs : aoASweepInputs);

            GUILayout.EndVertical();
        }

        private void GraphDisplay()
        {
            GUIStyle graphBackingStyle = new GUIStyle(GUI.skin.box);
            graphBackingStyle.hover = graphBackingStyle.active = graphBackingStyle.normal;

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(540));

            _graph.Display(graphBackingStyle, 0, 0);

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void RightGraphInputsGUI(GraphInputs input)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Celestial Body:");
            bodySettingDropdown.GUIDropDownDisplay();

            GUILayout.Label("Flap Setting:");
            flapSettingDropdown.GUIDropDownDisplay();
            input.flapSetting = flapSettingDropdown.ActiveSelection;
            GUILayout.Label("Pitch Setting:");
            input.pitchSetting = GUILayout.TextField(input.pitchSetting, GUILayout.ExpandWidth(true));
            input.pitchSetting = Regex.Replace(input.pitchSetting, @"[^-?[0-9]*(\.[0-9]*)?]", "");
            GUILayout.Label("Spoilers:");
            input.spoilers = GUILayout.Toggle(input.spoilers, input.spoilers ? "Deployed" : "Retracted");

            GUILayout.EndVertical();
        }

        private void BelowGraphInputsGUI(GraphInputs input)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label("Lower: ", GUILayout.Width(50.0F), GUILayout.Height(25.0F));
            input.lowerBound = GUILayout.TextField(input.lowerBound, GUILayout.ExpandWidth(true));
            GUILayout.Label("Upper: ", GUILayout.Width(50.0F), GUILayout.Height(25.0F));
            input.upperBound = GUILayout.TextField(input.upperBound, GUILayout.ExpandWidth(true));
            GUILayout.Label("Num Pts: ", GUILayout.Width(70.0F), GUILayout.Height(25.0F));
            input.numPts = GUILayout.TextField(input.numPts, GUILayout.ExpandWidth(true));
            GUILayout.Label(isMachMode ? "AoA" : "Mach", GUILayout.Width(50.0F), GUILayout.Height(25.0F));
            input.otherInput = GUILayout.TextField(input.otherInput, GUILayout.ExpandWidth(true));

            if (GUILayout.Button(isMachMode ? "Sweep Mach" : "Sweep AoA", GUILayout.Width(100.0F), GUILayout.Height(25.0F)))
            {
                input.lowerBound = Regex.Replace(input.lowerBound, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                input.upperBound = Regex.Replace(input.upperBound, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                input.numPts = Regex.Replace(input.numPts, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                input.pitchSetting = Regex.Replace(input.pitchSetting, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                input.otherInput = Regex.Replace(input.otherInput, @"[^-?[0-9]*(\.[0-9]*)?]", "");

                double lowerBound, upperBound, numPts, pitchSetting, otherInput;

                lowerBound = double.Parse(input.lowerBound);
                lowerBound = FARMathUtil.Clamp(lowerBound, -90, 90);
                input.lowerBound = lowerBound.ToString();

                upperBound = double.Parse(input.upperBound);
                upperBound = FARMathUtil.Clamp(upperBound, lowerBound, 90);
                input.upperBound = upperBound.ToString();

                numPts = double.Parse(input.numPts);
                numPts = Math.Ceiling(numPts);
                input.numPts = numPts.ToString();

                pitchSetting = double.Parse(input.pitchSetting);
                pitchSetting = FARMathUtil.Clamp(pitchSetting, -1, 1);
                input.pitchSetting = pitchSetting.ToString();

                otherInput = double.Parse(input.otherInput);

                GraphData data;

                if (isMachMode)
                    data = simManager.SweepSim.MachNumberSweep(otherInput, pitchSetting, lowerBound, upperBound, (int)numPts, input.flapSetting, input.spoilers, bodySettingDropdown.ActiveSelection);
                else
                    data = simManager.SweepSim.AngleOfAttackSweep(otherInput, pitchSetting, lowerBound, upperBound, (int)numPts, input.flapSetting, input.spoilers, bodySettingDropdown.ActiveSelection);

                UpdateGraph(data, isMachMode ? "Mach Number" : "Angle of Attack, degrees", "Cl\nCd\nCm\nL/D / 10", lowerBound, upperBound);
            }
            GUILayout.EndHorizontal();
        }

        private void UpdateGraph(GraphData data, string horizontalLabel, string verticalLabel, double lowerBound, double upperBound)
        {
            double newMinBounds = double.PositiveInfinity;
            double newMaxBounds = double.NegativeInfinity;

            for(int i = 0; i < data.yValues.Count; i++)
            {
                newMinBounds = Math.Min(newMinBounds, data.yValues[i].Min());
                newMaxBounds = Math.Max(newMaxBounds, data.yValues[i].Max());
            }

            // To allow switching between two graph setups to observe differences,
            // use both the current and the previous shown graph to estimate scale
            double minBounds = Math.Min(lastMinBounds, newMinBounds);
            double maxBounds = Math.Max(lastMaxBounds, newMaxBounds);
            lastMaxBounds = newMaxBounds;
            lastMinBounds = newMinBounds;

            double realMin = Math.Min(Math.Floor(minBounds), -0.25);
            double realMax = Math.Max(Math.Ceiling(maxBounds), 0.25);

            _graph.Clear();
            _graph.SetBoundaries(lowerBound, upperBound, realMin, realMax);
            _graph.SetGridScaleUsingValues(5, 0.5);

            for (int i = 0; i < data.yValues.Count; i++)
            {
                _graph.AddLine(data.lineNames[i], data.xValues, data.yValues[i], data.lineColors[i], 1, data.lineNameVisible[i]);
            }
            for (int i = 0; i < data.yValues.Count; i++)
            {
                AddZeroMarks(data.lineNames[i], data.xValues, data.yValues[i], upperBound - lowerBound, realMax - realMin, data.lineColors[i]);
            }

            _graph.horizontalLabel = horizontalLabel;
            _graph.verticalLabel = verticalLabel;// "Cl\nCd\nCm\nL/D / 10";
            _graph.Update();
        }

        private void AddZeroMarks(String key, double[] x, double[] y, double xsize, double ysize, Color color)
        {
            int j = 0;

            for (int i = 0; i < y.Length - 1; i++)
            {
                if (Math.Sign(y[i]) == Math.Sign(y[i + 1]))
                    continue;

                /*// Don't display if slope is good enough?..
                float dx = Mathf.Abs(x[i+1]-x[i])*400/xsize;
                float dy = Mathf.Abs(y[i+1]-y[i])*275/ysize;
                if (dx <= 2*dy)
                    continue;*/

                double xv = x[i] + Math.Abs(y[i]) * (x[i + 1] - x[i]) / Math.Abs(y[i + 1] - y[i]);
                double yv = ysize * 3 / 275;
                _graph.AddLine(key + (j++), new double[] { xv, xv }, new double[] { -yv, yv }, color, 1, false);
            }
        }

        class GraphInputs
        {
            public string lowerBound;
            public string upperBound;
            public string numPts;
            public int flapSetting;
            public string pitchSetting;
            public string otherInput;
            public bool spoilers;
        }

    }
}
