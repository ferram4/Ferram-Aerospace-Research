using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FerramAerospaceResearch.FAREditorSim
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
    }
}
