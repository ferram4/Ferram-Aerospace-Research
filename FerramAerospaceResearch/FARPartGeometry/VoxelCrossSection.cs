/*
Ferram Aerospace Research v0.15.6.4 "Kleinhans"
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
using System.Linq;
using System.Text;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeometry
{
    public struct VoxelCrossSection
    {
        public double area;
        public Vector3d centroid;
        public double secondAreaDeriv;   //second derivative of area, used in calculating slender body wave drag

        public double flatnessRatio;            //ratio of the longest distance to shortest distance of the cross-section.  Used in calculating body lift and drag
        public Vector3d flatNormalVector;       //unit vector indicating the direction perpendicular to the longest distance on the cross-section

        //public double additionalUnshadowedArea;        //area added to this crosssection that has no area ahead of it
        //public Vector3d additonalUnshadowedCentroid;     //centroid of unshadowedArea

        //public double removedArea;               //area removed from this particular crosssection, compared to the one in front of it
        //public Vector3d removedCentroid;          //centroid of removedArea

        public Dictionary<Part, SideAreaValues> partSideAreaValues;

        public double cpSonicForward, cpSonicBackward;    //pressure coefficients calculated for this section near Mach 1 when sweeping forward and backward through the voxel

        public class SideAreaValues
        {
            public double iP, iN, jP, jN, kP, kN;
            public double exposedAreaCount;
            public double crossSectionalAreaCount;
        }
    }
}
