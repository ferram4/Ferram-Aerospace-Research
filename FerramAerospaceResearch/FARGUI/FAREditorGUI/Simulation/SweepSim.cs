/*
Ferram Aerospace Research v0.15.5.3 "von Helmholtz"
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
using UnityEngine;
using ferram4;

namespace FerramAerospaceResearch.FARGUI.FAREditorGUI.Simulation
{
    class SweepSim
    {
        InstantConditionSim _instantCondition;

        public SweepSim(InstantConditionSim instantConditionSim)
        {
            _instantCondition = instantConditionSim;
        }

        public GraphData MachNumberSweep(double aoAdegrees, double pitch, double lowerBound, double upperBound, int numPoints, int flapSetting, bool spoilers, CelestialBody body)
        {
            FARAeroUtil.UpdateCurrentActiveBody(body);

            FARAeroUtil.ResetEditorParts();

            double[] ClValues = new double[(int)numPoints];
            double[] CdValues = new double[(int)numPoints];
            double[] CmValues = new double[(int)numPoints];
            double[] LDValues = new double[(int)numPoints];
            double[] AlphaValues = new double[(int)numPoints];

            InstantConditionSimInput input = new InstantConditionSimInput(aoAdegrees, 0, 0, 0, 0, 0, 0, pitch, flapSetting, spoilers);
            
            for (int i = 0; i < numPoints; i++)
            {
                input.machNumber = i / (double)numPoints * (upperBound - lowerBound) + lowerBound;

                if (input.machNumber == 0)
                    input.machNumber = 0.001;

                InstantConditionSimOutput output;

                _instantCondition.GetClCdCmSteady(input, out output, i == 0);
                AlphaValues[i] = input.machNumber;
                ClValues[i] = output.Cl;
                CdValues[i] = output.Cd;
                CmValues[i] = output.Cm;
                LDValues[i] = output.Cl * 0.1 / output.Cd;
            }

            GraphData data = new GraphData();
            data.xValues = AlphaValues;
            data.AddData(ClValues, GUIColors.GetColor(0), "Cl", true);
            data.AddData(CdValues, GUIColors.GetColor(1), "Cd", true);
            data.AddData(CmValues, GUIColors.GetColor(2), "Cm", true);
            data.AddData(LDValues, GUIColors.GetColor(3), "L/D", true);

            return data;
        }

        public GraphData AngleOfAttackSweep(double machNumber, double pitch, double lowerBound, double upperBound, int numPoints, int flapSetting, bool spoilers, CelestialBody body)
        {
            if (machNumber == 0)
                machNumber = 0.001;

            InstantConditionSimInput input = new InstantConditionSimInput(0, 0, 0, 0, 0, 0, machNumber, pitch, flapSetting, spoilers);

            FARAeroUtil.UpdateCurrentActiveBody(body);

            FARAeroUtil.ResetEditorParts();


            double[] ClValues = new double[(int)numPoints];
            double[] CdValues = new double[(int)numPoints];
            double[] CmValues = new double[(int)numPoints];
            double[] LDValues = new double[(int)numPoints];
            double[] AlphaValues = new double[(int)numPoints];
            double[] ClValues2 = new double[(int)numPoints];
            double[] CdValues2 = new double[(int)numPoints];
            double[] CmValues2 = new double[(int)numPoints];
            double[] LDValues2 = new double[(int)numPoints];

            for (int i = 0; i < 2 * numPoints; i++)
            {
                double angle = 0;
                if (i < numPoints)
                    angle = i / (double)numPoints * (upperBound - lowerBound) + lowerBound;
                else
                    angle = (i - (double)numPoints + 1) / (double)numPoints * (lowerBound - upperBound) + upperBound;

                input.alpha = angle;

                InstantConditionSimOutput output;

                _instantCondition.GetClCdCmSteady(input, out output, i == 0);

                //                MonoBehaviour.print("Cl: " + Cl + " Cd: " + Cd);
                if (i < numPoints)
                {
                    AlphaValues[i] = angle;
                    ClValues[i] = output.Cl;
                    CdValues[i] = output.Cd;
                    CmValues[i] = output.Cm;
                    LDValues[i] = output.Cl * 0.1 / output.Cd;
                }
                else
                {
                    ClValues2[numPoints * 2 - 1 - i] = output.Cl;
                    CdValues2[numPoints * 2 - 1 - i] = output.Cd;
                    CmValues2[numPoints * 2 - 1 - i] = output.Cm;
                    LDValues2[numPoints * 2 - 1 - i] = output.Cl * 0.1 / output.Cd;
                }
            }

            GraphData data = new GraphData();
            data.xValues = AlphaValues;
            data.AddData(ClValues2, GUIColors.GetColor(0) * 0.5f, "Cl2", false);
            data.AddData(ClValues, GUIColors.GetColor(0), "Cl", true);

            data.AddData(CdValues2, GUIColors.GetColor(1) * 0.5f, "Cd2", false);
            data.AddData(CdValues, GUIColors.GetColor(1), "Cd", true);

            data.AddData(CmValues2, GUIColors.GetColor(2) * 0.5f, "Cm2", false);
            data.AddData(CmValues, GUIColors.GetColor(2), "Cm", true);

            data.AddData(LDValues2, GUIColors.GetColor(3) * 0.5f, "L/D2", false);
            data.AddData(LDValues, GUIColors.GetColor(3), "L/D", true);


            return data;
        }

    }
}
