/*
Ferram Aerospace Research v0.14.2
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
using System.Text.RegularExpressions;
using UnityEngine;
using KSP;
//using Toolbar;

namespace ferram4
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class FARDebugOptions : MonoBehaviour
    {

        public static KSP.IO.PluginConfiguration config;
        private IButton FARDebugButtonBlizzy = null;
        private ApplicationLauncherButton FARDebugButtonStock = null;
        private bool debugMenu = false;
        private Rect debugWinPos = new Rect(50, 50, 700, 250);

        private enum MenuTab
        {
            DebugAndData,
            PartClassification,
            AeroStress,
            AtmComposition
        }

        private static string[] MenuTab_str = new string[]
        {
            "Debug Options",
            "Part Classification",
            "Aerodynamic Failure",
            "Atm Composition",
        };

        public static string[] FlowMode_str = new string[]
        {
            "NO_FLOW",
            "ALL_VESSEL",
            "STAGE_PRIORITY_FLOW",
            "STACK_PRIORITY_SEARCH",
        };
       
        private MenuTab activeTab = MenuTab.DebugAndData;

        private int aeroStressIndex = 0;
        private int atmBodyIndex = 1;

        public void Awake()
        {
            if (!CompatibilityChecker.IsAllCompatible())
            {
                this.enabled = false;
                return;
            }

            LoadConfigs();
            if (FARDebugValues.useBlizzyToolbar)
            {
                FARDebugButtonBlizzy = ToolbarManager.Instance.add("ferram4", "FARDebugButtonBlizzy");
                FARDebugButtonBlizzy.TexturePath = "FerramAerospaceResearch/Textures/icon_button_blizzy";
                FARDebugButtonBlizzy.ToolTip = "FAR Debug Options";
                FARDebugButtonBlizzy.OnClick += (e) => debugMenu = !debugMenu;
            }
            else
                GameEvents.onGUIApplicationLauncherReady.Add(OnGUIAppLauncherReady);
        }

        void OnGUIAppLauncherReady()
        {
            if (ApplicationLauncher.Ready)
            {
                    FARDebugButtonStock = ApplicationLauncher.Instance.AddModApplication(
                        onAppLaunchToggleOn,
                        onAppLaunchToggleOff,
                        DummyVoid,
                        DummyVoid,
                        DummyVoid,
                        DummyVoid,
                        ApplicationLauncher.AppScenes.SPACECENTER,
                        (Texture)GameDatabase.Instance.GetTexture("FerramAerospaceResearch/Textures/icon_button_stock", false));
                
            }
        }

        void onAppLaunchToggleOn()
        {
            debugMenu = true;
        }

        void onAppLaunchToggleOff()
        {
            debugMenu = false;
        }

        void DummyVoid() { }


        public void OnGUI()
        {
            GUI.skin = HighLogic.Skin;
            if (debugMenu)
                debugWinPos = GUILayout.Window("FARDebug".GetHashCode(), debugWinPos, debugWindow, "FAR Debug Options, v0.14.2", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        }


        private void debugWindow(int windowID)
        {

            GUIStyle thisStyle = new GUIStyle(GUI.skin.toggle);
            thisStyle.stretchHeight = true;
            thisStyle.stretchWidth = true;
            thisStyle.padding = new RectOffset(4, 4, 4, 4);
            thisStyle.margin = new RectOffset(4, 4, 4, 4);

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.stretchHeight = true;
            buttonStyle.stretchWidth = true;
            buttonStyle.padding = new RectOffset(4, 4, 4, 4);
            buttonStyle.margin = new RectOffset(4, 4, 4, 4);

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.stretchHeight = true;
            boxStyle.stretchWidth = true;
            boxStyle.padding = new RectOffset(4, 4, 4, 4);
            boxStyle.margin = new RectOffset(4, 4, 4, 4);

            activeTab = (MenuTab)GUILayout.SelectionGrid((int)activeTab, MenuTab_str, 4);

            if (activeTab == MenuTab.DebugAndData)
                DebugAndDataTab(thisStyle);
            else if (activeTab == MenuTab.PartClassification)
                PartClassificationTab(buttonStyle, boxStyle);
            else if (activeTab == MenuTab.AeroStress)
                AeroStressTab(buttonStyle, boxStyle);
            else
                AeroDataTab(buttonStyle, boxStyle);

            //            SaveWindowPos.x = windowPos.x;
            //            SaveWindowPos.y = windowPos.y;

            GUI.DragWindow();
            debugWinPos = FARGUIUtils.ClampToScreen(debugWinPos);
        }

        private void AeroDataTab(GUIStyle buttonStyle, GUIStyle boxStyle)
        {
            int i = 0;
            GUILayout.BeginVertical(boxStyle);

            string tmp;

            tmp = FARAeroUtil.areaFactor.ToString();
            FARGUIUtils.TextEntryField("Area Factor:", 160, ref tmp);
            tmp = Regex.Replace(tmp, @"[^\d+-\.]", "");
            FARAeroUtil.areaFactor = Convert.ToDouble(tmp);

            tmp = (FARAeroUtil.attachNodeRadiusFactor * 2).ToString();
            FARGUIUtils.TextEntryField("Node Diameter Factor:", 160, ref tmp);
            tmp = Regex.Replace(tmp, @"[^\d+-\.]", "");
            FARAeroUtil.attachNodeRadiusFactor = Convert.ToDouble(tmp) * 0.5;

            tmp = FARAeroUtil.incompressibleRearAttachDrag.ToString();
            FARGUIUtils.TextEntryField("Rear Node Drag, Incomp:", 160, ref tmp);
            tmp = Regex.Replace(tmp, @"[^\d+-\.]", "");
            FARAeroUtil.incompressibleRearAttachDrag = Convert.ToDouble(tmp);

            tmp = FARAeroUtil.sonicRearAdditionalAttachDrag.ToString();
            FARGUIUtils.TextEntryField("Rear Node Drag, M = 1:", 160, ref tmp);
            tmp = Regex.Replace(tmp, @"[^\d+-\.]", "");
            FARAeroUtil.sonicRearAdditionalAttachDrag = Convert.ToDouble(tmp);

            tmp = FARControllableSurface.timeConstant.ToString();
            FARGUIUtils.TextEntryField("Ctrl Surf Time Constant:", 160, ref tmp);
            tmp = Regex.Replace(tmp, @"[^\d+-\.]", "");
            FARControllableSurface.timeConstant = Convert.ToDouble(tmp);

            tmp = FARControllableSurface.timeConstantFlap.ToString();
            FARGUIUtils.TextEntryField("Flap/Spoiler Time Constant:", 160, ref tmp);
            tmp = Regex.Replace(tmp, @"[^\d+-\.]", "");
            FARControllableSurface.timeConstantFlap = Convert.ToDouble(tmp);

            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            GUILayout.Label("Celestial Body Atmosperic Properties");

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(boxStyle);

            GUILayout.BeginHorizontal();
            int j = 0;
            for (i = 0; i < FlightGlobals.Bodies.Count; i++)
            {
                CelestialBody body = FlightGlobals.Bodies[i];

                if (!body.atmosphere)
                    continue;

                bool active = GUILayout.Toggle(i == atmBodyIndex, body.GetName(), buttonStyle, GUILayout.Width(150), GUILayout.Height(40));
                if (active)
                    atmBodyIndex = i;
                if ((j + 1) % 4 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
                j++;
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.BeginVertical(boxStyle);

            int flightGlobalsIndex = FlightGlobals.Bodies[atmBodyIndex].flightGlobalsIndex;

            Vector3d atmProperties = FARAeroUtil.bodyAtmosphereConfiguration[flightGlobalsIndex];

            tmp = atmProperties.y.ToString();
            FARGUIUtils.TextEntryField("Ratio of Specific Heats:", 80, ref tmp);
            tmp = Regex.Replace(tmp, @"[^\d+-\.]", "");
            atmProperties.y = Convert.ToDouble(tmp);


            double dTmp = 8314.5 / atmProperties.z;
            tmp = dTmp.ToString();
            FARGUIUtils.TextEntryField("Gas Molecular Mass:", 80, ref tmp);
            tmp = Regex.Replace(tmp, @"[^\d+-\.]", "");
            atmProperties.z = 8314.5 / Convert.ToDouble(tmp);

            atmProperties.x = atmProperties.y * atmProperties.z;

            FARAeroUtil.bodyAtmosphereConfiguration[flightGlobalsIndex] = atmProperties;

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        
        private void AeroStressTab(GUIStyle buttonStyle, GUIStyle boxStyle)
        {
            int i = 0;
            int removeIndex = -1;
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(boxStyle);

            for (i = 0; i < FARAeroStress.StressTemplates.Count; i++)
            {
                GUILayout.BeginHorizontal();
                bool active = GUILayout.Toggle(i == aeroStressIndex, FARAeroStress.StressTemplates[i].name, buttonStyle, GUILayout.Width(150));
                if (GUILayout.Button("-", buttonStyle, GUILayout.Width(30), GUILayout.Height(30)))
                    removeIndex = i;
                GUILayout.EndHorizontal();
                if (active)
                    aeroStressIndex = i;
            }
            if (removeIndex >= 0)
            {
                FARAeroStress.StressTemplates.RemoveAt(removeIndex);
                if (aeroStressIndex == removeIndex)
                    aeroStressIndex--;

                removeIndex = -1;
            }
            if (GUILayout.Button("+", buttonStyle, GUILayout.Width(30), GUILayout.Height(30)))
            {
                FARPartStressTemplate newTemplate = new FARPartStressTemplate();
                newTemplate.XZmaxStress = 500;
                newTemplate.YmaxStress = 500;
                newTemplate.name = "default";
                newTemplate.isSpecialTemplate = false;
                newTemplate.minNumResources = 0;
                newTemplate.resources = new List<string>();
                newTemplate.excludeResources = new List<string>();
                newTemplate.rejectUnlistedResources = false;
                newTemplate.crewed = false;
                newTemplate.flowModeNeeded = false;
                newTemplate.flowMode = ResourceFlowMode.NO_FLOW;

                FARAeroStress.StressTemplates.Add(newTemplate);
            }

            GUILayout.EndVertical();
            GUILayout.BeginVertical(boxStyle);

            FARPartStressTemplate activeTemplate = FARAeroStress.StressTemplates[aeroStressIndex];

            string tmp;

            FARGUIUtils.TextEntryField("Name:", 80, ref activeTemplate.name);

            tmp = activeTemplate.YmaxStress.ToString();
            FARGUIUtils.TextEntryField("Axial (Y-axis) Max Stress:", 240, ref tmp);
                        tmp = Regex.Replace(tmp, @"[^\d+-\.]", "");
            activeTemplate.YmaxStress = Convert.ToDouble(tmp);

            tmp = activeTemplate.XZmaxStress.ToString();
            FARGUIUtils.TextEntryField("Lateral (X,Z-axis) Max Stress:", 240, ref tmp);
                        tmp = Regex.Replace(tmp, @"[^\d+-\.]", "");
            activeTemplate.XZmaxStress = Convert.ToDouble(tmp);
           
            activeTemplate.crewed = GUILayout.Toggle(activeTemplate.crewed, "Requires Crew Compartment");

            tmp = activeTemplate.minNumResources.ToString();
            FARGUIUtils.TextEntryField("Min Num Resources:", 80, ref tmp);
                        tmp = Regex.Replace(tmp, @"[^\d+-\.]", "");
            activeTemplate.minNumResources = Convert.ToInt32(tmp);

            GUILayout.Label("Req Resources:");
            StringListUpdateGUI(activeTemplate.resources, buttonStyle, boxStyle);

            GUILayout.Label("Exclude Resources:");
            StringListUpdateGUI(activeTemplate.excludeResources, buttonStyle, boxStyle);

            activeTemplate.rejectUnlistedResources = GUILayout.Toggle(activeTemplate.rejectUnlistedResources, "Reject Unlisted Res");

            activeTemplate.flowModeNeeded = GUILayout.Toggle(activeTemplate.flowModeNeeded, "Requires Specific Flow Mode");
            if (activeTemplate.flowModeNeeded)
            {
                activeTemplate.flowMode = (ResourceFlowMode)GUILayout.SelectionGrid((int)activeTemplate.flowMode, FlowMode_str, 1);
            }

            activeTemplate.isSpecialTemplate = GUILayout.Toggle(activeTemplate.isSpecialTemplate, "Special Hardcoded Usage");

            FARAeroStress.StressTemplates[aeroStressIndex] = activeTemplate;


            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
            
        private void PartClassificationTab(GUIStyle buttonStyle, GUIStyle boxStyle)
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("Greeble - Parts with low, orientation un-affected drag");

            //Greeble Title Section
            GUILayout.Label("Title Contains:");
            StringListUpdateGUI(FARPartClassification.greebleTitles, buttonStyle, boxStyle);

            //Greeble Modules Section
            GUILayout.Label("Part Modules:");
            StringListUpdateGUI(FARPartClassification.greebleModules, buttonStyle, boxStyle);

            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            GUILayout.Label("Exempt - Parts that do not get a FAR drag model");

            //Exempt Modules Section
            GUILayout.Label("Part Modules:");
            StringListUpdateGUI(FARPartClassification.exemptModules, buttonStyle, boxStyle);

            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            GUILayout.Label("Specialized Modules - Used to determine fairings and cargo bays");

            //Payload Fairing Section
            GUILayout.Label("Fairing Title Contains:");
            StringListUpdateGUI(FARPartClassification.payloadFairingTitles, buttonStyle, boxStyle);

            //Payload Fairing Section
            GUILayout.Label("Cargo Bay Title Contains:");
            StringListUpdateGUI(FARPartClassification.cargoBayTitles, buttonStyle, boxStyle);

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void StringListUpdateGUI(List<string> stringList, GUIStyle thisStyle, GUIStyle boxStyle)
        {
            int removeIndex = -1;
            GUILayout.BeginVertical(boxStyle);
            for (int i = 0; i < stringList.Count; i++)
            {
                string tmp = stringList[i];
                GUILayout.BeginHorizontal();
                tmp = GUILayout.TextField(tmp, GUILayout.Height(30));
                if (GUILayout.Button("-", thisStyle, GUILayout.Width(30), GUILayout.Height(30)))
                    removeIndex = i;
                GUILayout.EndHorizontal();
                if (removeIndex >= 0)
                    break;

                stringList[i] = tmp;
            }
            if (removeIndex >= 0)
            {
                stringList.RemoveAt(removeIndex);
                removeIndex = -1;
            }
            if (GUILayout.Button("+", thisStyle, GUILayout.Width(30), GUILayout.Height(30)))
                stringList.Add("");

            GUILayout.EndVertical();
        }
        
        private void DebugAndDataTab(GUIStyle thisStyle)
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("Part Right-Click Menu");
            FARDebugValues.displayForces = GUILayout.Toggle(FARDebugValues.displayForces, "Display Aero Forces", thisStyle);
            FARDebugValues.displayCoefficients = GUILayout.Toggle(FARDebugValues.displayCoefficients, "Display Coefficients", thisStyle);
            FARDebugValues.displayShielding = GUILayout.Toggle(FARDebugValues.displayShielding, "Display Shielding", thisStyle);
            GUILayout.Label("Debug / Cheat Options");
            FARDebugValues.useSplinesForSupersonicMath = GUILayout.Toggle(FARDebugValues.useSplinesForSupersonicMath, "Use Splines for Supersonic Math", thisStyle);
            FARDebugValues.allowStructuralFailures = GUILayout.Toggle(FARDebugValues.allowStructuralFailures, "Allow Aero-structural Failures", thisStyle);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            if (ToolbarManager.ToolbarAvailable)
            {
                GUILayout.Label("Other Options"); // DaMichel: put it above the toolbar toggle
                GUILayout.BeginVertical();
                FARDebugValues.useBlizzyToolbar = GUILayout.Toggle(FARDebugValues.useBlizzyToolbar, "Use Blizzy78 Toolbar instead of Stock AppManager", thisStyle);
                bool tmp = FARDebugValues.useBlizzyToolbar;

                if (tmp != FARDebugValues.useBlizzyToolbar)
                {
                    if (FARDebugButtonStock != null)
                        ApplicationLauncher.Instance.RemoveModApplication(FARDebugButtonStock);

                    if (FARDebugButtonBlizzy != null)
                        FARDebugButtonBlizzy.Destroy();

                    if (FARDebugValues.useBlizzyToolbar)
                    {
                        FARDebugButtonBlizzy = ToolbarManager.Instance.add("ferram4", "FARDebugButtonBlizzy");
                        FARDebugButtonBlizzy.TexturePath = "FerramAerospaceResearch/Textures/icon_button_blizzy";
                        FARDebugButtonBlizzy.ToolTip = "FAR Debug Options";
                        FARDebugButtonBlizzy.OnClick += (e) => debugMenu = !debugMenu;
                    }
                    else
                        GameEvents.onGUIApplicationLauncherReady.Add(OnGUIAppLauncherReady);
                }
                FARActionGroupConfiguration.DrawGUI();
                GUILayout.EndVertical();
            }
        }

        public static void LoadConfigs()
        {
            config = KSP.IO.PluginConfiguration.CreateForType<FARDebugOptions>();
            config.load();
            FARDebugValues.displayForces = Convert.ToBoolean(config.GetValue("displayForces", "false"));
            FARDebugValues.displayCoefficients = Convert.ToBoolean(config.GetValue("displayCoefficients", "false"));
            FARDebugValues.displayShielding = Convert.ToBoolean(config.GetValue("displayShielding", "false"));
            FARDebugValues.useSplinesForSupersonicMath = Convert.ToBoolean(config.GetValue("useSplinesForSupersonicMath", "true"));
            FARDebugValues.allowStructuralFailures = Convert.ToBoolean(config.GetValue("allowStructuralFailures", "true"));

            FARDebugValues.useBlizzyToolbar = Convert.ToBoolean(config.GetValue("useBlizzyToolbar", "false"));

            FARAeroStress.LoadStressTemplates();
            FARPartClassification.LoadClassificationTemplates();
            FARAeroUtil.LoadAeroDataFromConfig();
            FARActionGroupConfiguration.LoadConfiguration();
        }

        public static void SaveConfigs()
        {
            config.SetValue("displayForces", FARDebugValues.displayForces.ToString());
            config.SetValue("displayCoefficients", FARDebugValues.displayCoefficients.ToString());
            config.SetValue("displayShielding", FARDebugValues.displayShielding.ToString());

            config.SetValue("useSplinesForSupersonicMath", FARDebugValues.useSplinesForSupersonicMath.ToString());
            config.SetValue("allowStructuralFailures", FARDebugValues.allowStructuralFailures.ToString());

            config.SetValue("useBlizzyToolbar", FARDebugValues.useBlizzyToolbar.ToString());

            FARDebugValues.useBlizzyToolbar &= ToolbarManager.ToolbarAvailable;

            FARAeroUtil.SaveCustomAeroDataToConfig();
            FARPartClassification.SaveCustomClassificationTemplates();
            FARAeroStress.SaveCustomStressTemplates();
            FARActionGroupConfiguration.SaveConfigruration();
            config.save();
        }
        void OnDestroy()
        {
            if (!CompatibilityChecker.IsAllCompatible())
                return;

            SaveConfigs();
            GameEvents.onGUIApplicationLauncherReady.Remove(OnGUIAppLauncherReady);
            if (FARDebugButtonStock != null)
                ApplicationLauncher.Instance.RemoveModApplication(FARDebugButtonStock);

            if (FARDebugButtonBlizzy != null)
                FARDebugButtonBlizzy.Destroy();
        }

    }

    public static class FARDebugValues
    {
        //Right-click menu options
        public static bool displayForces = false;
        public static bool displayCoefficients = false;
        public static bool displayShielding = false;

        public static bool useSplinesForSupersonicMath = true;
        public static bool allowStructuralFailures = true;

        public static bool useBlizzyToolbar = false;
    }
}
