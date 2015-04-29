/*
Ferram Aerospace Research v0.14.6
Copyright 2014, Michael Ferrara, aka Ferram4

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
            			Taverius, for correcting a ton of incorrect values
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager 1.5 updates
            			ialdabaoth (who is awesome), who originally created Module Manager
                        Regex, for adding RPM support
            			Duxwing, for copy editing the readme
 * 
 * Kerbal Engineer Redux created by Cybutek, Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License
 *      Referenced for starting point for fixing the "editor click-through-GUI" bug
 *
 * Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/55219
 *
 * Toolbar integration powered by blizzy78's Toolbar plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/60863
 */

using System;
using System.Collections.Generic;
using PreFlightTests;
using FerramAerospaceResearch.FARGUI.FAREditorGUI.Simulation;

namespace FerramAerospaceResearch.FARGUI.FAREditorGUI.DesignConcerns
{
    class AeroStabilityConcern : DesignConcernBase
    {
        private InstantConditionSim _instantSim;
        private InstantConditionSimInput _simInput;
        private EditorFacilities _editorFacility;

        public AeroStabilityConcern(InstantConditionSim instantSim, EditorFacilities editorFacility)
        {
            _instantSim = instantSim;
            _editorFacility = editorFacility;
            _simInput = new InstantConditionSimInput();
        }

        public override bool TestCondition()
        {
            if (EditorLogic.SortedShipList.Count > 0)
            {
                _simInput.alpha = -1;
                _simInput.machNumber = 0.5;
                InstantConditionSimOutput output;
                _instantSim.GetClCdCmSteady(_simInput, out output, true, true);

                double Cm_1 = output.Cm;
                _simInput.alpha = 1;
                _instantSim.GetClCdCmSteady(_simInput, out output, true, true);

                if (output.Cm - Cm_1 > 0)
                    return false;
            }
            return true;
        }

        public override EditorFacilities GetEditorFacilities()
        {
            return base.GetEditorFacilities();
        }
        public override string GetConcernTitle()
        {
            return "Vehicle is aerodynamically unstable!";
        }
        public override string GetConcernDescription()
        {
            if (_editorFacility == EditorFacilities.VAB)
                return "The aerodynamic center is ahead of the center of mass; the rocket will require sufficient thrust vectoring to maintain forward flight.";
            else if (_editorFacility == EditorFacilities.SPH)
                return "The aerodynamic center is ahead of the center of mass; the plane will require sufficient control surfaces to maintain forward flight.";
            else
                return "";
        }
        public override DesignConcernSeverity GetSeverity()
        {
            if (_editorFacility == EditorFacilities.VAB)
                return DesignConcernSeverity.WARNING;
            else if (_editorFacility == EditorFacilities.SPH)
                return DesignConcernSeverity.CRITICAL;
            else
                return DesignConcernSeverity.WARNING;
        }
    }
}
