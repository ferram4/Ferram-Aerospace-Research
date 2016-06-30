/*
Ferram Aerospace Research v0.15.7.1 "Kutta"
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
using System.Collections;
using UnityEngine;
using FerramAerospaceResearch.FARThreading;


namespace FerramAerospaceResearch.FARPartGeometry
{
    public class PartSizePair8Bit : PartSizePair
    {
        const float AREA_SCALING = 1f / (255f * 255f * 255f);
        const int LENGTH_OF_VOXEL = 255;

        byte xPlaneUp, yPlaneUp, zPlaneUp;
        byte xPlaneDown, yPlaneDown, zPlaneDown;

        public PartSizePair8Bit() { }

        public override void Clear()
        {
            xPlaneUp = yPlaneUp = zPlaneUp = 0;
            xPlaneDown = yPlaneDown = zPlaneDown = 0;
            part = null;
        }

        public override float GetSize()
        {
            int x, y, z;

            x = xPlaneUp + xPlaneDown;
            y = yPlaneUp + yPlaneDown;
            z = zPlaneUp + zPlaneDown;

            if (x > LENGTH_OF_VOXEL)
                x -= LENGTH_OF_VOXEL;

            //if (y > LENGTH_OF_VOXEL)
            //    y -= LENGTH_OF_VOXEL;

            if (z > LENGTH_OF_VOXEL)
                z -= LENGTH_OF_VOXEL;

            if (x == 0 && y == 0 && z == 0)      //if they're all 0, this is 0
                return 0f;

            //If a plane actually passes through this, that means that any values that are 0 indicate no planes cutting through this, and thus, that they should fill that dimension
            if (x == 0)
                x = LENGTH_OF_VOXEL;

            //if (y == 0)
            //    y = LENGTH_OF_VOXEL;

            if (z == 0)
                z = LENGTH_OF_VOXEL;

            y = LENGTH_OF_VOXEL;    //quick solution; always full in flight direction

            float size = x * y * z;     //so then calc the volume
            size *= AREA_SCALING;       //scale for the 0-15 scaling used for the plane locations
            return size;
        }

        public override void SetFilledSides(VoxelOrientationPlane filledPlanes)
        {
            if ((filledPlanes & VoxelOrientationPlane.X_UP) == VoxelOrientationPlane.X_UP)
                xPlaneUp = LENGTH_OF_VOXEL;

            if ((filledPlanes & VoxelOrientationPlane.X_DOWN) == VoxelOrientationPlane.X_DOWN)
                xPlaneDown = LENGTH_OF_VOXEL;

            if ((filledPlanes & VoxelOrientationPlane.Y_UP) == VoxelOrientationPlane.Y_UP)
                yPlaneUp = LENGTH_OF_VOXEL;

            if ((filledPlanes & VoxelOrientationPlane.Y_DOWN) == VoxelOrientationPlane.Y_DOWN)
                yPlaneDown = LENGTH_OF_VOXEL;

            if ((filledPlanes & VoxelOrientationPlane.Z_UP) == VoxelOrientationPlane.Z_UP)
                zPlaneUp = LENGTH_OF_VOXEL;

            if ((filledPlanes & VoxelOrientationPlane.Z_DOWN) == VoxelOrientationPlane.Z_DOWN)
                zPlaneDown = LENGTH_OF_VOXEL;
        }

        //Will return true if the location is updated
        public override bool SetPlaneLocation(VoxelOrientationPlane plane, byte location)
        {
            bool returnVal = false;

            switch (plane)
            {
                case VoxelOrientationPlane.X_UP:
                    {
                        if (location > xPlaneUp)
                        {
                            xPlaneUp = location;
                            returnVal = true;
                        }
                        break;
                    }

                case VoxelOrientationPlane.X_DOWN:
                    {
                        if (location > xPlaneDown)
                        {
                            xPlaneDown = location;
                            returnVal = true;
                        }
                        break;
                    }

                case VoxelOrientationPlane.Y_UP:
                    {
                        if (location > yPlaneUp)
                        {
                            yPlaneUp = location;
                            returnVal = true;
                        }
                        break;
                    }

                case VoxelOrientationPlane.Y_DOWN:
                    {
                        if (location > yPlaneDown)
                        {
                            yPlaneDown = location;
                            returnVal = true;
                        }
                        break;

                    }

                case VoxelOrientationPlane.Z_UP:
                    {
                        if (location > zPlaneUp)
                        {
                            zPlaneUp = location;
                            returnVal = true;
                        }
                        break;

                    }

                case VoxelOrientationPlane.Z_DOWN:
                    {
                        if (location > zPlaneDown)
                        {
                            zPlaneDown = location;
                            returnVal = true;
                        }
                        break;

                    }

                case VoxelOrientationPlane.FILL_VOXEL:
                    {
                        if (location > xPlaneUp)
                            xPlaneUp = location;
                        if (location > xPlaneDown)
                            xPlaneDown = location;

                        if (location > yPlaneUp)
                            yPlaneUp = location;
                        if (location > yPlaneDown)
                            yPlaneDown = location;

                        if (location > zPlaneUp)
                            zPlaneUp = location;
                        if (location > zPlaneDown)
                            zPlaneDown = location;

                        break;
                    }
            }

            return returnVal;
        }
    }
}