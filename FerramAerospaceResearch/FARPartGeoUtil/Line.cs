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
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeoUtil
{
    class Line : IEquatable<Line>
    {
        public Vector3 point1;
        public Vector3 point2;

        public Line(Vector3 point1, Vector3 point2)
        {
            if (point1.y < point2.y)
            {
                this.point2 = point2;
                this.point1 = point1;
            }
            else
            {
                this.point1 = point2;
                this.point2 = point1;
            }
        }

        public void TransformLine(Matrix4x4 transformationMatrix)
        {
            point1 = transformationMatrix.MultiplyVector(point1);
            point2 = transformationMatrix.MultiplyVector(point2);
            if(point1.y > point2.y)
            {
                Vector3 tmp = point1;
                point1 = point2;
                point2 = tmp;
            }
        }

        public Vector3 GetPoint(float yVal)
        {
            Vector3 point = new Vector3();
            point.y = yVal;

            float tmp = (point1.y - point2.y);
            tmp = 1 / tmp;

            point.x = (point1.x - point2.x) * tmp * (yVal - point2.y) + point2.x;
            point.z = (point1.z - point2.z) * tmp * (yVal - point2.y) + point2.z;

            return point;
        }

        public bool Equals(Line other)
        {
            if (other.point1 == this.point1 && other.point2 == this.point2)
                return true;

            return false;
        }

        public override int GetHashCode()
        {
            return point1.GetHashCode() * point2.GetHashCode();
        }
    }
}
