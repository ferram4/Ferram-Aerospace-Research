using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using KSP;
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

        PhysicsCalcs _physicsCalcs;
        VesselFlightInfo infoParameters;

        FlightStatusGUI _flightStatusGUI;
        StabilityAugmentation _stabilityAugmentation;
        FlightDataGUI _flightDataGUI;

        bool ShowFlightDataWindow = false;

        internal static GUIStyle boxStyle = null;
        internal static GUIStyle buttonStyle = null;

        GUIDropDown<int> settingsWindow;

        void Start()
        {
            _vessel = GetComponent<Vessel>();
            _vesselAero = GetComponent<FARVesselAero>();
            _physicsCalcs = new PhysicsCalcs(_vessel, _vesselAero);
            _flightStatusGUI = new FlightStatusGUI();
            _stabilityAugmentation = new StabilityAugmentation(_vessel);
            _flightDataGUI = new FlightDataGUI();

            settingsWindow = new GUIDropDown<int>(new string[3]{"Flt Data","Stab Aug","Air Speed"}, new int[3]{0,1,2}, 0);
            //boxStyle.padding = new RectOffset(4, 4, 4, 4);


            this.enabled = true;
            OnGUIAppLauncherReady();
        }

        //Receives message from FARVesselAero through _vessel on the recalc being completed
        void UpdateAeroModules(List<FARAeroPartModule> newAeroModules)
        {
            _physicsCalcs.UpdateAeroModules(newAeroModules);
        }

        //Receives a message from any FARWingAerodynamicModel or FARAeroPartModule that has failed to update the GUI
        void AerodynamicFailureStatus()
        {
            _flightStatusGUI.AerodynamicFailureStatus();
        }

        #region PhysicsAndOrientationBlock
        void FixedUpdate()
        {
            Vector3d velVectorNorm = _vessel.srf_velocity.normalized;

            infoParameters = _physicsCalcs.UpdatePhysicsParameters();

            _stabilityAugmentation.UpdatePhysicsInfo(infoParameters);
            _flightStatusGUI.UpdateInfoParameters(infoParameters);
            _flightDataGUI.UpdateInfoParameters(infoParameters);

        }

        #endregion

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
                mainGuiRect = GUILayout.Window(this.GetHashCode(), mainGuiRect, MainFlightGUIWindow, "FAR Flight Systems", GUILayout.MinWidth(200));

                if(ShowFlightDataWindow)
                    dataGuiRect = GUILayout.Window(this.GetHashCode() + 1, dataGuiRect, FlightDataWindow, "FAR FlightData", GUILayout.MinWidth(150));

                settingsGuiRect = GUILayout.Window(this.GetHashCode() + 2, settingsGuiRect, SettingsWindow, "FAR Settings", GUILayout.MinWidth(200));
            }
        }

        void MainFlightGUIWindow(int windowId)
        {
            GUILayout.BeginVertical(GUILayout.Height(100));
            GUILayout.BeginHorizontal();
            GUILayout.Box("Mach: " + _vesselAero.MachNumber.ToString("F3") + "   Reynolds: " + _vesselAero.ReynoldsNumber.ToString("e2"), boxStyle, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.Box("ATM Density: " + _vessel.atmDensity.ToString("F3"), boxStyle, GUILayout.ExpandWidth(true));

            _flightStatusGUI.Display();
            ShowFlightDataWindow = GUILayout.Toggle(ShowFlightDataWindow, "Flt Data", buttonStyle, GUILayout.ExpandWidth(true));

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
            settingsWindow.GUIDropDownDisplay();
            int selection = settingsWindow.ActiveSelection;
            switch (selection)
            {
                case 0:
                    _flightDataGUI.SettingsDisplay();
                    break;
                case 1:
                    _stabilityAugmentation.SettingsDisplay();
                    break;
                    
            }
            GUI.DragWindow();
        }
        #endregion

        #region AppLauncher

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
    }
}
