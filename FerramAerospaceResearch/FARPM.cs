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
using KSP;

namespace ferram4 {
	public class FARPM : PartModule {
		public object ProcessVariable(string variable) {
			switch(variable) {
				case "FARAVAILABLE":
					if(FARControlSys.ActiveControlSys != null) {
						return 1;
					} else {
						return 0;
					}
				case "FARPM_DYNAMIC_PRESSURE_Q":
					return FARAPI.GetActiveControlSys_Q();
				case "FARPM_LIFT_COEFFICIENT_CL":
					return FARAPI.GetActiveControlSys_Cl();
				case "FARPM_DRAG_COEFFICIENT_CD":
					return FARAPI.GetActiveControlSys_Cd();
				case "FARPM_PITCHING_MOMENT_COEFFICIENT_CM":
					return FARAPI.GetActiveControlSys_Cm();
				case "FARPM_REFAREA":
					return FARAPI.GetActiveControlSys_RefArea();
				case "FARPM_MACHNUMBER":
					return FARAPI.GetActiveControlSys_MachNumber();
				case "FARPM_TERMINALVELOCITY":
					return FARAPI.GetActiveControlSys_TermVel();
				case "FARPM_BALLISTIC_COEFFICIENT":
					return FARAPI.GetActiveControlSys_BallisticCoeff();
				case "FARPM_ANGLE_OF_ATTACK":
					return FARAPI.GetActiveControlSys_AoA();
				case "FARPM_SIDESLIP":
					return FARAPI.GetActiveControlSys_Sideslip();
				case "FARPM_THRUST_SPECIFIC_FUEL_CONSUMPTION":
					return FARAPI.GetActiveControlSys_TSFC();
                case "FARPM_STALL_FRACTION":
                    return FARAPI.GetActiveControlSys_StallFrac();
                case "FARPM_STATUS_MESSAGE":
                    return FARAPI.GetActiveControlSys_StatusMessage();
            }
			return null;
		}
	}
}
