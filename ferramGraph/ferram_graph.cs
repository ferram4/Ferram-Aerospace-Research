/* Name:    FerramGraph (Graph GUI Plugin)
 * Version: 1.2   (KSP 0.22+)
 * 
 * Author: Michael Ferrara AKA ferram4, USA
 * License: BY: Attribution-ShareAlike 3.0 Unported (CC BY-SA 3.0): http://creativecommons.org/licenses/by-sa/3.0/
 * 
 * 
 * Disclaimer: You use this at your own risk; this is an alpha plugin for an alpha game; if your computer disintigrates, it's not my fault. :P
 * 
 * 
 */

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ferram4
{
    public class ferramGraph
    {
        class ferramGraphLine
        {
            private Texture2D lineDisplay;
            private Texture2D lineLegend;
            public bool displayInLegend;
            private float[] rawDataX = new float[1];
            private float[] rawDataY = new float[1];
            private int[] pixelDataX = new int[1];
            private int[] pixelDataY = new int[1];
            private Vector4 bounds;
            public int lineThickness;
            public Color lineColor = new Color();
            public Color backgroundColor = new Color();
            private float verticalScaling;
            private float horizontalScaling;

            #region Constructor
            public ferramGraphLine(int width, int height)
            {
                lineDisplay = new Texture2D(width, height, TextureFormat.ARGB32, false);
                SetBoundaries(new Vector4(0, 1, 0, 1));
                lineThickness = 1;
                lineColor = Color.red;
                verticalScaling = 1;
                horizontalScaling = 1;
            }


            #endregion
            #region InputData


            public void InputData(float[] xValues, float[] yValues)
            {
                int elements = xValues.Length;
                rawDataX = new float[elements];
                rawDataY = new float[elements];

                for (int i = 0; i < elements; i++)
                {
                    if (float.IsNaN(xValues[i]))
                    {
                        xValues[i] = 0;
                        MonoBehaviour.print("Warning: NaN in xValues array; value set to zero");
                    }
                    if (float.IsNaN(yValues[i]))
                    {
                        yValues[i] = 0;
                        MonoBehaviour.print("Warning: NaN in yValues array; value set to zero");
                    }
                }
//                MonoBehaviour.print("Raw Data Arrays Initialized...");

                rawDataX = xValues;
                rawDataY = yValues;
                ConvertRawToPixels();
            }
            #endregion

            #region ConvertRawToPixels

            private void ConvertRawToPixels()
            {
                pixelDataX = new int[rawDataX.Length];
                pixelDataY = new int[rawDataY.Length];

                float xScaling = lineDisplay.width / (bounds.y - bounds.x);
                float yScaling = lineDisplay.height / (bounds.w - bounds.z);
                float tmpx, tmpy;

                for(int i = 0; i < rawDataX.Length; i++)
                {
                    tmpx = rawDataX[i] * horizontalScaling;
                    tmpy = rawDataY[i] * verticalScaling;

                    tmpx -= bounds.x;
                    tmpx *= xScaling;
                    tmpx = Mathf.RoundToInt(tmpx);

                    tmpy -= bounds.z;
                    tmpy *= yScaling;
                    tmpy = Mathf.RoundToInt(tmpy);

//                    MonoBehaviour.print("x: " + tmpx.ToString() + " y: " + tmpy.ToString());
                    pixelDataX[i] = (int)tmpx;
                    pixelDataY[i] = (int)tmpy;
                }
                Update();
            }
            #endregion

            public void SetBoundaries(Vector4 boundaries)
            {
                bounds = boundaries;
                if (rawDataX.Length > 0)
                {
                    ConvertRawToPixels();
                }
            }

            public void Update()
            {
                ClearLine();
                int lastx = -1;
                int lasty = -1;
                if (lineThickness < 1)
                    lineThickness = 1;

                for(int k = 0; k < pixelDataX.Length; k++)
                {
                    int tmpx = pixelDataX[k];
                    int tmpy = pixelDataY[k];
                    if (lastx >= 0)
                    {
                        int tmpThick = lineThickness - 1;
                        int xstart = Mathf.Min(tmpx, lastx);
                        int xend = Mathf.Max(tmpx, lastx);
                        int ystart;
                        int yend;

                        if (xstart == tmpx)
                        {
                            ystart = tmpy;
                            yend = lasty;
                        }
                        else
                        {
                            ystart = lasty;
                            yend = tmpy;
                        }

                        float m = ((float)yend - (float)ystart) / ((float)xend - (float)xstart);
                        if (Mathf.Abs(m) <= 1 && (xstart != xend))
                        {
                            for (int i = xstart; i < xend; i++)
                                for (int j = -tmpThick; j <= tmpThick; j++)
                                {
                                    int linear = Mathf.RoundToInt(m * (i - xend) + yend);
                                    if((i >= 0 && i <= lineDisplay.width) && (linear + j >= 0 && linear + j <= lineDisplay.height))
                                        lineDisplay.SetPixel(i, linear + j, lineColor);
                                }
                        }
                        else
                        {
                            ystart = Mathf.Min(tmpy, lasty);
                            yend = Mathf.Max(tmpy, lasty);

                            if (ystart == tmpy)
                            {
                                xstart = tmpx;
                                xend = lastx;
                            }
                            else
                            {
                                xstart = lastx;
                                xend = tmpx;
                            }

                            m = ((float)xend - (float)xstart) / ((float)yend - (float)ystart);

                            for (int i = ystart; i < yend; i++)
                                for (int j = -tmpThick; j <= tmpThick; j++)
                                {
                                    int linear = Mathf.RoundToInt(m * (i - yend) + xend);
                                    if ((linear + j >= 0 && linear + j <= lineDisplay.width) && (i >= 0 && i <= lineDisplay.height))
                                        lineDisplay.SetPixel(linear + j, i, lineColor);
                                }

                        }
                    }
                    lastx = tmpx;
                    lasty = tmpy;
                }
                lineDisplay.Apply();
                UpdateLineLegend();

            }

            private void UpdateLineLegend()
            {
                lineLegend = new Texture2D(25, 15, TextureFormat.ARGB32, false);
                for (int i = 0; i < lineLegend.width; i++)
                    for (int j = 0; j < lineLegend.height; j++)
                    {
                        if (Mathf.Abs((int)(j - (lineLegend.height / 2f))) < lineThickness)
                            lineLegend.SetPixel(i, j, lineColor);
                        else
                            lineLegend.SetPixel(i, j, backgroundColor);
                    }
                lineLegend.Apply();
            }

            private void ClearLine()
            {
                for (int i = 0; i < lineDisplay.width; i++)
                    for (int j = 0; j < lineDisplay.height; j++)
                        lineDisplay.SetPixel(i, j, new Color(0, 0, 0, 0));
                lineDisplay.Apply();
            }

            /// <summary>
            /// XMin, XMax, YMin, YMax
            /// </summary>
            public Vector4 GetExtremeData()
            {
                Vector4 extremes = Vector4.zero;
                extremes.x = Mathf.Min(rawDataX);
                extremes.y = Mathf.Max(rawDataX);
                extremes.z = Mathf.Min(rawDataY);
                extremes.w = Mathf.Max(rawDataY);

                return extremes;
            }

            public Texture2D Line()
            {
                return lineDisplay;
            }

            public Texture2D LegendImage()
            {
                return lineLegend;
            }

            public void UpdateVerticalScaling(float scaling)
            {
                verticalScaling = scaling;
                ConvertRawToPixels();
            }
            public void UpdateHorizontalScaling(float scaling)
            {
                horizontalScaling = scaling;
                ConvertRawToPixels();
            }

            public void ClearTextures()
            {
                GameObject.Destroy(lineLegend);
                GameObject.Destroy(lineDisplay);
                lineDisplay = null;
                lineLegend = null;
            }
        }



        protected Texture2D graph;
        protected Rect displayRect = new Rect(0, 0, 0, 0);

        private Dictionary<string, ferramGraphLine> allLines = new Dictionary<string, ferramGraphLine>();

        private Vector4 bounds;
        public bool autoscale = false;

        public Color backgroundColor = Color.black;
        public Color gridColor = new Color(0.42f, 0.35f, 0.11f, 1);
        public Color axisColor = Color.white;

        private string leftBound;
        private string rightBound;
        private string topBound;
        private string bottomBound;
        public string horizontalLabel = "Axis Label Here";
        public string verticalLabel = "Axis Label Here";
        private Vector2 ScrollView = Vector2.zero;

        #region Constructors
        public ferramGraph(int width, int height)
        {
            graph = new Texture2D(width, height, TextureFormat.ARGB32, false);
            SetBoundaries(0, 1, 0, 1);
            displayRect = new Rect(1, 1, graph.width, graph.height);
            GridInit();
        }

        public ferramGraph(int width, int height, float minx, float maxx, float miny, float maxy)
        {
            graph = new Texture2D(width, height, TextureFormat.ARGB32, false);
            SetBoundaries(minx, maxx, miny, maxy);
            displayRect = new Rect(1, 1, graph.width, graph.height);
            GridInit();
        }
        #endregion

        #region Scaling Functions
        public void SetBoundaries(float minx, float maxx, float miny, float maxy)
        {
            bounds.x = minx;
            bounds.y = maxx;
            bounds.z = miny;
            bounds.w = maxy;
            SetBoundaries(bounds);
        }

        public void SetBoundaries(Vector4 boundaries)
        {
            bounds = boundaries;
            leftBound = bounds.x.ToString();
            rightBound = bounds.y.ToString();
            topBound = bounds.w.ToString();
            bottomBound = bounds.z.ToString();
            foreach (KeyValuePair<string, ferramGraphLine> pair in allLines)
                pair.Value.SetBoundaries(bounds);
        }


        public void SetGridScaleUsingPixels(int gridWidth, int gridHeight)
        {
            GridInit(gridWidth, gridHeight);
            Update();
        }

        public void SetGridScaleUsingValues(float gridWidth, float gridHeight)
        {
            int pixelWidth, pixelHeight;

            pixelWidth = Mathf.RoundToInt(((gridWidth * displayRect.width) / (bounds.y - bounds.x)));
            pixelHeight = Mathf.RoundToInt(((gridHeight * displayRect.height) / (bounds.w - bounds.z)));

            SetGridScaleUsingPixels(pixelWidth, pixelHeight);
            

        }

        public void SetLineVerticalScaling(string lineName, float scaling)
        {
            if (!allLines.ContainsKey(lineName))
            {
                MonoBehaviour.print("Error: No line with that name exists");
                return;
            }
            ferramGraphLine line;

            allLines.TryGetValue(lineName, out line);

            line.UpdateVerticalScaling(scaling);
        }


        public void SetLineHorizontalScaling(string lineName, float scaling)
        {
            if (!allLines.ContainsKey(lineName))
            {
                MonoBehaviour.print("Error: No line with that name exists");
                return;
            }
            ferramGraphLine line;

            allLines.TryGetValue(lineName, out line);

            line.UpdateHorizontalScaling(scaling);
        }

        #endregion
        #region GridInit

        private void GridInit()
        {
            int squareSize = 25;
            GridInit(squareSize, squareSize);
        }


        private void GridInit(int widthSize, int heightSize)
        {

            int horizontalAxis, verticalAxis;

            horizontalAxis = Mathf.RoundToInt(-bounds.x * displayRect.width / (bounds.y - bounds.x));
            verticalAxis = Mathf.RoundToInt(-bounds.z * displayRect.height / (bounds.w - bounds.z));

            for (int i = 0; i < graph.width; i++)
            {
                for (int j = 0; j < graph.height; j++)
                {

                    Color grid = new Color(0.42f, 0.35f, 0.11f, 1);
                    if (i - horizontalAxis == 0 || j - verticalAxis == 0)
                        graph.SetPixel(i, j, axisColor);
                    else if ((i - horizontalAxis) % widthSize == 0 || (j - verticalAxis) % heightSize == 0)
                        graph.SetPixel(i, j, gridColor);
                    else
                        graph.SetPixel(i, j, backgroundColor);
                }
            }

            graph.Apply();
        }
        #endregion

        #region Add / Remove Line Functions

        public void AddLine(string lineName)
        {
            if (allLines.ContainsKey(lineName))
            {
                MonoBehaviour.print("Error: A Line with that name already exists");
                return;
            }
            ferramGraphLine newLine = new ferramGraphLine((int)displayRect.width, (int)displayRect.height);
            newLine.SetBoundaries(bounds);
            allLines.Add(lineName, newLine);
            Update();
        }

        public void AddLine(string lineName, float[] xValues, float[] yValues)
        {
            int lineThickness = 1;
            AddLine(lineName, xValues, yValues, lineThickness);
        }

        public void AddLine(string lineName, float[] xValues, float[] yValues, Color lineColor)
        {
            int lineThickness = 1;
            AddLine(lineName, xValues, yValues, lineColor, lineThickness);
        }

        public void AddLine(string lineName, float[] xValues, float[] yValues, int lineThickness)
        {
            Color lineColor = Color.red;
            AddLine(lineName, xValues, yValues, lineColor, lineThickness);
        }        

        public void AddLine(string lineName, float[] xValues, float[] yValues, Color lineColor, int lineThickness)
        {
            AddLine(lineName, xValues, yValues, lineColor, lineThickness, true);

        }

        public void AddLine(string lineName, float[] xValues, float[] yValues, Color lineColor, int lineThickness, bool display)
        {
            if (allLines.ContainsKey(lineName))
            {
                MonoBehaviour.print("Error: A Line with that name already exists");
                return;
            }
            if (xValues.Length != yValues.Length)
            {
                MonoBehaviour.print("Error: X and Y value arrays are different lengths");
                return;
            }

            ferramGraphLine newLine = new ferramGraphLine((int)displayRect.width, (int)displayRect.height);
            newLine.InputData(xValues, yValues);
            newLine.SetBoundaries(bounds);
            newLine.lineColor = lineColor;
            newLine.lineThickness = lineThickness;
            newLine.backgroundColor = backgroundColor;
            newLine.displayInLegend = display;

            allLines.Add(lineName, newLine);
            Update();
        }

        public void RemoveLine(string lineName)
        {
            if (!allLines.ContainsKey(lineName))
            {
                MonoBehaviour.print("Error: No line with that name exists");
                return;
            }

            ferramGraphLine line = allLines[lineName];
            allLines.Remove(lineName);

            line.ClearTextures();
            Update();

        }

        public void Clear()
        {
            foreach (KeyValuePair<string,ferramGraphLine> line in allLines)
            {
                line.Value.ClearTextures();
            }
            allLines.Clear();
            Update();
        }

        #endregion

        #region Update Data Functions

        public void UpdateLineData(string lineName, float[] xValues, float[] yValues)
        {
            if (xValues.Length != yValues.Length)
            {
                MonoBehaviour.print("Error: X and Y value arrays are different lengths");
                return;
            }

            ferramGraphLine line;

            if (allLines.TryGetValue(lineName, out line))
            {

                line.InputData(xValues, yValues);

                allLines.Remove(lineName);
                allLines.Add(lineName, line);
                Update();
            }
            else
                MonoBehaviour.print("Error: No line with this name exists");

        }

        #endregion


        #region Update Visual Functions
        /// <summary>
        /// Use this to update the graph display
        /// </summary>
        public void Update()
        {
            #region Autoscaling
            if (autoscale)
            {
                Vector4 extremes = Vector4.zero;
                bool init = false;
                foreach (KeyValuePair<string, ferramGraphLine> pair in allLines)
                {
                    Vector4 tmp = pair.Value.GetExtremeData();

                    if (!init)
                    {
                        extremes.x = tmp.x;
                        extremes.y = tmp.y;
                        extremes.z = tmp.z;
                        extremes.w = tmp.w;
                        init = true;
                    }
                    else
                    {
                        extremes.x = Mathf.Min(extremes.x, tmp.x);
                        extremes.y = Mathf.Max(extremes.y, tmp.y);
                        extremes.z = Mathf.Min(extremes.z, tmp.z);
                        extremes.w = Mathf.Max(extremes.w, tmp.w);

                    }

                    extremes.x = Mathf.Floor(extremes.x);
                    extremes.y = Mathf.Ceil(extremes.y);
                    extremes.z = Mathf.Floor(extremes.z);
                    extremes.w = Mathf.Ceil(extremes.w);
                }
                SetBoundaries(extremes);
            }
            #endregion
            foreach (KeyValuePair<string, ferramGraphLine> pair in allLines)
            {
                pair.Value.backgroundColor = backgroundColor;
                pair.Value.Update();
            }

        }

        public void LineColor(string lineName, Color newColor)
        {
            ferramGraphLine line;
            if (allLines.TryGetValue(lineName, out line))
            {
                line.lineColor = newColor;

                allLines.Remove(lineName);
                allLines.Add(lineName, line);

            }
        }

        public void LineThickness(string lineName, int thickness)
        {
            ferramGraphLine line;
            if (allLines.TryGetValue(lineName, out line))
            {
                line.lineThickness = Mathf.Clamp(thickness, 1, 6);

                allLines.Remove(lineName);
                allLines.Add(lineName, line);

            }
        }


        #endregion



        /// <summary>
        /// This displays the graph
        /// </summary>
        public void Display(GUIStyle AreaStyle, int horizontalBorder, int verticalBorder)
        {
            ScrollView = GUILayout.BeginScrollView(ScrollView, false, false);
            GUIStyle BackgroundStyle = new GUIStyle(GUI.skin.box);
            BackgroundStyle.hover = BackgroundStyle.active = BackgroundStyle.normal;

            GUILayout.Space(verticalBorder);
            //Vertical axis and labels

            GUILayout.BeginVertical();
            GUILayout.BeginArea(new Rect(20 + horizontalBorder, 15 + verticalBorder, 30, displayRect.height + 2 * verticalBorder));

            GUIStyle LabelStyle = new GUIStyle(GUI.skin.label);
            LabelStyle.alignment = TextAnchor.UpperCenter;

            GUILayout.Label(topBound, LabelStyle, GUILayout.Height(20), GUILayout.ExpandWidth(true));
            int pixelspace = (int)displayRect.height / 2 - 72;
            GUILayout.Space(pixelspace);
            GUILayout.Label(verticalLabel, LabelStyle, GUILayout.Height(100), GUILayout.ExpandWidth(true));
            GUILayout.Space(pixelspace);
            GUILayout.Label(bottomBound, LabelStyle, GUILayout.Height(20), GUILayout.ExpandWidth(true));

            GUILayout.EndArea();
            GUILayout.EndVertical();


            //Graph itself

            GUILayout.BeginVertical();
            Rect areaRect = new Rect(50 + horizontalBorder, 15 + verticalBorder, displayRect.width + 2 * horizontalBorder, displayRect.height + 2 * verticalBorder);
            GUILayout.BeginArea(areaRect);

            GUI.DrawTexture(displayRect, graph);
            foreach (KeyValuePair<string, ferramGraphLine> pair in allLines)
                GUI.DrawTexture(displayRect, pair.Value.Line());
            GUILayout.EndArea();

            //Horizontal Axis and Labels

            GUILayout.BeginArea(new Rect(50 + horizontalBorder, displayRect.height + verticalBorder + 15, displayRect.width + 2 * horizontalBorder, 30));
            GUILayout.BeginHorizontal(GUILayout.Width(displayRect.width));


            GUILayout.Label(leftBound, LabelStyle, GUILayout.Width(20), GUILayout.ExpandWidth(true));
            pixelspace = (int)displayRect.width / 2 - 102;
            GUILayout.Space(pixelspace);
            GUILayout.Label(horizontalLabel, LabelStyle, GUILayout.Width(160));
            GUILayout.Space(pixelspace);
            GUILayout.Label(rightBound, LabelStyle, GUILayout.Width(20), GUILayout.ExpandWidth(true));

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUILayout.EndVertical();

            GUILayout.BeginVertical();

            //Legend Area

            int movementdownwards = ((int)displayRect.height - allLines.Count * 20)/2;
            foreach (KeyValuePair<string, ferramGraphLine> pair in allLines)
            {
                if (!pair.Value.displayInLegend)
                    continue;

                GUILayout.BeginArea(new Rect(60 + displayRect.width + 2 * horizontalBorder, 15 + verticalBorder + movementdownwards, 25, 15));
                GUI.DrawTexture(new Rect(1, 1, 25, 15), pair.Value.LegendImage());
                GUILayout.EndArea();
                GUILayout.BeginArea(new Rect(85 + displayRect.width + 2 * horizontalBorder, 15 + verticalBorder + movementdownwards, 35, 15));
                GUILayout.Label(pair.Key, LabelStyle);
                GUILayout.EndArea();
                movementdownwards += 20;
            }
            GUILayout.EndVertical();

            int rightofarea = (int)displayRect.width + 2 * horizontalBorder + 30;
            int bottomofarea = (int)displayRect.height + 2 * verticalBorder + 30;

            GUILayout.Space(bottomofarea);
            GUILayout.EndScrollView();

        }

    }
}
