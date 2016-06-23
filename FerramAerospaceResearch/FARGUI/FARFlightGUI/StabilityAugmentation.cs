/*
Ferram Aerospace Research v0.15.7 "Küchemann"
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
using UnityEngine;
using ferram4;

namespace FerramAerospaceResearch.FARGUI.FARFlightGUI
{
    class StabilityAugmentation
    {
        Vessel _vessel;
        static string[] systemLabel = new string[] { "Roll", "Yaw", "Pitch", "AoA", "DPCR" };
        static string[] systemLabelLong = new string[] { "Roll System", "Yaw System", "Pitch System", "AoA Limiter", "Dynamic Pressure Control Reduction" };
        static ControlSystem[] systemTemplates;
        ControlSystem[] systemInstances;

        static double aoALowLim, aoAHighLim;
        static double scalingDynPres;
        VesselFlightInfo info;

        GUIStyle buttonStyle;
        GUIStyle boxStyle;
        GUIDropDown<int> systemDropdown;

        public StabilityAugmentation(Vessel vessel)
        {
            _vessel = vessel;
            systemDropdown = new GUIDropDown<int>(systemLabel, new int[] { 0, 1, 2, 3, 4, 5 }, 0);
            LoadSettings();
            systemInstances = new ControlSystem[systemTemplates.Length];

            for (int i = 0; i < systemInstances.Length; i++)
            {
                systemInstances[i] = new ControlSystem(systemTemplates[i]);
            }
            _vessel.OnAutopilotUpdate += OnAutoPilotUpdate;
        }

        public void SaveAndDestroy()
        {
            if (_vessel != null)
                _vessel.OnAutopilotUpdate -= OnAutoPilotUpdate;
            SaveSettings();
        }

        public void UpdatePhysicsInfo(VesselFlightInfo info)
        {
            this.info = info;
        }

        public void Display()
        {
            if(buttonStyle == null)
            {
                buttonStyle = FlightGUI.buttonStyle;

            }
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            for (int i = 0; i < systemInstances.Length; i++)
            {
                systemInstances[i].active = GUILayout.Toggle(systemInstances[i].active, systemLabel[i], buttonStyle, GUILayout.MinWidth(30), GUILayout.ExpandWidth(true));
            }
            GUILayout.EndHorizontal();
        }

        public void SettingsDisplay()
        {
            if (boxStyle == null)
                boxStyle = FlightGUI.boxStyle;

            GUILayout.Label("Control System Tweaking");
            systemDropdown.GUIDropDownDisplay(GUILayout.Width(120));
            int selectedItem = systemDropdown.ActiveSelection;

            ControlSystem sys = systemInstances[selectedItem];
            GUILayout.BeginVertical(boxStyle);
            if (selectedItem != 4)
            {
                sys.kP = GUIUtils.TextEntryForDouble("Proportional Gain:", 120, sys.kP);
                sys.kD = GUIUtils.TextEntryForDouble("Derivative Gain:", 120, sys.kD);
                sys.kI = GUIUtils.TextEntryForDouble("Integral Gain:", 120, sys.kI);

                if (selectedItem == 3)
                {
                    aoALowLim = GUIUtils.TextEntryForDouble("Min AoA Lim:", 120, aoALowLim);
                    aoAHighLim = GUIUtils.TextEntryForDouble("Min AoA Lim:", 120, aoAHighLim);
                }
                else
                    sys.zeroPoint = GUIUtils.TextEntryForDouble("Desired Point:", 120, sys.zeroPoint);
            }
            else
                scalingDynPres = GUIUtils.TextEntryForDouble("Dyn Pres For Control Scaling:", 150, scalingDynPres);

            GUILayout.EndVertical();

        }

        private void OnAutoPilotUpdate(FlightCtrlState state)
        {
            if (_vessel.srfSpeed < 5)
                return;

            ControlSystem sys = systemInstances[0];     //wing leveler
            if (sys.active)
            {
                double phi = info.rollAngle - sys.zeroPoint;
                if (sys.kP < 0)
                {
                    phi += 180;
                    if (phi > 180)
                        phi -= 360;
                }
                else
                    phi = -phi;

                phi *= -FARMathUtil.deg2rad;

                double output = ControlStateChange(sys, phi);

                if (Math.Abs(state.roll - state.rollTrim) < 0.01)
                {
                    if (output > 1)
                        output = 1;
                    else if (output < -1)
                        output = -1;

                    state.roll = (float)output + state.rollTrim;
                }
            }
            else
                sys.errorIntegral = 0;
            sys = systemInstances[1];
            if (sys.active)
            {
                double beta = -(info.sideslipAngle - sys.zeroPoint) * FARMathUtil.deg2rad;

                double output = ControlStateChange(sys, beta);

                if (Math.Abs(state.yaw - state.yawTrim) < 0.01)
                {
                    if (output > 1)
                        output = 1;
                    else if (output < -1)
                        output = -1;

                    state.yaw = (float)output + state.yawTrim;
                }

            }
            else
                sys.errorIntegral = 0;
            sys = systemInstances[2];
            if (sys.active)
            {
                double pitch = (info.aoA - sys.zeroPoint) * FARMathUtil.deg2rad;

                double output = ControlStateChange(sys, pitch);

                if (Math.Abs(state.pitch - state.pitchTrim) < 0.01)
                {
                    if (output > 1)
                        output = 1;
                    else if (output < -1)
                        output = -1;

                    state.pitch = (float)output + state.pitchTrim;
                }

            }
            else
                sys.errorIntegral = 0;
            sys = systemInstances[3];
            if (sys.active)
            {
                if (info.aoA > aoAHighLim)
                {
                    state.pitch = (float)FARMathUtil.Clamp(ControlStateChange(sys, info.aoA - aoAHighLim), -1, 1) + state.pitchTrim;
                }
                else if (info.aoA < aoALowLim)
                {
                    state.pitch = (float)FARMathUtil.Clamp(ControlStateChange(sys, info.aoA - aoALowLim), -1, 1) + state.pitchTrim;
                }
            }
            else
                sys.errorIntegral = 0;
            sys = systemInstances[4];
            if (sys.active)
            {
                double scalingFactor = scalingDynPres / info.dynPres;

                if (scalingFactor > 1)
                    scalingFactor = 1;

                state.pitch = state.pitchTrim + (state.pitch - state.pitchTrim) * (float)scalingFactor;
                state.yaw = state.yawTrim + (state.yaw - state.yawTrim) * (float)scalingFactor;
                state.roll = state.rollTrim + (state.roll - state.rollTrim) * (float)scalingFactor;
            }
        }

        private double ControlStateChange(ControlSystem system, double error)
        {
            double state = 0;
            double dt = TimeWarp.fixedDeltaTime;

            double dError_dt = error - system.lastError;
            dError_dt /= dt;

            system.errorIntegral += error * dt;

            state -= system.kP * error + system.kD * dError_dt + system.kI * system.errorIntegral;

            system.lastError = error;

            return state;
        }

        public void LoadSettings()
        {
            List<ConfigNode> flightGUISettings = FARSettingsScenarioModule.FlightGUISettings;

            ConfigNode node = null;
            for (int i = 0; i < flightGUISettings.Count; i++)
                if (flightGUISettings[i].name == "StabilityAugmentationSettings")
                {
                    node = flightGUISettings[i];
                    break;
                }

            if (systemTemplates == null)
            {
                systemTemplates = new ControlSystem[5];
                for (int i = 0; i < systemTemplates.Length; i++)
                    systemTemplates[i] = new ControlSystem();



                if (node != null)
                {
                    for (int i = 0; i < systemTemplates.Length; i++)
                    {
                        string nodeName = "ControlSys" + i;
                        if (node.HasNode(nodeName))
                            TryLoadSystem(node.GetNode(nodeName), i);
                    }
                    if (node.HasValue("aoALowLim"))
                        double.TryParse(node.GetValue("aoALowLim"), out aoALowLim);
                    if (node.HasValue("aoAHighLim"))
                        double.TryParse(node.GetValue("aoAHighLim"), out aoAHighLim);
                    if (node.HasValue("scalingDynPres"))
                        double.TryParse(node.GetValue("scalingDynPres"), out scalingDynPres);
                }
                else
                {
                    BuildDefaultSystems();
                }
            }
        }

        public static void BuildDefaultSystems()
        {
            ControlSystem sys = new ControlSystem();
            //Roll system
            sys.kP = 0.5;
            sys.kD = 1;
            sys.kI = 0.5;

            systemTemplates[0] = sys;

            sys = new ControlSystem();
            //Yaw system
            sys.kP = 0;
            sys.kD = 1;
            sys.kI = 0;

            systemTemplates[1] = sys;

            sys = new ControlSystem();
            //Pitch system
            sys.kP = 0;
            sys.kD = 1;
            sys.kI = 0;

            systemTemplates[2] = sys;

            sys = new ControlSystem();
            //AoA system
            sys.kP = 0.25;
            sys.kD = 0;
            sys.kI = 0;

            aoALowLim = -10;
            aoAHighLim = 20;

            systemTemplates[3] = sys;

            scalingDynPres = 20;
        }

        public static bool TryLoadSystem(ConfigNode systemNode, int index)
        {
            bool sysExists = false;
            ControlSystem sys = systemTemplates[index];

            if (systemNode.HasValue("active"))
            {
                bool.TryParse(systemNode.GetValue("active"), out sys.active);
                sysExists |= true;
            }

            if (systemNode.HasValue("zeroPoint"))
                double.TryParse(systemNode.GetValue("zeroPoint"), out sys.zeroPoint);

            if (systemNode.HasValue("kP"))
                double.TryParse(systemNode.GetValue("kP"), out sys.kP);
            if (systemNode.HasValue("kD"))
                double.TryParse(systemNode.GetValue("kD"), out sys.kD);
            if (systemNode.HasValue("kI"))
                double.TryParse(systemNode.GetValue("kI"), out sys.kI);

            systemTemplates[index] = sys;
            return sysExists;
        }

        public void SaveSettings()
        {
            List<ConfigNode> flightGUISettings = FARSettingsScenarioModule.FlightGUISettings;
            if (flightGUISettings == null)
            {
                Debug.LogError("Could not save Stability Augmentation Settings because settings config list was null");
            }
            ConfigNode node = null;
            for (int i = 0; i < flightGUISettings.Count; i++)
                if (flightGUISettings[i].name == "StabilityAugmentationSettings")
                {
                    node = flightGUISettings[i];
                    break;
                }

            if (this._vessel == FlightGlobals.ActiveVessel)
            {
                systemTemplates = systemInstances;

                if (node == null)
                {
                    node = new ConfigNode("StabilityAugmentationSettings");
                    flightGUISettings.Add(node);
                }
                else
                    node.ClearData();

                for (int i = 0; i < systemTemplates.Length; i++)
                {
                    node.AddNode(BuildSystemNode(i));
                }
                node.AddValue("aoALowLim", aoALowLim);
                node.AddValue("aoAHighLim", aoAHighLim);
                node.AddValue("scalingDynPres", scalingDynPres);
            }
        }

        public static ConfigNode BuildSystemNode(int index)
        {
            ControlSystem sys = systemTemplates[index];

            ConfigNode node = new ConfigNode("ControlSys" + index);
            node.AddValue("active", sys.active);
            node.AddValue("zeroPoint", sys.zeroPoint);

            node.AddValue("kP", sys.kP);
            node.AddValue("kD", sys.kD);
            node.AddValue("kI", sys.kI);

            return node;
        }

        class ControlSystem
        {
            public bool active;

            public double zeroPoint;

            public double kP;
            public double kD;
            public double kI;

            public double lastError;
            public double errorIntegral;

            public ControlSystem(ControlSystem sys)
            {
                this.active = sys.active;
                this.zeroPoint = sys.zeroPoint;
                this.kP = sys.kP;
                this.kD = sys.kD;
                this.kI = sys.kI;

                this.lastError = 0;
                this.errorIntegral = 0;
            }

            public ControlSystem() { }
        }
    }
}
