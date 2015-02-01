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
using UnityEngine;
using KSP;

namespace FerramAerospaceResearch.FARPartGeometry
{
    class Polygon
    {
        List<Vector3d> points;
        public double area;
        public Vector3d centroid;
        public double xLength;
        public double zLength;

        public Polygon(List<Vector3d> pointsForConvexHull)
        {
            ConvexHull hull = new ConvexHull();
            points = hull.GrahamsScanVerts(pointsForConvexHull);

            area = CalculateArea();
            centroid = CalculateCentroid();
            CalculateMaxLengths();
        }

        private void CalculateMaxLengths()
        {
            double xMax = double.NegativeInfinity, xMin = double.PositiveInfinity;
            double zMax = double.NegativeInfinity, zMin = double.PositiveInfinity;
            for(int i = 0; i < points.Count; i++)
            {
                Vector3d pt = points[i];

                xMax = Math.Max(xMax, pt.x);
                xMin = Math.Min(xMin, pt.x);

                zMax = Math.Max(zMax, pt.z);
                zMin = Math.Min(zMin, pt.z);
            }

            xLength = xMax - xMin;
            zLength = zMax - zMin;
        }

        private double CalculateArea()
        {
            double area = 0;
            for(int i = 0; i < points.Count; i++)
            {
                Vector3d pt1, pt2;
                pt1 = points[i];

                if (i + 1 == points.Count)
                    pt2 = points[0];
                else
                    pt2 = points[i + 1];

                area += pt1.x * pt2.z;
                area -= pt2.x * pt1.z;
            }
            area *= 0.5;
            return area;
        }

        private Vector3d CalculateCentroid()
        {
            Vector3d centroid = new Vector3d();
            for (int i = 0; i < points.Count; i++)
                centroid += points[i];

            centroid /= points.Count;
            return centroid;
        }
    }
}
