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
using System.Text.RegularExpressions;
using UnityEngine;
using KSP.Localization;
using FerramAerospaceResearch.FARAeroComponents;
using FerramAerospaceResearch.FARGUI.FAREditorGUI.Simulation;
using ferram4;

namespace FerramAerospaceResearch.FARGUI.FAREditorGUI
{
    class StabilityDerivGUI
    {

        GUIDropDown<int> _flapSettingDropdown;
        GUIDropDown<CelestialBody> _bodySettingDropdown;

        StabilityDerivOutput stabDerivOutput;

        string altitude = "0";
        string machNumber = "0.35";
        bool spoilersDeployed = false;

        EditorSimManager simManager;

        Vector3 aoAVec;

        public StabilityDerivGUI(EditorSimManager simManager, GUIDropDown<int> flapSettingDropDown, GUIDropDown<CelestialBody> bodySettingDropdown)
        {
            this.simManager = simManager;
            _flapSettingDropdown = flapSettingDropDown;
            _bodySettingDropdown = bodySettingDropdown;

            stabDerivOutput = new StabilityDerivOutput();
        }

        public void ArrowAnim(ArrowPointer velArrow)
        {
            velArrow.Direction = -aoAVec;
            //Debug.Log(velArrow.Direction);
        }

        void SetAngleVectors(double aoA)
        {
            aoA *= FARMathUtil.deg2rad;

            if (EditorDriver.editorFacility == EditorFacility.SPH)
            {
                aoAVec = new Vector3d(0, -Math.Sin(aoA), Math.Cos(aoA));
            }
            else
            {
                aoAVec = new Vector3d(0, Math.Cos(aoA), Math.Sin(aoA));
            }
        }

        public void Display()
        {
            //stabDerivHelp = GUILayout.Toggle(stabDerivHelp, "?", ButtonStyle, GUILayout.Width(200));

            GUILayout.Label(Localizer.Format("FAREditorStabDerivFlightCond"));
            GUILayout.BeginHorizontal();
            GUILayout.Label(Localizer.Format("FAREditorStabDerivPlanet"));
            _bodySettingDropdown.GUIDropDownDisplay();

            GUILayout.Label(Localizer.Format("FAREditorStabDerivAlt"));
            altitude = GUILayout.TextField(altitude, GUILayout.ExpandWidth(true));

            GUILayout.Label(Localizer.Format("FAREditorStabDerivMach"));
            machNumber = GUILayout.TextField(machNumber, GUILayout.ExpandWidth(true));

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(Localizer.Format("FAREditorStabDerivFlap"));
            _flapSettingDropdown.GUIDropDownDisplay();
            GUILayout.Label(Localizer.Format("FAREditorStabDerivSpoiler"));
            spoilersDeployed = GUILayout.Toggle(spoilersDeployed, spoilersDeployed ? Localizer.Format("FAREditorStabDerivSDeploy") : Localizer.Format("FAREditorStabDerivSRetract"), GUILayout.Width(100));
            GUILayout.EndHorizontal();

            if (GUILayout.Button(Localizer.Format("FAREditorStabDerivSpoiler"), GUILayout.Width(250.0F), GUILayout.Height(25.0F)))
            {
                CelestialBody body = _bodySettingDropdown.ActiveSelection;
                FARAeroUtil.UpdateCurrentActiveBody(body);
                //atm_temp_str = Regex.Replace(atm_temp_str, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                //rho_str = Regex.Replace(rho_str, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                machNumber = Regex.Replace(machNumber, @"[^-?[0-9]*(\.[0-9]*)?]", "");

                altitude = Regex.Replace(altitude, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                double altitudeDouble = Convert.ToDouble(altitude);
                altitudeDouble *= 1000;


                double temp = body.GetTemperature(altitudeDouble);
                double pressure = body.GetPressure(altitudeDouble);
                if (pressure > 0)
                {
                    //double temp = Convert.ToSingle(atm_temp_str);
                    double machDouble = Convert.ToSingle(machNumber);
                    machDouble = FARMathUtil.Clamp(machDouble, 0.001, float.PositiveInfinity);

                    double density = body.GetDensity(pressure, temp);

                    double sspeed = body.GetSpeedOfSound(pressure, density);
                    double vel = sspeed * machDouble;

                    //UpdateControlSettings();

                    double q = vel * vel * density * 0.5f;

                    stabDerivOutput = simManager.StabDerivCalculator.CalculateStabilityDerivs(vel, q, machDouble, 0, 0, 0, _flapSettingDropdown.ActiveSelection, spoilersDeployed, body, altitudeDouble);
                    simManager.vehicleData = stabDerivOutput;
                    SetAngleVectors(stabDerivOutput.stableAoA);
                }
                else
                {
                    PopupDialog.SpawnPopupDialog(new Vector2(0, 0), new Vector2(0, 0), "FARStabDerivError", Localizer.Format("FAREditorStabDerivError"), Localizer.Format("FAREditorStabDerivErrorExp"), Localizer.Format("FARGUIOKButton "), true, HighLogic.UISkin);
                }
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label(Localizer.Format("FAREditorStabDerivAirProp"), GUILayout.Width(180));
            GUILayout.Label(Localizer.Format("FAREditorStabDerivMoI"), GUILayout.Width(160));
            GUILayout.Label(Localizer.Format("FAREditorStabDerivPoI"), GUILayout.Width(160));
            GUILayout.Label(Localizer.Format("FAREditorStabDerivLvlFl"), GUILayout.Width(140));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(180));
            GUILayout.Label(Localizer.Format("FAREditorStabDerivRefArea") + stabDerivOutput.area.ToString("G3") + " m²");
            GUILayout.Label(Localizer.Format("FAREditorStabDerivScaledChord") + stabDerivOutput.MAC.ToString("G3") + " m");
            GUILayout.Label(Localizer.Format("FAREditorStabDerivScaledSpan") + stabDerivOutput.b.ToString("G3") + " m");
            GUILayout.EndVertical();


            GUILayout.BeginVertical(GUILayout.Width(160));
            GUILayout.Label(new GUIContent(Localizer.Format("FAREditorStabDerivIxx") + stabDerivOutput.stabDerivs[0].ToString("G6") + " kg * m²", Localizer.Format("FAREditorStabDerivIxxExp")));
            GUILayout.Label(new GUIContent(Localizer.Format("FAREditorStabDerivIyy") + stabDerivOutput.stabDerivs[1].ToString("G6") + " kg * m²", Localizer.Format("FAREditorStabDerivIyyExp")));
            GUILayout.Label(new GUIContent(Localizer.Format("FAREditorStabDerivIzz") + stabDerivOutput.stabDerivs[2].ToString("G6") + " kg * m²", Localizer.Format("FAREditorStabDerivIzzExp")));
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.Width(160));
            GUILayout.Label(new GUIContent(Localizer.Format("FAREditorStabDerivIxy") + stabDerivOutput.stabDerivs[24].ToString("G6") + " kg * m²", Localizer.Format("FAREditorStabDerivIxyExp")));
            GUILayout.Label(new GUIContent(Localizer.Format("FAREditorStabDerivIyz") + stabDerivOutput.stabDerivs[25].ToString("G6") + " kg * m²", Localizer.Format("FAREditorStabDerivIyzExp")));
            GUILayout.Label(new GUIContent(Localizer.Format("FAREditorStabDerivIxz") + stabDerivOutput.stabDerivs[26].ToString("G6") + " kg * m²", Localizer.Format("FAREditorStabDerivIxzExp")));
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.Width(140));
            GUILayout.Label(new GUIContent(Localizer.Format("FAREditorStabDerivu0") + stabDerivOutput.nominalVelocity.ToString("G6") + " m/s", Localizer.Format("FAREditorStabDerivu0Exp")));
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(Localizer.Format("FARAbbrevCl") + ": " + stabDerivOutput.stableCl.ToString("G3"), Localizer.Format("FAREditorStabDerivClExp")));
            GUILayout.Label(new GUIContent(Localizer.Format("FARAbbrevCd") + ": " + stabDerivOutput.stableCd.ToString("G3"), Localizer.Format("FAREditorStabDerivCdExp")));
            GUILayout.EndHorizontal();
            GUILayout.Label(new GUIContent(Localizer.Format("FARAbbrevAoA") + ": " + stabDerivOutput.stableAoAState + stabDerivOutput.stableAoA.ToString("G6") + " deg", Localizer.Format("FAREditorStabDerivAoAExp")));
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(Localizer.Format("FAREditorLongDeriv"), GUILayout.Width(160));
            GUILayout.EndHorizontal();

            GUIStyle BackgroundStyle = new GUIStyle(GUI.skin.box);
            BackgroundStyle.hover = BackgroundStyle.active = BackgroundStyle.normal;

            GUILayout.BeginVertical(BackgroundStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label(Localizer.Format("FAREditorDownVelDeriv"), GUILayout.Width(160));
            GUILayout.Label(Localizer.Format("FAREditorFwdVelDeriv"), GUILayout.Width(160));
            GUILayout.Label(Localizer.Format("FAREditorPitchRateDeriv"), GUILayout.Width(160));
            GUILayout.Label(Localizer.Format("FAREditorPitchCtrlDeriv"), GUILayout.Width(160));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            StabilityLabel(Localizer.Format("FAREditorZw"), stabDerivOutput.stabDerivs[3], " s⁻¹", Localizer.Format("FAREditorZwExp"), 160, -1);
            StabilityLabel(Localizer.Format("FAREditorZu"), stabDerivOutput.stabDerivs[6], " s⁻¹", Localizer.Format("FAREditorZuExp"), 160, -1);
            StabilityLabel(Localizer.Format("FAREditorZq"), stabDerivOutput.stabDerivs[9], " m/s", Localizer.Format("FAREditorZqExp"), 160, 0);
            StabilityLabel(Localizer.Format("FAREditorZDeltae"), stabDerivOutput.stabDerivs[12], " m/s²", Localizer.Format("FAREditorZDeltaeExp"), 160, 0);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            StabilityLabel(Localizer.Format("FAREditorXw"), stabDerivOutput.stabDerivs[4], " s⁻¹", Localizer.Format("FAREditorXwExp"), 160, 0);
            StabilityLabel(Localizer.Format("FAREditorXu"), stabDerivOutput.stabDerivs[7], " s⁻¹", Localizer.Format("FAREditorXuExp"), 160, -1);
            StabilityLabel(Localizer.Format("FAREditorXq"), stabDerivOutput.stabDerivs[10], " m/s", Localizer.Format("FAREditorXqExp"), 160, 0);
            StabilityLabel(Localizer.Format("FAREditorXDeltae"), stabDerivOutput.stabDerivs[13], " m/s²", Localizer.Format("FAREditorXDeltaeExp"), 160, 0);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            StabilityLabel(Localizer.Format("FAREditorMw"), stabDerivOutput.stabDerivs[5], " (m * s)⁻¹", Localizer.Format("FAREditorMwExp"), 160, -1);
            StabilityLabel(Localizer.Format("FAREditorMu"), stabDerivOutput.stabDerivs[8], " (m * s)⁻¹", Localizer.Format("FAREditorMuExp"), 160, 0);
            StabilityLabel(Localizer.Format("FAREditorMq"), stabDerivOutput.stabDerivs[11], " s⁻¹", Localizer.Format("FAREditorMqExp"), 160, -1);
            StabilityLabel(Localizer.Format("FAREditorMDeltae"), stabDerivOutput.stabDerivs[14], " s⁻²", Localizer.Format("FAREditorMDeltaeExp"), 160, 1);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label(Localizer.Format("FAREditorLatDeriv"), GUILayout.Width(160));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label(Localizer.Format("FAREditorSideslipDeriv"), GUILayout.Width(160));
            GUILayout.Label(Localizer.Format("FAREditorRollRateDeriv"), GUILayout.Width(160));
            GUILayout.Label(Localizer.Format("FAREditorYawRateDeriv"), GUILayout.Width(160));
            GUILayout.EndHorizontal();
            GUILayout.BeginVertical(BackgroundStyle);
            GUILayout.BeginHorizontal();
            StabilityLabel(Localizer.Format("FAREditorYβ"), stabDerivOutput.stabDerivs[15], " m/s²", Localizer.Format("FAREditorYβExp"), 160, -1);
            StabilityLabel(Localizer.Format("FAREditorYp"), stabDerivOutput.stabDerivs[18], " m/s", Localizer.Format("FAREditorYpExp"), 160, 0);
            StabilityLabel(Localizer.Format("FAREditorYr"), stabDerivOutput.stabDerivs[21], " m/s", Localizer.Format("FAREditorYrExp"), 160, 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            StabilityLabel(Localizer.Format("FAREditorLβ"), stabDerivOutput.stabDerivs[16], " s⁻²", Localizer.Format("FAREditorLβExp"), 160, -1);
            StabilityLabel(Localizer.Format("FAREditorLp"), stabDerivOutput.stabDerivs[19], " s⁻¹", Localizer.Format("FAREditorLpExp"), 160, -1);
            StabilityLabel(Localizer.Format("FAREditorLr"), stabDerivOutput.stabDerivs[22], " s⁻¹", Localizer.Format("FAREditorLrExp"), 160, 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            StabilityLabel(Localizer.Format("FAREditorNβ"), stabDerivOutput.stabDerivs[17], " s⁻²", Localizer.Format("FAREditorNβExp"), 160, 1);
            StabilityLabel(Localizer.Format("FAREditorNp"), stabDerivOutput.stabDerivs[20], " s⁻¹", Localizer.Format("FAREditorNpExp"), 160, 0);
            StabilityLabel(Localizer.Format("FAREditorNr"), stabDerivOutput.stabDerivs[23], " s⁻¹", Localizer.Format("FAREditorNrExp"), 160, -1);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            DrawTooltip();
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
    }
}
