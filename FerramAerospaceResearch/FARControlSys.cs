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
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEngine;

namespace ferram4
{


    /// <summary>
    /// This Module gets put on the Command Module (Cockpit) for a mach readout and atm density.  Also includes a wing leveler with a variable gain because it's simple and useful
    /// </summary>
    public class FARControlSys : FARBaseAerodynamics
    {
        public static Rect windowPos;


        public double MachNumber;

        private static string mach;

        public static double activeMach
        {
            get
            {
                return ActiveControlSys.MachNumber;
            }
        }

        private static string AirDensity;
        private static bool DensityRelative = true;
        private static string DensityRelative_str = "REL";
        private static double invKerbinSLDensity = 0;

        private static bool AutopilotWindow = false;
        public static Rect AutopilotWinPos;

        private static bool WingLevelerOn = false;
        public static string k_wingleveler_str = "0.05";
        public static double k_wingleveler = 0.05;
        public static string kd_wingleveler_str = "0.002";
        public static double kd_wingleveler = 0.002;
        private static double lastPhi = 0;

        private static bool YawDamperOn = false;
        public static string k_yawdamper_str = "0.1";
        public static double k_yawdamper = 0.1;

        private static double lastBeta = 0;

        private static bool PitchDamperOn = false;
        public static string k_pitchdamper_str = "0.25";
        public static double k_pitchdamper = 0.25;

        private static double lastAlpha = 0;

        private static bool ControlReducer = false;
        public static string scaleVelocity_str = "150";
        public static double scaleVelocity = 150;
        public static string alt_str = "0";
        public static double alt = 0;

        private static bool AoALimiter = false;
        public static string upperLim_str = "25";
        public static double upperLim = 25;
        public static string lowerLim_str = "-25";
        public static double lowerLim = -25;
        public static string k_limiter_str = "0.25";
        public static double k_limiter = 0.25;

        private static double lastDt = 1;

        private Vector3d lastAngVelocity = Vector3.zero;

/*        private bool AltHold = false;
        private string holdAltitude_str = "5000";
        private float holdAltitude = 5000;*/

        private double q;

        private double scalingfactor = 1;

        public static Rect AutoPilotWindowPos;

        private static bool AutoPilotHelp = false;

        public static Rect HelpWindowPos;

        internal static bool FlightDataWindow = false;

        public static Rect FlightDataPos;

        private static bool FlightDataHelp = false;

        public static Rect FlightDataHelpPos;

        internal static bool AirSpeedWindow = false;

        public static Rect AirSpeedPos;

        private static bool AirSpeedHelp = false;

        public static Rect AirSpeedHelpPos; 
        
        private static bool WgLvHlp = false;
        private static bool YwDpHlp = false;
        private static bool PcDpHlp = false;
        private static bool DynCtrlHlp = false;
        private static bool AoAHlp = false;


        public static bool minimize = false;


/*        private static Vector3 SaveWindowPos = new Vector3();
        private static Vector3 SaveAutoPilotPos = new Vector3();
        private static Vector3 SaveHelpPos = new Vector3();
        private static Vector3 SaveFlightDataPos = new Vector3();
        private static Vector3 SaveFlightDataHelpPos = new Vector3();
        private static Vector3 SaveAirSpeedPos = new Vector3();
        private static Vector3 SaveAirSpeedHelpPos = new Vector3();*/



        //private float Cl;
        //private float Cd;
        private static double stallPercentage;
        private static double mass;
        private static double fuelmass;
        public static double termVel;
        public static double ballisticCoeff;
        private static double TSFC;
        private static double L_W;

        private static double AoA;
        private static double pitch;
        private static double roll;
        private static double heading;
        private static double yaw;

        private static double intakeDeficit = 0;

        public static bool StartedGUI = false;
        private static FARControlSys activeControlSys;

        public static FARControlSys ActiveControlSys
        {
            get
            {
                return activeControlSys;
            }
        }

        private bool startSequenceFinished = false;

        private static double timeSinceSave = 0;

        private static NavBall ball;

        private static bool ClCdHelp = false;
        private static bool L_DHelp = false;
        private static bool TSFCHelp = false;
        private static bool L_DTSFCHelp = false;

        private static string statusString = "";
        private static Color statusColor = Color.green;
        private static double statusOverrideTimer = 0;
        private static double statusBlinkerTimer = 0;
        private static bool statusBlinker = false;


        public enum SurfaceVelMode
        {
            DEFAULT,
            IAS,
            EAS,
            MACH
        }

        private static string[] surfModel_str = 
        {
            "Default",
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

        private static string[] surfUnit_str = 
        {
            "m/s",
            "knots",
            "mph",
            "km/h"
        };

        public static SurfaceVelMode velMode = SurfaceVelMode.DEFAULT;
        public static SurfaceVelUnit unitMode = SurfaceVelUnit.M_S;

/*        public void FlapChange()
        {
            if (Flaps == null)
            {
                Flaps = new List<FlapAndSlatModel>();
                foreach (Part p in vessel.parts)
                    foreach (PartModule m in p.Modules)
                        if (m is FlapAndSlatModel)
                            Flaps.Add(m as FlapAndSlatModel);
            }
            foreach (FlapAndSlatModel f in Flaps)
                if (f != null)
                    FlapDeflect = f.Deployed.ToString();
                else
                    Flaps.Remove(f);
        }*/

        private void GetNavball()
        {
            ball = FlightUIController.fetch.GetComponentInChildren<NavBall>();
        }

        private void GetFlightCondition()
        {

            double DragArea = 0;
            double LiftArea = 0;
            double stallArea = 0;

            double wingArea = 0;
            double otherArea = 0;
            mass = 0;
            fuelmass = 0;

            double totalthrust = 0;
            double fuelconsumption = 0;
            double airAvailable = 0;
            double airDemand = 0;
            PartResourceLibrary l = PartResourceLibrary.Instance;

            Vector3 tmpVec = vessel.ReferenceTransform.up * Vector3.Dot(vessel.ReferenceTransform.up, vessel.srf_velocity.normalized) + vessel.ReferenceTransform.forward * Vector3.Dot(vessel.ReferenceTransform.forward, vessel.srf_velocity.normalized);   //velocity vector projected onto a plane that divides the airplane into left and right halves
            AoA = Vector3.Dot(tmpVec.normalized, vessel.ReferenceTransform.forward);
            AoA = FARMathUtil.rad2deg * Math.Asin(AoA);
            if (double.IsNaN(AoA))
                AoA = 0;

            tmpVec = vessel.ReferenceTransform.up * Vector3.Dot(vessel.ReferenceTransform.up, vessel.srf_velocity.normalized) + vessel.ReferenceTransform.right * Vector3.Dot(vessel.ReferenceTransform.right, vessel.srf_velocity.normalized);     //velocity vector projected onto the vehicle-horizontal plane
            yaw = Vector3.Dot(tmpVec.normalized, vessel.ReferenceTransform.right);
            yaw = FARMathUtil.rad2deg * Math.Asin(yaw);
            if (double.IsNaN(yaw))
                yaw = 0;



            if (ball == null)
                GetNavball();
            if (ball)
            {
                Quaternion vesselRot = Quaternion.Inverse(ball.relativeGymbal);

                heading = vesselRot.eulerAngles.y;
                //vesselRot *= Quaternion.Euler(0, -heading, 0);
                //heading = 360 - heading;
                pitch = (vesselRot.eulerAngles.x > 180) ? (360 - vesselRot.eulerAngles.x) : -vesselRot.eulerAngles.x;
                roll = (vesselRot.eulerAngles.z > 180) ? (360 - vesselRot.eulerAngles.z) : -vesselRot.eulerAngles.z;
            }

            double soundspeed;
            double density = FARAeroUtil.GetCurrentDensity(vessel, out soundspeed);
            double realToStockDensityRatio = vessel.atmDensity / density;

            bool zero_q = FARMathUtil.Approximately(0, q);
            double drag_coeff = FlightGlobals.DragMultiplier * 1000 * realToStockDensityRatio;
            double fixedDeltaTime = TimeWarp.fixedDeltaTime;

            Vector3 lift_axis = -vessel.transform.forward;

            //stuff that needs to iterate through all the vessel's parts
            foreach (Part p in vessel.parts)
            {
                if (p == null)
                    continue;
                if (p.Resources.Count > 0)
                {
                    double rmass = p.GetResourceMass();
                    fuelmass += rmass;
                    mass += rmass;
                }
                mass += p.mass;

                foreach (PartModule m in p.Modules)
                {
                    if (m is ModuleEngines)
                    {
                        ModuleEngines e = m as ModuleEngines;
                        if (e.EngineIgnited && !e.engineShutdown)
                        {
                            totalthrust += e.finalThrust;
                            foreach (Propellant v in e.propellants)
                            {
                                string propName = v.name;
                                PartResourceDefinition r = l.resourceDefinitions[propName];
                                if (propName == "IntakeAir")
                                {
                                    airDemand += v.currentRequirement;
                                    continue;
                                }
                                fuelconsumption += r.density * v.currentRequirement / fixedDeltaTime;

                            }
                        }
                    }
                    else if (m is ModuleEnginesFX)
                    {
                        ModuleEnginesFX e = m as ModuleEnginesFX;
                        if (e.EngineIgnited && !e.engineShutdown)
                        {
                            totalthrust += e.finalThrust;
                            foreach (Propellant v in e.propellants)
                            {
                                string propName = v.name;
                                PartResourceDefinition r = l.resourceDefinitions[propName];
                                if (propName == "IntakeAir")
                                {
                                    airDemand += v.currentRequirement;
                                    continue;
                                }
                                fuelconsumption += r.density * v.currentRequirement / fixedDeltaTime;

                            }
                        }
                    }
                    else if (m is ModuleResourceIntake)
                    {
                        ModuleResourceIntake i = m as ModuleResourceIntake;
                        if (i.intakeEnabled)
                        {
                            //PartResourceDefinition r = l.resourceDefinitions[i.resourceName];
                            airAvailable += i.airFlow * fixedDeltaTime;// *r.density / i.unitScalar;
                        }
                    }
                    if (!zero_q)
                    {
                        if (m is FARWingAerodynamicModel)
                        {
                            FARWingAerodynamicModel w = m as FARWingAerodynamicModel;
                            wingArea += w.S;
                            DragArea += w.S * w.GetCd();
                            LiftArea += w.S * w.Cl * Vector3.Dot(w.GetLiftDirection(), lift_axis);
                            stallArea += w.S * w.GetStall();
                            break;
                        }
                        else if (m is FARBasicDragModel)
                        {
                            FARBasicDragModel d = m as FARBasicDragModel;
                            DragArea += d.S * d.Cd;
                            LiftArea += d.S * d.Cl * Vector3.Dot(d.GetLiftDirection(), lift_axis);
                            otherArea += d.S;
                            break;
                        }
                    }
                }
                DragArea += p.mass * p.maximum_drag * drag_coeff;
            }
            intakeDeficit = airAvailable / airDemand;

            TSFC = 0;

            if (totalthrust != 0)
                TSFC = fuelconsumption / totalthrust * 3600 * 9.81;

            double geeForce = FlightGlobals.getGeeForceAtPosition(vessel.CoM).magnitude;

            if (!FARMathUtil.Approximately(0, q))
            {
                if (!FARMathUtil.Approximately(wingArea, 0))
                    S = wingArea;
                else
                    S = otherArea;

                double recip_S = 1 / S;
                Cl = LiftArea * recip_S;
                Cd = DragArea * recip_S;
                stallPercentage = stallArea * recip_S;
                L_W = geeForce * mass;
                L_W = LiftArea * q / (L_W * 1000);
            }
            else
            {
                Cl = 0;
                Cd = 0;
                L_W = 0;
                stallPercentage = 0;
            }

            ballisticCoeff = mass * 1000 / DragArea;

            termVel = 2 * ballisticCoeff * geeForce;
            termVel /= density;
            termVel = Math.Sqrt(termVel);

            SetFlightStatusWindow();
        }

        [KSPEvent(name = "AerodynamicFailureStatus", active = true, guiActive = false, guiActiveUnfocused = false)]
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
            if(q < 10)
            {
                statusString = "Nominal";
                statusColor = Color.green;
                statusBlinker = false;
            }
            else if (stallPercentage > 0.5)
            {
                statusString = "Large-Scale Stall";
                statusColor = Color.yellow;
                statusBlinker = true;
            }
            else if (stallPercentage > 0.005)
            {
                statusString = "Minor Stalling";
                statusColor = Color.yellow;
                statusBlinker = false;
            }
            else if ((Math.Abs(AoA) > 20 && Math.Abs(AoA) < 160) || (Math.Abs(yaw) > 20 && Math.Abs(yaw) < 160))
            {
                statusString = "Large AoA / Sideslip";
                statusColor = Color.yellow;
                statusBlinker = false;
            }
            else if (q > 40000)
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



        #region GUI Functions

        private void FlightDataGUI(int windowID)
        {
            GUIStyle leftBox = new GUIStyle(GUI.skin.box);
            leftBox.normal.textColor = leftBox.focused.textColor = Color.white;
            leftBox.hover.textColor = leftBox.active.textColor = Color.yellow;
            leftBox.onNormal.textColor = leftBox.onFocused.textColor = leftBox.onHover.textColor = leftBox.onActive.textColor = Color.green;
            leftBox.padding = new RectOffset(4, 4, 4, 4);
            leftBox.alignment = TextAnchor.UpperLeft;

            GUIStyle mytoggle = new GUIStyle(GUI.skin.button);
            mytoggle.normal.textColor = mytoggle.focused.textColor = Color.white;
            mytoggle.hover.textColor = mytoggle.active.textColor = mytoggle.onActive.textColor = Color.yellow;
            mytoggle.onNormal.textColor = mytoggle.onFocused.textColor = mytoggle.onHover.textColor = Color.green;
            mytoggle.padding = new RectOffset(4, 4, 4, 4);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Box("Pitch Angle: \n\rHeading: \n\rRoll Angle: \n\r\n\rAngle of Attack: \n\rSideslip Angle: \n\r\n\rQ: \n\r\n\rCl: \n\rCd: \n\rReference Area: \n\rL/W: \n\r\n\rL/D: \n\rV*L/D: \n\r\n\rFuel Fraction: \n\rTSFC: \n\rAir Req Met: \n\r\n\rL/D / TSFC: \n\rV*L/D / TSFC:  \n\r\n\rTerminal V: \n\rBC:", leftBox, GUILayout.Width(120));
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            double L_D = Cl / Cd;
            double VL_D = this.vessel.srf_velocity.magnitude * L_D;
            double L_D_TSFC = 0;
            double VL_D_TSFC = 0;
            if (TSFC != 0)
            {
                L_D_TSFC = L_D / TSFC;
                VL_D_TSFC = VL_D / TSFC * 0.001;
            }

            StringBuilder readoutString = new StringBuilder();
            readoutString.AppendLine(pitch.ToString("N1") + "°");
            readoutString.AppendLine(heading.ToString("N1") + "°");
            readoutString.AppendLine(roll.ToString("N1") + "°");
            readoutString.AppendLine();
            readoutString.AppendLine(AoA.ToString("N1") + "°");
            readoutString.AppendLine(yaw.ToString("N1") + "°");
            readoutString.AppendLine();
            readoutString.AppendLine(q.ToString("N0") + " Pa");
            readoutString.AppendLine();
            readoutString.AppendLine(Cl.ToString("N3"));
            readoutString.AppendLine(Cd.ToString("N3"));
            readoutString.AppendLine(S.ToString("N1") + " m²");
            readoutString.AppendLine(L_W.ToString("N1"));
            readoutString.AppendLine();
            readoutString.AppendLine(L_D.ToString("N2"));
            readoutString.AppendLine(VL_D.ToString("N2") + " m/s");
            readoutString.AppendLine();
            readoutString.AppendLine((fuelmass / mass).ToString("N2"));
            readoutString.AppendLine(TSFC.ToString("N3") + " hr⁻¹");
            readoutString.AppendLine(intakeDeficit.ToString("P1"));
            readoutString.AppendLine();
            readoutString.AppendLine(L_D_TSFC.ToString("N2") + " hr");
            readoutString.AppendLine(VL_D_TSFC.ToString("N2") + " km");
            readoutString.AppendLine();
            readoutString.AppendLine(termVel.ToString("N0") + " m/s");
            readoutString.Append(ballisticCoeff.ToString("N1") + " kg/m²");

            GUILayout.Box(readoutString.ToString(), leftBox, GUILayout.Width(120));

            //GUILayout.Box(pitch + "\n\r" + heading + "\n\r" + roll + "\n\r\n\r" + AoA + "\n\r" + yaw + "\n\r\n\r" + q + "\n\r\n\r" + Cl + "\n\r" + Cd + "\n\r" + S + "\n\r" + L_W + "\n\r\n\r" + L_D + "\n\r" + VL_D + "\n\r\n\r" + (fuelmass / mass) + "\n\r" + TSFC + "\n\r" + intakeDeficit + "%\n\r\n\r" + L_D_TSFC + "\n\r" + VL_D_TSFC + "\n\r\n\r" + termVel + "\n\r" + ballisticCoeff, mySty, GUILayout.Width(120));
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            FlightDataHelp = GUILayout.Toggle(FlightDataHelp, "Help", mytoggle, GUILayout.ExpandWidth(true));

//            SaveFlightDataPos.x = FlightDataPos.x;
//            SaveFlightDataPos.y = FlightDataPos.y;

            GUI.DragWindow();


            FlightDataPos = FARGUIUtils.ClampToScreen(FlightDataPos);
        }

        private void FlightDataHelpGUI(int windowID)
        {

            GUIStyle mySty = new GUIStyle(GUI.skin.box);
            mySty.normal.textColor = mySty.focused.textColor = Color.white;
            mySty.hover.textColor = mySty.active.textColor = Color.yellow;
            mySty.onNormal.textColor = mySty.onFocused.textColor = mySty.onHover.textColor = mySty.onActive.textColor = Color.green;
            mySty.padding = new RectOffset(4, 4, 4, 4);

            GUIStyle mytoggle = new GUIStyle(GUI.skin.button);
            mytoggle.normal.textColor = mytoggle.focused.textColor = Color.white;
            mytoggle.hover.textColor = mytoggle.active.textColor = mytoggle.onActive.textColor = Color.yellow;
            mytoggle.onNormal.textColor = mytoggle.onFocused.textColor = mytoggle.onHover.textColor = Color.green;
            mytoggle.padding = new RectOffset(4, 4, 4, 4);

            GUIStyle TabLabelStyle = new GUIStyle(GUI.skin.label);
            TabLabelStyle.fontStyle = FontStyle.Bold;
            TabLabelStyle.alignment = TextAnchor.UpperCenter;
            bool tmp;

            GUILayout.BeginVertical(GUILayout.Height(200), GUILayout.Width(600), GUILayout.ExpandHeight(true));
            GUILayout.Label("Pitch, Heading, and Roll Angles", TabLabelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Box("These describe the orientation of the vehicle in space.  They are taken from the NavBall at the bottom of the flight screen", mySty);
            GUILayout.EndHorizontal();
            GUILayout.Label("Angle of Attack and Sideslip Angle", TabLabelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Box("These describe the orientation of the vehicle relative to its velocity vector; Angle of Attack is how much it is angled in pitch and Sideslip Angle is how much it is angled in yaw.", mySty);
            GUILayout.EndHorizontal();
            GUILayout.Label("Q", TabLabelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Box("This is the current dynamic pressure; it is equal to 1/2 * air density * velocity^2 and is a measure of the strength of aerodynamic forces.", mySty);
            GUILayout.EndHorizontal();
            GUILayout.Label("Cl and Cd", TabLabelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Box("These describe the amount of lift and drag produced by the plane without regard to airspeed, air density and wing area.", mySty);
            tmp = ClCdHelp;
            ClCdHelp = GUILayout.Toggle(ClCdHelp, "More", mytoggle, GUILayout.Width(60.0F), GUILayout.Height(30.0F));
            if (ClCdHelp)
            {
                GUILayout.EndHorizontal();
                GUILayout.Box("Lift and drag, both being aerodynamic forces, are proportional to dynamic pressure and surface area, both easily measured quantities.  However, lift and drag depend on other factors, such as wing shape and orientation (its angle of attack, for instance) which leads to a lift coefficient (Cl) and drag coefficient (Cd) to describe these factors.  In general, Cd is proportional to Cl squared.\n\r\n\rFor the plane, the wing area is defined as the area of all wing parts, including the tails and canards.  For non-planes, the area is replaced with the surface area of all parts.", mySty);
                GUILayout.BeginHorizontal();
            }
            else if (tmp)
                FlightDataHelpPos.height = 0;
            GUILayout.EndHorizontal();

            GUILayout.Label("L/D and V*L/D", TabLabelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Box("These describe the effectiveness of the plane in endurance and range, respectively.", mySty);
            tmp = L_DHelp;
            L_DHelp = GUILayout.Toggle(L_DHelp, "More", mytoggle, GUILayout.Width(60.0F), GUILayout.Height(30.0F));
            if (L_DHelp)
            {
                GUILayout.EndHorizontal();
                GUILayout.Box("L/D is the ratio of lift to drag, and is a measure of how efficiently the wing lifts; higher values are always better.  At low speeds, L/D is low due to the large amount of drag caused by the very high lift coefficient needed to maintain flight; at high speeds, L/D is low due to very high friction drag; it is lowest in a happy medium between these extremes.\n\r\n\rWhen considering the maximum amount of time a plane can stay in the air, which is called endurance, the maximum endurance is found to occur at maximum L/D: The plane can lift its weight using the smallest amount of thrust necessary to overcome drag.  V*L/D measures the efficiency of the plane at getting somewhere in flight; it peaks at a higher speed than L/D.  This reflects the fact that while the plane may not be as efficient at lifting at that higher speed, it does still go farther due to its higher velocity.\n\r\n\rIt should be noted that these measurements of flight efficiency become increasingly inaccurate at higher speeds; they do not account for the effect of a spherical Kerbin.", mySty);
                GUILayout.BeginHorizontal();
            }
            else if (tmp)
                FlightDataHelpPos.height = 0;
            GUILayout.EndHorizontal();

            GUILayout.Label("Fuel Fraction", TabLabelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Box("The fraction of the vehicle that is fuel, by weight.", mySty);
            GUILayout.EndHorizontal();
            GUILayout.Label("TSFC", TabLabelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Box("This describes the efficiency of the engine at producing thrust.", mySty);
            tmp = TSFCHelp;
            TSFCHelp = GUILayout.Toggle(TSFCHelp, "More", mytoggle, GUILayout.Width(60.0F), GUILayout.Height(30.0F));
            if (TSFCHelp)
            {
                GUILayout.EndHorizontal();
                GUILayout.Box("The efficiency of jet engines are ususally measured through a value called Thrust Specific Fuel Consumption (TSFC).  This value describes how many newtons of fuel are burned per hour to provide a newton of thrust (Yes, it does describes fuel weight rather than mass.  Blame tradition in engineering).  Lower values are more efficient.", mySty);
                GUILayout.BeginHorizontal();
            }
            else if (tmp)
                FlightDataHelpPos.height = 0;
            GUILayout.EndHorizontal();
            GUILayout.Label("Air Req Met", TabLabelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Box("Percentage of jet engine air requirements met by current active intakes; > 100% indicates normal operation, while < 100% will result in flameouts.", mySty);
            GUILayout.EndHorizontal();

            GUILayout.Label("L/D / TSFC and V*L/D / TSFC", TabLabelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Box("These are a combination of the above efficiencies.", mySty);
            tmp = L_DTSFCHelp;
            L_DTSFCHelp = GUILayout.Toggle(L_DTSFCHelp, "More", mytoggle, GUILayout.Width(60.0F), GUILayout.Height(30.0F));
            if (L_DTSFCHelp)
            {
                GUILayout.EndHorizontal();
                GUILayout.Box("By combining the aerodynamic efficiency measurements and the thrust efficiency measurements, we can come up with a measure of the relative efficiency of an airplane in flight with regard to either time in the air or distance travelled.", mySty);
                GUILayout.BeginHorizontal();
            }
            else if (tmp)
                FlightDataHelpPos.height = 0;
            GUILayout.EndHorizontal();

            GUILayout.Label("Terminal V", TabLabelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Box("Velocity at which drag forces cancel out gravity", mySty);
            GUILayout.EndHorizontal();
            GUILayout.Label("BC", TabLabelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Box("Ballistic Coefficient; a measure of how effectively the vehicle's inertia can overcome drag forces.  Lower numbers indicate higher drag.", mySty);
            GUILayout.EndHorizontal();



            GUILayout.EndVertical();
//            SaveFlightDataHelpPos.x = FlightDataHelpPos.x;
//            SaveFlightDataHelpPos.y = FlightDataHelpPos.y;

            GUI.DragWindow();

            FlightDataHelpPos = FARGUIUtils.ClampToScreen(FlightDataHelpPos);
        }


        private void HelpWindowGUI(int windowID)
        {
            GUIStyle mySty = new GUIStyle(GUI.skin.box);
            mySty.normal.textColor = mySty.focused.textColor = Color.white;
            mySty.hover.textColor = mySty.active.textColor = Color.yellow;
            mySty.onNormal.textColor = mySty.onFocused.textColor = mySty.onHover.textColor = mySty.onActive.textColor = Color.green;
            mySty.padding = new RectOffset(4, 4, 4, 4);

            GUIStyle mytoggle = new GUIStyle(GUI.skin.button);
            mytoggle.normal.textColor = mytoggle.focused.textColor = Color.white;
            mytoggle.hover.textColor = mytoggle.active.textColor = mytoggle.onActive.textColor = Color.yellow;
            mytoggle.onNormal.textColor = mytoggle.onFocused.textColor = mytoggle.onHover.textColor = Color.green;
            mytoggle.padding = new RectOffset(4, 4, 4, 4);

            GUIStyle TabLabelStyle = new GUIStyle(GUI.skin.label);
            TabLabelStyle.fontStyle = FontStyle.Bold;
            TabLabelStyle.alignment = TextAnchor.UpperCenter;
            bool tmp;

            GUILayout.BeginVertical(GUILayout.Height(200), GUILayout.Width(500), GUILayout.ExpandHeight(true));
            GUILayout.Label("Wing Leveler", TabLabelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Box("The wing leveler system tries to bring the roll angle of the plane to zero using the ailerons.", mySty);
            tmp = WgLvHlp;
            WgLvHlp = GUILayout.Toggle(WgLvHlp, "More", mytoggle, GUILayout.Width(60.0F), GUILayout.Height(30.0F));
            if(WgLvHlp)
            {
                GUILayout.EndHorizontal();
                GUILayout.Box("In normal flight, a plane can drift off of a wings-level attitude due to an interaction between the vertical tail and the wings called 'spiral divergence' (so named because it puts the plane on its side in a spiral; note that this is different from a spin).\n\r\n\rA wing leveler prevents this by creating a roll input proportional to the roll angle to bring the plane to level flight.\n\r\n\rThe 'k' exposed on the control panel is the proportional gain for this system; higher values will bring it to level flight faster, but may cause the plane to overshoot and roll the other way, especially if the time for the control surfaces to deflect becomes an issue.  If the gain is too small, the plane will not level out.\n\r\n\rThe 'kd' value given is the derivative gain for the system; this will attempt to damp out rolling motion regardless of roll angle.  Very large values will prevent the system from counteracting roll, while very small values will allow rolling oscillations to occur.", mySty);
                GUILayout.BeginHorizontal();
            }
            else if(tmp)
                HelpWindowPos.height = 0;


            GUILayout.EndHorizontal();

            GUILayout.Label("Yaw Damper", TabLabelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Box("The yaw damper system attempts to prevent the plane from turning about the yaw axis using the rudder.", mySty);
            tmp = YwDpHlp;
            YwDpHlp = GUILayout.Toggle(YwDpHlp, "More", mytoggle, GUILayout.Width(60.0F), GUILayout.Height(30.0F));
            if (YwDpHlp)
            {
                GUILayout.EndHorizontal();
                GUILayout.Box("In flight, a plane can experience an oscillatory motion called 'Dutch Roll' caused by an interaction between the wings and the vertical tail.  This motion includes a minor amount of rolling and a large amount of yawing and sideslip which can make landing a plane dangerous or difficult.  A yaw damper counters this by creating a yaw input proportional and counter to the yaw rate to end this motion.\n\r\n\rThe 'k' exposed on the control panel is the gain for this system; higher values will damp the oscillations faster, but may cause unnecessary rudder deflections after the primary motion is damped out.", mySty);
                GUILayout.BeginHorizontal();
            }
            else if(tmp)
                HelpWindowPos.height = 0;

            GUILayout.EndHorizontal();

            GUILayout.Label("Pitch Damper", TabLabelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Box("The pitch damper system stops the plane from pitching using the elevator.", mySty);
            tmp = PcDpHlp;
            PcDpHlp = GUILayout.Toggle(PcDpHlp, "More", mytoggle, GUILayout.Width(60.0F), GUILayout.Height(30.0F));
            if (PcDpHlp)
            {
                GUILayout.EndHorizontal();
                GUILayout.Box("Planes experience a type of motion called the 'Longitudinal Short-Period' mode (Longitudinal meaning motion limited to forwards-backwards, up-down and pitching; Short-Period refering to the fast oscillations) when a pitch command is given.\n\r\n\rWhile for some planes this motion dies out quickly, for others it results in the plane bobbing along as it flies; this makes the plane harder to control and also allows the possibility of stalling under a strong pitch command.  The pitch damper stops this motion by using the elevator or forward canards to slow the plane's movement.\n\r\n\rThe 'k' exposed on the panel is the gain for this system.  It behaves in a manner similar to the yaw damper gain.", mySty);
                GUILayout.BeginHorizontal();
            }
            else if(tmp)
                HelpWindowPos.height = 0;

            GUILayout.EndHorizontal();

            GUILayout.Label("AoA Limiter", TabLabelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Box("The aoa limiter prevents the vehicle from exceeding a given angle of attack along the pitch axis", mySty);
            tmp = AoAHlp;
            AoAHlp = GUILayout.Toggle(AoAHlp, "More", mytoggle, GUILayout.Width(60.0F), GUILayout.Height(30.0F));
            if (AoAHlp)
            {
                GUILayout.EndHorizontal();
                GUILayout.Box("At very high angles of attack airplane wings can stall, which describes a situation where rather than flowing smoothly over the wing surface, the airflow 'separates' from the surface and creates a region of low pressure, recirculating air behind the wing.  When this happens the wing suffers a sudden drop in lift and an increase in drag, which may lead to a dangerous flight condition or crash.  The AoA limiter is designed to prevent the plane from exceeding a given angle of attack to prevent this.", mySty);
                GUILayout.BeginHorizontal();
            }
            else if (tmp)
                HelpWindowPos.height = 0; 
            
            GUILayout.EndHorizontal();
            
            GUILayout.Label("Dynamic Control Adjustment", TabLabelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Box("This system varies control power with velocity squared and air density to give better control over the plane, particularly to players using keyboards.", mySty);
            tmp = DynCtrlHlp;
            DynCtrlHlp = GUILayout.Toggle(DynCtrlHlp, "More", mytoggle, GUILayout.Width(60.0F), GUILayout.Height(30.0F));
            if (DynCtrlHlp)
            {
                GUILayout.EndHorizontal();
                GUILayout.Box("Control surfaces change the attitude and rotation of a plane by manipulating aerodynamic forces.  Aerodynamic forces vary with air density and velocity squared (dynamic pressure, a value given by 0.5 * density * V^2), so as the plane goes faster at a given density, it gains more control authority; the same holds for going into thicker air at a constant velocity.  This leads to two situations:\n\r\n\r1. Too much control authority in normal flight, which can cause unintended stalls, spins or structural failures;\n\r2. Too little control authority during take-off and landing, which can prevent take-off until the end of the runway or cause crashes during landing due to excessively high landing speeds or an unintended pitch-down tendency at lower speeds.\n\r\n\rThis system automatically scales down control authority above the dynamic pressure calculated from the velocity and altitude exposed on the control panel to allow for greater control over the plane.", mySty);
                GUILayout.BeginHorizontal();
            }
            else if (tmp)
                HelpWindowPos.height = 0;

            GUILayout.EndHorizontal();



            GUILayout.EndVertical();

//            SaveHelpPos.x = HelpWindowPos.x;
//            SaveHelpPos.y = HelpWindowPos.y;

            GUI.DragWindow();

            HelpWindowPos = FARGUIUtils.ClampToScreen(HelpWindowPos);
        }


        private void AutopilotWindowGUI(int windowID)
        { 
            
            GUIStyle mySty = new GUIStyle(GUI.skin.box);
            mySty.normal.textColor = mySty.focused.textColor = Color.white;
            mySty.hover.textColor = mySty.active.textColor = Color.yellow;
            mySty.onNormal.textColor = mySty.onFocused.textColor = mySty.onHover.textColor = mySty.onActive.textColor = Color.green;
            mySty.padding = new RectOffset(4, 4, 4, 4);

            GUIStyle mytoggle = new GUIStyle(GUI.skin.button);
            mytoggle.normal.textColor = mytoggle.focused.textColor = Color.white;
            mytoggle.hover.textColor = mytoggle.active.textColor = mytoggle.onActive.textColor = Color.yellow;
            mytoggle.onNormal.textColor = mytoggle.onFocused.textColor = mytoggle.onHover.textColor = Color.green;
            mytoggle.padding = new RectOffset(4, 4, 4, 4);

            GUIStyle TabLabelStyle = new GUIStyle(GUI.skin.label);
            TabLabelStyle.fontStyle = FontStyle.Bold;
            TabLabelStyle.alignment = TextAnchor.UpperCenter;

            GUILayout.Box("Wing Leveler", mySty, GUILayout.ExpandWidth(true));

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("k:", GUILayout.Width(30));
            k_wingleveler_str = GUILayout.TextField(k_wingleveler_str, GUILayout.ExpandWidth(true));
            k_wingleveler_str = Regex.Replace(k_wingleveler_str, @"[^\d+-.]", "");
            GUILayout.Label("kd:", GUILayout.Width(30));
            kd_wingleveler_str = GUILayout.TextField(kd_wingleveler_str, GUILayout.ExpandWidth(true));
            kd_wingleveler_str = Regex.Replace(kd_wingleveler_str, @"[^\d+-.]", "");
            GUILayout.EndHorizontal();



            GUILayout.Box("Yaw Damper", mySty, GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("k:", GUILayout.Width(30));
            k_yawdamper_str = GUILayout.TextField(k_yawdamper_str, GUILayout.ExpandWidth(true));
            k_yawdamper_str = Regex.Replace(k_yawdamper_str, @"[^\d+-.]", "");
            GUILayout.EndHorizontal();

            GUILayout.Box("Pitch Damper", mySty, GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("k:", GUILayout.Width(30));
            k_pitchdamper_str = GUILayout.TextField(k_pitchdamper_str, GUILayout.ExpandWidth(true));
            k_pitchdamper_str = Regex.Replace(k_pitchdamper_str, @"[^\d+-.]", "");

            GUILayout.EndHorizontal();

            GUILayout.Box("AoA Limiter", mySty, GUILayout.ExpandWidth(true));

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Upper Lim:", GUILayout.Width(75));
            upperLim_str = GUILayout.TextField(upperLim_str, GUILayout.ExpandWidth(true));
            upperLim_str = Regex.Replace(upperLim_str, @"[^\d+-.]", "");
            GUILayout.Label("Lower Lim:", GUILayout.Width(75));
            lowerLim_str = GUILayout.TextField(lowerLim_str, GUILayout.ExpandWidth(true));
            lowerLim_str = Regex.Replace(lowerLim_str, @"[^\d+-.]", "");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("k:", GUILayout.Width(30));
            k_limiter_str = GUILayout.TextField(k_limiter_str, GUILayout.ExpandWidth(true));
            k_limiter_str = Regex.Replace(k_limiter_str, @"[^\d+-.]", "");

            GUILayout.EndHorizontal();


            GUILayout.Box("Dynamic Control Adjustment", mySty, GUILayout.ExpandWidth(true));

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Scaling Velocity:", GUILayout.Width(100));
            scaleVelocity_str = GUILayout.TextField(scaleVelocity_str, GUILayout.ExpandWidth(true));
            scaleVelocity_str = Regex.Replace(scaleVelocity_str, @"[^\d+-.]", "");
            GUILayout.Label("Ctrl Factor:", GUILayout.Width(80));
            GUILayout.Box(scalingfactor.ToString(), mySty, GUILayout.Width(90.0F), GUILayout.Height(30.0F));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Scaling Altitude:", GUILayout.Width(100));
            alt_str = GUILayout.TextField(alt_str, GUILayout.ExpandWidth(true));
            alt_str = Regex.Replace(alt_str, @"[^\d+-.]", "");

            GUILayout.EndHorizontal(); 
            
            if (GUILayout.Button("Update Gains, Limits and Values", mytoggle, GUILayout.ExpandWidth(true), GUILayout.Height(30.0F)))
            {
                k_wingleveler = Convert.ToSingle(k_wingleveler_str);
                kd_wingleveler = Convert.ToSingle(kd_wingleveler_str);
                k_yawdamper = Convert.ToSingle(k_yawdamper_str);
                k_pitchdamper = Convert.ToSingle(k_pitchdamper_str);
                upperLim = Convert.ToSingle(upperLim_str);
                lowerLim = Convert.ToSingle(lowerLim_str);
                k_limiter = Convert.ToSingle(k_limiter_str);

                alt = Convert.ToSingle(alt_str);
                scaleVelocity = Convert.ToSingle(scaleVelocity_str);
                if (alt < 0)
                {
                    alt = 0;
                    alt_str = alt.ToString();
                }
                if (scaleVelocity < 0)
                {
                    scaleVelocity = -scaleVelocity;
                    scaleVelocity_str = scaleVelocity.ToString("N3");
                }

                GUIUtility.keyboardControl = 0;
            }

            

            AutoPilotHelp = GUILayout.Toggle(AutoPilotHelp, "Help", mytoggle, GUILayout.ExpandWidth(true));


            GUI.DragWindow();

            AutoPilotWindowPos = FARGUIUtils.ClampToScreen(AutoPilotWindowPos);
        }

        private void WindowGUI(int windowID)
        {

            GUIStyle blankstyle = new GUIStyle();
            blankstyle.stretchHeight = true;
            blankstyle.stretchWidth = true;
            blankstyle.padding = new RectOffset(0, 0, 0, 0);
            blankstyle.margin = new RectOffset(0, 0, 0, 0);

            GUIStyle mySty = new GUIStyle(GUI.skin.box);
            mySty.normal.textColor = mySty.focused.textColor = Color.white;
            mySty.hover.textColor = mySty.active.textColor = Color.yellow;
            mySty.onNormal.textColor = mySty.onFocused.textColor = mySty.onHover.textColor = mySty.onActive.textColor = Color.green;
            mySty.padding = new RectOffset(4, 4, 4, 4);

            GUIStyle mytoggle = new GUIStyle(GUI.skin.button);
            mytoggle.normal.textColor = mytoggle.focused.textColor = Color.white;
            mytoggle.hover.textColor = mytoggle.active.textColor = mytoggle.onActive.textColor = Color.yellow;
            mytoggle.onNormal.textColor = mytoggle.onFocused.textColor = mytoggle.onHover.textColor = Color.green;
            mytoggle.padding = new RectOffset(4, 4, 4, 4);

            if (!minimize)
            {

                GUILayout.BeginVertical(GUILayout.Height(100));
                GUILayout.BeginHorizontal();
                GUILayout.Box("Mach Number: " + mach, mySty, GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (DensityRelative)
                {
                    GUILayout.Box("Frac SL Density: " + AirDensity, mySty, GUILayout.ExpandWidth(true));
                    DensityRelative_str = "ABS";
                }
                else
                {
                    GUILayout.Box("ATM Density: " + AirDensity, mySty, GUILayout.ExpandWidth(true));
                    DensityRelative_str = "REL";
                }
                if (GUILayout.Button(DensityRelative_str, mytoggle, GUILayout.Width(30.0F), GUILayout.Height(30.0F)))
                    DensityRelative = !DensityRelative;

                GUILayout.EndHorizontal();
                GUIStyle minorTitle = new GUIStyle(GUI.skin.label);
                minorTitle.alignment = TextAnchor.UpperCenter;
                minorTitle.padding = new RectOffset(0, 0, 0, 0);
                GUILayout.Label("Flight Status", minorTitle, GUILayout.ExpandWidth(true));
                GUILayout.BeginHorizontal();
                GUIStyle stallStyle = mySty;
                if (statusBlinker)
                {
                    stallStyle.normal.textColor = stallStyle.focused.textColor = stallStyle.hover.textColor = stallStyle.active.textColor = stallStyle.onActive.textColor = stallStyle.onNormal.textColor = stallStyle.onFocused.textColor = stallStyle.onHover.textColor = stallStyle.onActive.textColor = statusColor;
                    if(statusBlinkerTimer < 0.5)
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

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                AirSpeedWindow = GUILayout.Toggle(AirSpeedWindow, "Airspd Settings", mytoggle, GUILayout.ExpandWidth(true));
                FlightDataWindow = GUILayout.Toggle(FlightDataWindow, "Flight Data", mytoggle, GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();
                GUILayout.Label("Flight Assistance Toggles:");

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                WingLevelerOn = GUILayout.Toggle(WingLevelerOn, "Lvl", mytoggle, GUILayout.MinWidth(30));
                YawDamperOn = GUILayout.Toggle(YawDamperOn, "Yaw", mytoggle, GUILayout.MinWidth(30));
                PitchDamperOn = GUILayout.Toggle(PitchDamperOn, "Pitch", mytoggle, GUILayout.ExpandWidth(true));
                AoALimiter = GUILayout.Toggle(AoALimiter, "AoA", mytoggle, GUILayout.MinWidth(30));
                ControlReducer = GUILayout.Toggle(ControlReducer, "DCA", mytoggle, GUILayout.MinWidth(30));
                GUILayout.EndHorizontal();

                AutopilotWindow = GUILayout.Toggle(AutopilotWindow, "FAR Flight Assistance Options", mytoggle, GUILayout.ExpandWidth(true));

                GUILayout.EndVertical();
            }

//            SaveWindowPos.x = windowPos.x;
//            SaveWindowPos.y = windowPos.y;

            GUI.DragWindow();

            windowPos = FARGUIUtils.ClampToScreen(windowPos);
        }

        private void AirSpeedGUI(int windowID)
        {

            GUIStyle mySty = new GUIStyle(GUI.skin.box);
            mySty.normal.textColor = mySty.focused.textColor = Color.white;
            mySty.hover.textColor = mySty.active.textColor = Color.yellow;
            mySty.onNormal.textColor = mySty.onFocused.textColor = mySty.onHover.textColor = mySty.onActive.textColor = Color.green;
            mySty.padding = new RectOffset(4, 4, 4, 4);

            GUIStyle mytoggle = new GUIStyle(GUI.skin.button);
            mytoggle.normal.textColor = mytoggle.focused.textColor = Color.white;
            mytoggle.hover.textColor = mytoggle.active.textColor = mytoggle.onActive.textColor = Color.yellow;
            mytoggle.onNormal.textColor = mytoggle.onFocused.textColor = mytoggle.onHover.textColor = Color.green;
            mytoggle.padding = new RectOffset(4, 4, 4, 4);

            GUIStyle TabLabelStyle = new GUIStyle(GUI.skin.label);
            TabLabelStyle.fontStyle = FontStyle.Bold;
            TabLabelStyle.alignment = TextAnchor.UpperCenter;

            GUILayout.BeginVertical();
            GUILayout.Label("Select Surface Velocity Settings", TabLabelStyle);
            GUILayout.Space(10);
            GUILayout.EndVertical();
            GUILayout.BeginHorizontal();
            velMode = (SurfaceVelMode)GUILayout.SelectionGrid((int)velMode, surfModel_str, 1, mytoggle);
            unitMode = (SurfaceVelUnit)GUILayout.SelectionGrid((int)unitMode, surfUnit_str, 1, mytoggle);
            GUILayout.EndHorizontal();


            AirSpeedHelp = GUILayout.Toggle(AirSpeedHelp, "Help", mytoggle, GUILayout.ExpandWidth(true));

//            SaveAirSpeedPos.x = AirSpeedPos.x;
//            SaveAirSpeedPos.y = AirSpeedPos.y;

            GUI.DragWindow();
            AirSpeedPos = FARGUIUtils.ClampToScreen(AirSpeedPos);
        }

        private void AirSpeedHelpGUI(int windowID)
        {

            GUIStyle mySty = new GUIStyle(GUI.skin.box);
            mySty.normal.textColor = mySty.focused.textColor = Color.white;
            mySty.hover.textColor = mySty.active.textColor = Color.yellow;
            mySty.onNormal.textColor = mySty.onFocused.textColor = mySty.onHover.textColor = mySty.onActive.textColor = Color.green;
            mySty.padding = new RectOffset(4, 4, 4, 4);

            GUIStyle mytoggle = new GUIStyle(GUI.skin.button);
            mytoggle.normal.textColor = mytoggle.focused.textColor = Color.white;
            mytoggle.hover.textColor = mytoggle.active.textColor = mytoggle.onActive.textColor = Color.yellow;
            mytoggle.onNormal.textColor = mytoggle.onFocused.textColor = mytoggle.onHover.textColor = Color.green;
            mytoggle.padding = new RectOffset(4, 4, 4, 4);

            GUIStyle TabLabelStyle = new GUIStyle(GUI.skin.label);
            TabLabelStyle.fontStyle = FontStyle.Bold;
            TabLabelStyle.alignment = TextAnchor.UpperCenter;

            GUILayout.BeginVertical(GUILayout.Height(200), GUILayout.Width(500), GUILayout.ExpandHeight(true));
            GUILayout.Label("Default", TabLabelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Box("This setting causes the surface velocity indicator to display the true airspeed (TAS) of the vehicle, relative to the rotating body is on.", mySty);
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            GUILayout.Label("IAS", TabLabelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Box("This sets the surface velocity indicator to display indicated airspeed (IAS), which is the airspeed that would be measured by a standard pitot tube (device used for measuring airspeed) on this vehicle", mySty);
            GUILayout.EndHorizontal();
            GUILayout.Space(10); 
            GUILayout.Label("EAS", TabLabelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Box("This sets the surface velocity indicator to display equivalent airspeed (EAS), which is the velocity the vehicle would have to be going at Kerbin sea level to cause an equivalent dynamic pressure.  Holding a constant EAS while gaining altitude will result in an increase in TAS, but keep the strength of aerodynamic forces constant.  Therefore, EAS is valuable for preventing overspeeding in the atmosphere, which could cause structural failure.", mySty);
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            GUILayout.Label("Mach", TabLabelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Box("This sets the surface velocity indicator to display Mach Number, which is how fast the vehicle is going relative to the speed of sound.", mySty);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

//            SaveAirSpeedHelpPos.x = AirSpeedHelpPos.x;
//            SaveAirSpeedHelpPos.y = AirSpeedHelpPos.y;

            GUI.DragWindow();

            AirSpeedHelpPos = FARGUIUtils.ClampToScreen(AirSpeedHelpPos);
        }

        #endregion

        private void ChangeSurfVelocity(SurfaceVelMode velMode)
        {
            if (FlightUIController.speedDisplayMode != FlightUIController.SpeedDisplayModes.Surface)
                return;
            FlightUIController UI = FlightUIController.fetch;

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

            if (velMode == SurfaceVelMode.DEFAULT)
            {
                UI.spdCaption.text = "Surface";
                UI.speed.text = (vessel.srf_velocity.magnitude * unitConversion).ToString("F1") + unitString;
            }
            else if (velMode == SurfaceVelMode.IAS)
            {
                UI.spdCaption.text = "IAS";
                double densityRatio = (FARAeroUtil.GetCurrentDensity(vessel.mainBody, vessel.altitude) * invKerbinSLDensity);
                double pressureRatio = FARAeroUtil.StagnationPressureCalc(MachNumber);
                UI.speed.text = (vessel.srf_velocity.magnitude * Math.Sqrt(densityRatio) * pressureRatio * unitConversion).ToString("F1") + unitString;
            }
            else if (velMode == SurfaceVelMode.EAS)
            {
                UI.spdCaption.text = "EAS";
                double densityRatio = (FARAeroUtil.GetCurrentDensity(vessel.mainBody, vessel.altitude) * invKerbinSLDensity);
                UI.speed.text = (vessel.srf_velocity.magnitude * Math.Sqrt(densityRatio) * unitConversion).ToString("F1") + unitString;
            }
            else if (velMode == SurfaceVelMode.MACH)
            {
                UI.spdCaption.text = "Mach";
                UI.speed.text = mach;
            }
        }

        private void drawGUI()
        {
            GUI.skin = HighLogic.Skin;
            StartedGUI = true;
            if (!minimize)
            {
                windowPos = GUILayout.Window(250, windowPos, WindowGUI, "FAR Flight Systems, v0.13.2", GUILayout.MinWidth(150));
                if (AutopilotWindow)
                {
                    AutoPilotWindowPos = GUILayout.Window(251, AutoPilotWindowPos, AutopilotWindowGUI, "FAR Flight Assistance System Options, v0.13.2", GUILayout.MinWidth(330));
                    if (AutoPilotHelp)
                        HelpWindowPos = GUILayout.Window(252, HelpWindowPos, HelpWindowGUI, "FAR FAS Help, v0.13.2", GUILayout.MinWidth(150));
                }
                if (FlightDataWindow)
                {
                    FlightDataPos = GUILayout.Window(253, FlightDataPos, FlightDataGUI, "FAR Flight Data, v0.13.2", GUILayout.MinWidth(250));
                    if(FlightDataHelp)
                        FlightDataHelpPos = GUILayout.Window(254, FlightDataHelpPos, FlightDataHelpGUI, "FAR Flight Data Help, v0.13.2", GUILayout.MinWidth(150));
                }
                if (AirSpeedWindow)
                {
                    AirSpeedPos = GUILayout.Window(255, AirSpeedPos, AirSpeedGUI, "FAR Airspeed Settings, v0.13.2", GUILayout.MinWidth(200));
                    if (AirSpeedHelp)
                        AirSpeedHelpPos = GUILayout.Window(256, AirSpeedHelpPos, AirSpeedHelpGUI, "FAR Airspeed Settings Help, v0.13.2", GUILayout.MinWidth(170));
                }

            }
            if (this.part == null)
                OnDestroy();
        }



        
        public override void OnStart(StartState start)
        {
            base.OnStart(start);
            Fields["isShielded"].guiActive = false;

            print("GUI Started? " + StartedGUI);
            if (start != StartState.Editor && !StartedGUI && vessel.isActiveVessel)
            {
                //LoadGUIParameters();

                RenderingManager.AddToPostDrawQueue(0, new Callback(drawGUI));//start the GUI
                this.part.OnJustAboutToBeDestroyed += OnDestroy;

                activeControlSys = this;
                StartedGUI = true;
                vessel.OnFlyByWire += new FlightInputCallback(StabilityAugmentation);

            }
            OnVesselPartsChange += GetNavball;
            startSequenceFinished = true;
            invKerbinSLDensity = 1 / FARAeroUtil.GetCurrentDensity(FlightGlobals.Bodies[1], 0);
        }


        public void OnDestroy()
        {
            //SaveGUIParameters();
            if (StartedGUI && start != StartState.Editor)
            {
                RenderingManager.RemoveFromPostDrawQueue(0, new Callback(drawGUI)); //close the GUI
                StartedGUI = false;
                if(vessel)
                    vessel.OnFlyByWire -= new FlightInputCallback(StabilityAugmentation);
            }
        }

        public override void FixedUpdate()
        {
            if (start != StartState.Editor && part && startSequenceFinished)
            {
                if (vessel.isActiveVessel)
                {
                    if (!StartedGUI && activeControlSys == null)
                    {
                        vessel.OnFlyByWire += new FlightInputCallback(StabilityAugmentation);

                        RenderingManager.AddToPostDrawQueue(0, new Callback(drawGUI));//start the GUI
                        this.part.OnJustAboutToBeDestroyed += OnDestroy;

                        activeControlSys = this;
                        StartedGUI = true;
                    }
                    if (activeControlSys == this)
                    {
                        if (vessel.staticPressure > 0)
                        {
                            double soundspeed;
                            double density = FARAeroUtil.GetCurrentDensity(vessel, out soundspeed);
                            MachNumber = this.vessel.srf_velocity.magnitude / soundspeed;

                            if (DensityRelative)
                                AirDensity = (density * invKerbinSLDensity).ToString("F3");
                            else
                                AirDensity = (density).ToString("F3");


                            q = density * vessel.srf_velocity.sqrMagnitude * 0.5;

                            mach = MachNumber.ToString("F3");
                        }
                        else
                        {
                            q = 0;
                            mach = "0.000";
                            AirDensity = "0.000";
                        }

                        timeSinceSave++;

                    }
                }
                else if (activeControlSys == this && StartedGUI)
                {
                    vessel.OnFlyByWire -= new FlightInputCallback(StabilityAugmentation);
                    RenderingManager.RemoveFromPostDrawQueue(0, new Callback(drawGUI)); //close the GUI
                    activeControlSys = null;
                    StartedGUI = false;
                }
            }
        }

        public override void LateUpdate()
        {
            if (StartedGUI && part)
            {
                ChangeSurfVelocity(velMode);
                if (activeControlSys == this && TimeWarp.CurrentRate <= 4)
                    GetFlightCondition();
//                if (start != StartState.Editor && startSequenceFinished && MachNumber > 0.5f && MachNumber < 1.5f)
//                    condEffectIntensity = CalculateCondensationIntensity();
            }
        }

        public void StabilityAugmentation(FlightCtrlState state)
        {

            double tmp = 0;
            double dt = (TimeWarp.fixedDeltaTime + lastDt) * 0.5;      //Not really proper, but since dT jumps around a lot this should lower the jitters
            double recipDt = 1 / dt;
            double ctrlTimeConst = FARControllableSurface.timeConstant * recipDt;
            if (WingLevelerOn)
            {
                if (k_wingleveler > 0)
                {
                    double phi = -roll * FARMathUtil.deg2rad;
                    double d_phi = (phi - lastPhi) * recipDt;
                    if (Math.Abs(state.roll - state.rollTrim) < 0.01)
                    {
                        tmp = k_wingleveler * phi + Math.Abs(kd_wingleveler) * d_phi;
                        tmp = tmp * ctrlTimeConst / (1 - Math.Abs(tmp) * ctrlTimeConst);
                        state.roll = (float)FARMathUtil.Clamp(state.roll + tmp, -1, 1);
                    }
                    lastPhi = phi;
                }
                else
                {
                    double phi = roll + 180;
                    if (phi > 180)
                        phi -= 360;
                    phi = -phi * FARMathUtil.deg2rad;
                    double d_phi = (phi - lastPhi) * recipDt;
                    if (Mathf.Abs(state.roll - state.rollTrim) < 0.01f)
                    {
                        tmp = -k_wingleveler * phi + Math.Abs(kd_wingleveler) * d_phi;
                        tmp = tmp * ctrlTimeConst / (1 - Math.Abs(tmp) * ctrlTimeConst);
                        state.roll = (float)FARMathUtil.Clamp(state.roll + tmp, -1, 1);
                    }
                    lastPhi = phi;
                }
            }

            if (YawDamperOn)
            {
                double beta = (yaw * FARMathUtil.deg2rad + 0.5 * lastBeta) * 0.66666667;
                double d_beta = (beta - lastBeta) * recipDt;
                //float dd_beta =  (d_beta - lastD_beta)/ dt;
                if (Math.Abs(state.yaw - state.yawTrim) < 0.01)
                {
                    tmp = k_yawdamper * d_beta;// +k_yawdamper / 5 * dd_beta;
                    tmp = tmp * ctrlTimeConst / (1 - Math.Abs(tmp) * ctrlTimeConst);
                    state.yaw = (float)FARMathUtil.Clamp(state.yaw + tmp, -1, 1);
                }
                lastBeta = beta;
                //lastD_beta = d_beta;
            }
            if (PitchDamperOn)
            {

                double alpha = (-AoA * FARMathUtil.deg2rad + 0.5 * lastAlpha) * 0.66666667;
                double d_alpha = (alpha - lastAlpha) * recipDt;
                //float dd_alpha = (d_alpha - lastD_alpha) / dt;
                if (Math.Abs(state.pitch - state.pitchTrim) < 0.01)
                {
                    tmp = k_pitchdamper * d_alpha;// +k_pitchdamper / 5 * dd_alpha;
                    tmp = tmp * ctrlTimeConst / (1 - Math.Abs(tmp) * ctrlTimeConst);
                    state.pitch = (float)FARMathUtil.Clamp(tmp + state.pitch, -1, 1);
                }
                lastAlpha = alpha;
                //lastD_alpha = d_alpha;
            }
            if (AoALimiter)
            {
                if (AoA > upperLim)
                    state.pitch = (float)FARMathUtil.Clamp(state.pitch - k_limiter * (AoA - upperLim), -1, 1);
                else if (AoA < lowerLim)
                    state.pitch = (float)FARMathUtil.Clamp(state.pitch + k_limiter * (lowerLim - AoA), -1, 1);
            }
            if (ControlReducer)
            {
                double std_q = FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(alt, vessel.mainBody)) * scaleVelocity * scaleVelocity * 0.5;

                if (q < std_q)
                {
                    scalingfactor = 1;
                    return;
                }
                scalingfactor = std_q / q;

                state.pitch = state.pitchTrim + (state.pitch - state.pitchTrim) * (float)scalingfactor;
                state.yaw = state.yawTrim + (state.yaw - state.yawTrim) * (float)scalingfactor;
                state.roll = state.rollTrim + (state.roll - state.rollTrim) * (float)scalingfactor;
            }
            lastDt = dt;
        }

        /*public void SaveGUIParameters()
        {
            var config = KSP.IO.PluginConfiguration.CreateForType<FAREditorGUI>();
            config.load();
            config.SetValue("FlightWindowPos", windowPos);
            config.SetValue("AutopilotWinPos", AutopilotWinPos);
            config.SetValue("HelpWindowPos", HelpWindowPos);
            config.SetValue("FlightDataPos", FlightDataPos);
            config.SetValue("FlightDataHelpPos", FlightDataHelpPos);
            config.SetValue("AirSpeedPos", AirSpeedPos);
            config.SetValue("AirSpeedHelpPos", AirSpeedHelpPos);
            config.SetValue("FlightGUIBool", minimize);
            config.save();
        }

        public void LoadGUIParameters()
        {
            var config = KSP.IO.PluginConfiguration.CreateForType<FAREditorGUI>();
            config.load();
            windowPos = config.GetValue("FlightWindowPos", new Rect(100, 100, 150, 100));
            AutopilotWinPos = config.GetValue("AutopilotWinPos", new Rect());
            HelpWindowPos = config.GetValue("HelpWindowPos", new Rect());
            FlightDataPos = config.GetValue("FlightDataPos", new Rect());
            FlightDataHelpPos = config.GetValue("FlightDataHelpPos", new Rect());
            AirSpeedPos = config.GetValue("AirSpeedPos", new Rect());
            AirSpeedHelpPos = config.GetValue("AirSpeedHelpPos", new Rect());
            minimize = config.GetValue<bool>("FlightGUIBool", false);
        }*/
    }

}