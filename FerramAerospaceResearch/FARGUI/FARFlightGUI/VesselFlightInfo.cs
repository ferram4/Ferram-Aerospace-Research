using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FerramAerospaceResearch.FARGUI.FARFlightGUI
{
    public struct VesselFlightInfo
    {
        public double liftForce, dragForce, sideForce;
        public double dynPres;

        public double liftCoeff, dragCoeff, sideCoeff;
        public double refArea;

        public double liftToDragRatio;
        public double velocityLiftToDragRatio;

        public double aoA, sideslipAngle;
        public double pitchAngle, headingAngle, rollAngle;

        public double dryMass, fullMass;

        public double tSFC;
        public double intakeAirFrac;
        public double specExcessPower;

        public double range;
        public double endurance;

        public double ballisticCoeff;
        public double termVelEst;

        public double stallFraction;
    }
}
