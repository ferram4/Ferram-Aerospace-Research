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
    public class PartSizePair4Bit : PartSizePair
    {
        const float AREA_SCALING = 1f / (15f * 15f * 15f);
        const int LENGTH_OF_VOXEL = 15;
        const byte UP_MASK = 0xF0;
        const byte DOWN_MASK = 0x0F;

        byte xPlane, yPlane, zPlane;
        //byte xPlaneDown, yPlaneDown, zPlaneDown;

        public PartSizePair4Bit() { }

        public override void Clear()
        {
            xPlane = yPlane = zPlane = 0;
            part = null;
        }

        public override float GetSize()
        {
            int x, y, z;

            x = (xPlane & DOWN_MASK) + (xPlane >> 4);
            y = (yPlane & DOWN_MASK) + (yPlane >> 4);
            z = (zPlane & DOWN_MASK) + (zPlane >> 4);

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
                xPlane |= UP_MASK;

            if ((filledPlanes & VoxelOrientationPlane.X_DOWN) == VoxelOrientationPlane.X_DOWN)
                xPlane |= DOWN_MASK;

            if ((filledPlanes & VoxelOrientationPlane.Y_UP) == VoxelOrientationPlane.Y_UP)
                yPlane |= UP_MASK;

            if ((filledPlanes & VoxelOrientationPlane.Y_DOWN) == VoxelOrientationPlane.Y_DOWN)
                yPlane |= DOWN_MASK;

            if ((filledPlanes & VoxelOrientationPlane.Z_UP) == VoxelOrientationPlane.Z_UP)
                zPlane |= UP_MASK;

            if ((filledPlanes & VoxelOrientationPlane.Z_DOWN) == VoxelOrientationPlane.Z_DOWN)
                zPlane |= DOWN_MASK;
        }

        //Will return true if the location is updated
        public override bool SetPlaneLocation(VoxelOrientationPlane plane, byte location)
        {
            bool returnVal = false;

            switch (plane)
            {
                case VoxelOrientationPlane.X_UP:
                    {
                        location = (byte)(location << 4);   //shift the byte as necessary
                        if (location > (xPlane & UP_MASK))     //only compare upper bits
                        {
                            xPlane &= DOWN_MASK;     //clear out all 4 upper bits but keep lower bits
                            xPlane |= location; //then overwrite them from location
                            returnVal = true;
                        }
                        break;
                    }

                case VoxelOrientationPlane.X_DOWN:
                    {
                        if (location > (xPlane & DOWN_MASK))
                        {
                            xPlane &= UP_MASK;     //clear out all 4 lower bits
                            xPlane |= location;
                            returnVal = true;
                        }
                        break;
                    }

                case VoxelOrientationPlane.Y_UP:
                    {
                        location = (byte)(location << 4);   //shift the byte as necessary
                        if (location > (yPlane & UP_MASK))
                        {
                            yPlane &= DOWN_MASK;     //clear out all 4 upper bits
                            yPlane |= location; //then overwrite them from location
                            returnVal = true;
                        }
                        break;
                    }

                case VoxelOrientationPlane.Y_DOWN:
                    {
                        if (location > (yPlane & DOWN_MASK))
                        {
                            yPlane &= UP_MASK;     //clear out all 4 lower bits
                            yPlane |= location;
                            returnVal = true;
                        }
                        break;
                    }

                case VoxelOrientationPlane.Z_UP:
                    {
                        location = (byte)(location << 4);   //shift the byte as necessary
                        if (location > (zPlane & UP_MASK))
                        {
                            zPlane &= DOWN_MASK;     //clear out all 4 upper bits
                            zPlane |= location; //then overwrite them from location
                            returnVal = true;
                        }
                        break;
                    }

                case VoxelOrientationPlane.Z_DOWN:
                    {
                        if (location > (zPlane & DOWN_MASK))
                        {
                            zPlane &= UP_MASK;     //clear out all 4 lower bits
                            zPlane |= location;
                            returnVal = true;
                        }
                        break;
                    }

                case VoxelOrientationPlane.FILL_VOXEL:
                    {
                        if (location > (xPlane & DOWN_MASK))
                        {
                            xPlane &= UP_MASK;     //clear out all 4 lower bits
                            xPlane |= location;
                        }
                        if (location > (yPlane & DOWN_MASK))
                        {
                            yPlane &= UP_MASK;     //clear out all 4 lower bits
                            yPlane |= location;
                        }
                        if (location > (zPlane & DOWN_MASK))
                        {
                            zPlane &= UP_MASK;     //clear out all 4 lower bits
                            zPlane |= location;
                        }
                        location = (byte)(location << 4);   //shift the byte as necessary

                        if (location > (xPlane & UP_MASK))
                        {
                            xPlane &= DOWN_MASK;     //clear out all 4 upper bits
                            xPlane |= location; //then overwrite them from location
                        }
                        if (location > (yPlane & UP_MASK))
                        {
                            yPlane &= DOWN_MASK;     //clear out all 4 upper bits
                            yPlane |= location; //then overwrite them from location
                        }
                        if (location > (zPlane & UP_MASK))
                        {
                            zPlane &= DOWN_MASK;     //clear out all 4 upper bits
                            zPlane |= location; //then overwrite them from location
                        }
                        break;
                    }
            }

            return returnVal;
        }
    }
}
