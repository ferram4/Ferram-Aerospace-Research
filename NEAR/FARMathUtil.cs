/*
Neophyte's Elementary Aerodynamics Replacement v1.1.1
Copyright 2014, Michael Ferrara, aka Ferram4

    This file is part of Neophyte's Elementary Aerodynamics Replacement.

    Neophyte's Elementary Aerodynamics Replacement is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Kerbal Joint Reinforcement is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Neophyte's Elementary Aerodynamics Replacement.  If not, see <http://www.gnu.org/licenses/>.

    Serious thanks:		a.g., for tons of bugfixes and code-refactorings
            			Taverius, for correcting a ton of incorrect values
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager 1.5 updates
            			ialdabaoth (who is awesome), who originally created Module Manager
            			Duxwing, for copy editing the readme
 *
 * Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/55219
 *
 */


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace NEAR
{
    public unsafe static class FARMathUtil
    {
        private static FloatCurve fastSin = null;
        public static double deg2rad = Math.PI / 180;
        public static double rad2deg = 180 / Math.PI;

        public static float FastSin(float angle)
        {
            float input = angle;
            if (fastSin == null)
            {
                MonoBehaviour.print("Fast Sine Curve Initialized");
                fastSin = new FloatCurve();
                for (int i = 0; i <= 36; i++)
                {
                    float time = Mathf.PI * i / 72;
                    float value = Mathf.Sin(time);
                    float deriv = Mathf.Cos(time);
                    fastSin.Add(time, value, deriv, deriv);
                }
            }
            while (input <= 0)
                input += 2 * Mathf.PI;
            while (input > 2 * Mathf.PI)
                input -= 2 * Mathf.PI;

            if (input < Mathf.PI * 0.5f)
                return fastSin.Evaluate(input);
            else if (input < Mathf.PI)
                return fastSin.Evaluate(Mathf.PI - input);
            else if (input < 1.5f * Mathf.PI)
                return -fastSin.Evaluate(input - Mathf.PI);
            else
                return -fastSin.Evaluate(2 * Mathf.PI - input);
        }

        public static float FastCos(float angle)
        {
            return FastSin(angle + Mathf.PI * 0.5f);
        }

        public static float FastTan(float angle)
        {
            float input = angle;

            float tan = FastSin(input) / FastCos(input);

            return tan;

        }

        public static string FormatTime(double time)
        {
            int iTime = (int)time % 3600;
            int seconds = iTime % 60;
            int minutes = (iTime / 60) % 60;
            int hours = (iTime / 3600);
            return hours.ToString("D2")
            + ":" + minutes.ToString("D2") + ":" + seconds.ToString("D2");
        }
        
        public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0) return min;
            else if (val.CompareTo(max) > 0) return max;
            else return val;
        }

        public static bool Approximately(double p, double q)
        {
            if (Math.Abs(p - q) < double.Epsilon)
                return true;
            return false;
        }

        public static bool Approximately(double p, double q, double error)
        {
            if (Math.Abs(p - q) < error)
                return true;
            return false;
        }
        
        public static double ArithmeticGeometricMean(double a, double b, double error)
        {
            while (!Approximately(a, b, error))
            {
                double tmpA = 0.5 * (a + b);
                b = Math.Sqrt(a * b);
                a = tmpA;
            }
            return (a + b) * 0.5;
        }

        public static double ModifiedArithmeticGeometricMean(double a, double b, double error)
        {
            double c = 0;
            while (!Approximately(a, b, error))
            {
                double tmpA = 0.5 * (a + b);
                double tmpSqrt = Math.Sqrt((a - c) * (b - c));
                b = c + tmpSqrt;
                c = c - tmpSqrt; 
                a = tmpA;
            }
            return (a + b) * 0.5;
        }

        public static double CompleteEllipticIntegralSecondKind(double k, double error)
        {
            double value = 2 * ArithmeticGeometricMean(1, k, error);
            value = Math.PI * ModifiedArithmeticGeometricMean(1, k * k, error) / value;

            return value;
        }
    }
}
