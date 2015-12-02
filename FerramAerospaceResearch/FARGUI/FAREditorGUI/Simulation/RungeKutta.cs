/*
<<<<<<< HEAD:FerramAerospaceResearch/FARRungeKutta.cs
Ferram Aerospace Research v0.14.7
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
=======
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
>>>>>>> 89b2865ff34b6d3d23d7e6860f7820d7aa80af02:FerramAerospaceResearch/FARGUI/FAREditorGUI/Simulation/RungeKutta.cs
 */

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FerramAerospaceResearch.FARGUI.FAREditorGUI.Simulation
{
    class RungeKutta4
    {
//        Vector4 a = new Vector4(0, 0.5f, 0.5f, 1);
        Vector4 c = new Vector4(1f / 6, 1f / 3, 1f / 3, 1f / 6);

//        FARMatrix b = new FARMatrix(3, 4);

        double dt;
        double endTime;
        double[] initCond;

        SimMatrix stateEquations;

        public double[,] soln;
        public double[] time;

        public RungeKutta4(double endTime, double dt, SimMatrix eqns, double[] initCond)
        {
//            b.Add(0.5f, 0, 1);
//            b.Add(0.5f, 1, 2);
//            b.Add(1, 2, 3);
            this.endTime = endTime;
            this.dt = dt;
            this.stateEquations = eqns;
            this.initCond = initCond;
            soln = new double[initCond.Length, (int)Math.Ceiling(endTime / dt)];
            time = new double[(int)Math.Ceiling(endTime / dt)];
        }

        public void Solve()
        {
            double t = 0;
            double[] currentState = initCond;
            int j = 0;

            while (j < time.Length)
            {
                for (int i = 0; i < currentState.Length; i++)
                    soln[i, j] = currentState[i];
                time[j] = t;

                currentState = NextState(currentState);

                for(int k = 0; k < currentState.Length; k++)
                    if (double.IsNaN(currentState[k]) || double.IsInfinity(currentState[k]))
                    {
                        currentState[k] = 0;
                        j = time.Length;
                        t = endTime;
                    }
                j++;
                t += dt;
            }
        }

        public double[] GetSolution(int i)
        {
            if (i + 1 > soln.GetLength(0))
            {
                MonoBehaviour.print("Error; Index out of bounds");
                return new double[time.Length];
            }

            double[] solution = new double[time.Length];
            for (int j = 0; j < solution.Length; j++)
                solution[j] = soln[i, j];
            return solution;
        }


        private double[] NextState(double[] currentState)
        {
            double[] next = new double[currentState.Length];
            double[] f1, f2, f3, f4;
            f1 = f2 = f3 = f4 = new double[currentState.Length];

            for (int j = 0; j < next.Length; j++)
            {
                for (int i = 0; i < currentState.Length; i++)
                {
                    f1[j] += currentState[i] * stateEquations.Value(i, j);
                }
            }

            for (int j = 0; j < next.Length; j++)
            {
                for (int i = 0; i < currentState.Length; i++)
                {
                    f2[j] += (currentState[i] + 0.5f * dt * f1[i]) * stateEquations.Value(i, j);
                }
            }

            for (int j = 0; j < next.Length; j++)
            {
                for (int i = 0; i < currentState.Length; i++)
                {
                    f3[j] += (currentState[i] + 0.5f * dt * f2[i]) * stateEquations.Value(i, j);
                }
            }

            for (int j = 0; j < next.Length; j++)
            {
                for (int i = 0; i < currentState.Length; i++)
                {
                    f4[j] += (currentState[i] + dt * f3[i]) * stateEquations.Value(i, j);
                }
            }

            for (int i = 0; i < next.Length; i++)
            {
                next[i] = currentState[i] + dt * (c[0] * f1[i] + c[1] * f2[i] + c[2] * f3[i] + c[3] * f4[i]);
            }


            return next;
        }
    }
}
