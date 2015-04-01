/*
Ferram Aerospace Research v0.14.7
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
        private bool inputLocked = false;
        private Rect debugWinPos = new Rect(50, 50, 700, 250);
        private static Texture2D cLTexture = new Texture2D(25, 15);
        private static Texture2D cDTexture = new Texture2D(25, 15);
        private static Texture2D cMTexture = new Texture2D(25, 15);
        private static Texture2D l_DTexture = new Texture2D(25, 15);

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
            {
                debugWinPos = GUILayout.Window("FARDebug".GetHashCode(), debugWinPos, debugWindow, "FAR Debug Options, v0.14.7", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                if (!inputLocked && debugWinPos.Contains(FARGUIUtils.GetMousePos()))
                {
                    InputLockManager.SetControlLock(ControlTypes.KSC_ALL, "FARDebugLock");
                    inputLocked = true;
                }
                else if (inputLocked && !debugWinPos.Contains(FARGUIUtils.GetMousePos()))
                {
                    InputLockManager.RemoveControlLock("FARDebugLock");
                    inputLocked = false;
                }
            }
            else if (inputLocked)
            {
                InputLockManager.RemoveControlLock("FARDebugLock");
                inputLocked = false;
            }
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

            FARAeroUtil.areaFactor = FARGUIUtils.TextEntryForDouble("Area Factor:", 160, FARAeroUtil.areaFactor);
            FARAeroUtil.attachNodeRadiusFactor = FARGUIUtils.TextEntryForDouble("Node Diameter Factor:", 160, FARAeroUtil.attachNodeRadiusFactor * 2) * 0.5;
            FARAeroUtil.incompressibleRearAttachDrag = FARGUIUtils.TextEntryForDouble("Rear Node Drag, Incomp:", 160, FARAeroUtil.incompressibleRearAttachDrag);
            FARAeroUtil.sonicRearAdditionalAttachDrag = FARGUIUtils.TextEntryForDouble("Rear Node Drag, M = 1:", 160, FARAeroUtil.sonicRearAdditionalAttachDrag);
            FARControllableSurface.timeConstant = FARGUIUtils.TextEntryForDouble("Ctrl Surf Time Constant:", 160, FARControllableSurface.timeConstant);
            FARControllableSurface.timeConstantFlap = FARGUIUtils.TextEntryForDouble("Flap Time Constant:", 160, FARControllableSurface.timeConstantFlap);
            FARControllableSurface.timeConstantSpoiler = FARGUIUtils.TextEntryForDouble("Spoiler Time Constant:", 160, FARControllableSurface.timeConstantSpoiler);


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

            double[] atmProperties = FARAeroUtil.bodyAtmosphereConfiguration[flightGlobalsIndex];

            atmProperties[1] = FARGUIUtils.TextEntryForDouble("Ratio of Specific Heats:", 80, atmProperties[1]);


            double dTmp = 8314.5 / atmProperties[2];
            dTmp = FARGUIUtils.TextEntryForDouble("Gas Molecular Mass:", 80, dTmp);
            atmProperties[2] = 8314.5 / dTmp;

            atmProperties[0] = atmProperties[1] * atmProperties[2];

            atmProperties[3] = FARGUIUtils.TextEntryForDouble("Gas Viscosity:", 80, atmProperties[3]);
            atmProperties[4] = FARGUIUtils.TextEntryForDouble("Ref Temp for Viscosity:", 80, atmProperties[4]);

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

            activeTemplate.YmaxStress = FARGUIUtils.TextEntryForDouble("Axial (Y-axis) Max Stress:", 240, activeTemplate.YmaxStress);
            activeTemplate.XZmaxStress = FARGUIUtils.TextEntryForDouble("Lateral (X,Z-axis) Max Stress:", 240, activeTemplate.XZmaxStress);
           
            activeTemplate.crewed = GUILayout.Toggle(activeTemplate.crewed, "Requires Crew Compartment");

            tmp = activeTemplate.minNumResources.ToString();
            FARGUIUtils.TextEntryField("Min Num Resources:", 80, ref tmp);
                        tmp = Regex.Replace(tmp, @"[^\d]", "");
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
            GUILayout.Label("Editor GUI Graph Colors");
            ChangeColor("Cl", ref FAREditorGUI.clColor, ref cLTexture);
            ChangeColor("Cd", ref FAREditorGUI.cdColor, ref cDTexture);
            ChangeColor("Cm", ref FAREditorGUI.cmColor, ref cMTexture);
            ChangeColor("L_D", ref FAREditorGUI.l_DColor, ref l_DTexture);
            

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Other Options"); // DaMichel: put it above the toolbar toggle
            GUILayout.BeginVertical();
            FARDebugValues.aeroFailureExplosions = GUILayout.Toggle(FARDebugValues.aeroFailureExplosions, "Aero Failures Create Explosions", thisStyle);
            if (ToolbarManager.ToolbarAvailable)
            {
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
            }
            FARActionGroupConfiguration.DrawGUI();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void ChangeColor(string colorTitle, ref Color input, ref Texture2D texture)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label(colorTitle + " (r,g,b):", GUILayout.Width(80));

            bool updateTexture = false;

            GUILayout.BeginHorizontal(GUILayout.Width(150));
            float tmp = input.r;
            input.r = (float)FARGUIUtils.TextEntryForDouble("", 0, input.r);
            updateTexture |= tmp != input.r;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.Width(150));
            tmp = input.g;
            input.g = (float)FARGUIUtils.TextEntryForDouble("", 0, input.g);
            updateTexture |= tmp != input.g;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.Width(150));
            tmp = input.b;
            input.b = (float)FARGUIUtils.TextEntryForDouble("", 0, input.b);
            updateTexture |= tmp != input.b;
            GUILayout.EndHorizontal();

            if (updateTexture)
                ReColorTexture(ref input, ref texture);

            Rect textRect = GUILayoutUtility.GetRect(25, 15);
            textRect.Set(textRect.x, textRect.y + 10, textRect.width, textRect.height);
            GUI.DrawTexture(textRect, texture);
            GUILayout.EndHorizontal();
        }

        public static void ReColorTexture(ref Color color, ref Texture2D texture)
        {
            for (int i = 0; i < texture.width; i++)
                for (int j = 0; j < texture.height; j++)
                    texture.SetPixel(i, j, color);

            texture.Apply();
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
            FARDebugValues.aeroFailureExplosions = Convert.ToBoolean(config.GetValue("aeroFailureExplosions", "true"));

            FARAeroStress.LoadStressTemplates();
            FARPartClassification.LoadClassificationTemplates();
            FARAeroUtil.LoadAeroDataFromConfig();
            FARActionGroupConfiguration.LoadConfiguration();
            FAREditorGUI.LoadColors();
            ReColorTexture(ref FAREditorGUI.clColor, ref cLTexture);
            ReColorTexture(ref FAREditorGUI.cdColor, ref cDTexture);
            ReColorTexture(ref FAREditorGUI.cmColor, ref cMTexture);
            ReColorTexture(ref FAREditorGUI.l_DColor, ref l_DTexture);
        }

        public static void SaveConfigs()
        {
            config.SetValue("displayForces", FARDebugValues.displayForces.ToString());
            config.SetValue("displayCoefficients", FARDebugValues.displayCoefficients.ToString());
            config.SetValue("displayShielding", FARDebugValues.displayShielding.ToString());

            config.SetValue("useSplinesForSupersonicMath", FARDebugValues.useSplinesForSupersonicMath.ToString());
            config.SetValue("allowStructuralFailures", FARDebugValues.allowStructuralFailures.ToString());

            config.SetValue("useBlizzyToolbar", FARDebugValues.useBlizzyToolbar.ToString());
            config.SetValue("aeroFailureExplosions", FARDebugValues.aeroFailureExplosions.ToString());

            FARDebugValues.useBlizzyToolbar &= ToolbarManager.ToolbarAvailable;

            FARAeroUtil.SaveCustomAeroDataToConfig();
            FARPartClassification.SaveCustomClassificationTemplates();
            FARAeroStress.SaveCustomStressTemplates();
            FARActionGroupConfiguration.SaveConfigruration();
            FAREditorGUI.SaveCustomColors();
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
        public static bool aeroFailureExplosions = true;
    }
}
