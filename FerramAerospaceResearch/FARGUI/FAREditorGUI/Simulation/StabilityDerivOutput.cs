using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FerramAerospaceResearch.FARGUI.FAREditorGUI.Simulation
{
    class StabilityDerivOutput
    {
        public double[] stabDerivs = new double[27];
        public double b;
        public double MAC;
        public double area;

        public double stableCl;
        public double stableCd;
        public double stableAoA;
        public string stableAoAState;

        public double nominalVelocity;
        public CelestialBody body;
        public double altitude;
    }
}
