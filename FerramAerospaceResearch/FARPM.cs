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
			}
			return null;
		}
	}
}
