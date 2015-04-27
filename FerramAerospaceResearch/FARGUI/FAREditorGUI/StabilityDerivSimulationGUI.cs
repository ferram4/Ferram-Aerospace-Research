using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FerramAerospaceResearch.FARGUI.FAREditorGUI.Simulation;
using UnityEngine;
using ferram4;

namespace FerramAerospaceResearch.FARGUI.FAREditorGUI
{
    class StabilityDerivSimulationGUI
    {

        private SimMode simMode = 0;

        EditorSimManager simManager;
        InitialConditions lonConditions, latConditions;
        ferramGraph _graph = new ferramGraph(400, 200);

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

        public StabilityDerivSimulationGUI(EditorSimManager simManager)
        {
            this.simManager = simManager;

            lonConditions = new InitialConditions(new string[] { "0", "0", "0", "0" }, new string[] { "u", "w", "q", "θ" }, new double[]{1, 1, Math.PI/180, Math.PI/180}, "0.01", "10");
            latConditions = new InitialConditions(new string[] { "0", "0", "0", "0" }, new string[] { "β", "r", "p", "φ" }, new double[]{Math.PI/180, Math.PI/180, Math.PI/180, Math.PI/180}, "0.01", "10");

            _graph.SetBoundaries(0, 10, 0, 2);
            _graph.SetGridScaleUsingValues(1, 0.25);
            _graph.horizontalLabel = "time";
            _graph.verticalLabel = "params";
            _graph.Update();
        }

        public void Display()
        {
            GUILayout.BeginHorizontal();
            simMode = (SimMode)GUILayout.SelectionGrid((int)simMode, SimMode_str, 2);

            GUILayout.EndHorizontal();
            StabilityDerivOutput vehicleData = simManager.vehicleData;



            if (simMode == SimMode.LONG)
            {
                LongitudinalGUI(vehicleData);
                DataInput(lonConditions, vehicleData);
            }
            else
            {
                LateralGUI(vehicleData);
                DataInput(latConditions, vehicleData);
            }
            GUIStyle BackgroundStyle = new GUIStyle(GUI.skin.box);
            BackgroundStyle.hover = BackgroundStyle.active = BackgroundStyle.normal;

            _graph.Display(BackgroundStyle, 0, 0);
            //graph.Display(GUILayout.Width(540), GUILayout.Height(300));

            DrawTooltip();
        }

        private void LongitudinalGUI(StabilityDerivOutput vehicleData)
        {

            GUIStyle BackgroundStyle = new GUIStyle(GUI.skin.box);
            BackgroundStyle.hover = BackgroundStyle.active = BackgroundStyle.normal;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Longitudinal Derivatives", GUILayout.Width(160));
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Down Vel Derivatives", GUILayout.Width(160));
            GUILayout.Label("Fwd Vel Derivatives", GUILayout.Width(160));
            GUILayout.Label("Pitch Rate Derivatives", GUILayout.Width(160));
            GUILayout.Label("Pitch Ctrl Derivatives", GUILayout.Width(160));
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(BackgroundStyle);
            GUILayout.BeginHorizontal();
            StabilityLabel("Zw: ", vehicleData.stabDerivs[3], " s⁻¹", "Change in Z-direction acceleration with respect to Z-direction velocity; should be negative", 160, -1);
            StabilityLabel("Zu: ", vehicleData.stabDerivs[6], " s⁻¹", "Change in Z-direction acceleration with respect to X-direction velocity; should be negative", 160, -1);
            StabilityLabel("Zq: ", vehicleData.stabDerivs[9], " m/s", "Change in Z-direction acceleration with respect to pitch-up rate; sign unimportant", 160, 0);
            StabilityLabel("Zδe: ", vehicleData.stabDerivs[12], " m/s²", "Change in Z-direction acceleration with respect to pitch control input; should be negative", 160, -1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            StabilityLabel("Xw: ", vehicleData.stabDerivs[4], " s⁻¹", "Change in X-direction acceleration with respect to Z-direction velocity; should be positive", 160, 1);
            StabilityLabel("Xu: ", vehicleData.stabDerivs[7], " s⁻¹", "Change in X-direction acceleration with respect to X-direction velocity; should be negative", 160, -1);
            StabilityLabel("Xq: ", vehicleData.stabDerivs[10], " m/s", "Change in X-direction acceleration with respect to pitch-up rate; sign unimportant", 160, 0);
            StabilityLabel("Xδe: ", vehicleData.stabDerivs[13], " m/s²", "Change in X-direction acceleration with respect to pitch control input; sign unimportant", 160, 0);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            StabilityLabel("Mw: ", vehicleData.stabDerivs[5], " (m * s)⁻¹", "Change in pitch-up angular acceleration with respect to Z-direction velocity; should be negative", 160, -1);
            StabilityLabel("Mu: ", vehicleData.stabDerivs[8], " (m * s)⁻¹", "Change in pitch-up angular acceleration acceleration with respect to X-direction velocity; sign unimportant", 160, 0);
            StabilityLabel("Mq: ", vehicleData.stabDerivs[11], " s⁻¹", "Change in pitch-up angular acceleration acceleration with respect to pitch-up rate; should be negative", 160, -1);
            StabilityLabel("Mδe: ", vehicleData.stabDerivs[14], " s⁻²", "Change in pitch-up angular acceleration acceleration with respect to pitch control input; should be positive", 160, 1);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndVertical();
        }

        private void LateralGUI(StabilityDerivOutput vehicleData)
        {
            GUIStyle BackgroundStyle = new GUIStyle(GUI.skin.box);
            BackgroundStyle.hover = BackgroundStyle.active = BackgroundStyle.normal;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Lateral Derivatives", GUILayout.Width(160));
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Sideslip Derivatives", GUILayout.Width(160));
            GUILayout.Label("Roll Rate Derivatives", GUILayout.Width(160));
            GUILayout.Label("Yaw Rate Derivatives", GUILayout.Width(160));
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(BackgroundStyle);
            GUILayout.BeginHorizontal();
            StabilityLabel("Yβ: ", vehicleData.stabDerivs[15], " m/s²", "Change in Y-direction acceleration with respect to sideslip angle β; should be negative", 160, -1);
            StabilityLabel("Yp: ", vehicleData.stabDerivs[18], " m/s", "Change in Y-direction acceleration with respect to roll-right rate; should be negative", 160, -1);
            StabilityLabel("Yr: ", vehicleData.stabDerivs[21], " m/s", "Change in Y-direction acceleration with respect to yaw-right rate; should be positive", 160, 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            StabilityLabel("Lβ: ", vehicleData.stabDerivs[16], " s⁻²", "Change in roll-right angular acceleration with respect to sideslip angle β; should be negative", 160, -1);
            StabilityLabel("Lp: ", vehicleData.stabDerivs[19], " s⁻¹", "Change in roll-right angular acceleration with respect to roll-right rate; should be negative", 160, -1);
            StabilityLabel("Lr: ", vehicleData.stabDerivs[22], " s⁻¹", "Change in roll-right angular acceleration with respect to yaw-right rate; should be positive", 160, 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            StabilityLabel("Nβ: ", vehicleData.stabDerivs[17], " s⁻²", "Change in yaw-right angular acceleration with respect to sideslip angle β; should be positive", 160, 1);
            StabilityLabel("Np: ", vehicleData.stabDerivs[20], " s⁻¹", "Change in yaw-right angular acceleration with respect to roll-right rate; sign unimportant", 160, 0);
            StabilityLabel("Nr: ", vehicleData.stabDerivs[23], " s⁻¹", "Change in yaw-right angular acceleration with respect to yaw-right rate; should be negative", 160, -1);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndVertical();


        }

        private void DataInput(InitialConditions inits, StabilityDerivOutput vehicleData)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < inits.inits.Length; i++)
            {
                GUILayout.Label("Init " + inits.names[i] +": ");
                inits.inits[i] = GUILayout.TextField(inits.inits[i], GUILayout.ExpandWidth(true));
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("End Time: ");
            inits.maxTime = GUILayout.TextField(inits.maxTime, GUILayout.ExpandWidth(true));
            GUILayout.Label("dt: ");
            inits.dt = GUILayout.TextField(inits.dt, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Run Simulation", GUILayout.Width(150.0F), GUILayout.Height(25.0F)))
            {
                for (int i = 0; i < inits.inits.Length; i++)
                {
                    inits.inits[i] = Regex.Replace(inits.inits[i], @"[^-?[0-9]*(\.[0-9]*)?]", "");
                }
                inits.maxTime = Regex.Replace(inits.maxTime, @"[^-?[0-9]*(\.[0-9]*)?]", "");
                inits.dt = Regex.Replace(inits.dt, @"[^-?[0-9]*(\.[0-9]*)?]", "");

                double[] initCond = new double[inits.inits.Length];
                for (int i = 0; i < initCond.Length; i++)
                {
                    initCond[i] = Convert.ToDouble(inits.inits[i]) * inits.scaling[i];
                }


                GraphData data = simManager.StabDerivLinearSim.RunTransientSimLateral(vehicleData, Convert.ToDouble(inits.maxTime), Convert.ToDouble(inits.dt), initCond);

                UpdateGraph(data, "time", "params", 0, Convert.ToDouble(inits.dt));
            }
            GUILayout.EndHorizontal();
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

        private void DrawTooltip()
        {
            if (GUI.tooltip == "")
                return;

            GUIStyle BackgroundStyle = new GUIStyle(GUI.skin.box);
            BackgroundStyle.hover = BackgroundStyle.active = BackgroundStyle.normal;

            Vector3 mousePos = GUIUtils.GetMousePos();
            Rect windowRect = EditorGUI.GUIRect;

            Rect tooltipRect = new Rect(Mathf.Clamp(mousePos.x - windowRect.x, 0, windowRect.width - 300), Mathf.Clamp(mousePos.y - windowRect.y, 0, windowRect.height - 80), 300, 80);

            GUIStyle toolTipStyle = BackgroundStyle;
            toolTipStyle.normal.textColor = toolTipStyle.active.textColor = toolTipStyle.hover.textColor = toolTipStyle.focused.textColor = toolTipStyle.onNormal.textColor = toolTipStyle.onHover.textColor = toolTipStyle.onActive.textColor = toolTipStyle.onFocused.textColor = new Color(1, 0.75f, 0);

            GUI.Box(tooltipRect, GUI.tooltip, toolTipStyle);
        }

        private void UpdateGraph(GraphData data, string horizontalLabel, string verticalLabel, double lowerBound, double upperBound)
        {
            double minBounds = double.PositiveInfinity;
            double maxBounds = double.NegativeInfinity;

            for (int i = 0; i < data.yValues.Count; i++)
            {
                minBounds = Math.Min(minBounds, data.yValues[i].Min());
                maxBounds = Math.Max(maxBounds, data.yValues[i].Max());
            }

            // To allow switching between two graph setups to observe differences,
            // use both the current and the previous shown graph to estimate scale

            double realMin = Math.Min(Math.Floor(minBounds), -0.25);
            double realMax = Math.Max(Math.Ceiling(maxBounds), 0.25);

            _graph.Clear();
            _graph.SetBoundaries(lowerBound, upperBound, realMin, realMax);
            _graph.SetGridScaleUsingValues(5, 0.5);

            for (int i = 0; i < data.yValues.Count; i++)
            {
                _graph.AddLine(data.lineNames[i], data.xValues, data.yValues[i], data.lineColors[i], 1, data.lineNameVisible[i]);
            }

            _graph.horizontalLabel = horizontalLabel;
            _graph.verticalLabel = verticalLabel;// "Cl\nCd\nCm\nL/D / 10";
            _graph.Update();
        }

        struct InitialConditions
        {
            public string[] inits;
            public string[] names;
            public double[] scaling;

            public string dt;
            public string maxTime;

            public InitialConditions(string[] inits, string[] names, double[] scaling, string dt, string maxTime)
            {
                this.inits = inits;
                this.names = names;
                this.scaling = scaling;
                this.dt = dt;
                this.maxTime = maxTime;
            }
        }
    }
}
