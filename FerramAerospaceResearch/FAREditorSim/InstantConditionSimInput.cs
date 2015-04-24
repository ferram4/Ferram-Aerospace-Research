using System;

namespace FerramAerospaceResearch.FAREditorSim
{
    class InstantConditionSimInput
    {
        public double alpha;
        public double beta;
        public double phi;
        public double alphaDot;
        public double betaDot;
        public double phiDot;
        public double machNumber;
        public double pitchValue;
        public int flaps;
        public bool spoilers;

        public InstantConditionSimInput() { }

        public InstantConditionSimInput(double alpha, double beta, double phi, double alphaDot, double betaDot, double phiDot, double machNumber, double pitchValue)
        {
            this.alpha = alpha;
            this.beta = beta;
            this.phi = phi;
            this.alphaDot = alphaDot;
            this.betaDot = betaDot;
            this.phiDot = phiDot;
            this.machNumber = machNumber;
            this.pitchValue = pitchValue;

            flaps = 0;
            spoilers = false;
        }

        public InstantConditionSimInput(double alpha, double beta, double phi, double alphaDot, double betaDot, double phiDot, double machNumber, double pitchValue, int flaps, bool spoilers)
        {
            this.alpha = alpha;
            this.beta = beta;
            this.phi = phi;
            this.alphaDot = alphaDot;
            this.betaDot = betaDot;
            this.phiDot = phiDot;
            this.machNumber = machNumber;
            this.pitchValue = pitchValue;
            this.flaps = flaps;
            this.spoilers = spoilers;
        }
    }
}
