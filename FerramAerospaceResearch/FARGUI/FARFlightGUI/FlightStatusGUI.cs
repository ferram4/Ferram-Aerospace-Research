using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FerramAerospaceResearch.FARGUI.FARFlightGUI
{
    class FlightStatusGUI
    {
        string statusString;
        Color statusColor;
        double statusOverrideTimer;
        double statusBlinkerTimer;
        bool statusBlinker;
        GUIStyle stallStyle;

        VesselFlightInfo infoParameters;


        public void UpdateInfoParameters(VesselFlightInfo info)
        {
            infoParameters = info;
            SetFlightStatusWindow();
        }

        public void AerodynamicFailureStatus()
        {
            statusString = "Aerodynamic Failure";
            statusColor = Color.yellow;
            statusOverrideTimer = 5;
            statusBlinker = true;
        }

        private void SetFlightStatusWindow()
        {
            if (statusOverrideTimer > 0)
            {
                statusOverrideTimer -= TimeWarp.deltaTime;
                return;
            }
            if (infoParameters.dynPres < 10)
            {
                statusString = "Nominal";
                statusColor = Color.green;
                statusBlinker = false;
            }
            else if (infoParameters.stallFraction > 0.5)
            {
                statusString = "Large-Scale Stall";
                statusColor = Color.yellow;
                statusBlinker = true;
            }
            else if (infoParameters.stallFraction > 0.005)
            {
                statusString = "Minor Stalling";
                statusColor = Color.yellow;
                statusBlinker = false;
            }
            else if ((Math.Abs(infoParameters.aoA) > 20 && Math.Abs(infoParameters.aoA) < 160) || (Math.Abs(infoParameters.sideslipAngle) > 20 && Math.Abs(infoParameters.sideslipAngle) < 160))
            {
                statusString = "Large AoA / Sideslip";
                statusColor = Color.yellow;
                statusBlinker = false;
            }
            else if (infoParameters.dynPres > 40000)
            {
                statusString = "High Dyn Pressure";
                statusColor = Color.yellow;
                statusBlinker = false;
            }
            else
            {
                statusString = "Nominal";
                statusColor = Color.green;
                statusBlinker = false;
            }
        }

        public void Display()
        {
            GUIStyle minorTitle = new GUIStyle(GUI.skin.label);
            minorTitle.alignment = TextAnchor.UpperCenter;
            minorTitle.padding = new RectOffset(0, 0, 0, 0);

            if (stallStyle == null)
                stallStyle = new GUIStyle(FlightGUI.boxStyle);

            GUILayout.Label("Flight Status", minorTitle, GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal();
            if (statusBlinker)
            {
                stallStyle.normal.textColor = stallStyle.focused.textColor = stallStyle.hover.textColor = stallStyle.active.textColor = stallStyle.onActive.textColor = stallStyle.onNormal.textColor = stallStyle.onFocused.textColor = stallStyle.onHover.textColor = stallStyle.onActive.textColor = statusColor;
                if (statusBlinkerTimer < 0.5)
                    GUILayout.Box(statusString, stallStyle, GUILayout.ExpandWidth(true));
                else
                    GUILayout.Box("", stallStyle, GUILayout.ExpandWidth(true));

                if (statusBlinkerTimer < 1)
                    statusBlinkerTimer += TimeWarp.deltaTime;
                else
                    statusBlinkerTimer = 0;
            }
            else
            {
                stallStyle.normal.textColor = stallStyle.focused.textColor = stallStyle.hover.textColor = stallStyle.active.textColor = stallStyle.onActive.textColor = stallStyle.onNormal.textColor = stallStyle.onFocused.textColor = stallStyle.onHover.textColor = stallStyle.onActive.textColor = statusColor;
                GUILayout.Box(statusString, stallStyle, GUILayout.ExpandWidth(true));
            }
            GUILayout.EndHorizontal();
        }
    }
}
