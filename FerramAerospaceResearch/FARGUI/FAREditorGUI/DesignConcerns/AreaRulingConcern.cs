/*
Ferram Aerospace Research v0.15.6.5 "Knudsen"
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
using PreFlightTests;
using FerramAerospaceResearch.FARAeroComponents;

namespace FerramAerospaceResearch.FARGUI.FAREditorGUI.DesignConcerns
{
    class AreaRulingConcern : DesignConcernBase
    {
        private VehicleAerodynamics _vesselAero;

        public AreaRulingConcern(VehicleAerodynamics vesselAero)
        {
            _vesselAero = vesselAero;
        }

        public override bool TestCondition()
        {
            if (_vesselAero == null)
                return true;

            if (_vesselAero.SonicDragArea * 0.75 < _vesselAero.MaxCrossSectionArea)
                return true;

            return false;
        }

        public override EditorFacilities GetEditorFacilities()
        {
            return base.GetEditorFacilities();
        }
        public override string GetConcernTitle()
        {
            return "High Transonic / Supersonic Drag!";
        }
        public override string GetConcernDescription()
        {
            return "Cross-sectional area distribution is insufficiently smooth and/or contains very large instantaneous changes in area";
        }
        public override DesignConcernSeverity GetSeverity()
        {
            return DesignConcernSeverity.WARNING;
        }
    }
}
