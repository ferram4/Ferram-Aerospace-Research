using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FerramAerospaceResearch.FARGUI.FARFlightGUI;

namespace FerramAerospaceResearch
{
    public static class FARAPI
    {
        public static FlightGUI VesselFlightInfo(Vessel v)
        {
            FlightGUI gui = null;
            FlightGUI.vesselFlightGUI.TryGetValue(v, out gui);

            return gui;
        }

        public static double VesselDynPres(Vessel v)
        {
            FlightGUI gui = VesselFlightInfo(v);
            if(gui == null)
                return 0;
            else
                return gui.InfoParameters.dynPres;
        }

        public static double VesselLiftCoeff(Vessel v)
        {
            FlightGUI gui = VesselFlightInfo(v);
            if(gui == null)
                return 0;
            else
                return gui.InfoParameters.liftCoeff;
        }

        public static double VesselDragCoeff(Vessel v)
        {
            FlightGUI gui = VesselFlightInfo(v);
            if(gui == null)
                return 0;
            else
                return gui.InfoParameters.dragCoeff;
        }

        public static double VesselRefArea(Vessel v)
        {
            FlightGUI gui = VesselFlightInfo(v);
            if(gui == null)
                return 0;
            else
                return gui.InfoParameters.refArea;
        }

        public static double VesselTermVelEst(Vessel v)
        {
            FlightGUI gui = VesselFlightInfo(v);
            if(gui == null)
                return 0;
            else
                return gui.InfoParameters.termVelEst;
        }

        public static double VesselTermBallisticCoeff(Vessel v)
        {
            FlightGUI gui = VesselFlightInfo(v);
            if(gui == null)
                return 0;
            else
                return gui.InfoParameters.ballisticCoeff;
        }

        public static double VesselTermAoA(Vessel v)
        {
            FlightGUI gui = VesselFlightInfo(v);
            if(gui == null)
                return 0;
            else
                return gui.InfoParameters.aoA;
        }

        public static double VesselTermSideslip(Vessel v)
        {
            FlightGUI gui = VesselFlightInfo(v);
            if(gui == null)
                return 0;
            else
                return gui.InfoParameters.sideslipAngle;
        }

        public static double VesselTermTSFC(Vessel v)
        {
            FlightGUI gui = VesselFlightInfo(v);
            if(gui == null)
                return 0;
            else
                return gui.InfoParameters.tSFC;
        }

        public static double VesselTermStallFrac(Vessel v)
        {
            FlightGUI gui = VesselFlightInfo(v);
            if(gui == null)
                return 0;
            else
                return gui.InfoParameters.stallFraction;
        }
    }
}
