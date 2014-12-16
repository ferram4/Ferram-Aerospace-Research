/*
Ferram Aerospace Research v0.14.5
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
using System.Linq;
using UnityEngine;
//using ferramGraph;

namespace ferram4
{
    public class FAREditorGUI
    {

        public static Rect windowPos;
        protected static Rect helpPos;
        protected static Rect analysisHelpPos;
        protected static Rect stabDerivHelpPos;
        protected static GUIStyle BackgroundStyle;
        protected static GUIStyle ButtonStyle;

        private static Vector3 mousePos = Vector3.zero;

        protected static Vector2 EditorVertScroll = Vector2.zero;
        protected static bool AnalysisHelp = false;
        protected static bool stabDerivHelp = false;
        public static List<FARWingAerodynamicModel> AllControlSurfaces = new List<FARWingAerodynamicModel>();
        public static List<FARWingAerodynamicModel> AllWings = new List<FARWingAerodynamicModel>();

        public static bool minimize = true;
        public static bool hide = false;

        private static double lastMinBounds = 0;
        private static double lastMaxBounds = 0;

        protected static ferramGraph graph = new ferramGraph(400, 275);
        protected static FARGUIDropDown<CelestialBody> celestialBodyDropdown;
        private static CelestialBody activeBody;

        private static string lowerBound_str = "0";
        private static string upperBound_str = "25";
        private static string numPoints_str = "50";
        private static string extra_str = "0.2";
        private static int flap_setting = 0;
        private static string pitch_str = "0";

        private static double lowerBound = 0;
        private static double upperBound = 20;
        private static uint numPoints = 10;

        private static bool vehicleFueled = true;
        private static bool spoilersDeployed = false;

        public static Color clColor;
        public static Color cdColor;
        public static Color cmColor;
        public static Color l_DColor;

        private static FAREditorAeroSim aeroSim;


        private AnalysisHelpTab analysisHelpTab = 0;

        private enum AnalysisHelpTab
        {
            AIRCRAFT_PROPERTIES,
            LONGITUDINAL,
            LATERAL,
        }

        private string[] AnalysisHelpTab_str = 
        {
            "Aircraft Properties",
            "Longitudinal Motion",
            "Lateral Motion",
        };


        private FAREditorMode Mode = 0;

        private enum FAREditorMode
        {
            STATIC,
            STABILITY,
            SIMULATION,
            DEBUG
        }

        private static string[] FAReditorMode_str = 
        {
            "Static",
            "Data + Stability Derivatives",
            "Simulation",
            "Debug FAR Modules"
        };

        private SimMode simMode = 0;

        private enum SimMode
        {
            LONG,
            LAT,
        }

        private static string[] SimMode_str = 
        {
            "Longitudinal Sim",
            "Lateral Sim",
        };

        int index = 1;

        string alt_str = "0";
        double alt = 0;
        string Mach_str = "0.35";
        string alpha_str = "0.1";
        string beta_str = "0";
        string phi_str = "0";

        string u_init = "0";
        string w_init = "0";
        string q_init = "0";
        string θ_init = "0";

        string beta_init = "0";
        string r_init = "0";
        string p_init = "0";
        string φ_init = "0";

        string surfArea = "0";
        string MAC = "0";
        string b = "0";
        double stable_Cl = 0;
        double stable_Cd = 0;
        double stable_AoA = 0;
        string stable_AoA_state = "";

        double u0 = 100;

        string time_end = "10";
        string dT = "0.02";

        double[] MOI_stabDerivs = new double[27];

        public FAREditorGUI()
        {
            CelestialBody[] bodies = FlightGlobals.Bodies.ToArray();
            string[] bodyNames = new string[bodies.Length];
            for (int i = 0; i < bodyNames.Length; i++)
                bodyNames[i] = bodies[i].bodyName;

            int kerbinIndex = 1;

            celestialBodyDropdown = new FARGUIDropDown<CelestialBody>(bodyNames, bodies, kerbinIndex);
            activeBody = bodies[1];
            LoadColors();
            windowPos.height = 500;
            aeroSim = new FAREditorAeroSim();
        }

        public void OnDestroy()
        {
            EditorLogic.fetch.Unlock("FAREdLock");
        }

        public void OnGUI()
        {
            if ((windowPos.x == 0) && (windowPos.y == 0))
            {
                windowPos = new Rect(Screen.width / 2, Screen.height / 4, 10, 10);
                windowPos.height = Screen.height / 3;
                windowPos.width = 400;
            }
            if ((helpPos.x == 0) && (helpPos.y == 0))
            {
                analysisHelpPos = helpPos = new Rect(Screen.width / 4, Screen.height / 4, 10, 10);
            }
            GUI.skin = HighLogic.Skin;
            //if (GUI.Button(switchButton, "FAR CAS"))
            //    minimize = !minimize;

            if (BackgroundStyle == null || ButtonStyle == null)
            {
                // DM: initialize styles on first use
                BackgroundStyle = new GUIStyle(GUI.skin.box);
                BackgroundStyle.richText = true;
                BackgroundStyle.hover = BackgroundStyle.active = BackgroundStyle.normal;
                BackgroundStyle.padding = new RectOffset(2, 2, 2, 2);

                ButtonStyle = new GUIStyle(GUI.skin.button);
                ButtonStyle.normal.textColor = ButtonStyle.focused.textColor = Color.white;
                ButtonStyle.hover.textColor = ButtonStyle.active.textColor = Color.yellow;
                ButtonStyle.onNormal.textColor = ButtonStyle.onFocused.textColor = ButtonStyle.onHover.textColor = ButtonStyle.onActive.textColor = Color.green;
                ButtonStyle.padding = new RectOffset(4, 4, 4, 4);
            }

            mousePos = FARGUIUtils.GetMousePos();

            EditorLogic EdLogInstance = EditorLogic.fetch;

            bool cursorInGUI = false;

            if (!minimize && !hide)
            {
                windowPos = GUILayout.Window(256, windowPos, ActualGUI, "FAR Control & Analysis Systems, v0.14.5");
                if (AnalysisHelp)
                {
                    analysisHelpPos = GUILayout.Window(258, analysisHelpPos, AnalysisHelpGUI, "FAR Analysis Systems Help", GUILayout.Width(400), GUILayout.Height(Screen.height / 3));
                    analysisHelpPos = FARGUIUtils.ClampToScreen(analysisHelpPos);
                }
                if (stabDerivHelp)
                {
                    stabDerivHelpPos = GUILayout.Window(259, stabDerivHelpPos, StabDerivHelpGUI, "FAR Data & Stability Derivative Help", GUILayout.Width(600), GUILayout.Height(Screen.height / 3));
                    stabDerivHelpPos = FARGUIUtils.ClampToScreen(stabDerivHelpPos);
                }
                cursorInGUI = windowPos.Contains(mousePos);

                if (AnalysisHelp)
                    cursorInGUI |= analysisHelpPos.Contains(mousePos);
                if (stabDerivHelp)
                    cursorInGUI |= stabDerivHelpPos.Contains(mousePos);

            }

            //This locks and unlocks the editor as necessary; cannot constantly call the lock or unlock functions as that causes the editor to be constantly locked
            if (cursorInGUI)
            {
                EdLogInstance.Lock(false, false, false, "FAREdLock");
                EditorTooltip.Instance.HideToolTip();
            }
            else if (!cursorInGUI)
            {
                EdLogInstance.Unlock("FAREdLock");
            }

            windowPos = FARGUIUtils.ClampToScreen(windowPos);

            Update();
        }

        private void AnalysisHelpGUI(int windowID)
        {
            GUILayout.BeginVertical();

            GUILayout.Box("The analysis window is designed to help you determine the performance of your airplane before you attempt to fly it by calculating various aerodynamic parameters.", BackgroundStyle);

            GUILayout.BeginVertical(BackgroundStyle);
            GUILayout.Label("<b>Analyzer modes:</b>\n\rSweep AoA (Angle of attack)\n\rSweep Mach\n\r\n\r<b>Sweep AoA:</b> Vary AoA of plane at a constant Mach number.  Set angles using lower and upper bounds and choose enough points for accuracy.  Analyzer will produce two curves; one with increasing AoA, one with decreasing to display effects of stall. The sweep from high to low AoA is displayed in darker tones. \n\r\n\r<b>Sweep Mach:</b> Vary Mach number at a constant AoA.  Will only sweep from lower Mach Number to upper.  Will not accept negative Mach Numbers.\n\r\n\r<b>Parameters Drawn:</b> Cl, Cd, Cm, L/D\n\r\n\r<b>Cl:</b> Lift coefficient; describes the lift of the plane after removing effects of air density and velocity.  Will increase with AoA until stall, where it will drop greatly.  AoA must be lowered greatly before stall ends.\n\r\n\r<b>Cd:</b> Drag coefficient; like the above, but for drag.  Notice the large increase following stall.\n\r\n\r<b>Cm:</b> Pitching moment coefficient; Angular force (think torque) applied to the plane when effects of air density and velocity are removed; must decrease with angle of attack for the plane to be stable.\n\r\n\r<b>L/D:</b> Lift over drag; measure of how efficiently the plane flies.");
            GUILayout.EndVertical();

            GUILayout.EndVertical();

            GUI.DragWindow();
        }

        private void StabDerivHelpGUI(int windowID)
        {
            GUILayout.Box("The data and stability derivative GUI is designed to help you analyze the dynamic properties of your aircraft at a glance.  The simulation GUI is designed to display the dynamic motion of the vehicle as predicted from the stability derivatives.", BackgroundStyle);

            analysisHelpTab = (AnalysisHelpTab)GUILayout.SelectionGrid((int)analysisHelpTab, AnalysisHelpTab_str, 3, ButtonStyle);
            GUILayout.BeginVertical(BackgroundStyle);


            if (analysisHelpTab == AnalysisHelpTab.AIRCRAFT_PROPERTIES)
            {
                GUILayout.Label("<b>Surface Area, Scaled Chord and Scaled Span</b>");
                //GUILayout.Space(5);
                GUILayout.Label("These are used in scaling the aerodynamic properties of the aircraft; larger wing surface areas increase lift, larger scaled chords increase pitching moments and larger scaled spans increase lateral forces and moments.");
                //GUILayout.Space(10);

                GUILayout.Label("<b>Moments of Inertia</b>");
                //GUILayout.Space(5);
                GUILayout.Label("These describe the vehicle's resistance to rotation about a given axis caused by its mass distribution about that axis.  Larger Ixx increases rolling inertia, larger Iyy increases pitching inertia and larger Izz increases yawing inertia.");
                //GUILayout.Space(10);

                GUILayout.Label("<b>Products of Inertia</b>");
                //GUILayout.Space(5);
                GUILayout.Label("These describe the vehicle's resistance to rotation about a given axis caused by its mass distribution about a different axis; it is caused by a vehicle's assymmetry.  For a standard airplane, Ixy and Iyz should be near 0, or the stability analysis will be invalid.");
            }
            if (analysisHelpTab == AnalysisHelpTab.LONGITUDINAL)
            {
                GUILayout.Label("<b>Basics</b>");
                GUILayout.Space(5);
                GUILayout.Label("Longitudinal motion refers to the interaction between the motion and forces acting in the forwards / backwards and downwards / upwards directions (relative to the vehicle) and pitching rates and moments; this is the motion is primarily due to keeping the aircraft moving and aloft.  It consists of two overlapping responses: the short-period response and the phugoid (long-period) response.");
                GUILayout.Space(10);
                GUILayout.Label("<b>Short-Period Motion</b>");
                GUILayout.Space(5);
                GUILayout.Label("The short-period response is essentially the aircraft pitching up and down without any significant change in forward velocity; It is so named because it consists of relatively rapid oscillations (high frequency = short period) and it typically damps out within a few oscillations, though with some designs the oscillations will damp out within a single period.  This motion generally becomes less stable as flight velocity increases.");
                GUILayout.Space(10);
                GUILayout.Label("<b>Phugoid Motion</b>");
                GUILayout.Space(5);
                GUILayout.Label("The phugoid motion consists of an exchange between altitude (displayed as pitch angle θ) and forward velocity; it is named after a a mis-translation of the phrase \"to fly\".  The lower the lift-to-drag ratio (L/D) of the aircraft, the more damped (and thus, stable) this mode becomes.  Therefore, it typically becomes more stable with increasing velocity.");
                GUILayout.Space(10);
                GUILayout.Label("<b>Stability Derivatives</b>");
                GUILayout.Space(5);
                GUILayout.Label("These describe the changes in forces / moments with respect to (wrt) changes in some value.\n\r\n\r<b>Legend:</b>\n\rX_: forward forces; positive forwards\n\rZ_: vertical forces; positive downwards\n\rM_: pitching moment; positive upwards\n\r\n\r[]u: wrt changes in forward velocity\n\r[]w: wrt changes in downward velocity\n\r[]q: wrt changes in pitch-rate\n\r[]δe: wrt changes in pitch-control");
            }
            if (analysisHelpTab == AnalysisHelpTab.LATERAL)
            {
                GUILayout.Label("<b>Basics</b>");
                GUILayout.Space(5);
                GUILayout.Label("Lateral motion refers to the interaction between the rates and moments in rolling and yawing and the motion and forces in the sideways direction; this motion is primarily due to keeping the aircraft facing in a given direction.  It consists of three different motions: the roll subsidence response, the spiral response, and the dutch roll response.");
                GUILayout.Space(10);
                GUILayout.Label("<b>Roll Subsidence Motion</b>");
                GUILayout.Space(5);
                GUILayout.Label("This is a simple damped, non-oscillating motion due to the roll rate.  If the vehicle is rolling, the roll rate will drop to zero.  This motion also controls the upper limit on rolling rate.");
                GUILayout.Space(10);
                GUILayout.Label("<b>Spiral Motion</b>");
                GUILayout.Space(5);
                GUILayout.Label("The spiral motion is either a slight convergent or slightly divergent non-oscillating motion caused by an interaction between the vehicle's yaw and roll stability that, when unstable, often causes the plane to fly in an ever-tightening spiral with an increase in sideslip angle and roll angle until the plane crashes; note that this is not a spin, which requires unequal stalling on the wings.  This motion is usually subconsciously corrected by pilots before it becomes noticable in good visibilty conditions.  Any attempt to increase the stability of the spiral motion will inevitably decrease the stability of the dutch roll motion (see below).");
                GUILayout.Space(10);
                GUILayout.Label("<b>Dutch Roll Motion</b>");
                GUILayout.Space(5);
                GUILayout.Label("The dutch roll motion consists of an exchange between sideslip angle and roll angle that is best described by the plane \"wagging\" in flight; it is named after a motion that appears in ice skating.  While this motion is normally stable, it is often very lightly damped, which can make flight difficult.  It generally becomes less stable as velocity increases and attempts to make the dutch roll motion damp faster will inevitably cause lowered stability in the spiral motion (see above).");

                GUILayout.Space(10);
                GUILayout.Label("<b>Stability Derivatives</b>");
                GUILayout.Space(5);
                GUILayout.Label("These describe the changes in forces / moments with respect to (wrt) changes in some value.\n\r\n\r<b>Legend:</b>\n\rY_: sideways forces; positive right\n\rN_: yawing moment; positive right\n\rL_: rolling moment; positive right\n\r\n\r[]ß: wrt changes in sideslip angle\n\r[]p: wrt changes in roll-rate\n\r[]r: wrt changes in yaw-rate");
            }


            GUILayout.EndVertical();

            GUI.DragWindow();
        }


        private void ActualGUI(int windowID)
        {
            GUIStyle TabLabelStyle = new GUIStyle(GUI.skin.label);
            TabLabelStyle.fontStyle = FontStyle.Bold;
            TabLabelStyle.alignment = TextAnchor.UpperCenter;
            if (!minimize)
            {
                //GUILayout.BeginHorizontal();

                FAREditorMode lastMode = Mode;

                Mode = (FAREditorMode)GUILayout.SelectionGrid((int)Mode, FAReditorMode_str, 4, ButtonStyle);

                bool tmp = !(lastMode == Mode);


                //GUILayout.EndHorizontal();
                if (Mode == FAREditorMode.STATIC)
                    GraphGUI(tmp);
                else if (Mode == FAREditorMode.STABILITY)
                    StabilityDerivativeGUI(tmp);
                else if (Mode == FAREditorMode.SIMULATION)
                    SimulationGUI(tmp);
                else
                    DebugGUI(tmp);

            }

            GUI.DragWindow();

        }
        private void DebugGUI(bool tmp)
        {
            GUIStyle TabLabelStyle = new GUIStyle(GUI.skin.label);
            TabLabelStyle.fontStyle = FontStyle.Bold;
            TabLabelStyle.alignment = TextAnchor.UpperCenter;

            if (tmp)
            {
                windowPos.height = 150;
                windowPos.width = 650;
            }
            List<Part> selectedParts = EditorActionGroups.Instance.GetSelectedParts();
            if (selectedParts.Count > 0)
            {
                Part p = selectedParts[0];  //Selected parts should only ever be symmetry counterparts
                FARBasicDragModel d = null;
                d = p.GetComponent<FARBasicDragModel>();
                if ((object)d != null)
                    DisplayFARModuleProperties(d);
                else
                {
                    FARWingAerodynamicModel w = p.GetComponent<FARWingAerodynamicModel>();
                    if ((object)w != null)
                        DisplayFARModuleProperties(w);
                }
            }
            else
            {
                GUILayout.Label("Switch to Action Groups Menu and select a part to display FAR Module Properties");
                windowPos.height = 150;
            }
        }

        private void DisplayFARModuleProperties(FARWingAerodynamicModel w)
        {
            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.alignment = TextAnchor.MiddleLeft;

            GUILayout.Label(w.part.partInfo.title + "\n\rFARWingAerodynamicModel Data");
            GUILayout.Box("Area (S): " + w.S +
                "\n\rSemispan (b_2): " + w.b_2 +
                "\n\rMeanAeroChord (MAC): " + w.MAC +
                "\n\rTaperRatio: " + w.TaperRatio +
                "\n\rMidChordSweep: " + w.MidChordSweep, boxStyle);
        }

        private void DisplayFARModuleProperties(FARBasicDragModel d)
        {
            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.alignment = TextAnchor.MiddleLeft;

            GUILayout.Label(d.part.partInfo.title + "\n\rFARBasicDragModel Data");
            string s = "Area (S): " + d.S +
                "\n\rcosAngleCutoff: " + d.cosAngleCutoff +
                "\n\rtaperCrossSectionAreaRatio: " + d.taperCrossSectionAreaRatio +
                "\n\rmajorMinorAxisRatio" + d.majorMinorAxisRatio;
            s += "\n\r\n\rCdCurve\n\r" + FloatCurveToString(d.CdCurve);
            s += "\n\rClPotentialCurve\n\r" + FloatCurveToString(d.ClPotentialCurve);
            GUILayout.BeginHorizontal();
            GUILayout.Box(s, boxStyle);
            s = "ClViscousCurve\n\r" + FloatCurveToString(d.ClViscousCurve);
            s += "\n\rCmCurve\n\r" + FloatCurveToString(d.CmCurve);
            GUILayout.Box(s, boxStyle);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Create DragModel Config"))
                CreateDragModelConfig(d);
        }

        private void CreateDragModelConfig(FARBasicDragModel d)
        {
            ConfigNode node = new ConfigNode("MODULE");
            node.AddValue("name", "FARBasicDragModel");
            node.AddValue("S", d.S);
            node.AddValue("cosAngleCutoff", d.cosAngleCutoff);
            node.AddValue("majorMinorAxisRatio", d.majorMinorAxisRatio);
            node.AddValue("taperCrossSectionAreaRatio", d.taperCrossSectionAreaRatio);
            node.AddValue("ignoreAnim", d.ignoreAnim);

            ConfigNode newNode = new ConfigNode("CdCurve");
            d.CdCurve.Save(newNode);
            node.AddNode(newNode);

            newNode = new ConfigNode("ClPotentialCurve");
            d.ClPotentialCurve.Save(newNode);
            node.AddNode(newNode);

            newNode = new ConfigNode("ClViscousCurve");
            d.ClViscousCurve.Save(newNode);
            node.AddNode(newNode);

            newNode = new ConfigNode("CmCurve");
            d.CmCurve.Save(newNode);
            node.AddNode(newNode);

            ConfigNode saveNode = new ConfigNode();
            saveNode.AddNode(node);
            string savePath = KSPUtil.ApplicationRootPath.Replace("\\", "/") + "GameData/FerramAerospaceResearch/Part" + d.part.partInfo.title + ".cfg";
            saveNode.Save(savePath);
            PopupDialog.SpawnPopupDialog("Config Saved", "FARBasicDragModel data saved to:\n\r" + savePath, "OK", true, HighLogic.Skin);
        }

        private string FloatCurveToString(FloatCurve curve)
        {
            string s = "";
            ConfigNode n = new ConfigNode();
            curve.Save(n);
            for (int i = 0; i < n.values.Count; i++)
                s += n.values[i].name + " " + n.values[i].value + "\n\r";

            return s;
        }

        private void SimulationGUI(bool tmp)
        {
            GUIStyle TabLabelStyle = new GUIStyle(GUI.skin.label);
            TabLabelStyle.fontStyle = FontStyle.Bold;
            TabLabelStyle.alignment = TextAnchor.UpperCenter;

            if (tmp)
            {
                windowPos.height = 600;
                windowPos.width = 650;
                graph.Clear();
            }

            GUILayout.BeginHorizontal();
            simMode = (SimMode)GUILayout.SelectionGrid((int)simMode, SimMode_str, 2, ButtonStyle);

            stabDerivHelp = GUILayout.Toggle(stabDerivHelp, "?", ButtonStyle, GUILayout.Width(200));
            GUILayout.EndHorizontal();

            if (simMode == SimMode.LONG)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Longitudinal Derivatives", GUILayout.Width(160));
                GUILayout.EndHorizontal();

                GUILayout.BeginVertical(BackgroundStyle);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Down Vel Derivatives", GUILayout.Width(160));
                GUILayout.Label("Fwd Vel Derivatives", GUILayout.Width(160));
                GUILayout.Label("Pitch Rate Derivatives", GUILayout.Width(160));
                GUILayout.Label("Pitch Ctrl Derivatives", GUILayout.Width(160));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                StabilityLabel("Zw: ", MOI_stabDerivs[3], " s⁻¹", "Change in Z-direction acceleration with respect to Z-direction velocity; should be negative", 160, -1);
                StabilityLabel("Zu: ", MOI_stabDerivs[6], " s⁻¹", "Change in Z-direction acceleration with respect to X-direction velocity; should be negative", 160, -1);
                StabilityLabel("Zq: ", MOI_stabDerivs[9], " m/s", "Change in Z-direction acceleration with respect to pitch-up rate; sign unimportant", 160, 0);
                StabilityLabel("Zδe: ", MOI_stabDerivs[12], " m/s²", "Change in Z-direction acceleration with respect to pitch control input; should be negative", 160, -1);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                StabilityLabel("Xw: ", MOI_stabDerivs[4], " s⁻¹", "Change in X-direction acceleration with respect to Z-direction velocity; should be positive", 160, 1);
                StabilityLabel("Xu: ", MOI_stabDerivs[7], " s⁻¹", "Change in X-direction acceleration with respect to X-direction velocity; should be negative", 160, -1);
                StabilityLabel("Xq: ", MOI_stabDerivs[10], " m/s", "Change in X-direction acceleration with respect to pitch-up rate; sign unimportant", 160, 0);
                StabilityLabel("Xδe: ", MOI_stabDerivs[13], " m/s²", "Change in X-direction acceleration with respect to pitch control input; sign unimportant", 160, 0);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                StabilityLabel("Mw: ", MOI_stabDerivs[5], " (m * s)⁻¹", "Change in pitch-up angular acceleration with respect to Z-direction velocity; should be negative", 160, -1);
                StabilityLabel("Mu: ", MOI_stabDerivs[8], " (m * s)⁻¹", "Change in pitch-up angular acceleration acceleration with respect to X-direction velocity; sign unimportant", 160, 0);
                StabilityLabel("Mq: ", MOI_stabDerivs[11], " s⁻¹", "Change in pitch-up angular acceleration acceleration with respect to pitch-up rate; should be negative", 160, -1);
                StabilityLabel("Mδe: ", MOI_stabDerivs[14], " s⁻²", "Change in pitch-up angular acceleration acceleration with respect to pitch control input; should be positive", 160, 1);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Init u: ");
                u_init = GUILayout.TextField(u_init, GUILayout.ExpandWidth(true));
                GUILayout.Label("Init w: ");
                w_init = GUILayout.TextField(w_init, GUILayout.ExpandWidth(true));
                GUILayout.Label("Init q: ");
                q_init = GUILayout.TextField(q_init, GUILayout.ExpandWidth(true));
                GUILayout.Label("Init θ: ");
                θ_init = GUILayout.TextField(θ_init, GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("End Time: ");
                time_end = GUILayout.TextField(time_end, GUILayout.ExpandWidth(true));
                GUILayout.Label("dT: ");
                dT = GUILayout.TextField(dT, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Run Simulation", ButtonStyle, GUILayout.Width(150.0F), GUILayout.Height(25.0F)))
                {
                    u_init = Regex.Replace(u_init, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                    w_init = Regex.Replace(w_init, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                    q_init = Regex.Replace(q_init, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                    θ_init = Regex.Replace(θ_init, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                    time_end = Regex.Replace(time_end, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                    dT = Regex.Replace(dT, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                    double[] InitCond = new double[4] { Convert.ToDouble(w_init), Convert.ToDouble(u_init), Convert.ToDouble(q_init) * Math.PI / 180, Convert.ToDouble(θ_init) * Math.PI / 180 };
                    RunTransientSimLongitudinal(Convert.ToDouble(time_end), Convert.ToDouble(dT), InitCond);
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Lateral Derivatives", GUILayout.Width(160));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Sideslip Derivatives", GUILayout.Width(160));
                GUILayout.Label("Roll Rate Derivatives", GUILayout.Width(160));
                GUILayout.Label("Yaw Rate Derivatives", GUILayout.Width(160));
                GUILayout.EndHorizontal();
                GUILayout.BeginVertical(BackgroundStyle);
                GUILayout.BeginHorizontal();
                StabilityLabel("Yβ: ", MOI_stabDerivs[15], " m/s²", "Change in Y-direction acceleration with respect to sideslip angle β; should be negative", 160, -1);
                StabilityLabel("Yp: ", MOI_stabDerivs[18], " m/s", "Change in Y-direction acceleration with respect to roll-right rate; should be negative", 160, -1);
                StabilityLabel("Yr: ", MOI_stabDerivs[21], " m/s", "Change in Y-direction acceleration with respect to yaw-right rate; should be positive", 160, 1);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                StabilityLabel("Lβ: ", MOI_stabDerivs[16], " s⁻²", "Change in roll-right angular acceleration with respect to sideslip angle β; should be negative", 160, -1);
                StabilityLabel("Lp: ", MOI_stabDerivs[19], " s⁻¹", "Change in roll-right angular acceleration with respect to roll-right rate; should be negative", 160, -1);
                StabilityLabel("Lr: ", MOI_stabDerivs[22], " s⁻¹", "Change in roll-right angular acceleration with respect to yaw-right rate; should be positive", 160, 1);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                StabilityLabel("Nβ: ", MOI_stabDerivs[17], " s⁻²", "Change in yaw-right angular acceleration with respect to sideslip angle β; should be positive", 160, 1);
                StabilityLabel("Np: ", MOI_stabDerivs[20], " s⁻¹", "Change in yaw-right angular acceleration with respect to roll-right rate; sign unimportant", 160, 0);
                StabilityLabel("Nr: ", MOI_stabDerivs[23], " s⁻¹", "Change in yaw-right angular acceleration with respect to yaw-right rate; should be negative", 160, -1);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Init β: ");
                beta_init = GUILayout.TextField(beta_init, GUILayout.ExpandWidth(true));
                GUILayout.Label("Init r: ");
                r_init = GUILayout.TextField(r_init, GUILayout.ExpandWidth(true));
                GUILayout.Label("Init p: ");
                p_init = GUILayout.TextField(p_init, GUILayout.ExpandWidth(true));
                GUILayout.Label("Init φ: ");
                φ_init = GUILayout.TextField(φ_init, GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("End Time: ");
                time_end = GUILayout.TextField(time_end, GUILayout.ExpandWidth(true));
                GUILayout.Label("dT: ");
                dT = GUILayout.TextField(dT, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Run Simulation", ButtonStyle, GUILayout.Width(150.0F), GUILayout.Height(25.0F)))
                {
                    beta_init = Regex.Replace(beta_init, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                    r_init = Regex.Replace(r_init, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                    p_init = Regex.Replace(p_init, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                    φ_init = Regex.Replace(φ_init, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                    time_end = Regex.Replace(time_end, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                    dT = Regex.Replace(dT, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                    double[] InitCond = new double[4] { Convert.ToDouble(beta_init) * Math.PI / 180, Convert.ToDouble(p_init) * Math.PI / 180, Convert.ToDouble(r_init) * Math.PI / 180, Convert.ToDouble(φ_init) * Math.PI / 180 };
                    RunTransientSimLateral(Convert.ToDouble(time_end), Convert.ToDouble(dT), InitCond);
                }
                GUILayout.EndHorizontal();
            }
            graph.Display(BackgroundStyle, 0, 0);
            //graph.Display(GUILayout.Width(540), GUILayout.Height(300));

            DrawTooltip();
        }

        private void RunTransientSimLateral(double endTime, double initDt, double[] InitCond)
        {
            FARMatrix A = new FARMatrix(4, 4);
            FARMatrix B = new FARMatrix(1, 4);
            MonoBehaviour.print("Init Dump...");

            A.PrintToConsole();

            MonoBehaviour.print("Deriv Dump...");
            int i = 0;
            int j = 0;
            int num = 0;
            double[] Derivs = new double[27];

            MOI_stabDerivs.CopyTo(Derivs, 0);

            Derivs[15] = Derivs[15] / u0;
            Derivs[18] = Derivs[18] / u0;
            Derivs[21] = Derivs[21] / u0 - 1;

            double Lb = Derivs[16] / (1 - Derivs[26] * Derivs[26] / (Derivs[0] * Derivs[2]));
            double Nb = Derivs[17] / (1 - Derivs[26] * Derivs[26] / (Derivs[0] * Derivs[2]));

            double Lp = Derivs[19] / (1 - Derivs[26] * Derivs[26] / (Derivs[0] * Derivs[2]));
            double Np = Derivs[20] / (1 - Derivs[26] * Derivs[26] / (Derivs[0] * Derivs[2]));

            double Lr = Derivs[22] / (1 - Derivs[26] * Derivs[26] / (Derivs[0] * Derivs[2]));
            double Nr = Derivs[23] / (1 - Derivs[26] * Derivs[26] / (Derivs[0] * Derivs[2]));

            Derivs[16] = Lb + Derivs[26] / Derivs[0] * Nb;
            Derivs[17] = Nb + Derivs[26] / Derivs[2] * Lb;

            Derivs[19] = Lp + Derivs[26] / Derivs[0] * Np;
            Derivs[20] = Np + Derivs[26] / Derivs[2] * Lp;

            Derivs[22] = Lr + Derivs[26] / Derivs[0] * Nr;
            Derivs[23] = Nr + Derivs[26] / Derivs[2] * Lr;

            for (int k = 0; k < Derivs.Length; k++)
            {
                double f = Derivs[k];
                if (num < 15)
                {
                    num++;              //Avoid Ix, Iy, Iz and long derivs
                    continue;
                }
                else
                    num++;
                MonoBehaviour.print(i + "," + j);
                if (i <= 2)
                    A.Add(f, i, j);

                if (j < 2)
                    j++;
                else
                {
                    j = 0;
                    i++;
                }

            }
            A.Add(aeroSim.CalculateAccelerationDueToGravity(activeBody, alt) * Math.Cos(stable_AoA * FARMathUtil.deg2rad) / u0, 3, 0);
            A.Add(1, 1, 3);


            A.PrintToConsole();                //We should have an array that looks like this:
            B.PrintToConsole();

            /*             i --------------->
             *       j  [ Yb / u0 , Yp / u0 , -(1 - Yr/ u0) ,  g Cos(θ0) / u0 ]
             *       |  [   Lb    ,    Lp   ,      Lr       ,          0          ]
             *       |  [   Nb    ,    Np   ,      Nr       ,          0          ]
             *      \ / [    0    ,    1    ,      0        ,          0          ]
             *       V                              //And one that looks like this:
             *                              
             *          [ Z e ]
             *          [ X e ]
             *          [ M e ]
             *          [  0  ]
             * 
             * 
             */
            FARRungeKutta4 transSolve = new FARRungeKutta4(endTime, initDt, A, InitCond);
            MonoBehaviour.print("Runge-Kutta 4 init...");
            transSolve.Solve();
            MonoBehaviour.print("Solved...");
            graph.Clear();
            double[] yVal = transSolve.GetSolution(0);
            MonoBehaviour.print("Got 0");
            for (int k = 0; k < yVal.Length; k++)
            {
                yVal[k] = yVal[k] * 180 / Mathf.PI;
                if (yVal[k] > 50)
                    yVal[k] = 50;
                else if (yVal[k] < -50)
                    yVal[k] = -50;
            }
            graph.AddLine("β", transSolve.time, yVal, l_DColor);
            yVal = transSolve.GetSolution(1);
            MonoBehaviour.print("Got 1");
            for (int k = 0; k < yVal.Length; k++)
            {
                yVal[k] = yVal[k] * 180 / Mathf.PI;
                if (yVal[k] > 50)
                    yVal[k] = 50;
                else if (yVal[k] < -50)
                    yVal[k] = -50;
            }
            graph.AddLine("p", transSolve.time, yVal, cmColor);
            yVal = transSolve.GetSolution(2);
            for (int k = 0; k < yVal.Length; k++)
            {
                yVal[k] = yVal[k] * 180 / Mathf.PI;
                if (yVal[k] > 50)
                    yVal[k] = 50;
                else if (yVal[k] < -50)
                    yVal[k] = -50;
            }
            MonoBehaviour.print("Got 2");
            graph.AddLine("r", transSolve.time, yVal, cdColor);
            yVal = transSolve.GetSolution(3);
            for (int k = 0; k < yVal.Length; k++)
            {
                yVal[k] = yVal[k] * 180 / Mathf.PI;
                if (yVal[k] > 50)
                    yVal[k] = 50;
                else if (yVal[k] < -50)
                    yVal[k] = -50;
            }
            MonoBehaviour.print("Got 3");
            graph.AddLine("φ", transSolve.time, yVal, clColor);
            graph.SetBoundaries(0, endTime, -10, 10);
            graph.SetGridScaleUsingValues(1, 5);
            graph.horizontalLabel = "time";
            graph.verticalLabel = "value";
            graph.Update();
        }

        private void RunTransientSimLongitudinal(double endTime, double initDt, double[] InitCond)
        {
            FARMatrix A = new FARMatrix(4, 4);
            FARMatrix B = new FARMatrix(1, 4);
            MonoBehaviour.print("Init Dump...");

            A.PrintToConsole();

            MonoBehaviour.print("Deriv Dump...");
            int i = 0;
            int j = 0;
            int num = 0;
            for (int k = 0; k < MOI_stabDerivs.Length; k++)
            {
                double f = MOI_stabDerivs[k];
                if (num < 3 || num >= 15)
                {
                    num++;              //Avoid Ix, Iy, Iz
                    continue;
                }
                else
                    num++;
                MonoBehaviour.print(i + "," + j);
                if (i <= 2)
                    if (num == 10)
                        A.Add(f + u0, i, j);
                    else
                        A.Add(f, i, j);
                else
                    B.Add(f, 0, j);
                if (j < 2)
                    j++;
                else
                {
                    j = 0;
                    i++;
                }

            }
            A.Add(-aeroSim.CalculateAccelerationDueToGravity(activeBody, alt), 3, 1);
            A.Add(1, 2, 3);


            A.PrintToConsole();                //We should have an array that looks like this:
            B.PrintToConsole();

            /*             i --------------->
             *       j  [ Z w , Z u , Z q ,  0 ]
             *       |  [ X w , X u , X q , -g ]
             *       |  [ M w , M u , M q ,  0 ]
             *      \ / [  0  ,  0  ,  1  ,  0 ]
             *       V                              //And one that looks like this:
             *                              
             *          [ Z e ]
             *          [ X e ]
             *          [ M e ]
             *          [  0  ]
             * 
             * 
             */
            FARRungeKutta4 transSolve = new FARRungeKutta4(endTime, initDt, A, InitCond);
            MonoBehaviour.print("Runge-Kutta 4 init...");
            transSolve.Solve();
            MonoBehaviour.print("Solved...");
            graph.Clear();
            double[] yVal = transSolve.GetSolution(0);
            MonoBehaviour.print("Got 0");
            for (int k = 0; k < yVal.Length; k++)
                if (yVal[k] > 50)
                    yVal[k] = 50;
                else if (yVal[k] < -50)
                    yVal[k] = -50;
            graph.AddLine("w", transSolve.time, yVal, l_DColor);
            yVal = transSolve.GetSolution(1);
            MonoBehaviour.print("Got 1");
            for (int k = 0; k < yVal.Length; k++)
                if (yVal[k] > 50)
                    yVal[k] = 50;
                else if (yVal[k] < -50)
                    yVal[k] = -50;
            graph.AddLine("u", transSolve.time, yVal, cmColor);
            yVal = transSolve.GetSolution(2);
            for (int k = 0; k < yVal.Length; k++)
            {
                yVal[k] = yVal[k] * 180 / Mathf.PI;
                if (yVal[k] > 50)
                    yVal[k] = 50;
                else if (yVal[k] < -50)
                    yVal[k] = -50;
            }
            MonoBehaviour.print("Got 2");
            graph.AddLine("q", transSolve.time, yVal, cdColor);
            yVal = transSolve.GetSolution(3);
            for (int k = 0; k < yVal.Length; k++)
            {
                yVal[k] = yVal[k] * 180 / Mathf.PI;
                if (yVal[k] > 50)
                    yVal[k] = 50;
                else if (yVal[k] < -50)
                    yVal[k] = -50;
            }
            MonoBehaviour.print("Got 3");
            graph.AddLine("θ", transSolve.time, yVal, clColor);
            graph.SetBoundaries(0, endTime, -10, 10);
            graph.SetGridScaleUsingValues(1, 5);
            graph.horizontalLabel = "time";
            graph.verticalLabel = "value";
            graph.Update();
        }

        private void StabilityDerivativeGUI(bool tmp)
        {
            GUIStyle ButtonStyle = new GUIStyle(GUI.skin.button);
            ButtonStyle.normal.textColor = ButtonStyle.focused.textColor = Color.white;
            ButtonStyle.hover.textColor = ButtonStyle.active.textColor = Color.yellow;
            ButtonStyle.onNormal.textColor = ButtonStyle.onFocused.textColor = ButtonStyle.onHover.textColor = ButtonStyle.onActive.textColor = Color.green;
            ButtonStyle.padding = new RectOffset(4, 4, 4, 4);

            GUIStyle BackgroundStyle = new GUIStyle(GUI.skin.box);
            BackgroundStyle.hover = BackgroundStyle.active = BackgroundStyle.normal;



            GUIStyle TabLabelStyle = new GUIStyle(GUI.skin.label);
            TabLabelStyle.fontStyle = FontStyle.Bold;
            TabLabelStyle.alignment = TextAnchor.UpperCenter;

            if (tmp)
            {
                windowPos.height = 500;
                windowPos.width = 650;
            }

            stabDerivHelp = GUILayout.Toggle(stabDerivHelp, "?", ButtonStyle, GUILayout.Width(200));

            double q;
            double Mach;

            double alpha;
            double beta;
            double phi;

            GUILayout.Label("Flight Condition:");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Planet:");
            celestialBodyDropdown.GUIDropDownDisplay();

            activeBody = celestialBodyDropdown.ActiveSelection();

            GUILayout.Label("Altitude (km):");
            alt_str = GUILayout.TextField(alt_str, GUILayout.ExpandWidth(true));

            /*GUILayout.Label("Temperature: ");
            atm_temp_str = GUILayout.TextField(atm_temp_str, GUILayout.ExpandWidth(true));

            GUILayout.Label("Density: ");
            rho_str = GUILayout.TextField(rho_str, GUILayout.ExpandWidth(true));
            */

            GUILayout.Label("Mach Number: ");
            Mach_str = GUILayout.TextField(Mach_str, GUILayout.ExpandWidth(true));

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Flap Setting: ");
            FlapToggle();
            GUILayout.Label("Fuel Status:");
            vehicleFueled = GUILayout.Toggle(vehicleFueled, vehicleFueled ? "Full" : "Empty", GUILayout.Width(100));
            GUILayout.Label("Spoilers:");
            spoilersDeployed = GUILayout.Toggle(spoilersDeployed, spoilersDeployed ? "Deployed" : "Retracted", GUILayout.Width(100));
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Calculate Stability Derivatives", ButtonStyle, GUILayout.Width(250.0F), GUILayout.Height(25.0F)))
            {
                FARAeroUtil.UpdateCurrentActiveBody(index, activeBody);
                //atm_temp_str = Regex.Replace(atm_temp_str, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                //rho_str = Regex.Replace(rho_str, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                Mach_str = Regex.Replace(Mach_str, @"[^-?[0-9]*(\.[0-9]*)?]", "");

                alt_str = Regex.Replace(alt_str, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                alt = Convert.ToDouble(alt_str);
                alt *= 1000;

                double temp = FlightGlobals.getExternalTemperature((float)alt, activeBody);
                double rho = FARAeroUtil.GetCurrentDensity(activeBody, alt, false);
                //double temp = Convert.ToSingle(atm_temp_str);
                Mach = Convert.ToSingle(Mach_str);
                Mach = FARMathUtil.Clamp(Mach, 0.001f, float.PositiveInfinity);

                double sspeed = Math.Sqrt(FARAeroUtil.currentBodyAtm[0] * Math.Max(0.1, temp + 273.15));
                double vel = sspeed * Mach;

                UpdateControlSettings();

                q = vel * vel * rho * 0.5f;
                alpha = Convert.ToSingle(alpha_str) * 180 / Mathf.PI;
                beta = Convert.ToSingle(beta_str);
                phi = Convert.ToSingle(phi_str);
                MOI_stabDerivs = CalculateStabilityDerivs(vel, q, Mach, alpha, beta, phi);
                u0 = vel;
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label("Aircraft Properties", GUILayout.Width(180));
            GUILayout.Label("Moments of Inertia", GUILayout.Width(160));
            GUILayout.Label("Products of Inertia", GUILayout.Width(160));
            GUILayout.Label("Level Flight", GUILayout.Width(140));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(180));
            GUILayout.Label("Wing Area: " + surfArea + " m²");
            GUILayout.Label("Scaled Chord: " + MAC + " m");
            GUILayout.Label("Scaled Span: " + b + " m");
            GUILayout.EndVertical();


            GUILayout.BeginVertical(GUILayout.Width(160));
            GUILayout.Label(new GUIContent("Ixx: " + MOI_stabDerivs[0].ToString("G6") + " kg * m²", "Inertia about X-axis due to rotation about X-axis"));
            GUILayout.Label(new GUIContent("Iyy: " + MOI_stabDerivs[1].ToString("G6") + " kg * m²", "Inertia about Y-axis due to rotation about Y-axis"));
            GUILayout.Label(new GUIContent("Izz: " + MOI_stabDerivs[2].ToString("G6") + " kg * m²", "Inertia about Z-axis due to rotation about Z-axis"));
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.Width(160));
            GUILayout.Label(new GUIContent("Ixy: " + MOI_stabDerivs[24].ToString("G6") + " kg * m²", "Inertia about X-axis due to rotation about Y-axis; is equal to inertia about Y-axis due to rotation about X-axis"));
            GUILayout.Label(new GUIContent("Iyz: " + MOI_stabDerivs[25].ToString("G6") + " kg * m²", "Inertia about Y-axis due to rotation about Z-axis; is equal to inertia about Z-axis due to rotation about Y-axis"));
            GUILayout.Label(new GUIContent("Ixz: " + MOI_stabDerivs[26].ToString("G6") + " kg * m²", "Inertia about X-axis due to rotation about Z-axis; is equal to inertia about Z-axis due to rotation about X-axis"));
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.Width(140));
            GUILayout.Label(new GUIContent("u0: " + u0.ToString("G6") + " m/s", "Air speed based on this mach number and temperature."));
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Cl: " + stable_Cl.ToString("G3"), "Required lift coefficient at this mass, speed and air density."));
            GUILayout.Label(new GUIContent("Cd: " + stable_Cd.ToString("G3"), "Resulting drag coefficient at this mass, speed and air density."));
            GUILayout.EndHorizontal();
            GUILayout.Label(new GUIContent("AoA: " + stable_AoA_state + stable_AoA.ToString("G6") + " deg", "Angle of attack required to achieve the necessary lift force."));
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Longitudinal Derivatives", GUILayout.Width(160));
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(BackgroundStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Down Vel Derivatives", GUILayout.Width(160));
            GUILayout.Label("Fwd Vel Derivatives", GUILayout.Width(160));
            GUILayout.Label("Pitch Rate Derivatives", GUILayout.Width(160));
            GUILayout.Label("Pitch Ctrl Derivatives", GUILayout.Width(160));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            StabilityLabel("Zw: ", MOI_stabDerivs[3], " s⁻¹", "Change in Z-direction acceleration with respect to Z-direction velocity; should be negative", 160, -1);
            StabilityLabel("Zu: ", MOI_stabDerivs[6], " s⁻¹", "Change in Z-direction acceleration with respect to X-direction velocity; should be negative", 160, -1);
            StabilityLabel("Zq: ", MOI_stabDerivs[9], " m/s", "Change in Z-direction acceleration with respect to pitch-up rate; sign unimportant", 160, 0);
            StabilityLabel("Zδe: ", MOI_stabDerivs[12], " m/s²", "Change in Z-direction acceleration with respect to pitch control input; should be negative", 160, 0);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            StabilityLabel("Xw: ", MOI_stabDerivs[4], " s⁻¹", "Change in X-direction acceleration with respect to Z-direction velocity; should be positive", 160, 1);
            StabilityLabel("Xu: ", MOI_stabDerivs[7], " s⁻¹", "Change in X-direction acceleration with respect to X-direction velocity; should be negative", 160, -1);
            StabilityLabel("Xq: ", MOI_stabDerivs[10], " m/s", "Change in X-direction acceleration with respect to pitch-up rate; sign unimportant", 160, 0);
            StabilityLabel("Xδe: ", MOI_stabDerivs[13], " m/s²", "Change in X-direction acceleration with respect to pitch control input; sign unimportant", 160, 0);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            StabilityLabel("Mw: ", MOI_stabDerivs[5], " (m * s)⁻¹", "Change in pitch-up angular acceleration with respect to Z-direction velocity; should be negative", 160, -1);
            StabilityLabel("Mu: ", MOI_stabDerivs[8], " (m * s)⁻¹", "Change in pitch-up angular acceleration acceleration with respect to X-direction velocity; sign unimportant", 160, 0);
            StabilityLabel("Mq: ", MOI_stabDerivs[11], " s⁻¹", "Change in pitch-up angular acceleration acceleration with respect to pitch-up rate; should be negative", 160, -1);
            StabilityLabel("Mδe: ", MOI_stabDerivs[14], " s⁻²", "Change in pitch-up angular acceleration acceleration with respect to pitch control input; should be positive", 160, 1);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Lateral Derivatives", GUILayout.Width(160));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Sideslip Derivatives", GUILayout.Width(160));
            GUILayout.Label("Roll Rate Derivatives", GUILayout.Width(160));
            GUILayout.Label("Yaw Rate Derivatives", GUILayout.Width(160));
            GUILayout.EndHorizontal();
            GUILayout.BeginVertical(BackgroundStyle);
            GUILayout.BeginHorizontal();
            StabilityLabel("Yβ: ", MOI_stabDerivs[15], " m/s²", "Change in Y-direction acceleration with respect to sideslip angle β; should be negative", 160, -1);
            StabilityLabel("Yp: ", MOI_stabDerivs[18], " m/s", "Change in Y-direction acceleration with respect to roll-right rate; should be negative", 160, -1);
            StabilityLabel("Yr: ", MOI_stabDerivs[21], " m/s", "Change in Y-direction acceleration with respect to yaw-right rate; should be positive", 160, 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            StabilityLabel("Lβ: ", MOI_stabDerivs[16], " s⁻²", "Change in roll-right angular acceleration with respect to sideslip angle β; should be negative", 160, -1);
            StabilityLabel("Lp: ", MOI_stabDerivs[19], " s⁻¹", "Change in roll-right angular acceleration with respect to roll-right rate; should be negative", 160, -1);
            StabilityLabel("Lr: ", MOI_stabDerivs[22], " s⁻¹", "Change in roll-right angular acceleration with respect to yaw-right rate; should be positive", 160, 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            StabilityLabel("Nβ: ", MOI_stabDerivs[17], " s⁻²", "Change in yaw-right angular acceleration with respect to sideslip angle β; should be positive", 160, 1);
            StabilityLabel("Np: ", MOI_stabDerivs[20], " s⁻¹", "Change in yaw-right angular acceleration with respect to roll-right rate; sign unimportant", 160, 0);
            StabilityLabel("Nr: ", MOI_stabDerivs[23], " s⁻¹", "Change in yaw-right angular acceleration with respect to yaw-right rate; should be negative", 160, -1);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            DrawTooltip();
        }

        private void DrawTooltip()
        {
            if (GUI.tooltip == "")
                return;

            GUIStyle BackgroundStyle = new GUIStyle(GUI.skin.box);
            BackgroundStyle.hover = BackgroundStyle.active = BackgroundStyle.normal;

            Rect tooltipRect = new Rect(Mathf.Clamp(mousePos.x - windowPos.x, 0, windowPos.width - 300), Mathf.Clamp(mousePos.y - windowPos.y, 0, windowPos.height - 80), 300, 80);

            GUIStyle toolTipStyle = BackgroundStyle;
            toolTipStyle.normal.textColor = toolTipStyle.active.textColor = toolTipStyle.hover.textColor = toolTipStyle.focused.textColor = toolTipStyle.onNormal.textColor = toolTipStyle.onHover.textColor = toolTipStyle.onActive.textColor = toolTipStyle.onFocused.textColor = new Color(1, 0.75f, 0);

            GUI.Box(tooltipRect, GUI.tooltip, toolTipStyle);
        }

        private void StabilityLabel(String text1, double val, String text2, String tooltip, int width, int sign)
        {
            Color color = Color.white;
            if (sign != 0)
                color = (Math.Sign(val) == sign) ? Color.green : Color.red;

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.normal.textColor = style.hover.textColor = color;

            GUILayout.Label(new GUIContent(text1 + val.ToString("G6") + text2, tooltip), style, GUILayout.Width(width));
        }

        private void FlapToggle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.padding = new RectOffset(2, 2, 0, 0);

            GUILayout.BeginHorizontal(GUILayout.Height(25), GUILayout.Width((30 + 4) * 4));
            for (int i = 0; i <= 3; i++)
            {
                bool on = GUILayout.Toggle(flap_setting == i, i.ToString(), style, GUILayout.Width(30), GUILayout.Height(25));
                if (on) flap_setting = i;
            }
            GUILayout.EndHorizontal();
        }

        private double[] CalculateStabilityDerivs(double u0, double q, double M, double alpha, double beta, double phi)
        {
            double[] stabDerivs = new double[27];
            Vector3d CoM = Vector3d.zero;
            double mass = 0;
            double MAC = 0;
            double b = 0;
            double area = 0;
            double Ix = 0;
            double Iy = 0;
            double Iz = 0;
            double Ixy = 0;
            double Iyz = 0;
            double Ixz = 0;

            double nomCl = 0, nomCd = 0, nomCm = 0, nomCy = 0, nomCn = 0, nomC_roll = 0;

            for (int i = 0; i < FARAeroUtil.CurEditorParts.Count; i++)
            {
                Part p = FARAeroUtil.CurEditorParts[i];
                for (int k = 0; k < p.Modules.Count; k++)
                {
                    PartModule m = p.Modules[k];
                    if (m is FARPartModule)
                    {
                        (m as FARPartModule).ForceOnVesselPartsChange();
                    }
                }
            }

            aeroSim.GetClCdCmSteady(Vector3d.zero, alpha, beta, phi, 0, 0, 0, M, 0, out nomCl, out nomCd, out nomCm, out nomCy, out nomCn, out nomC_roll, true, true, flap_setting, spoilersDeployed);

            for (int i = 0; i < FARAeroUtil.CurEditorParts.Count; i++)
            {
                Part p = FARAeroUtil.CurEditorParts[i];

                if (FARAeroUtil.IsNonphysical(p))
                    continue;
                double partMass = p.mass;
                if (vehicleFueled && p.Resources.Count > 0)
                    partMass += p.GetResourceMass();

                partMass += p.GetModuleMass(p.mass);
                CoM += partMass * (Vector3d)p.transform.TransformPoint(p.CoMOffset);
                mass += partMass;
                FARWingAerodynamicModel w = p.GetComponent<FARWingAerodynamicModel>();
                if (w != null)
                {
                    area += w.S;
                    MAC += w.GetMAC() * w.S;
                    b += w.Getb_2() * w.S;
                }
            }
            if (area == 0)
            {
                area = 1;
                MAC = 1;
                b = 1;
            }
            MAC /= area;
            b /= area;
            CoM /= mass;
            mass *= 1000;

            this.b = b.ToString("G6");
            this.MAC = MAC.ToString("G6");
            this.surfArea = area.ToString("G6");

            MonoBehaviour.print("Mass: " + mass + "\n\rS: " + area + "\n\rMAC: " + MAC);

            for (int i = 0; i < FARAeroUtil.CurEditorParts.Count; i++)
            {
                Part p = FARAeroUtil.CurEditorParts[i];

                if (p == null || FARAeroUtil.IsNonphysical(p))
                    continue;
                //This section handles the parallel axis theorem
                Vector3 relPos = p.transform.TransformPoint(p.CoMOffset) - CoM;
                double x2, y2, z2, x, y, z;
                x2 = relPos.z * relPos.z;
                y2 = relPos.x * relPos.x;
                z2 = relPos.y * relPos.y;
                x = relPos.z;
                y = relPos.x;
                z = relPos.y;

                double partMass = p.mass;
                if (vehicleFueled && p.Resources.Count > 0)
                    partMass += p.GetResourceMass();

                partMass += p.GetModuleMass(p.mass);

                Ix += (y2 + z2) * partMass;
                Iy += (x2 + z2) * partMass;
                Iz += (x2 + y2) * partMass;

                Ixy += -x * y * partMass;
                Iyz += -z * y * partMass;
                Ixz += -x * z * partMass;

                //And this handles the part's own moment of inertia
                Vector3 principalInertia = p.Rigidbody.inertiaTensor;
                Quaternion prncInertRot = p.Rigidbody.inertiaTensorRotation;

                //The rows of the direction cosine matrix for a quaternion
                Vector3 Row1 = new Vector3(prncInertRot.x * prncInertRot.x - prncInertRot.y * prncInertRot.y - prncInertRot.z * prncInertRot.z + prncInertRot.w * prncInertRot.w,
                    2 * (prncInertRot.x * prncInertRot.y + prncInertRot.z * prncInertRot.w),
                    2 * (prncInertRot.x * prncInertRot.z - prncInertRot.y * prncInertRot.w));

                Vector3 Row2 = new Vector3(2 * (prncInertRot.x * prncInertRot.y - prncInertRot.z * prncInertRot.w),
                    -prncInertRot.x * prncInertRot.x + prncInertRot.y * prncInertRot.y - prncInertRot.z * prncInertRot.z + prncInertRot.w * prncInertRot.w,
                    2 * (prncInertRot.y * prncInertRot.z + prncInertRot.x * prncInertRot.w));

                Vector3 Row3 = new Vector3(2 * (prncInertRot.x * prncInertRot.z + prncInertRot.y * prncInertRot.w),
                    2 * (prncInertRot.y * prncInertRot.z - prncInertRot.x * prncInertRot.w),
                    -prncInertRot.x * prncInertRot.x - prncInertRot.y * prncInertRot.y + prncInertRot.z * prncInertRot.z + prncInertRot.w * prncInertRot.w);


                //And converting the principal moments of inertia into the coordinate system used by the system
                Ix += principalInertia.x * Row1.x * Row1.x + principalInertia.y * Row1.y * Row1.y + principalInertia.z * Row1.z * Row1.z;
                Iy += principalInertia.x * Row2.x * Row2.x + principalInertia.y * Row2.y * Row2.y + principalInertia.z * Row2.z * Row2.z;
                Iz += principalInertia.x * Row3.x * Row3.x + principalInertia.y * Row3.y * Row3.y + principalInertia.z * Row3.z * Row3.z;

                Ixy += principalInertia.x * Row1.x * Row2.x + principalInertia.y * Row1.y * Row2.y + principalInertia.z * Row1.z * Row2.z;
                Ixz += principalInertia.x * Row1.x * Row3.x + principalInertia.y * Row1.y * Row3.y + principalInertia.z * Row1.z * Row3.z;
                Iyz += principalInertia.x * Row2.x * Row3.x + principalInertia.y * Row2.y * Row3.y + principalInertia.z * Row2.z * Row3.z;
            }
            Ix *= 1000;
            Iy *= 1000;
            Iz *= 1000;

            stabDerivs[0] = Ix;
            stabDerivs[1] = Iy;
            stabDerivs[2] = Iz;

            stabDerivs[24] = Ixy;
            stabDerivs[25] = Iyz;
            stabDerivs[26] = Ixz;


            double effectiveG = aeroSim.CalculateAccelerationDueToGravity(activeBody, alt);     //This is the effect of gravity
            effectiveG -= u0 * u0 / (alt + activeBody.Radius);                          //This is the effective reduction of gravity due to high velocity
            double neededCl = mass * effectiveG / (q * area);

            //Longitudinal Mess
            aeroSim.SetState(M, neededCl, CoM, 0, flap_setting, spoilersDeployed);


            double pertCl, pertCd, pertCm, pertCy, pertCn, pertC_roll;
/*            int iter = 7;
            for (; ; )
            {
                aeroSim.GetClCdCmSteady(CoM, alpha, beta, phi, 0, 0, 0, M, 0, out nomCl, out nomCd, out nomCm, out nomCy, out nomCn, out nomC_roll, true, true, flap_setting, spoilersDeployed);


                aeroSim.GetClCdCmSteady(CoM, alpha + 0.1, beta, phi, 0, 0, 0, M, 0, out pertCl, out pertCd, out pertCm, out pertCy, out pertCn, out pertC_roll, true, false, flap_setting, spoilersDeployed);
                pertCl = (pertCl - nomCl) / 0.1 * FARMathUtil.rad2deg;                   //vert vel derivs


                if (--iter <= 0 || Math.Abs((nomCl - neededCl) / neededCl) < 0.1)
                    break;

                double delta = -(neededCl - nomCl) / ((pertCl - nomCl) * 100);
                delta = Math.Sign(delta) * Math.Min(0.4f * iter * iter, Math.Abs(delta));
                alpha = Math.Max(-5f, Math.Min(25f, alpha + delta));
            };*/
            alpha = FARMathUtil.BrentsMethod(aeroSim.FunctionIterateForAlpha, -5, 25, 0.01, 35);
            nomCl = neededCl;
            nomCd = aeroSim.Cd;
            nomCm = aeroSim.Cm;
            nomCy = aeroSim.Cy;
            nomCn = aeroSim.Cn;
            nomC_roll = aeroSim.C_roll;

            //alpha_str = (alpha * Mathf.PI / 180).ToString();

            aeroSim.GetClCdCmSteady(CoM, alpha + 0.1, beta, phi, 0, 0, 0, M, 0, out pertCl, out pertCd, out pertCm, out pertCy, out pertCn, out pertC_roll, true, true, flap_setting, spoilersDeployed);

            stable_Cl = neededCl;
            stable_Cd = nomCd;
            stable_AoA = alpha;
            stable_AoA_state = "";
            if (Math.Abs((nomCl - neededCl) / neededCl) > 0.1)
                stable_AoA_state = ((nomCl > neededCl) ? "<" : ">");

            Debug.Log("Cl needed: " + neededCl + ", AoA: " + alpha + ", Cl: " + nomCl + ", Cd: " + stable_Cd);

            pertCl = (pertCl - nomCl) / 0.1 * FARMathUtil.rad2deg;                   //vert vel derivs
            pertCd = (pertCd - nomCd) / 0.1 * FARMathUtil.rad2deg;
            pertCm = (pertCm - nomCm) / 0.1 * FARMathUtil.rad2deg;

            pertCl += nomCd;
            pertCd -= nomCl;

            pertCl *= -q * area / (mass * u0);
            pertCd *= -q * area / (mass * u0);
            pertCm *= q * area * MAC / (Iy * u0);

            stabDerivs[3] = pertCl;
            stabDerivs[4] = pertCd;
            stabDerivs[5] = pertCm;

            aeroSim.GetClCdCmSteady(CoM, alpha, beta, phi, 0, 0, 0, M + 0.01, 0, out pertCl, out pertCd, out pertCm, out pertCy, out pertCn, out pertC_roll, true, false, flap_setting, spoilersDeployed);

            pertCl = (pertCl - nomCl) / 0.01 * M;                   //fwd vel derivs
            pertCd = (pertCd - nomCd) / 0.01 * M;
            pertCm = (pertCm - nomCm) / 0.01 * M;

            pertCl += 2 * nomCl;
            pertCd += 2 * nomCd;

            pertCl *= -q * area / (mass * u0);
            pertCd *= -q * area / (mass * u0);
            pertCm *= q * area * MAC / (u0 * Iy);

            stabDerivs[6] = pertCl;
            stabDerivs[7] = pertCd;
            stabDerivs[8] = pertCm;

            aeroSim.GetClCdCmSteady(CoM, alpha, beta, phi, 0, 0, 0, M, 0, out pertCl, out pertCd, out pertCm, out pertCy, out pertCn, out pertC_roll, true, false, flap_setting, spoilersDeployed);
            aeroSim.GetClCdCmSteady(CoM, alpha, beta, phi, -0.05, 0, 0, M, 0, out pertCl, out pertCd, out pertCm, out pertCy, out pertCn, out pertC_roll, false, false, flap_setting, spoilersDeployed);
            pertCl = (pertCl - nomCl) / 0.05;                   //pitch rate derivs
            pertCd = (pertCd - nomCd) / 0.05;
            pertCm = (pertCm - nomCm) / 0.05;

            pertCl *= q * area * MAC / (2 * u0 * mass);
            pertCd *= q * area * MAC / (2 * u0 * mass);
            pertCm *= q * area * MAC * MAC / (2 * u0 * Iy);

            stabDerivs[9] = pertCl;
            stabDerivs[10] = pertCd;
            stabDerivs[11] = pertCm;

            aeroSim.GetClCdCmSteady(CoM, alpha, beta, phi, 0, 0, 0, M, 0.1, out pertCl, out pertCd, out pertCm, out pertCy, out pertCn, out pertC_roll, true, false, flap_setting, spoilersDeployed);
            pertCl = (pertCl - nomCl) / 0.1;                   //elevator derivs
            pertCd = (pertCd - nomCd) / 0.1;
            pertCm = (pertCm - nomCm) / 0.1;

            pertCl *= q * area / mass;
            pertCd *= q * area / mass;
            pertCm *= q * area * MAC / Iy;

            stabDerivs[12] = pertCl;
            stabDerivs[13] = pertCd;
            stabDerivs[14] = pertCm;

            //Lateral Mess

            aeroSim.GetClCdCmSteady(CoM, alpha, beta + 0.1, phi, 0, 0, 0, M, 0, out pertCl, out pertCd, out pertCm, out pertCy, out pertCn, out pertC_roll, true, false, flap_setting, spoilersDeployed);
            pertCy = (pertCy - nomCy) / 0.1 * FARMathUtil.rad2deg;                   //sideslip angle derivs
            pertCn = (pertCn - nomCn) / 0.1 * FARMathUtil.rad2deg;
            pertC_roll = (pertC_roll - nomC_roll) / 0.1 * FARMathUtil.rad2deg;

            pertCy *= q * area / mass;
            pertCn *= q * area * b / Iz;
            pertC_roll *= q * area * b / Ix;

            stabDerivs[15] = pertCy;
            stabDerivs[17] = pertCn;
            stabDerivs[16] = pertC_roll;


            aeroSim.GetClCdCmSteady(CoM, alpha, beta, phi, 0, 0, 0, M, 0, out pertCl, out pertCd, out pertCm, out pertCy, out pertCn, out pertC_roll, true, false, flap_setting, spoilersDeployed);
            aeroSim.GetClCdCmSteady(CoM, alpha, beta, phi, 0, 0, 0.05, M, 0, out pertCl, out pertCd, out pertCm, out pertCy, out pertCn, out pertC_roll, false, false, flap_setting, spoilersDeployed);
            pertCy = (pertCy - nomCy) / 0.05;                   //roll rate derivs
            pertCn = (pertCn - nomCn) / 0.05;
            pertC_roll = (pertC_roll - nomC_roll) / 0.05;

            pertCy *= q * area * b / (2 * mass * u0);
            pertCn *= q * area * b * b / (2 * Iz * u0);
            pertC_roll *= q * area * b * b / (2 * Ix * u0);

            stabDerivs[18] = pertCy;
            stabDerivs[20] = pertCn;
            stabDerivs[19] = pertC_roll;


            aeroSim.GetClCdCmSteady(CoM, alpha, beta, phi, 0, 0, 0, M, 0, out pertCl, out pertCd, out pertCm, out pertCy, out pertCn, out pertC_roll, true, false, flap_setting, spoilersDeployed);
            aeroSim.GetClCdCmSteady(CoM, alpha, beta, phi, 0, 0.05f, 0, M, 0, out pertCl, out pertCd, out pertCm, out pertCy, out pertCn, out pertC_roll, false, false, flap_setting, spoilersDeployed);
            pertCy = (pertCy - nomCy) / 0.05f;                   //yaw rate derivs
            pertCn = (pertCn - nomCn) / 0.05f;
            pertC_roll = (pertC_roll - nomC_roll) / 0.05f;

            pertCy *= q * area * b / (2 * mass * u0);
            pertCn *= q * area * b * b / (2 * Iz * u0);
            pertC_roll *= q * area * b * b / (2 * Ix * u0);

            stabDerivs[21] = pertCy;
            stabDerivs[23] = pertCn;
            stabDerivs[22] = pertC_roll;

            for (int i = 0; i < FARAeroUtil.CurEditorParts.Count; i++)
            {
                Part p = FARAeroUtil.CurEditorParts[i];

                if (FARAeroUtil.IsNonphysical(p))
                    continue;
                for (int k = 0; k < p.Modules.Count; k++)
                {
                    PartModule m = p.Modules[k];
                    if (m is FARControllableSurface)
                    {
                        (m as FARControllableSurface).SetControlStateEditor(CoM, p.transform.up, (float)0, 0, 0, flap_setting, spoilersDeployed);
                    }
                }
            }

            return stabDerivs;
        }

        private void GraphGUI(bool tmp)
        {
            GUIStyle ButtonStyle = new GUIStyle(GUI.skin.button);
            ButtonStyle.normal.textColor = ButtonStyle.focused.textColor = Color.white;
            ButtonStyle.hover.textColor = ButtonStyle.active.textColor = Color.yellow;
            ButtonStyle.onNormal.textColor = ButtonStyle.onFocused.textColor = ButtonStyle.onHover.textColor = ButtonStyle.onActive.textColor = Color.green;
            ButtonStyle.padding = new RectOffset(4, 4, 4, 4);

            GUIStyle BackgroundStyle = new GUIStyle(GUI.skin.box);
            BackgroundStyle.hover = BackgroundStyle.active = BackgroundStyle.normal;



            GUIStyle TabLabelStyle = new GUIStyle(GUI.skin.label);
            TabLabelStyle.fontStyle = FontStyle.Bold;
            TabLabelStyle.alignment = TextAnchor.UpperCenter;

            if (tmp)
            {
                windowPos.height = 500;
                windowPos.width = 650;
            }

            //GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Steady-State Aerodynamic Analysis", TabLabelStyle);
            GUILayout.Space(20F);
            if (GUILayout.Button("Update CoL", ButtonStyle))
            {
                FARGlobalControlEditorObject.EditorPartsChanged = true;
                ResetAll(null);
            }
            GUILayout.Space(20F);
            AnalysisHelp = GUILayout.Toggle(AnalysisHelp, "?", ButtonStyle);
            GUILayout.EndHorizontal();
            //GUILayout.EndVertical();

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(540));
            graph.Display(BackgroundStyle, 0, 0);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.Label("Flap Setting:");
            FlapToggle();
            GUILayout.Label("Pitch Setting:");
            pitch_str = GUILayout.TextField(pitch_str, GUILayout.ExpandWidth(true));
            pitch_str = Regex.Replace(pitch_str, @"[^-?[0-9]*(\.[0-9]*)?]", "");
            GUILayout.Label("Fuel Status:");
            vehicleFueled = GUILayout.Toggle(vehicleFueled, vehicleFueled ? "Full" : "Empty");
            GUILayout.Label("Spoilers:");
            spoilersDeployed = GUILayout.Toggle(spoilersDeployed, spoilersDeployed ? "Deployed" : "Retracted");
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Lower: ", GUILayout.Width(50.0F), GUILayout.Height(25.0F));
            lowerBound_str = GUILayout.TextField(lowerBound_str, GUILayout.ExpandWidth(true));
            GUILayout.Label("Upper: ", GUILayout.Width(50.0F), GUILayout.Height(25.0F));
            upperBound_str = GUILayout.TextField(upperBound_str, GUILayout.ExpandWidth(true));
            GUILayout.Label("Num Pts: ", GUILayout.Width(70.0F), GUILayout.Height(25.0F));
            numPoints_str = GUILayout.TextField(numPoints_str, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Mach / AoA: ", GUILayout.Width(80.0F), GUILayout.Height(25.0F));
            extra_str = GUILayout.TextField(extra_str, GUILayout.ExpandWidth(true));
            bool AoASweep = GUILayout.Button("Sweep AoA", ButtonStyle, GUILayout.Width(100.0F), GUILayout.Height(25.0F));
            bool MSweep = GUILayout.Button("Sweep Mach", ButtonStyle, GUILayout.Width(100.0F), GUILayout.Height(25.0F));
            if (AoASweep)
            {
                lowerBound_str = Regex.Replace(lowerBound_str, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                lowerBound = Convert.ToDouble(lowerBound_str);
                lowerBound = FARMathUtil.Clamp(lowerBound, -90, 90);
                lowerBound_str = lowerBound.ToString();
                upperBound_str = Regex.Replace(upperBound_str, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                upperBound = Convert.ToDouble(upperBound_str);
                upperBound = FARMathUtil.Clamp(upperBound, lowerBound, 90);
                upperBound_str = upperBound.ToString();
                numPoints_str = Regex.Replace(numPoints_str, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                numPoints = Convert.ToUInt32(numPoints_str);
                double pitch = UpdateControlSettings();
                extra_str = Regex.Replace(extra_str, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                double M = Math.Abs(Convert.ToDouble(extra_str));
                AngleOfAttackSweep(M, pitch);
            }
            else if (MSweep)
            {
                lowerBound_str = Regex.Replace(lowerBound_str, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                lowerBound = Convert.ToDouble(lowerBound_str);
                lowerBound = FARMathUtil.Clamp(lowerBound, 0, double.PositiveInfinity);
                lowerBound_str = lowerBound.ToString();
                upperBound_str = Regex.Replace(upperBound_str, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                upperBound = Convert.ToDouble(upperBound_str);
                upperBound = FARMathUtil.Clamp(upperBound, lowerBound, double.PositiveInfinity);
                upperBound_str = upperBound.ToString();
                numPoints_str = Regex.Replace(numPoints_str, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                numPoints = Convert.ToUInt32(numPoints_str);
                double pitch = UpdateControlSettings();
                extra_str = Regex.Replace(extra_str, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                double AoA = Math.Abs(Convert.ToDouble(extra_str));
                MachNumberSweep(AoA, pitch);
            }
            //if (GUILayout.Button("Dump Graph Data", ButtonStyle, GUILayout.Width(100.0F), GUILayout.Height(25.0F)))
            //    graph.DumpDataToCSV("FAR_" + EditorLogic.fetch.shipNameField.Text + "_Data.csv");
            GUILayout.EndHorizontal();
        }

        public static int CurrentEditorFlapSetting = 0;
        public static bool CurrentEditorSpoilerSetting = false;

        private double UpdateControlSettings()
        {
            double pitch = Convert.ToDouble(pitch_str);
            pitch = FARMathUtil.Clamp(pitch, -1, 1);
            pitch_str = pitch.ToString();
            CurrentEditorFlapSetting = flap_setting;
            CurrentEditorSpoilerSetting = spoilersDeployed;
            return pitch;
        }

        private void MachNumberSweep(double AoA, double pitch)
        {
            FARAeroUtil.UpdateCurrentActiveBody(index, FlightGlobals.Bodies[1]);

            double Cl = 0;
            double Cd = 0;
            double Cm = 0;
            double mass = 0;
            Vector3d CoM = Vector3d.zero;

            for (int i = 0; i < FARAeroUtil.CurEditorParts.Count; i++)
            {
                Part p = FARAeroUtil.CurEditorParts[i];

                if (FARAeroUtil.IsNonphysical(p))
                    continue;

                double partMass = p.mass;
                if (vehicleFueled && p.Resources.Count > 0)
                    partMass += p.GetResourceMass();

                partMass += p.GetModuleMass(p.mass);
                CoM += partMass * (Vector3d)p.transform.TransformPoint(p.CoMOffset);
                mass += partMass;
            }
            CoM /= mass;

            for (int i = 0; i < FARAeroUtil.CurEditorParts.Count; i++)
            {
                Part p = FARAeroUtil.CurEditorParts[i];
                for (int k = 0; k < p.Modules.Count; k++)
                {
                    PartModule m = p.Modules[k];
                    if (m is FARPartModule)
                    {
                        (m as FARPartModule).ForceOnVesselPartsChange();
                    }
                }
            }

            double[] ClValues = new double[(int)numPoints];
            double[] CdValues = new double[(int)numPoints];
            double[] CmValues = new double[(int)numPoints];
            double[] LDValues = new double[(int)numPoints];
            double[] AlphaValues = new double[(int)numPoints];

            for (int i = 0; i < numPoints; i++)
            {

                double M = i / (double)numPoints * (upperBound - lowerBound) + lowerBound;

                if (M == 0)
                    M = 0.001;

                double cy, cn, cr;

                aeroSim.GetClCdCmSteady(CoM, AoA, 0, 0, 0, 0, 0, M, pitch, out Cl, out Cd, out Cm, out cy, out cn, out cr, true, i == 0);


                //                MonoBehaviour.print("Cl: " + Cl + " Cd: " + Cd);
                AlphaValues[i] = M;
                ClValues[i] = Cl;
                CdValues[i] = Cd;
                CmValues[i] = Cm;
                LDValues[i] = Cl / Cd;
            }
            for (int i = 0; i < FARAeroUtil.CurEditorParts.Count; i++)
            {
                Part p = FARAeroUtil.CurEditorParts[i];

                if (FARAeroUtil.IsNonphysical(p))
                    continue;
                for (int k = 0; k < p.Modules.Count; k++)
                {
                    PartModule m = p.Modules[k];
                    if (m is FARControllableSurface)
                    {
                        (m as FARControllableSurface).SetControlStateEditor(CoM, p.transform.up, (float)pitch, 0, 0, flap_setting, spoilersDeployed);
                    }
                }
            }

            string horizontalLabel = "Mach Number";
            UpdateGraph(AlphaValues, ClValues, CdValues, CmValues, LDValues, null, null, null, null, horizontalLabel);
        }

        private void AngleOfAttackSweep(double M, double pitch)
        {
            if (M == 0)
                M = 0.001;

            FARAeroUtil.UpdateCurrentActiveBody(index, FlightGlobals.Bodies[1]);

            double Cl = 0;
            double Cd = 0;
            double Cm = 0;
            double mass = 0;
            Vector3d CoM = Vector3.zero;


            for (int i = 0; i < FARAeroUtil.CurEditorParts.Count; i++)
            {
                Part p = FARAeroUtil.CurEditorParts[i];

                if (FARAeroUtil.IsNonphysical(p))
                    continue;

                double partMass = p.mass;
                if (vehicleFueled && p.Resources.Count > 0)
                    partMass += p.GetResourceMass();

                partMass += p.GetModuleMass(p.mass);
                CoM += partMass * (Vector3d)p.transform.TransformPoint(p.CoMOffset);
                mass += partMass;
            }
            CoM /= mass;

            for (int i = 0; i < FARAeroUtil.CurEditorParts.Count; i++)
            {
                Part p = FARAeroUtil.CurEditorParts[i];
                for (int k = 0; k < p.Modules.Count; k++)
                {
                    PartModule m = p.Modules[k];
                    if (m is FARPartModule)
                    {
                        (m as FARPartModule).ForceOnVesselPartsChange();
                    }
                }
            }

            double[] ClValues = new double[(int)numPoints];
            double[] CdValues = new double[(int)numPoints];
            double[] CmValues = new double[(int)numPoints];
            double[] LDValues = new double[(int)numPoints];
            double[] AlphaValues = new double[(int)numPoints];
            double[] ClValues2 = new double[(int)numPoints];
            double[] CdValues2 = new double[(int)numPoints];
            double[] CmValues2 = new double[(int)numPoints];
            double[] LDValues2 = new double[(int)numPoints];

            for (int i = 0; i < 2 * numPoints; i++)
            {
                double angle = 0;
                if (i < numPoints)
                    angle = i / (double)numPoints * (upperBound - lowerBound) + lowerBound;
                else
                    angle = (i - (double)numPoints + 1) / (double)numPoints * (lowerBound - upperBound) + upperBound;

                double cy, cn, cr;

                aeroSim.GetClCdCmSteady(CoM, angle, 0, 0, 0, 0, 0, M, pitch, out Cl, out Cd, out Cm, out cy, out cn, out cr, true, i == 0);


                //                MonoBehaviour.print("Cl: " + Cl + " Cd: " + Cd);
                if (i < numPoints)
                {
                    AlphaValues[i] = angle;
                    ClValues[i] = Cl;
                    CdValues[i] = Cd;
                    CmValues[i] = Cm;
                    LDValues[i] = Cl / Cd;
                }
                else
                {
                    ClValues2[numPoints * 2 - 1 - i] = Cl;
                    CdValues2[numPoints * 2 - 1 - i] = Cd;
                    CmValues2[numPoints * 2 - 1 - i] = Cm;
                    LDValues2[numPoints * 2 - 1 - i] = Cl / Cd;
                }
            }

            for (int i = 0; i < FARAeroUtil.CurEditorParts.Count; i++)
            {
                Part p = FARAeroUtil.CurEditorParts[i];

                if (FARAeroUtil.IsNonphysical(p))
                    continue;
                for (int k = 0; k < p.Modules.Count; k++)
                {
                    PartModule m = p.Modules[k];
                    if (m is FARControllableSurface)
                    {
                        (m as FARControllableSurface).SetControlStateEditor(CoM, p.transform.up, (float)pitch, 0, 0, flap_setting, spoilersDeployed);
                    }
                }
            }

            string horizontalLabel = "Angle of Attack, degrees";
            UpdateGraph(AlphaValues,
                        ClValues, CdValues, CmValues, LDValues,
                        ClValues2, CdValues2, CmValues2, LDValues2,
                        horizontalLabel);
        }

        private void InitGraph()
        {
            graph.SetBoundaries(0, 25, 0, 2);
            graph.SetGridScaleUsingValues(5, 0.5);
            graph.horizontalLabel = "Angle of Attack, degrees";
            graph.verticalLabel = "Cl\nCd\nCm\nL/D / 10";
            graph.Update();


        }

        private void UpdateGraph(double[] AlphaValues, double[] ClValues, double[] CdValues, double[] CmValues, double[] LDValues, double[] ClValues2, double[] CdValues2, double[] CmValues2, double[] LDValues2, string horizontalLabel)
        {
            // To allow switching between two graph setups to observe differences,
            // use both the current and the previous shown graph to estimate scale
            double newMinBounds = Math.Min(Math.Min(LDValues.Min() / 10, ClValues.Min()), Math.Min(CdValues.Min(), CmValues.Min()));
            double newMaxBounds = Math.Max(Math.Max(LDValues.Max() / 10, ClValues.Max()), Math.Max(CdValues.Max(), CmValues.Max()));
            double minBounds = Math.Min(lastMinBounds, newMinBounds);
            double maxBounds = Math.Max(lastMaxBounds, newMaxBounds);
            lastMaxBounds = newMaxBounds;
            lastMinBounds = newMinBounds;

            double realMin = Math.Min(Math.Floor(minBounds), -0.25);
            double realMax = Math.Max(Math.Ceiling(maxBounds), 0.25);

            Color darkCl = new Color(clColor.r * 0.5f, clColor.g * 0.5f, clColor.b * 0.5f);
            Color darkCd = new Color(cdColor.r * 0.5f, cdColor.g * 0.5f, cdColor.b * 0.5f);
            Color darkCm = new Color(cmColor.r * 0.5f, cmColor.g * 0.5f, cmColor.b * 0.5f);
            Color darkL_D = new Color(l_DColor.r * 0.5f, l_DColor.g * 0.5f, l_DColor.b * 0.5f);
            bool hasHighToLowAoA = ClValues2 != null && CdValues2 != null && CmValues2 != null && LDValues2 != null;
            graph.Clear();
            graph.SetBoundaries(lowerBound, upperBound, realMin, realMax);
            graph.SetGridScaleUsingValues(5, 0.5);

            if (hasHighToLowAoA)
                graph.AddLine("Cd2", AlphaValues, CdValues2, darkCd, 1, false);
            graph.AddLine("Cd", AlphaValues, CdValues, cdColor);

            if (hasHighToLowAoA)
                graph.AddLine("Cl2", AlphaValues, ClValues2, darkCl, 1, false);
            graph.AddLine("Cl", AlphaValues, ClValues, clColor);

            if (hasHighToLowAoA)
                graph.AddLine("L/D2", AlphaValues, LDValues2, darkL_D, 1, false);
            graph.AddLine("L/D", AlphaValues, LDValues, l_DColor);

            if (hasHighToLowAoA)
                graph.AddLine("Cm2", AlphaValues, CmValues2, darkCm, 1, false);
            graph.AddLine("Cm", AlphaValues, CmValues, cmColor);

            if (hasHighToLowAoA)
            {
                graph.SetLineVerticalScaling("L/D2", 0.1);
                AddZeroMarks("Cm2", AlphaValues, CmValues2, upperBound - lowerBound, realMax - realMin, darkCm);
            }
            graph.SetLineVerticalScaling("L/D", 0.1);
            AddZeroMarks("Cm", AlphaValues, CmValues, upperBound - lowerBound, realMax - realMin, cmColor);

            graph.horizontalLabel = horizontalLabel;
            graph.verticalLabel = "Cl\nCd\nCm\nL/D / 10";
            graph.Update();
        }

        private void AddZeroMarks(String key, double[] x, double[] y, double xsize, double ysize, Color color)
        {
            int j = 0;

            for (int i = 0; i < y.Length - 1; i++)
            {
                if (Math.Sign(y[i]) == Math.Sign(y[i + 1]))
                    continue;

                /*// Don't display if slope is good enough?..
                float dx = Mathf.Abs(x[i+1]-x[i])*400/xsize;
                float dy = Mathf.Abs(y[i+1]-y[i])*275/ysize;
                if (dx <= 2*dy)
                    continue;*/

                double xv = x[i] + Math.Abs(y[i]) * (x[i + 1] - x[i]) / Math.Abs(y[i + 1] - y[i]);
                double yv = ysize * 3 / 275;
                graph.AddLine(key + (j++), new double[] { xv, xv }, new double[] { -yv, yv }, color, 1, false);
            }
        }

        #region GUI Start / End Functions

        public void ResetAll(ShipConstruct c)
        {
            RenderingManager.RemoveFromPostDrawQueue(0, new Callback(OnGUI));
            AllWings.Clear();
            AllControlSurfaces.Clear();
            RestartCtrlGUI();
        }

        public void RestartCtrlGUI()
        {
            lastMinBounds = lastMaxBounds = 0;
            CurrentEditorFlapSetting = 0;
            CurrentEditorSpoilerSetting = false;
            windowPos = FARGUIUtils.ClampToScreen(windowPos);
            RenderingManager.AddToPostDrawQueue(0, new Callback(OnGUI));
        }
        #endregion


        public void Update()
        {
            if (AllWings == null)
                AllWings = new List<FARWingAerodynamicModel>();
            if (AllControlSurfaces == null)
                AllControlSurfaces = new List<FARWingAerodynamicModel>();

            List<FARWingAerodynamicModel> nullWings = new List<FARWingAerodynamicModel>();

            for (int i = 0; i < AllWings.Count; i++)
            {
                FARWingAerodynamicModel w = AllWings[i];
                if (w.part == null || (w.part.parent == null && w.part != EditorLogic.RootPart))
                    nullWings.Add(w);
            }

            for (int i = 0; i < nullWings.Count; i++)
            {
                FARWingAerodynamicModel w = nullWings[i];
                if (AllWings.Contains(w))
                    AllWings.Remove(w);
                if (AllControlSurfaces.Contains(w))
                    AllControlSurfaces.Remove(w);
            }

            if (EditorLogic.RootPart)
            {
                for (int i = 0; i < FARAeroUtil.CurEditorParts.Count; i++)
                {
                    Part p = FARAeroUtil.CurEditorParts[i];
                    FARWingAerodynamicModel w = p.GetComponent<FARWingAerodynamicModel>();
                    if (w != null && !AllWings.Contains(w))
                    {
                        AllWings.Add(w);
                        if (w is FARControllableSurface)
                            AllControlSurfaces.Add(w);

                    }
                }
            }
        }

        public static void LoadColors()
        {
            if (clColor != new Color())
                return;

            clColor = Color.cyan;
            cdColor = Color.red;
            cmColor = Color.yellow;
            l_DColor = Color.green;

            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("FARGUIColors"))
            {
                if (node.HasValue("ClColor"))
                    clColor = ReadColor(node.GetValue("ClColor"));

                if (node.HasValue("CdColor"))
                    cdColor = ReadColor(node.GetValue("CdColor"));

                if (node.HasValue("CmColor"))
                    cmColor = ReadColor(node.GetValue("CmColor"));

                if (node.HasValue("L_DColor"))
                    l_DColor = ReadColor(node.GetValue("L_DColor"));
            }
        }

        private static Color ReadColor(string input)
        {
            char[] separators = {',', ' ', ';'};
            string[] splitValues = input.Split(separators);

            int curIndex = 0;
            Color color = new Color();
            for(int i = 0; i < splitValues.Length; i++)
            {
                string s = splitValues[i];
                if(s.Length > 0)
                {
                    float val;
                    if(float.TryParse(s, out val))
                    {
                        if (curIndex == 0)
                            color.r = val;
                        else if (curIndex == 1)
                            color.g = val;
                        else
                        {
                            color.b = val;
                            return color;
                        }
                        curIndex++;
                    }
                }
            }

            return color;
        }

        public static void SaveCustomColors()
        {
            ConfigNode node = new ConfigNode("@FARGUIColors[Default]:FINAL");
            node.AddValue("ClColor", clColor.ToString());
            node.AddValue("CdColor", cdColor.ToString());
            node.AddValue("CmColor", cmColor.ToString());
            node.AddValue("L_DColor", l_DColor.ToString());

            ConfigNode saveNode = new ConfigNode();
            saveNode.AddNode(node);
            saveNode.Save(KSPUtil.ApplicationRootPath.Replace("\\", "/") + "GameData/FerramAerospaceResearch/CustomFARGUIColors.cfg");
        }

        private static ConfigNode StringOverrideNode(List<string> stringList, string nodeName, string fieldName)
        {
            ConfigNode node = new ConfigNode(nodeName);
            int i = 0;

            foreach (string s in stringList)
            {
                string tmp = fieldName;
                i++;
                node.AddValue(tmp, s);
            }

            return node;
        }
        /*        public void SaveGUIParameters()
                {
                    var config = KSP.IO.PluginConfiguration.CreateForType<FAREditorGUI>();
                    config.load();
                    config.SetValue("windowPos", windowPos);
                    config.SetValue("EditorGUIBool", minimize);
                    config.save();
                }

                public void LoadGUIParameters()
                {
                    var config = KSP.IO.PluginConfiguration.CreateForType<FAREditorGUI>();
                    config.load();
                    windowPos = config.GetValue("windowPos", new Rect());
                    minimize = config.GetValue("EditorGUIBool", true);
                    if (windowPos.y < 75)
                        windowPos.y = 75;
                } */
    }
}

