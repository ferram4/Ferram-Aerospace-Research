/*
Ferram Aerospace Research v0.15.8.1 "Lewis"
=========================
Aerodynamics model for Kerbal Space Program

Copyright 2017, Michael Ferrara, aka Ferram4

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
using FerramAerospaceResearch.FARGUI.FAREditorGUI.Simulation;
using KSP.Localization;
using UnityEngine;
using ferram4;

namespace FerramAerospaceResearch.FARGUI.FAREditorGUI
{
    class StabilityDerivSimulationGUI : IDisposable
    {

        private SimMode simMode = 0;

        EditorSimManager simManager;
        InitialConditions lonConditions, latConditions;
        ferramGraph _graph = new ferramGraph(400, 200);

        private enum SimMode
        {
            LONG,
            LAT,
        }

        private static string[] SimMode_str = 
        {
            Localizer.Format("FAREditorSimModeLong"),
            Localizer.Format("FAREditorSimModeLat"),
        };

        public StabilityDerivSimulationGUI(EditorSimManager simManager)
        {
            this.simManager = simManager;

            lonConditions = new InitialConditions(new string[] { "0", "0", "0", "0" }, new string[] { "w", "u", "q", "θ" }, new double[]{1, 1, Math.PI/180, Math.PI/180}, "0.01", "10");
            latConditions = new InitialConditions(new string[] { "0", "0", "0", "0" }, new string[] { "β", "p", "r", "φ" }, new double[]{Math.PI/180, Math.PI/180, Math.PI/180, Math.PI/180}, "0.01", "10");

            _graph.SetBoundaries(0, 10, 0, 2);
            _graph.SetGridScaleUsingValues(1, 0.25);
            _graph.horizontalLabel = Localizer.Format("FAREditorSimGraphTime");
            _graph.verticalLabel = Localizer.Format("FAREditorSimGraphParams");
            _graph.Update();
        }

        public void Dispose()
        {
            simManager = null;
            _graph = null;
        }

        public void Display()
        {
            GUILayout.BeginHorizontal();
            simMode = (SimMode)GUILayout.SelectionGrid((int)simMode, SimMode_str, 2);

            GUILayout.EndHorizontal();
            StabilityDerivOutput vehicleData = simManager.vehicleData;



            if (simMode == SimMode.LONG)
            {
                LongitudinalGUI(vehicleData);
                DataInput(lonConditions, vehicleData, true);
            }
            else
            {
                LateralGUI(vehicleData);
                DataInput(latConditions, vehicleData, false);
            }
            GUIStyle BackgroundStyle = new GUIStyle(GUI.skin.box);
            BackgroundStyle.hover = BackgroundStyle.active = BackgroundStyle.normal;

            _graph.Display(BackgroundStyle, 0, 0);
            //graph.Display(GUILayout.Width(540), GUILayout.Height(300));

            DrawTooltip();
        }

        private void LongitudinalGUI(StabilityDerivOutput vehicleData)
        {

            GUIStyle BackgroundStyle = new GUIStyle(GUI.skin.box);
            BackgroundStyle.hover = BackgroundStyle.active = BackgroundStyle.normal;

            GUILayout.BeginHorizontal();
            GUILayout.Label(Localizer.Format("FAREditorLongDeriv"), GUILayout.Width(160));
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label(Localizer.Format("FAREditorDownVelDeriv"), GUILayout.Width(160));
            GUILayout.Label(Localizer.Format("FAREditorFwdVelDeriv"), GUILayout.Width(160));
            GUILayout.Label(Localizer.Format("FAREditorPitchRateDeriv"), GUILayout.Width(160));
            GUILayout.Label(Localizer.Format("FAREditorPitchCtrlDeriv"), GUILayout.Width(160));
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(BackgroundStyle);
            GUILayout.BeginHorizontal();
            StabilityLabel(Localizer.Format("FAREditorZw"), vehicleData.stabDerivs[3], " s⁻¹", Localizer.Format("FAREditorZwExp"), 160, -1);
            StabilityLabel(Localizer.Format("FAREditorZu"), vehicleData.stabDerivs[6], " s⁻¹", Localizer.Format("FAREditorZuExp"), 160, -1);
            StabilityLabel(Localizer.Format("FAREditorZq"), vehicleData.stabDerivs[9], " m/s", Localizer.Format("FAREditorZqExp"), 160, 0);
            StabilityLabel(Localizer.Format("FAREditorZDeltae"), vehicleData.stabDerivs[12], " m/s²", Localizer.Format("FAREditorZDeltaeExp"), 160, 0);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            StabilityLabel(Localizer.Format("FAREditorXw"), vehicleData.stabDerivs[4], " s⁻¹", Localizer.Format("FAREditorXwExp"), 160, 0);
            StabilityLabel(Localizer.Format("FAREditorXu"), vehicleData.stabDerivs[7], " s⁻¹", Localizer.Format("FAREditorXuExp"), 160, -1);
            StabilityLabel(Localizer.Format("FAREditorXq"), vehicleData.stabDerivs[10], " m/s", Localizer.Format("FAREditorXqExp"), 160, 0);
            StabilityLabel(Localizer.Format("FAREditorXDeltae"), vehicleData.stabDerivs[13], " m/s²", Localizer.Format("FAREditorXDeltaeExp"), 160, 0);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            StabilityLabel(Localizer.Format("FAREditorMw"), vehicleData.stabDerivs[5], " (m * s)⁻¹", Localizer.Format("FAREditorMwExp"), 160, -1);
            StabilityLabel(Localizer.Format("FAREditorMu"), vehicleData.stabDerivs[8], " (m * s)⁻¹", Localizer.Format("FAREditorMuExp"), 160, 0);
            StabilityLabel(Localizer.Format("FAREditorMq"), vehicleData.stabDerivs[11], " s⁻¹", Localizer.Format("FAREditorMqExp"), 160, -1);
            StabilityLabel(Localizer.Format("FAREditorMDeltae"), vehicleData.stabDerivs[14], " s⁻²", Localizer.Format("FAREditorMDeltaeExp"), 160, 1);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndVertical();
        }

        private void LateralGUI(StabilityDerivOutput vehicleData)
        {
            GUIStyle BackgroundStyle = new GUIStyle(GUI.skin.box);
            BackgroundStyle.hover = BackgroundStyle.active = BackgroundStyle.normal;

            GUILayout.BeginHorizontal();
            GUILayout.Label(Localizer.Format("FAREditorLatDeriv"), GUILayout.Width(160));
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label(Localizer.Format("FAREditorSideslipDeriv"), GUILayout.Width(160));
            GUILayout.Label(Localizer.Format("FAREditorRollRateDeriv"), GUILayout.Width(160));
            GUILayout.Label(Localizer.Format("FAREditorYawRateDeriv"), GUILayout.Width(160));
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(BackgroundStyle);
            GUILayout.BeginHorizontal();
            StabilityLabel(Localizer.Format("FAREditorYβ"), vehicleData.stabDerivs[15], " m/s²", Localizer.Format("FAREditorYβExp"), 160, -1);
            StabilityLabel(Localizer.Format("FAREditorYp"), vehicleData.stabDerivs[18], " m/s", Localizer.Format("FAREditorYpExp"), 160, 0);
            StabilityLabel(Localizer.Format("FAREditorYr"), vehicleData.stabDerivs[21], " m/s", Localizer.Format("FAREditorYrExp"), 160, 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            StabilityLabel(Localizer.Format("FAREditorLβ"), vehicleData.stabDerivs[16], " s⁻²", Localizer.Format("FAREditorLβExp"), 160, -1);
            StabilityLabel(Localizer.Format("FAREditorLp"), vehicleData.stabDerivs[19], " s⁻¹", Localizer.Format("FAREditorLpExp"), 160, -1);
            StabilityLabel(Localizer.Format("FAREditorLr"), vehicleData.stabDerivs[22], " s⁻¹", Localizer.Format("FAREditorLrExp"), 160, 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            StabilityLabel(Localizer.Format("FAREditorNβ"), vehicleData.stabDerivs[17], " s⁻²", Localizer.Format("FAREditorNβExp"), 160, 1);
            StabilityLabel(Localizer.Format("FAREditorNp"), vehicleData.stabDerivs[20], " s⁻¹", Localizer.Format("FAREditorNpExp"), 160, 0);
            StabilityLabel(Localizer.Format("FAREditorNr"), vehicleData.stabDerivs[23], " s⁻¹", Localizer.Format("FAREditorNrExp"), 160, -1);
            GUILayout.EndHorizontal(); GUILayout.EndVertical();
            GUILayout.EndVertical();


        }

        private void DataInput(InitialConditions inits, StabilityDerivOutput vehicleData, bool longitudinal)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < inits.inits.Length; i++)
            {
                GUILayout.Label(Localizer.Format("FAREditorSimInit") + inits.names[i] +": ");
                inits.inits[i] = GUILayout.TextField(inits.inits[i], GUILayout.ExpandWidth(true));
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(Localizer.Format("FAREditorSimEndTime"));
            inits.maxTime = GUILayout.TextField(inits.maxTime, GUILayout.ExpandWidth(true));
            GUILayout.Label(Localizer.Format("FAREditorSimTimestep"));
            inits.dt = GUILayout.TextField(inits.dt, GUILayout.ExpandWidth(true));
            if (GUILayout.Button(Localizer.Format("FAREditorSimRunButton"), GUILayout.Width(150.0F), GUILayout.Height(25.0F)))
            {
                for (int i = 0; i < inits.inits.Length; i++)
                {
                    inits.inits[i] = Regex.Replace(inits.inits[i], @"[^-?[0-9]*(\.[0-9]*)?]", "");
                }
                inits.maxTime = Regex.Replace(inits.maxTime, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                inits.dt = Regex.Replace(inits.dt, @"[^-?[0-9]*(\.[0-9]*)?]", "");

                double[] initCond = new double[inits.inits.Length];
                for (int i = 0; i < initCond.Length; i++)
                {
                    initCond[i] = Convert.ToDouble(inits.inits[i]) * inits.scaling[i];
                }


                GraphData data;
                if(longitudinal)
                    data = simManager.StabDerivLinearSim.RunTransientSimLongitudinal(vehicleData, Convert.ToDouble(inits.maxTime), Convert.ToDouble(inits.dt), initCond);
                else
                    data = simManager.StabDerivLinearSim.RunTransientSimLateral(vehicleData, Convert.ToDouble(inits.maxTime), Convert.ToDouble(inits.dt), initCond);

                UpdateGraph(data, Localizer.Format("FAREditorSimGraphTime"), Localizer.Format("FAREditorSimGraphParams"), 0, Convert.ToDouble(inits.maxTime), 50);
            }
            GUILayout.EndHorizontal();
        }

        private void StabilityLabel(String text1, double val, String text2, String tooltip, int width, int sign)
        {
            Color color = Color.white;
            if (sign != 0)
                color = (Math.Sign(val) == sign) ? Color.green : Color.red;

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.normal.textColor = style.hover.textColor = color;

            GUILayout.Label(new GUIContent(text1 + val.ToString("G6") + text2, tooltip), style, GUILayout.Width(width));
        }

        private void DrawTooltip()
        {
            if (GUI.tooltip == "")
                return;

            GUIStyle BackgroundStyle = new GUIStyle(GUI.skin.box);
            BackgroundStyle.hover = BackgroundStyle.active = BackgroundStyle.normal;

            Vector3 mousePos = GUIUtils.GetMousePos();
            Rect windowRect = EditorGUI.GUIRect;

            Rect tooltipRect = new Rect(Mathf.Clamp(mousePos.x - windowRect.x, 0, windowRect.width - 300), Mathf.Clamp(mousePos.y - windowRect.y, 0, windowRect.height - 80), 300, 80);

            GUIStyle toolTipStyle = BackgroundStyle;
            toolTipStyle.normal.textColor = toolTipStyle.active.textColor = toolTipStyle.hover.textColor = toolTipStyle.focused.textColor = toolTipStyle.onNormal.textColor = toolTipStyle.onHover.textColor = toolTipStyle.onActive.textColor = toolTipStyle.onFocused.textColor = new Color(1, 0.75f, 0);

            GUI.Box(tooltipRect, GUI.tooltip, toolTipStyle);
        }

        private void UpdateGraph(GraphData data, string horizontalLabel, string verticalLabel, double lowerXBound, double upperXBound, double clampYBounds)
        {
            double minBounds = double.PositiveInfinity;
            double maxBounds = double.NegativeInfinity;

            for (int i = 0; i < data.yValues.Count; i++)
            {
                minBounds = Math.Min(minBounds, data.yValues[i].Min());
                maxBounds = Math.Max(maxBounds, data.yValues[i].Max());
            }
            minBounds *= 2;
            maxBounds *= 2;

            if (minBounds < -clampYBounds)
                minBounds = -clampYBounds;
            if (maxBounds > clampYBounds)
                maxBounds = clampYBounds;

            // To allow switching between two graph setups to observe differences,
            // use both the current and the previous shown graph to estimate scale

            double realMin = Math.Min(Math.Floor(minBounds), -0.25);
            double realMax = Math.Max(Math.Ceiling(maxBounds), 0.25);

            _graph.Clear();
            _graph.SetBoundaries(lowerXBound, upperXBound, realMin, realMax);
            _graph.SetGridScaleUsingValues(5, 0.5);

            for (int i = 0; i < data.yValues.Count; i++)
            {
                _graph.AddLine(data.lineNames[i], data.xValues, data.yValues[i], data.lineColors[i], 1, data.lineNameVisible[i]);
            }

            _graph.horizontalLabel = horizontalLabel;
            _graph.verticalLabel = verticalLabel;// "Cl\nCd\nCm\nL/D / 10";
            _graph.Update();
        }

        class InitialConditions
        {
            public string[] inits;
            public string[] names;
            public double[] scaling;

            public string dt;
            public string maxTime;

            public InitialConditions(string[] inits, string[] names, double[] scaling, string dt, string maxTime)
            {
                this.inits = inits;
                this.names = names;
                this.scaling = scaling;
                this.dt = dt;
                this.maxTime = maxTime;
            }
        }
    }
}
