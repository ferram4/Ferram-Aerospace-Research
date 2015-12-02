/*
Ferram Aerospace Research v0.15.5.4 "Hoerner"
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
