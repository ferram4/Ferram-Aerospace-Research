using System;
using UnityEngine;
using ferram4;

namespace FerramAerospaceResearch.FARGUI.FARFlightGUI
{
    class StabilityAugmentation
    {
        Vessel _vessel;
        ControlSystem[] systems;
        string[] systemLabel = new string[] { "Lvl", "Yaw", "Pitch", "AoA", "DCA" };

        double aoALowLim, aoAHighLim;
        double scalingDynPres;
        VesselFlightInfo info;

        GUIStyle buttonStyle;
        GUIStyle boxStyle;
        GUIDropDown<int> systemDropdown;

        public StabilityAugmentation(Vessel vessel)
        {
            systems = new ControlSystem[5];
            _vessel = vessel;
            _vessel.OnAutopilotUpdate += OnAutoPilotUpdate;
            systemDropdown = new GUIDropDown<int>(systemLabel, new int[] { 0, 1, 2, 3, 4, 5 }, 0);
        }

        ~StabilityAugmentation()
        {
            if ((object)_vessel != null)
                _vessel.OnAutopilotUpdate -= OnAutoPilotUpdate;
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
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].active = GUILayout.Toggle(systems[i].active, systemLabel[i], buttonStyle, GUILayout.MinWidth(30), GUILayout.ExpandWidth(true));
            }
            GUILayout.EndHorizontal();
        }

        public void SettingsDisplay()
        {
            if (boxStyle == null)
                boxStyle = FlightGUI.boxStyle;

            GUILayout.Label("Control System Tweaking");
            systemDropdown.GUIDropDownDisplay();
            int selectedItem = systemDropdown.ActiveSelection;

            ControlSystem sys = systems[selectedItem];
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
            }
            else
                scalingDynPres = GUIUtils.TextEntryForDouble("Dyn Pres For Control Scaling:", 150, scalingDynPres);
            systems[selectedItem] = sys;

            GUILayout.EndVertical();

        }

        private void OnAutoPilotUpdate(FlightCtrlState state)
        {
            ControlSystem sys = systems[0];     //wing leveler
            if (sys.active)
            {
                double phi = info.rollAngle;
                if (sys.kP < 0)
                {
                    phi += 180;
                    if (phi > 180)
                        phi -= 360;
                }
                else
                    phi = -phi;

                phi *= FARMathUtil.deg2rad;

                double output = ControlStateChange(sys, phi);

                if (Math.Abs(state.roll - state.rollTrim) < 0.01)
                {
                    if (output > 1)
                        output = 1;
                    else if (output < -1)
                        output = -1;

                    state.roll = (float)output;
                }
            }
            sys = systems[1];
            if (sys.active)
            {
                double beta = info.sideslipAngle * FARMathUtil.deg2rad;

                double output = ControlStateChange(sys, beta);

                if (Math.Abs(state.yaw - state.yawTrim) < 0.01)
                {
                    if (output > 1)
                        output = 1;
                    else if (output < -1)
                        output = -1;

                    state.yaw = (float)output;
                }

            }
            sys = systems[2];
            if (sys.active)
            {
                double pitch = info.aoA * FARMathUtil.deg2rad;

                double output = ControlStateChange(sys, pitch);

                if (Math.Abs(state.pitch - state.pitchTrim) < 0.01)
                {
                    if (output > 1)
                        output = 1;
                    else if (output < -1)
                        output = -1;

                    state.pitch = (float)output;
                }

            }
            sys = systems[3];
            if (sys.active)
            {
                if (info.aoA > aoAHighLim)
                {
                    state.pitch = (float)FARMathUtil.Clamp(ControlStateChange(sys, info.aoA - aoAHighLim), -1, 1);
                }
                else if (info.aoA < aoALowLim)
                {
                    state.pitch = (float)FARMathUtil.Clamp(ControlStateChange(sys, info.aoA - aoALowLim), -1, 1);
                }
            }
            sys = systems[4];
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

            system.errorIntegral += error;

            state -= system.kP * error + system.kD * dError_dt + system.kI * system.errorIntegral;

            system.lastError += error;

            return state;
        }

        struct ControlSystem
        {
            public bool active;
            public double kP;
            public double kD;
            public double kI;

            public double lastError;
            public double errorIntegral;
        }
    }
}
