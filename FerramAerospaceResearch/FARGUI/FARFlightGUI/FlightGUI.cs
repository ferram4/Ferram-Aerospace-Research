/*
Ferram Aerospace Research v0.15.6.2 "Kartveli"
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
using System.Text;
using UnityEngine;
using KSP;
using KSP.UI.Screens;
using FerramAerospaceResearch.FARAeroComponents;
using ferram4;

namespace FerramAerospaceResearch.FARGUI.FARFlightGUI
{
    public class FlightGUI : VesselModule
    {
        Vessel _vessel;
        FARVesselAero _vesselAero;

        static bool showGUI = false;
        public static bool showAllGUI = true;
        static Rect mainGuiRect;
        static Rect dataGuiRect;
        static Rect settingsGuiRect;
        static ApplicationLauncherButton flightGUIAppLauncherButton;
        static IButton blizzyFlightGUIButton;
        static int activeFlightGUICount = 0;
        public static Dictionary<Vessel, FlightGUI> vesselFlightGUI;

        PhysicsCalcs _physicsCalcs;
        VesselFlightInfo infoParameters;
        public VesselFlightInfo InfoParameters
        {
            get { return infoParameters; }
        }

        FlightStatusGUI _flightStatusGUI;
        StabilityAugmentation _stabilityAugmentation;
        FlightDataGUI _flightDataGUI;
        AeroVisualizationGUI _aeroVizGUI;
        public AeroVisualizationGUI AeroVizGUI
        {
            get { return _aeroVizGUI; }
        }
        AirspeedSettingsGUI _airSpeedGUI;
        public AirspeedSettingsGUI airSpeedGUI
        {
            get { return _airSpeedGUI; }
        }

        bool showFlightDataWindow = false;
        bool showSettingsWindow = false;

        internal static GUIStyle boxStyle = null;
        internal static GUIStyle buttonStyle = null;

        GUIDropDown<int> settingsWindow;

        public override void OnAwake()
        {
            if (vesselFlightGUI == null)
            {
                vesselFlightGUI = new Dictionary<Vessel, FlightGUI>();
            }
        }

        void Start()
        {
            if (!CompatibilityChecker.IsAllCompatible())
            {
                this.enabled = false;
                return;
            }

            _vessel = GetComponent<Vessel>();
            _vesselAero = GetComponent<FARVesselAero>();
            _physicsCalcs = new PhysicsCalcs(_vessel, _vesselAero);
            _flightStatusGUI = new FlightStatusGUI();
            _stabilityAugmentation = new StabilityAugmentation(_vessel);
            _flightDataGUI = new FlightDataGUI();
            _aeroVizGUI = new AeroVisualizationGUI();

            settingsWindow = new GUIDropDown<int>(new string[4]{"Flt Data","Stab Aug", "Air Spd","Aero Viz"}, new int[4]{0,1,2,3}, 0);
            //boxStyle.padding = new RectOffset(4, 4, 4, 4);

            if (vesselFlightGUI.ContainsKey(_vessel))
                vesselFlightGUI[_vessel] = this;
            else
                vesselFlightGUI.Add(_vessel, this);

            this.enabled = true;

            if (FARDebugValues.useBlizzyToolbar)
            {
                GenerateBlizzyToolbarButton();
            }
            else
                OnGUIAppLauncherReady();

            activeFlightGUICount++;

            if(_vessel == FlightGlobals.ActiveVessel)
                LoadConfigs();

            GameEvents.onShowUI.Add(ShowUI);
            GameEvents.onHideUI.Add(HideUI);
        }

        void OnDestroy()
        {
            GameEvents.onShowUI.Remove(ShowUI);
            GameEvents.onHideUI.Remove(HideUI);
            SaveConfigs();
            if (_vessel)
            {
                vesselFlightGUI.Remove(_vessel);
            }
            _physicsCalcs = null;

            if(_flightDataGUI != null)
                _flightDataGUI.SaveSettings();
            _flightDataGUI = null;

            if(_stabilityAugmentation != null)
                _stabilityAugmentation.SaveAndDestroy();
            _stabilityAugmentation = null;

            if(_airSpeedGUI != null)
                _airSpeedGUI.SaveSettings();
            _airSpeedGUI = null;

            if (_aeroVizGUI != null)
                _aeroVizGUI.SaveSettings();

            _flightStatusGUI = null;
            settingsWindow = null;

            activeFlightGUICount--;

            if (activeFlightGUICount <= 0)
            {
                activeFlightGUICount = 0;
                if (blizzyFlightGUIButton != null)
                    ClearBlizzyToolbarButton();
            }
            
        }

        public void SaveData()
        {
            if (_vessel == FlightGlobals.ActiveVessel)
            {
                SaveConfigs();
                if(_airSpeedGUI != null)
                    _airSpeedGUI.SaveSettings();
                if(_stabilityAugmentation != null)
                    _stabilityAugmentation.SaveSettings();
                if(_flightDataGUI != null)
                    _flightDataGUI.SaveSettings();
                if(_aeroVizGUI != null)
                    _aeroVizGUI.SaveSettings();
            }
        }
        public static void SaveActiveData()
        {
            FlightGUI gui;
            if (FlightGlobals.ready && FlightGlobals.ActiveVessel != null && vesselFlightGUI != null && vesselFlightGUI.TryGetValue(FlightGlobals.ActiveVessel, out gui))
            {
                if(gui != null)
                    gui.SaveData();
            }
        }

        //Receives message from FARVesselAero through _vessel on the recalc being completed
        public void UpdateAeroModules(List<FARAeroPartModule> newAeroModules, List<FARWingAerodynamicModel> legacyWingModels)
        {
            _physicsCalcs.UpdateAeroModules(newAeroModules, legacyWingModels);
        }

        //Receives a message from any FARWingAerodynamicModel or FARAeroPartModule that has failed to update the GUI
        void AerodynamicFailureStatus()
        {
            if(_flightStatusGUI != null)
                _flightStatusGUI.AerodynamicFailureStatus();
        }

        #region PhysicsAndOrientationBlock
        void FixedUpdate()
        {
            if (_physicsCalcs == null)
                return;

            infoParameters = _physicsCalcs.UpdatePhysicsParameters();

            _stabilityAugmentation.UpdatePhysicsInfo(infoParameters);
            _flightStatusGUI.UpdateInfoParameters(infoParameters);
            _flightDataGUI.UpdateInfoParameters(infoParameters);
        }

        #endregion

        void LateUpdate()
        {
            OnGUIAppLauncherReady();
            if (_airSpeedGUI != null)
                _airSpeedGUI.ChangeSurfVelocity();
            else if (_vessel != null)
                _airSpeedGUI = new AirspeedSettingsGUI(_vessel);
        }

        #region GUI Functions

        void OnGUI()
        {
            GUI.skin = HighLogic.Skin;
            if(boxStyle == null)
            {
                boxStyle = new GUIStyle(GUI.skin.box);
                boxStyle.normal.textColor = boxStyle.focused.textColor = Color.white;
                boxStyle.hover.textColor = boxStyle.active.textColor = Color.yellow;
                boxStyle.onNormal.textColor = boxStyle.onFocused.textColor = boxStyle.onHover.textColor = boxStyle.onActive.textColor = Color.green;
                boxStyle.padding = new RectOffset(2, 2, 2, 2);
            }
            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.button);
                buttonStyle.normal.textColor = buttonStyle.focused.textColor = Color.white;
                buttonStyle.hover.textColor = buttonStyle.active.textColor = buttonStyle.onActive.textColor = Color.yellow;
                buttonStyle.onNormal.textColor = buttonStyle.onFocused.textColor = buttonStyle.onHover.textColor = Color.green;
                buttonStyle.padding = new RectOffset(2, 2, 2, 2);

            }
            if (_vessel == FlightGlobals.ActiveVessel && showGUI && showAllGUI)
            {
                mainGuiRect = GUILayout.Window(this.GetHashCode(), mainGuiRect, MainFlightGUIWindow, "FAR, v0.15.6.2 'Kartveli'", GUILayout.MinWidth(230));
                GUIUtils.ClampToScreen(mainGuiRect);

                if (showFlightDataWindow)
                {
                    dataGuiRect = GUILayout.Window(this.GetHashCode() + 1, dataGuiRect, FlightDataWindow, "FAR Flight Data", GUILayout.MinWidth(150));
                    GUIUtils.ClampToScreen(dataGuiRect);
                }

                if (showSettingsWindow)
                {
                    settingsGuiRect = GUILayout.Window(this.GetHashCode() + 2, settingsGuiRect, SettingsWindow, "FAR Settings", GUILayout.MinWidth(200));
                    GUIUtils.ClampToScreen(settingsGuiRect);
                }
            }
        }

        void MainFlightGUIWindow(int windowId)
        {
            GUILayout.BeginVertical(GUILayout.Height(100));
            GUILayout.BeginHorizontal();
            GUILayout.Box("Mach: " + _vesselAero.MachNumber.ToString("F3") + " \n\rReynolds: " + _vesselAero.ReynoldsNumber.ToString("e2"), boxStyle, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.Box("ATM Density: " + _vessel.atmDensity.ToString("F3"), boxStyle, GUILayout.ExpandWidth(true));

            _flightStatusGUI.Display();
            showFlightDataWindow = GUILayout.Toggle(showFlightDataWindow, "Flt Data", buttonStyle, GUILayout.ExpandWidth(true));
            showSettingsWindow = GUILayout.Toggle(showSettingsWindow, "Flt Settings", buttonStyle, GUILayout.ExpandWidth(true));

            GUILayout.Label("Flight Assistance Toggles:");

            _stabilityAugmentation.Display();

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        void FlightDataWindow(int windowId)
        {
            _flightDataGUI.DataDisplay();
            GUI.DragWindow();
        }

        void SettingsWindow(int windowId)
        {
            GUILayout.Label("Current Settings Group:");
            settingsWindow.GUIDropDownDisplay();
            int selection = settingsWindow.ActiveSelection;
            switch (selection)
            {
                case 0:
                    if (_flightDataGUI.SettingsDisplay())
                        dataGuiRect.height = 0;
                    break;
                case 1:
                    _stabilityAugmentation.SettingsDisplay();
                    break;
                case 2:
                    _airSpeedGUI.AirSpeedSettings();
                    break;
                case 3:
                    _aeroVizGUI.SettingsDisplay();
                    break;
            }
            GUI.DragWindow();
        }
        #endregion

        #region AppLauncher

        private static void ClearBlizzyToolbarButton()
        {
            blizzyFlightGUIButton.Destroy();
            blizzyFlightGUIButton = null;
        }

        private void GenerateBlizzyToolbarButton()
        {
            if (blizzyFlightGUIButton == null)
            {
                blizzyFlightGUIButton = ToolbarManager.Instance.add("FerramAerospaceResearch", "FARFlightButtonBlizzy");
                blizzyFlightGUIButton.TexturePath = "FerramAerospaceResearch/Textures/icon_button_blizzy";
                blizzyFlightGUIButton.ToolTip = "FAR Flight Sys";
                blizzyFlightGUIButton.OnClick += (e) => showGUI = !showGUI;
            }
        }

        public void OnGUIAppLauncherReady()
        {
            if (ApplicationLauncher.Ready && flightGUIAppLauncherButton == null)
            {
                flightGUIAppLauncherButton = ApplicationLauncher.Instance.AddModApplication(
                    onAppLaunchToggleOn,
                    onAppLaunchToggleOff,
                    DummyVoid,
                    DummyVoid,
                    DummyVoid,
                    DummyVoid,
                    ApplicationLauncher.AppScenes.FLIGHT,
                    (Texture)GameDatabase.Instance.GetTexture("FerramAerospaceResearch/Textures/icon_button_stock", false));
            }
        }

        void onAppLaunchToggleOn()
        {
            showGUI = true;
        }

        void onAppLaunchToggleOff()
        {
            showGUI = false;
        }

        void DummyVoid() { }

        private void HideUI()
        {
            showAllGUI = false;
        }

        private void ShowUI()
        {
            showAllGUI = true;
        }
        #endregion

        void SaveConfigs()
        {
            KSP.IO.PluginConfiguration config = KSP.IO.PluginConfiguration.CreateForType<FlightGUI>();
            config.SetValue("flight_mainGuiRect", mainGuiRect);
            config.SetValue("flight_dataGuiRect", dataGuiRect);
            config.SetValue("flight_settingsGuiRect", settingsGuiRect);
            config.save();
        }

        void LoadConfigs()
        {
            KSP.IO.PluginConfiguration config = KSP.IO.PluginConfiguration.CreateForType<FlightGUI>();
            config.load();
            mainGuiRect = config.GetValue("flight_mainGuiRect", new Rect());
            dataGuiRect = config.GetValue("flight_dataGuiRect", new Rect());
            settingsGuiRect = config.GetValue("flight_settingsGuiRect", new Rect());
        }
    }
}
