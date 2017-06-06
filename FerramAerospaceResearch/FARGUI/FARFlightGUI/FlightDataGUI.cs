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
using System.Text;
using StringLeakTest;
using UnityEngine;
using KSP.Localization;

namespace FerramAerospaceResearch.FARGUI.FARFlightGUI
{
    class FlightDataGUI
    {
        bool[] activeFlightDataSections = new bool[9] { true, true, true, true, true, true, true, true, true };
        bool[] oldFlightDataSections = new bool[9] { false, false, false, false, false, false, false, false, false };
        string[] flightDataOptionLabels = new string[9]{
            Localizer.Format("FARFlightDataOption0"),
            Localizer.Format("FARFlightDataOption1"),
            Localizer.Format("FARFlightDataOption2"),
            Localizer.Format("FARFlightDataOption3"),
            Localizer.Format("FARFlightDataOption4"),
            Localizer.Format("FARFlightDataOption5"), 
            Localizer.Format("FARFlightDataOption6"),
            Localizer.Format("FARFlightDataOption7"),
            Localizer.Format("FARFlightDataOption8")
        };

        VesselFlightInfo infoParameters;
	    StringBuilder dataStringBuilder = new StringBuilder();
	    StringBuilder labelStringBuilder = new StringBuilder();

        GUIStyle buttonStyle;
        GUIStyle boxStyle;

        int thisFrame = 0;

        public FlightDataGUI()
        {
            LoadSettings();
        }

        public void UpdateInfoParameters(VesselFlightInfo info)
        {
            infoParameters = info;
        }


        void CreateLabelString()
        {
            bool change = false;
            for(int i=0; i<activeFlightDataSections.Length;++i)
            {
                change |= (oldFlightDataSections[i] == activeFlightDataSections[i]);
            }
            if(!change && labelStringBuilder.Length != 0) //no need to recreate string, we still have one, and the settings have not been changed.
                return;
            for(int i=0; i<activeFlightDataSections.Length;++i)
            {
               oldFlightDataSections[i] = activeFlightDataSections[i];
            }

            labelStringBuilder.Length = 0;
            labelStringBuilder.AppendLine();
            if (activeFlightDataSections[0])        //PYR angles
            {
                labelStringBuilder.AppendLine(Localizer.Format("FARFlightData0"));
                labelStringBuilder.AppendLine();
            }
            if (activeFlightDataSections[1])        //AoA and sidelip
            {
                labelStringBuilder.AppendLine(Localizer.Format("FARFlightData1"));
                labelStringBuilder.AppendLine();
            }
            if (activeFlightDataSections[2])        //Dyn pres
            {
                labelStringBuilder.AppendLine(Localizer.Format("FARFlightData2"));
                labelStringBuilder.AppendLine();
            }
            if (activeFlightDataSections[3])        //Raw Forces
            {
                labelStringBuilder.AppendLine(Localizer.Format("FARFlightData3"));
                labelStringBuilder.AppendLine();
            }
            if (activeFlightDataSections[4])        //Coeffs + refArea
            {
                labelStringBuilder.AppendLine(Localizer.Format("FARFlightData4"));
                labelStringBuilder.AppendLine();
            }
            if (activeFlightDataSections[5])        //L/D and VL/D
            {
                labelStringBuilder.AppendLine(Localizer.Format("FARFlightData5"));
                labelStringBuilder.AppendLine();
            }
            if (activeFlightDataSections[6])        //Engine and intake data; Thrust Specific Fuel Consumption and Specific Excess Power
            {

                labelStringBuilder.AppendLine(Localizer.Format("FARFlightData6"));
                labelStringBuilder.AppendLine();
            }
            if (activeFlightDataSections[7])        //Range, Endurance est
            {
                labelStringBuilder.AppendLine(Localizer.Format("FARFlightData7"));
                labelStringBuilder.AppendLine();
            }
            if (activeFlightDataSections[8])        //Ballistic Coeff and Term Vel
            {
                labelStringBuilder.AppendLine(Localizer.Format("FARFlightData8"));
                labelStringBuilder.AppendLine();
            }
        }

        void CreateDataString()
        {
            dataStringBuilder.Length = 0;
            dataStringBuilder.AppendLine();
            if (activeFlightDataSections[0])        //PYR angles
            {
                dataStringBuilder.Concat((float)(infoParameters.pitchAngle),1);
                dataStringBuilder.AppendLine("°");
		dataStringBuilder.Concat((float)(infoParameters.headingAngle),1);
                dataStringBuilder.AppendLine("°");
		dataStringBuilder.Concat((float)(infoParameters.rollAngle),1);
                dataStringBuilder.AppendLine("°");
                dataStringBuilder.AppendLine();
            }
            if (activeFlightDataSections[1])        //AoA and sidelip
            {
                dataStringBuilder.Concat((float)(infoParameters.aoA),1);
                dataStringBuilder.AppendLine("°");
                dataStringBuilder.Concat((float)(infoParameters.sideslipAngle),1);
                dataStringBuilder.AppendLine("°");
                dataStringBuilder.AppendLine();
            }
            if (activeFlightDataSections[2])        //Dyn pres
            {
                dataStringBuilder.Concat((float)(infoParameters.dynPres),3);
                dataStringBuilder.AppendLine(" kPa");
                dataStringBuilder.AppendLine();
            }
            if (activeFlightDataSections[3])        //Raw Forces
            {
                dataStringBuilder.Concat((float)(infoParameters.liftForce),3);
                dataStringBuilder.AppendLine(" kN");
                dataStringBuilder.Concat((float)(infoParameters.dragForce),3);
                dataStringBuilder.AppendLine(" kN");
                dataStringBuilder.Concat((float)(infoParameters.sideForce),3);
                dataStringBuilder.AppendLine(" kN");
                dataStringBuilder.AppendLine();
            }
            if (activeFlightDataSections[4])        //Coeffs + refArea
            {
                dataStringBuilder.Concat((float)(infoParameters.liftCoeff),4).AppendLine();
                dataStringBuilder.Concat((float)(infoParameters.dragCoeff),4).AppendLine();
                dataStringBuilder.Concat((float)(infoParameters.sideCoeff),4).AppendLine();
                dataStringBuilder.Concat((float)(infoParameters.refArea),3);
                dataStringBuilder.AppendLine(" m²");
                dataStringBuilder.AppendLine();
            }
            if (activeFlightDataSections[5])        //L/D and VL/D
            {
                dataStringBuilder.Concat((float)(infoParameters.liftToDragRatio),3).AppendLine();
                dataStringBuilder.Concat((float)(infoParameters.velocityLiftToDragRatio),3).AppendLine();
                dataStringBuilder.AppendLine();
            }
            if (activeFlightDataSections[6])        //Engine and intake data
            {
                dataStringBuilder.Concat((float)((infoParameters.fullMass - infoParameters.dryMass) / infoParameters.fullMass),2).AppendLine();
                dataStringBuilder.Concat((float)(infoParameters.tSFC),3);
                dataStringBuilder.AppendLine(" hr⁻¹");
                if(double.IsInfinity(infoParameters.intakeAirFrac))
                    dataStringBuilder.AppendLine("Infinity");
                else
                {
                    dataStringBuilder.Concat((float)(infoParameters.intakeAirFrac * 100),1); //Note: Originally this was output using P1 format, leading to an effective factor of 100*100.
                    dataStringBuilder.AppendLine("%");
                }
                dataStringBuilder.Concat((float)(infoParameters.specExcessPower),2); //this is a noticable change to original code: Here N2 format was used...
                dataStringBuilder.AppendLine(" W/kg");
                dataStringBuilder.AppendLine();
            }
            if (activeFlightDataSections[7])        //Range, Endurance est
            {
                dataStringBuilder.Concat((float)(infoParameters.endurance),2);
                dataStringBuilder.AppendLine(" hr");
                dataStringBuilder.Concat((float)(infoParameters.range),2); //also here: originall N2 format.
                dataStringBuilder.AppendLine(" km");
                dataStringBuilder.AppendLine();
            }
            if (activeFlightDataSections[8])        //Ballistic Coeff and Term Vel
            {
                dataStringBuilder.Concat((float)(infoParameters.ballisticCoeff),2);
                dataStringBuilder.AppendLine(" kg/m²");
                dataStringBuilder.Concat((float)(infoParameters.termVelEst),2);
                dataStringBuilder.AppendLine(" m/s");
                dataStringBuilder.AppendLine();
            }
        }

        public void DataDisplay()
        {
            if (boxStyle == null)
                boxStyle = FlightGUI.boxStyle;
            if(Time.frameCount != thisFrame)
            {
                thisFrame = Time.frameCount;
                CreateLabelString();
                CreateDataString();
            }
            
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Box(labelStringBuilder.ToString(), boxStyle, GUILayout.Width(140));
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            GUILayout.Box(dataStringBuilder.ToString(), boxStyle, GUILayout.Width(140));
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        //Returns true on a setting change
        public bool SettingsDisplay()
        {
            if (buttonStyle == null)
                buttonStyle = FlightGUI.buttonStyle;

            GUILayout.Label(Localizer.Format("FARFlightDataOptionLabel"));
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
