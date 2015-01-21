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

namespace FerramAerospaceResearch.FARPartGeoUtil
{
    //Returns a list of verts defining a convex polygon created by using Graham's Scan on a set of points
    class ConvexHull
    {
        //Something to make the turning of the algorithm clearer
        private enum TURN
        {
            LEFT,
            RIGHT,
            NONE
        }
        //Calculates whether the turn is right, left, or not at all for a path going from vert1 to vert2 to vert3
        private TURN TurnDirection(Vector3d vert1, Vector3d vert2, Vector3d vert3)
        {
            double tmp = (vert2[0] - vert1[0]) * (vert3[1] - vert1[1]) - (vert3[0] - vert1[0]) * (vert2[1] - vert1[1]);
            if (tmp > 0)
                return TURN.LEFT;
            if (tmp < 0)
                return TURN.RIGHT;
            return TURN.NONE;
        }
        private List<Vector3d> CorrectHullMistakes(List<Vector3d> hull, Vector3d r)
        {
            while (hull.Count > 1 && TurnDirection(hull[hull.Count - 2], hull[hull.Count - 1], r) != TURN.LEFT)
                hull.RemoveAt(hull.Count - 1);
            if (hull.Count == 0 || hull[hull.Count - 1] != r)
                hull.Add(r);
            return hull;
        }
        public List<Vector3d> GrahamsScanVerts(List<Vector3d> verts)
        {
            verts.Sort(new Vector3dXComparer());
            ////The above was necessary due to Sort()'s failures when the comparer used > / < rather than CompareTo
            //Let's not take any chances with this, MergeSort hasn't had any issues
            //verts.Sort(new Vector3dXComparer());
            string s = "";
            for (int i = 0; i < verts.Count; i++)
                s += verts[i] + "\n\r";
            Debug.Log(s);
            List<Vector3d> l = new List<Vector3d>();
            List<Vector3d> u = new List<Vector3d>();
            for (int i = 0; i < verts.Count; i++)
            {
                l = CorrectHullMistakes(l, verts[i]);
                u = CorrectHullMistakes(u, verts[verts.Count - (1 + i)]);
            }
            verts = l;
            if(u.Count > 1)
                verts.AddRange(u.GetRange(1, Math.Max(u.Count - 2,1)));
            return verts;
        }

        private class Vector3dXComparer : IComparer<Vector3d>
        {
            public int Compare(Vector3d x, Vector3d y)
            {
                int tmp = x.x.CompareTo(y.x); //Must use CompareTo, not < / >
                if (tmp == 0)
                    return x.z.CompareTo(y.z);
                return tmp;
            }
        }
    }
}
