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

namespace FerramAerospaceResearch.FARPartGeoUtil
{
    struct CrossSection : IComparable<CrossSection>
    {
        public Vector2d centroid;
        public double area;
        public double station;
        public double radius;

        public CrossSection(ref Polygon poly, double station)
        {
            this.centroid = new Vector2d(poly.centroid.x, poly.centroid.z);
            this.area = poly.area;
            this.station = station;
            this.radius = Math.Sqrt(area / Math.PI);
        }

        public int CompareTo(CrossSection other)
        {
            return this.station.CompareTo(other.station);
        }

        public ConfigNode Save()
        {
            ConfigNode sectionNode = new ConfigNode("CROSS_SECTION");
            sectionNode.AddValue("station", this.station);
            sectionNode.AddValue("area", this.area);
            sectionNode.AddValue("centroid", this.centroid);
            sectionNode.AddValue("radius", this.radius);

            return sectionNode;
        }

        public void Load(ConfigNode sectionNode)
        {
            if (sectionNode.HasValue("station"))
            {
                if (!double.TryParse(sectionNode.GetValue("station"), out this.station))
                    this.station = 0;
            }
            if (sectionNode.HasValue("area"))
            {
                if (!double.TryParse(sectionNode.GetValue("area"), out this.area))
                    this.area = 0;
            }
            if (sectionNode.HasValue("radius"))
            {
                if (!double.TryParse(sectionNode.GetValue("radius"), out this.radius))
                    this.radius = 0;
            }
            if (sectionNode.HasValue("centroid"))
            {
                string s = sectionNode.GetValue("centroid");
                string[] split = s.Split(new char[] { ',', ' ', ';', ':' });

                if (!double.TryParse(split[0], out this.centroid.x))
                    this.centroid.x = 0;

                if (!double.TryParse(split[1], out this.centroid.y))
                    this.centroid.y = 0;

            }
        }
    }
}
