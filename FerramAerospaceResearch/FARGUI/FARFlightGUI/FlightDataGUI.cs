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
using System.Text;
using UnityEngine;

namespace FerramAerospaceResearch.FARGUI.FARFlightGUI
{
    class FlightDataGUI
    {
        bool[] activeFlightDataSections = new bool[9] { true, true, true, true, true, true, true, true, true };
        string[] flightDataOptionLabels = new string[9]{
            "PYR Angles",
            "AoA + Sideslip",
            "Dyn Pres",
            "Aero Forces",
            "Coeffs + Ref Area",
            "L/D and V*L/D", 
            "Engine + Intake",
            "Range + Endurance",
            "BC and Term Vel"
        };

        VesselFlightInfo infoParameters;
        string labelString, dataString;
        GUIStyle buttonStyle;
        GUIStyle boxStyle;

        public FlightDataGUI()
        {
            LoadSettings();
        }

        public void UpdateInfoParameters(VesselFlightInfo info)
        {
            infoParameters = info;
            CreateLabelString();
            CreateDataString();
        }


        void CreateLabelString()
        {
            StringBuilder dataReadoutString = new StringBuilder();
            dataReadoutString.AppendLine();
            if (activeFlightDataSections[0])        //PYR angles
            {
                dataReadoutString.AppendLine("Pitch Angle: \n\rHeading: \n\rRoll Angle: ");
                dataReadoutString.AppendLine();
            }
            if (activeFlightDataSections[1])        //AoA and sidelip
            {
                dataReadoutString.AppendLine("Angle of Attack: \n\rSideslip Angle: ");
                dataReadoutString.AppendLine();
            }
            if (activeFlightDataSections[2])        //Dyn pres
            {
                dataReadoutString.AppendLine("Dyn Pres: ");
                dataReadoutString.AppendLine();
            }
            if (activeFlightDataSections[3])        //Raw Forces
            {
                dataReadoutString.AppendLine("Lift: \n\rDrag: \n\rSideForce: ");
                dataReadoutString.AppendLine();
            }
            if (activeFlightDataSections[4])        //Coeffs + refArea
            {
                dataReadoutString.AppendLine("Cl: \n\rCd: \n\rCy: \n\rRef Area: ");
                dataReadoutString.AppendLine();
            }
            if (activeFlightDataSections[5])        //L/D and VL/D
            {
                dataReadoutString.AppendLine("L/D: \n\rV*L/D: ");
                dataReadoutString.AppendLine();
            }
            if (activeFlightDataSections[6])        //Engine and intake data
            {

                dataReadoutString.AppendLine("Fuel Fraction: \n\rTSFC: \n\rAir Req Met: \n\rSpec. Excess Pwr:");
                dataReadoutString.AppendLine();
            }
            if (activeFlightDataSections[7])        //Range, Endurance est
            {
                dataReadoutString.AppendLine("Est. Endurance: \n\rEst. Range: ");
                dataReadoutString.AppendLine();
            }
            if (activeFlightDataSections[8])        //Ballistic Coeff and Term Vel
            {
                dataReadoutString.AppendLine("BC: \n\rTerminal V: ");
                dataReadoutString.AppendLine();
            }
            labelString = dataReadoutString.ToString();
        }

        void CreateDataString()
        {
            StringBuilder dataReadoutString = new StringBuilder();
            dataReadoutString.AppendLine();
            if (activeFlightDataSections[0])        //PYR angles
            {
                dataReadoutString.Append(infoParameters.pitchAngle.ToString("N1"));
                dataReadoutString.AppendLine("°");
                dataReadoutString.Append(infoParameters.headingAngle.ToString("N1"));
                dataReadoutString.AppendLine("°");
                dataReadoutString.Append(infoParameters.rollAngle.ToString("N1"));
                dataReadoutString.AppendLine("°");
                dataReadoutString.AppendLine();
            }
            if (activeFlightDataSections[1])        //AoA and sidelip
            {
                dataReadoutString.Append(infoParameters.aoA.ToString("N1"));
                dataReadoutString.AppendLine("°");
                dataReadoutString.Append(infoParameters.sideslipAngle.ToString("N1"));
                dataReadoutString.AppendLine("°");
                dataReadoutString.AppendLine();
            }
            if (activeFlightDataSections[2])        //Dyn pres
            {
                dataReadoutString.Append(infoParameters.dynPres.ToString("F3"));
                dataReadoutString.AppendLine(" kPa");
                dataReadoutString.AppendLine();
            }
            if (activeFlightDataSections[3])        //Raw Forces
            {
                dataReadoutString.Append(infoParameters.liftForce.ToString("F3"));
                dataReadoutString.AppendLine(" kN");
                dataReadoutString.Append(infoParameters.dragForce.ToString("F3"));
                dataReadoutString.AppendLine(" kN");
                dataReadoutString.Append(infoParameters.sideForce.ToString("F3"));
                dataReadoutString.AppendLine(" kN");
                dataReadoutString.AppendLine();
            }
            if (activeFlightDataSections[4])        //Coeffs + refArea
            {
                dataReadoutString.AppendLine(infoParameters.liftCoeff.ToString("F3"));
                dataReadoutString.AppendLine(infoParameters.dragCoeff.ToString("F3"));
                dataReadoutString.AppendLine(infoParameters.sideCoeff.ToString("F3"));
                dataReadoutString.Append(infoParameters.refArea.ToString("F3"));
                dataReadoutString.AppendLine(" m²");
                dataReadoutString.AppendLine();
            }
            if (activeFlightDataSections[5])        //L/D and VL/D
            {
                dataReadoutString.AppendLine(infoParameters.liftToDragRatio.ToString("F3"));
                dataReadoutString.AppendLine(infoParameters.velocityLiftToDragRatio.ToString("F3"));
                dataReadoutString.AppendLine();
            }
            if (activeFlightDataSections[6])        //Engine and intake data
            {

                dataReadoutString.AppendLine(((infoParameters.fullMass - infoParameters.dryMass) / infoParameters.fullMass).ToString("N2"));
                dataReadoutString.Append(infoParameters.tSFC.ToString("N3"));
                dataReadoutString.AppendLine(" hr⁻¹");
                dataReadoutString.AppendLine((infoParameters.intakeAirFrac * 100).ToString("P1"));
                dataReadoutString.Append(infoParameters.specExcessPower.ToString("N2"));
                dataReadoutString.AppendLine(" m²/s²");
                dataReadoutString.AppendLine();
            }
            if (activeFlightDataSections[7])        //Range, Endurance est
            {

                dataReadoutString.Append(infoParameters.endurance.ToString("N2"));
                dataReadoutString.AppendLine(" hr");
                dataReadoutString.Append(infoParameters.range.ToString("N2"));
                dataReadoutString.AppendLine(" km");
                dataReadoutString.AppendLine();
            }
            if (activeFlightDataSections[8])        //Ballistic Coeff and Term Vel
            {

                dataReadoutString.Append(infoParameters.ballisticCoeff.ToString("N2"));
                dataReadoutString.AppendLine(" kg/m²");
                dataReadoutString.Append(infoParameters.termVelEst.ToString("N2"));
                dataReadoutString.AppendLine(" m/s");
                dataReadoutString.AppendLine();
            }
            dataString = dataReadoutString.ToString();
        }

        public void DataDisplay()
        {
            if (boxStyle == null)
                boxStyle = FlightGUI.boxStyle;
            

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Box(labelString, boxStyle, GUILayout.Width(120));
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            GUILayout.Box(dataString, boxStyle, GUILayout.Width(120));
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        //Returns true on a setting change
        public bool SettingsDisplay()
        {
            if (buttonStyle == null)
                buttonStyle = FlightGUI.buttonStyle;

            GUILayout.Label("Flight Data Items To Show");
            GUILayout.BeginVertical();
            bool change = false;
            for (int i = 0; i < activeFlightDataSections.Length; i++)
            {
                bool currentVal = activeFlightDataSections[i];
                bool newVal = GUILayout.Toggle(currentVal, flightDataOptionLabels[i], GUILayout.Width(100));
                activeFlightDataSections[i] = newVal;

                change |= (newVal != currentVal);
            }
            GUILayout.EndVertical();

            if (change)
            {
                CreateDataString();
                CreateLabelString();
            }

            return change;
        }

        public void SaveSettings()
        {
            List<ConfigNode> flightGUISettings = FARSettingsScenarioModule.FlightGUISettings;
            if (flightGUISettings == null)
            {
                Debug.LogError("Could not save Flight Data Settings because settings config list was null");
            }
            ConfigNode node = null;
            for (int i = 0; i < flightGUISettings.Count; i++)
                if (flightGUISettings[i].name == "FlightDataSettings")
                {
                    node = flightGUISettings[i];
                    break;
                }

            if (node == null)
            {
                node = new ConfigNode("FlightDataSettings");
                flightGUISettings.Add(node);
            }
            node.ClearData();

            for (int i = 0; i < activeFlightDataSections.Length; i++)
            {
                node.AddValue("section" + i + "active", activeFlightDataSections[i]);
            }
        }

        void LoadSettings()
        {
            List<ConfigNode> flightGUISettings = FARSettingsScenarioModule.FlightGUISettings;

            ConfigNode node = null;
            for (int i = 0; i < flightGUISettings.Count; i++)
                if (flightGUISettings[i].name == "FlightDataSettings")
                {
                    node = flightGUISettings[i];
                    break;
                }

            if (node != null)
            {
                for(int i = 0; i < activeFlightDataSections.Length; i++)
                {
                    bool tmp = true;
                    if (bool.TryParse(node.GetValue("section" + i + "active"), out tmp))
                        activeFlightDataSections[i] = tmp;
                }
            }
        }
    }
}
