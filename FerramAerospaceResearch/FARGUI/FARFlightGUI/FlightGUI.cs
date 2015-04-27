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

        string labelString = "";
        string dataString = "";

        static bool showGUI = false;
        public static bool showAllGUI = true;
        static Rect mainGuiRect;
        static Rect dataGuiRect;
        static ApplicationLauncherButton flightGUIAppLauncherButton;

        PhysicsCalcs _physicsCalcs;
        VesselFlightInfo infoParameters;

        FlightStatusGUI _flightStatusGUI;
        StabilityAugmentation _stabilityAugmentation;

        bool[] activeFlightDataSections = new bool[] { true, true, true, true, true, true, true, true, true };

        void Start()
        {
            _vessel = GetComponent<Vessel>();
            _vesselAero = GetComponent<FARVesselAero>();
            _physicsCalcs = new PhysicsCalcs(_vessel, _vesselAero);
            _flightStatusGUI = new FlightStatusGUI();
            _stabilityAugmentation = new StabilityAugmentation(_vessel);

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

            CreateLabelString();
            CreateDataString();
        }

        #endregion

        #region GUI Functions
        void LateUpdate()
        {

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
                dataReadoutString.Append(infoParameters.pitchAngle.ToString("N1") + "°");
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
                dataReadoutString.Append(infoParameters.dynPres.ToString("F3"));
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
                dataReadoutString.AppendLine(infoParameters.intakeAirFrac.ToString("P1"));
                dataReadoutString.Append(infoParameters.specExcessPower.ToString("N2") + " m²/s²");
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


        void OnGUI()
        {
            GUI.skin = HighLogic.Skin;
            if (_vessel == FlightGlobals.ActiveVessel && showGUI && showAllGUI)
            {
                mainGuiRect = GUILayout.Window(this.GetHashCode(), mainGuiRect, MainFlightGUIWindow, "FAR Flight Systems", GUILayout.MinWidth(150));
                dataGuiRect = GUILayout.Window(this.GetHashCode() + 1, dataGuiRect, FlightDataWindow, "FAR Flight Systems", GUILayout.MinWidth(150));
            }
        }

        void MainFlightGUIWindow(int windowId)
        {
            GUILayout.BeginVertical(GUILayout.Height(100));
            GUILayout.BeginHorizontal();
            GUILayout.Box("Mach: " + _vesselAero.MachNumber + "   Reynolds: " + _vesselAero.ReynoldsNumber.ToString("e2"), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.Box("ATM Density: " + _vessel.atmDensity, GUILayout.ExpandWidth(true));

            _flightStatusGUI.Display(GUI.skin.box);

            GUILayout.Label("Flight Assistance Toggles:");

            _stabilityAugmentation.Display();

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        void FlightDataWindow(int windowId)
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Box(labelString, GUILayout.Width(120));
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            GUILayout.Box(dataString, GUILayout.Width(120));
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
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
