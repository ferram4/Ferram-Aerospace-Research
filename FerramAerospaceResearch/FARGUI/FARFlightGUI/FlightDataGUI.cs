using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FerramAerospaceResearch.FARGUI.FARFlightGUI
{
    class FlightDataGUI
    {
        bool[] activeFlightDataSections = new bool[9] { true, true, true, true, true, true, true, true, true };
        string[] flightDataOptionLabels = new string[9]{
            "PYR Angles",
            "AoA + Sideslip",
            "Dyn Pres",
            "Aero Forces",
            "Coeffs + Ref Area",
            "L//D and V*L//D", 
            "Engine + Intake",
            "Range + Endurance",
            "BC and Term Vel"
        };

        VesselFlightInfo infoParameters;
        string labelString, dataString;
        GUIStyle buttonStyle;
        GUIStyle boxStyle;
        public void UpdateInfoParameters(VesselFlightInfo info)
        {
            infoParameters = info;
            CreateLabelString();
            CreateDataString();
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
                dataReadoutString.Append(infoParameters.pitchAngle.ToString("N1"));
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
                dataReadoutString.AppendLine((infoParameters.intakeAirFrac * 100).ToString("P1"));
                dataReadoutString.Append(infoParameters.specExcessPower.ToString("N2"));
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

        public void DataDisplay()
        {
            if (boxStyle == null)
                boxStyle = FlightGUI.boxStyle;
            

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Box(labelString, boxStyle, GUILayout.Width(120));
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            GUILayout.Box(dataString, boxStyle, GUILayout.Width(120));
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        //Returns true on a setting change
        public bool SettingsDisplay()
        {
            if (buttonStyle == null)
                buttonStyle = FlightGUI.buttonStyle;

            GUILayout.Label("Flight Data Items To Show");
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
    }
}
