using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerramAerospaceResearch.FARGUI.FARFlightGUI
{
    class AirspeedSettingsGUI
    {
        Vessel _vessel;

        GUIStyle buttonStyle;

        public AirspeedSettingsGUI(Vessel vessel)
        {
            _vessel = vessel;
        }

        public enum SurfaceVelMode
        {
            TAS,
            IAS,
            EAS,
            MACH
        }

        private string[] surfModel_str = 
        {
            "Surface",
            "IAS",
            "EAS",
            "Mach"
        };

        public enum SurfaceVelUnit
        {
            M_S,
            KNOTS,
            MPH,
            KM_H,
        }

        private string[] surfUnit_str = 
        {
            "m/s",
            "knots",
            "mph",
            "km/h"
        };

        SurfaceVelMode velMode = SurfaceVelMode.TAS;
        SurfaceVelUnit unitMode = SurfaceVelUnit.M_S;

        private List<InternalSpeed> speedometers = null;

        public void AirSpeedSettings()
        {
            if (buttonStyle == null)
                buttonStyle = FlightGUI.buttonStyle;

            GUILayout.BeginVertical();
            GUILayout.Label("Select Surface Velocity Settings");
            GUILayout.Space(10);
            GUILayout.EndVertical();
            GUILayout.BeginHorizontal();
            velMode = (SurfaceVelMode)GUILayout.SelectionGrid((int)velMode, surfModel_str, 1, buttonStyle);
            unitMode = (SurfaceVelUnit)GUILayout.SelectionGrid((int)unitMode, surfUnit_str, 1, buttonStyle);
            GUILayout.EndHorizontal();

            //            SaveAirSpeedPos.x = AirSpeedPos.x;
            //            SaveAirSpeedPos.y = AirSpeedPos.y;
        }

        public void ChangeSurfVelocity()
        {
            //DaMichel: Keep our fingers off of this also if there is no atmosphere (staticPressure <= 0)
            if (FlightUIController.speedDisplayMode != FlightUIController.SpeedDisplayModes.Surface || _vessel.atmDensity <= 0)
                return;
            FlightUIController UI = FlightUIController.fetch;

            if (UI.spdCaption == null || UI.speed == null)
                return;
            Vessel activeVessel = _vessel;

            string speedometerCaption = "Surf: ";
            double unitConversion = 1;
            string unitString = "m/s";
            if (unitMode == SurfaceVelUnit.KNOTS)
            {
                unitConversion = 1.943844492440604768413343347219;
                unitString = "knots";
            }
            else if (unitMode == SurfaceVelUnit.KM_H)
            {
                unitConversion = 3.6;
                unitString = "km/h";
            }
            else if (unitMode == SurfaceVelUnit.MPH)
            {
                unitConversion = 2.236936;
                unitString = "mph";
            }
            if (velMode == SurfaceVelMode.TAS)
            {
                UI.spdCaption.text = "Surface";
                UI.speed.text = (activeVessel.srfSpeed * unitConversion).ToString("F1") + unitString;
            }
            else
            {
                if (velMode == SurfaceVelMode.IAS)
                {
                    UI.spdCaption.text = "IAS";
                    speedometerCaption = "IAS: ";
                    double densityRatio = (FARAeroUtil.GetCurrentDensity(activeVessel.mainBody, activeVessel.altitude, false) * 1.225);
                    double pressureRatio = FARAeroUtil.StagnationPressureCalc(_vessel.mach);
                    UI.speed.text = (activeVessel.srfSpeed * Math.Sqrt(densityRatio) * pressureRatio * unitConversion).ToString("F1") + unitString;
                }
                else if (velMode == SurfaceVelMode.EAS)
                {
                    UI.spdCaption.text = "EAS";
                    speedometerCaption = "EAS: ";
                    double densityRatio = (FARAeroUtil.GetCurrentDensity(activeVessel.mainBody, activeVessel.altitude, false) * 1.225);
                    UI.speed.text = (activeVessel.srfSpeed * Math.Sqrt(densityRatio) * unitConversion).ToString("F1") + unitString;
                }
                else// if (velMode == SurfaceVelMode.MACH)
                {
                    UI.spdCaption.text = "Mach";
                    speedometerCaption = "Mach: ";
                    UI.speed.text = _vessel.mach.ToString("F3");
                }
            }
            /* DaMichel: cache references to current IVA speedometers.
             * IVA stuff is reallocated whenever you switch between vessels. So i see
             * little point in storing the list of speedometers permanently. It just has
             * to be freshly cached whenever something changes. */
            if (FlightGlobals.ready)
            {
                if (speedometers == null)
                {
                    speedometers = new List<InternalSpeed>();
                    for (int i = 0; i < _vessel.parts.Count; ++i)
                    {
                        Part p = _vessel.parts[i];
                        if (p && p.internalModel)
                        {
                            speedometers.AddRange(p.internalModel.GetComponentsInChildren<InternalSpeed>());
                        }
                    }
                    //Debug.Log("FAR: Got new references to speedometers"); // check if it is really only executed when vessel change
                }
                string text = speedometerCaption + UI.speed.text;
                for (int i = 0; i < speedometers.Count; ++i)
                {
                    speedometers[i].textObject.text.Text = text; // replace with FAR velocity readout
                }
            }
        }
    }
}
