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
using System.Text;
using UnityEngine;

namespace ferram4
{
    class FARMatrix
    {
        private double[,] matrix;
        public int m;
        public int n;

        public FARMatrix(int m, int n)
        {
            matrix = new double[m, n];
            this.m = m;
            this.n = n;
        }

        public double Value(int i, int j)
        {
            return matrix[i, j];
        }


        public void Add(double Element, int i, int j)
        {
            matrix[i, j] = Element;
        }

        public void PrintToConsole()
        {
            StringBuilder MatrixDump = new StringBuilder();
            for (int j = 0; j < n; j++)
            {
                MatrixDump.Append("[");
                for (int i = 0; i < m; i++)
                {
                    MatrixDump.Append(matrix[i, j]);
                    if (i < m - 1)
                        MatrixDump.Append(",");
                    else
                        MatrixDump.Append("]\n\r");
                }
            }
            MonoBehaviour.print(MatrixDump.ToString());


        }
    }
}
