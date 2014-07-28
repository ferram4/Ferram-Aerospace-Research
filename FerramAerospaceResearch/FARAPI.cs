/*
Ferram Aerospace Research v0.14.1.2
Copyright 2014, Michael Ferrara, aka Ferram4

    This file is part of Ferram Aerospace Research.

    Ferram Aerospace Research is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Kerbal Joint Reinforcement is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Ferram Aerospace Research.  If not, see <http://www.gnu.org/licenses/>.

    Serious thanks:		a.g., for tons of bugfixes and code-refactorings
            			Taverius, for correcting a ton of incorrect values
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager 1.5 updates
            			ialdabaoth (who is awesome), who originally created Module Manager
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
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;

namespace ferram4
{
    //Can be used to access the 
    public static class FARAPI
    {
        public static bool ActiveControlSysIsOnVessel(Vessel v)
        {
            if (FARControlSys.ActiveControlSys != null)
                return FARControlSys.ActiveControlSys.vessel == v;

            return false;
        }

        public static double GetActiveControlSys_Q()
        {
            if (FARControlSys.ActiveControlSys != null)
                return FARControlSys.ActiveControlSys.q;
            return 0;
        }
        public static double GetActiveControlSys_Cl()
        {
            if (FARControlSys.ActiveControlSys != null)
                return FARControlSys.ActiveControlSys.Cl;
            return 0;
        }
        public static double GetActiveControlSys_Cd()
        {
            if (FARControlSys.ActiveControlSys != null)
                return FARControlSys.ActiveControlSys.Cd;
            return 0;
        }
        public static double GetActiveControlSys_Cm()
        {
            if (FARControlSys.ActiveControlSys != null)
                return FARControlSys.ActiveControlSys.Cm;
            return 0;
        }
        public static double GetActiveControlSys_RefArea()
        {
            if (FARControlSys.ActiveControlSys != null)
                return FARControlSys.ActiveControlSys.S;
            return 0;
        }
        public static double GetActiveControlSys_MachNumber()
        {
            if (FARControlSys.ActiveControlSys != null)
                return FARControlSys.ActiveControlSys.MachNumber;
            return 0;
        }
        public static double GetActiveControlSys_TermVel()
        {
            if (FARControlSys.ActiveControlSys != null)
                return FARControlSys.termVel;
            return 0;
        }
        public static double GetActiveControlSys_BallisticCoeff()
        {
            if (FARControlSys.ActiveControlSys != null)
                return FARControlSys.ballisticCoeff;
            return 0;
        }

        public static double GetActiveControlSys_AoA()
        {
            if (FARControlSys.ActiveControlSys != null)
                return FARControlSys.AoA;
            return 0;
        }
        public static double GetActiveControlSys_Sideslip()
        {
            if (FARControlSys.ActiveControlSys != null)
                return FARControlSys.yaw;
            return 0;
        }

        public static double GetActiveControlSys_TSFC()
        {
            if (FARControlSys.ActiveControlSys != null)
                return FARControlSys.TSFC;
            return 0;
        }
    }
}
