/*
Ferram Aerospace Research v0.13.2
Copyright 2014, Michael Ferrara, aka Ferram4

    This file is part of Ferram Aerospace Research.

    Ferram Aerospace Research is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Kerbal Joint Reinforcement is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Ferram Aerospace Research.  If not, see <http://www.gnu.org/licenses/>.

    Serious thanks:		a.g., for tons of bugfixes and code-refactorings
            			Taverius, for correcting a ton of incorrect values
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager 1.5 updates
            			ialdabaoth (who is awesome), who originally created Module Manager
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
using Toolbar;

namespace ferram4
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class FARDebugOptions : MonoBehaviour
    {

        public static KSP.IO.PluginConfiguration config;
        private IButton FARDebugButton;
        private bool debugMenu = false;
        private Rect debugWinPos = new Rect(50, 50, 250, 250);

        public void Awake()
        {
            LoadConfigs();
            FARDebugButton = ToolbarManager.Instance.add("ferram4", "FARDebugButton");
            FARDebugButton.TexturePath = "FerramAerospaceResearch/Textures/icon_button";
            FARDebugButton.ToolTip = "FAR Debug Options";
            FARDebugButton.OnClick += (e) => debugMenu = !debugMenu;
        }

        public void OnGUI()
        {
            GUI.skin = HighLogic.Skin;
            if (debugMenu)
                debugWinPos = GUILayout.Window("FARDebug".GetHashCode(), debugWinPos, debugWindow, "FAR Debug Options, v0.13.2", GUILayout.MinWidth(250), GUILayout.ExpandHeight(true));
        }


        private void debugWindow(int windowID)
        {

            GUIStyle thisStyle = new GUIStyle(GUI.skin.toggle);
            thisStyle.stretchHeight = true;
            thisStyle.stretchWidth = true;
            thisStyle.padding = new RectOffset(4, 0, 0, 0);
            thisStyle.margin = new RectOffset(4, 0, 0, 0);

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

            //            SaveWindowPos.x = windowPos.x;
            //            SaveWindowPos.y = windowPos.y;

            GUI.DragWindow();

            debugWinPos = FARGUIUtils.ClampToScreen(debugWinPos);
        }

        public static void LoadConfigs()
        {
            config = KSP.IO.PluginConfiguration.CreateForType<FARDebugOptions>();
            config.load();
            FARAeroUtil.areaFactor = Convert.ToDouble(config.GetValue("areaFactor", "1"));
            FARAeroUtil.attachNodeRadiusFactor = Convert.ToDouble(config.GetValue("attachNodeDiameterFactor", "1.25")) * 0.5f;
            FARAeroUtil.incompressibleRearAttachDrag = Convert.ToDouble(config.GetValue("incompressibleRearAttachDrag", "0.5"));
            FARAeroUtil.sonicRearAdditionalAttachDrag = Convert.ToDouble(config.GetValue("sonicRearAdditionalAttachDrag", "0.5"));

            FARDebugValues.displayForces = Convert.ToBoolean(config.GetValue("displayForces", "false"));
            FARDebugValues.displayCoefficients = Convert.ToBoolean(config.GetValue("displayCoefficients", "false"));
            FARDebugValues.displayShielding = Convert.ToBoolean(config.GetValue("displayShielding", "false"));
            FARDebugValues.useSplinesForSupersonicMath = Convert.ToBoolean(config.GetValue("useSplinesForSupersonicMath", "true"));
            FARDebugValues.allowStructuralFailures = Convert.ToBoolean(config.GetValue("allowStructuralFailures", "true"));

            FARControllableSurface.timeConstant = Convert.ToSingle(config.GetValue("ctrlSurfTimeConstant", "0.05"));

            FARAeroUtil.bodyAtmosphereConfiguration = new Dictionary<int, Vector3d>();
            int i = 0;
            while (i < FlightGlobals.Bodies.Count)
            {
                int index = FlightGlobals.Bodies[i].flightGlobalsIndex;
                Vector3d Rgamma_and_gamma = new Vector3d();

                Rgamma_and_gamma.y = Convert.ToDouble(config.GetValue("Body" + index + "SpecHeatRatio", "1.4"));
                Rgamma_and_gamma.z = 8.3145 * 1000 / Convert.ToDouble(config.GetValue("Body" + index + "GasMolecularWeight", "28.96"));
                Rgamma_and_gamma.x = Rgamma_and_gamma.y * Rgamma_and_gamma.z;

                FARAeroUtil.bodyAtmosphereConfiguration.Add(index, Rgamma_and_gamma);
                i++;
            }

            FARMiscData.cargoBayTitles = new List<string>();
            FARMiscData.payloadFairingTitles = new List<string>();
            FARMiscData.exemptAttachNodes = new List<string>();

            i = 0;
            do
            {
                string tmpCargoBayTitle, tmpPayloadFairingTitle, tmpAttach;
                tmpCargoBayTitle = config.GetValue("cargoBayTitle" + i, "");
                tmpPayloadFairingTitle = config.GetValue("payloadFairingTitle" + i, "");
                tmpAttach = config.GetValue("exemptAttachNodeString" + i, "");


                if (tmpCargoBayTitle != "")
                    FARMiscData.cargoBayTitles.Add(tmpCargoBayTitle);
                else
                    config.SetValue("cargoBayTitle" + i, "");
                if (tmpPayloadFairingTitle != "")
                    FARMiscData.payloadFairingTitles.Add(tmpPayloadFairingTitle);
                else
                    config.SetValue("payloadFairingTitle" + i, "");

                if (tmpAttach != "")
                    FARMiscData.exemptAttachNodes.Add(tmpAttach);
                else
                    config.SetValue("exemptAttachNodeString" + i, "");

                if (tmpCargoBayTitle == "" && tmpPayloadFairingTitle == "" && tmpAttach == "")
                    break;

                i++;
            } while (true);
        }

        public static void SaveConfigs()
        {
            config.SetValue("areaFactor", FARAeroUtil.areaFactor.ToString());
            config.SetValue("attachNodeDiameterFactor", (FARAeroUtil.attachNodeRadiusFactor * 2).ToString());
            config.SetValue("incompressibleRearAttachDrag", FARAeroUtil.incompressibleRearAttachDrag.ToString());
            config.SetValue("sonicRearAdditionalAttachDrag", FARAeroUtil.sonicRearAdditionalAttachDrag.ToString());

            config.SetValue("ctrlSurfTimeConstant", FARControllableSurface.timeConstant.ToString());

            config.SetValue("displayForces", FARDebugValues.displayForces.ToString());
            config.SetValue("displayCoefficients", FARDebugValues.displayCoefficients.ToString());
            config.SetValue("displayShielding", FARDebugValues.displayShielding.ToString());

            config.SetValue("useSplinesForSupersonicMath", FARDebugValues.useSplinesForSupersonicMath.ToString());
            config.SetValue("allowStructuralFailures", FARDebugValues.allowStructuralFailures.ToString());

            config.save();
        }
        void OnDestroy()
        {
            SaveConfigs();
            FARDebugButton.Destroy();
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
    }
}
