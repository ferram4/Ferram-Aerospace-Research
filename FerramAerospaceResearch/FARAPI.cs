/*
Ferram Aerospace Research v0.15.3.1 "Garabedian"
=========================
Aerodynamics model for Kerbal Space Program

Copyright 2015, Michael Ferrara, aka Ferram4

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
				stupid_chris, for the RealChuteLite implementation
            			Taverius, for correcting a ton of incorrect values  
				Tetryds, for finding lots of bugs and issues and not letting me get away with them, and work on example crafts
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager updates  
            			ialdabaoth (who is awesome), who originally created Module Manager  
                        	Regex, for adding RPM support  
				DaMichel, for some ferramGraph updates and some control surface-related features  
            			Duxwing, for copy editing the readme  
   
   CompatibilityChecker by Majiir, BSD 2-clause http://opensource.org/licenses/BSD-2-Clause

   Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission  
	http://forum.kerbalspaceprogram.com/threads/55219

   ModularFLightIntegrator by Sarbian, Starwaster and Ferram4, MIT: http://opensource.org/licenses/MIT
	http://forum.kerbalspaceprogram.com/threads/118088

   Toolbar integration powered by blizzy78's Toolbar plugin; used with permission  
	http://forum.kerbalspaceprogram.com/threads/60863
 */

using System;
using UnityEngine;
using FerramAerospaceResearch.FARGUI.FARFlightGUI;
using FerramAerospaceResearch.FARAeroComponents;

namespace FerramAerospaceResearch
{
    public partial class FARAPI
    {
        private static FARAPI instance;
        private static FARAPI Instance
        {
            get
            {
                if (instance == null)
                    instance = new FARAPI();
                return instance;
            }
        }



        #region CurrentFlightInfo

        private FlightGUI GetFlightInfo(Vessel v)
        {
            FlightGUI gui = null;
            FlightGUI.vesselFlightGUI.TryGetValue(v, out gui);

            return gui;
        }

        public static FlightGUI VesselFlightInfo(Vessel v)
        {
            return Instance.GetFlightInfo(v);
        }

        public static double ActiveVesselDynPres()
        {
            return VesselDynPres(FlightGlobals.ActiveVessel);
        }

        public static double VesselDynPres(Vessel v)
        {
            FlightGUI gui = VesselFlightInfo(v);
            if(gui == null)
                return 0;
            else
                return gui.InfoParameters.dynPres;
        }

        public static double ActiveVesselLiftCoeff()
        {
            return VesselLiftCoeff(FlightGlobals.ActiveVessel);
        }

        public static double VesselLiftCoeff(Vessel v)
        {
            FlightGUI gui = VesselFlightInfo(v);
            if(gui == null)
                return 0;
            else
                return gui.InfoParameters.liftCoeff;
        }

        public static double ActiveVesselDragCoeff()
        {
            return VesselDragCoeff(FlightGlobals.ActiveVessel);
        }

        public static double VesselDragCoeff(Vessel v)
        {
            FlightGUI gui = VesselFlightInfo(v);
            if(gui == null)
                return 0;
            else
                return gui.InfoParameters.dragCoeff;
        }

        public static double ActiveVesselRefArea()
        {
            return VesselRefArea(FlightGlobals.ActiveVessel);
        }

        public static double VesselRefArea(Vessel v)
        {
            FlightGUI gui = VesselFlightInfo(v);
            if(gui == null)
                return 0;
            else
                return gui.InfoParameters.refArea;
        }

        public static double ActiveVesselTermVelEst()
        {
            return VesselTermVelEst(FlightGlobals.ActiveVessel);
        }

        public static double VesselTermVelEst(Vessel v)
        {
            FlightGUI gui = VesselFlightInfo(v);
            if(gui == null)
                return 0;
            else
                return gui.InfoParameters.termVelEst;
        }

        public static double ActiveVesselBallisticCoeff()
        {
            return VesselBallisticCoeff(FlightGlobals.ActiveVessel);
        }

        public static double VesselBallisticCoeff(Vessel v)
        {
            FlightGUI gui = VesselFlightInfo(v);
            if(gui == null)
                return 0;
            else
                return gui.InfoParameters.ballisticCoeff;
        }

        public static double ActiveVesselAoA()
        {
            return VesselAoA(FlightGlobals.ActiveVessel);
        }

        public static double VesselAoA(Vessel v)
        {
            FlightGUI gui = VesselFlightInfo(v);
            if(gui == null)
                return 0;
            else
                return gui.InfoParameters.aoA;
        }

        public static double ActiveVesselSideslip()
        {
            return VesselSideslip(FlightGlobals.ActiveVessel);
        }

        public static double VesselSideslip(Vessel v)
        {
            FlightGUI gui = VesselFlightInfo(v);
            if(gui == null)
                return 0;
            else
                return gui.InfoParameters.sideslipAngle;
        }

        public static double ActiveVesselTSFC()
        {
            return VesselTSFC(FlightGlobals.ActiveVessel);
        }
        
        public static double VesselTSFC(Vessel v)
        {
            FlightGUI gui = VesselFlightInfo(v);
            if(gui == null)
                return 0;
            else
                return gui.InfoParameters.tSFC;
        }

        public static double ActiveVesselStallFrac()
        {
            return VesselStallFrac(FlightGlobals.ActiveVessel);
        }
        
        public static double VesselStallFrac(Vessel v)
        {
            FlightGUI gui = VesselFlightInfo(v);
            if(gui == null)
                return 0;
            else
                return gui.InfoParameters.stallFraction;
        }

        /// <summary>
        /// Increases flap deflection level for all control surfaces on this vessel, up to max setting of 3
        /// </summary>
        public static void VesselIncreaseFlapDeflection(Vessel v)
        {
            for(int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if(p.Modules.Contains("FARControllableSurface"))
                {
                    ferram4.FARControllableSurface surface = (ferram4.FARControllableSurface)p.Modules["FARControllableSurface"];
                    surface.SetDeflection(surface.flapDeflectionLevel + 1);
                }
            }
        }

        /// <summary>
        /// Decreases flap deflection level for all control surfaces on this vessel, down to min setting of 0
        /// </summary>
        public static void VesselDecreaseFlapDeflection(Vessel v)
        {
            for(int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (p.Modules.Contains("FARControllableSurface"))
                {
                    ferram4.FARControllableSurface surface = (ferram4.FARControllableSurface)p.Modules["FARControllableSurface"];
                    surface.SetDeflection(surface.flapDeflectionLevel - 1);
                }
            }
        }

        /// <summary>
        /// Returns flap setting for this vessel
        /// </summary>
        /// <param name="v"></param>
        /// <returns>Flap setting; 0 - 3 indicates no to full flap deflections; -1 indicates lack of any control surface parts</returns>
        public static int VesselFlapSetting(Vessel v)
        {
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (p.Modules.Contains("FARControllableSurface"))
                {
                    ferram4.FARControllableSurface surface = (ferram4.FARControllableSurface)p.Modules["FARControllableSurface"];
                    if(surface.isFlap)
                        return surface.flapDeflectionLevel;
                }
            }

            return -1;
        }
        
        /// <summary>
        /// Sets spoilers to a certain value on this vessel
        /// </summary>
        public static void VesselSetSpoilers(Vessel v, bool spoilerActive)
        {
            for(int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if(p.Modules.Contains("FARControllableSurface"))
                {
                    ferram4.FARControllableSurface surface = (ferram4.FARControllableSurface)p.Modules["FARControllableSurface"];
                    surface.brake = spoilerActive;
                }
            }
        }

        /// <summary>
        /// Returns spoiler setting for this vessel
        /// </summary>
        /// <param name="v"></param>
        /// <returns>Spoiler setting; true indicates active spoilers, false indicates inactive or no spoilers in existence</returns>
        public static bool VesselSpoilerSetting(Vessel v)
        {
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (p.Modules.Contains("FARControllableSurface"))
                {
                    ferram4.FARControllableSurface surface = (ferram4.FARControllableSurface)p.Modules["FARControllableSurface"];
                    if (surface.isSpoiler)
                        return surface.brake;
                }
            }

            return false;
        }
        
        #endregion

        #region AeroPredictions

        private void InstanceCalcVesselAeroForces(Vessel vessel, out Vector3 aeroForce, out Vector3 aeroTorque, Vector3 velocityWorldVector, double altitude)
        {
            aeroForce = aeroTorque = Vector3.zero;
            if (vessel == null)
            {
                Debug.LogError("FAR API Error: attempted to simulate aerodynamics of null vessel");
                return;
            }

            FARVesselAero vesselAero = vessel.GetComponent<FARVesselAero>();

            if (vesselAero == null)
            {
                Debug.LogError("FAR API Error: vessel does not have FARVesselAero aerocomponent for simulation");
                return;
            }

            vesselAero.SimulateAeroProperties(out aeroForce, out aeroTorque, velocityWorldVector, altitude);
        }
        
        /// <summary>
        /// Calculates the forces and torque on a vessel at a given condition at the CoM
        /// </summary>
        /// <param name="vessel">Vessel in question</param>
        /// <param name="aeroForce">Total aerodynamic force at CoM, in kN</param>
        /// <param name="aeroTorque">Total aerodynamic torque at CoM, in kN * m</param>
        /// <param name="velocityWorldVector">Velocity vector in worldspace relative to the atmosphere for CURRENT vessel orientation, m/s</param>
        /// <param name="density">Atm density at that location, kg/m^3</param>
        /// <param name="machNumber">Mach number at that location</param>
        public static void CalculateVesselAeroForces(Vessel vessel, out Vector3 aeroForce, out Vector3 aeroTorque, Vector3 velocityWorldVector, double altitude)
        {
            Instance.InstanceCalcVesselAeroForces(vessel, out aeroForce, out aeroTorque, velocityWorldVector, altitude);
        }
        #endregion
    }
}
