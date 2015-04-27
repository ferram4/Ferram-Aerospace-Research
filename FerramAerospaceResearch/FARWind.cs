using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FerramAerospaceResearch
{
    /// <summary>
    /// Entry point where another assembly can specify a function to calculate the wind.
    /// The rest of the simulation uses this class to get the wind and includes it in the
    /// total airspeed for the simulation.
    /// </summary>
    public static class FARWind
    {
        /// <summary>
        /// A WindFunction takes the current celestial body and a position (should be the position of the part)
        /// and returns the wind as a Vector3 (unit is m/s)
        /// </summary>
        public delegate Vector3 WindFunction(CelestialBody body, Part part, Vector3 position);


        /// <summary>
        /// The actual delegate is private: we want to avoid a public get method, so that the only way to call the function
        /// is using the safe wrapper GetWind, which suppresses all exceptions
        /// </summary>
        private static WindFunction func = ZeroWind;

        /// <summary>
        /// Calculates the wind's intensity using the specified wind function.
        /// If any exception occurs, it is suppressed and Vector3.zero is returned.
        /// This function will never throw, (although it will spam the log).
        /// </summary>
        public static Vector3 GetWind(CelestialBody body, Part part, Vector3 position)
        {
            try
            {
                return func(body, part, position);
            }
            catch (Exception e)
            {
                Debug.Log("[FARWIND] Exception! " + e.Message + e.StackTrace);
                return Vector3.zero;
            }
        }


        /// <summary>
        /// "Set" method for the wind function.
        /// If newFunction is null, it resets the function to ZeroWind.
        /// </summary>
        public static void SetWindFunction(WindFunction newFunction)
        {
            if (newFunction == null)
            {
                Debug.Log("[FARWind] Attempted to set a null wind function, using ZeroWind instead.");
                FARWind.func = ZeroWind;
            }
            else
            {
                Debug.Log("[FARWind] Setting wind function to " + newFunction.Method.Name);
                FARWind.func = newFunction;
            }            
        }


        public static Vector3 ZeroWind(CelestialBody body, Part part, Vector3 position)
        {
            return Vector3.zero;
        }        
    }
}
