using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerramAerospaceResearch.FAREditorSim
{
    class GraphData
    {
        public double[] xValues;
        public List<double[]> yValues;
        public List<string> lineNames;
        public List<bool> lineNameVisible;
        public List<Color> lineColors;

        public GraphData()
        {
            yValues = new List<double[]>();
            lineNames = new List<string>();
            lineColors = new List<Color>();
            lineNameVisible = new List<bool>();
            xValues = null;
        }

        public void AddData(double[] yVals, Color lineColor, string name, bool nameVisible)
        {
            yValues.Add(yVals);
            lineColors.Add(lineColor);
            lineNames.Add(name);
            lineNameVisible.Add(nameVisible);
        }
    }
}
