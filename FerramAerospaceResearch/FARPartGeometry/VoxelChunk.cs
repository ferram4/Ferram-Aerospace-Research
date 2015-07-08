/*
Ferram Aerospace Research v0.15.3.1 "Garabedian"
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

namespace FerramAerospaceResearch.FARPartGeometry
{
    unsafe class VoxelChunk
    {
        //private Part[] voxelPoints = null;
        //private float[] voxelSize = null;
        private PartSizePair[] voxelPoints = null;
        private DebugVisualVoxel[, ,] visualVoxels = null;
        private HashSet<Part> overridingParts;

        double size;
        Vector3d lowerCorner;
        //int iOffset, jOffset, kOffset;
        int offset;

        public VoxelChunk(double size, Vector3d lowerCorner, int iOffset, int jOffset, int kOffset, HashSet<Part> overridingParts)
        {
            this.size = size;
            offset = iOffset + 8 * jOffset + 64 * kOffset;
            //voxelPoints = new Part[512];
            //voxelSize = new float[512];
            voxelPoints = new PartSizePair[512];
            for (int i = 0; i < voxelPoints.Length; i++)
                voxelPoints[i] = new PartSizePair();

            this.lowerCorner = lowerCorner;
            this.overridingParts = overridingParts;
        }

        public void SetChunk(double size, Vector3d lowerCorner, int iOffset, int jOffset, int kOffset, HashSet<Part> overridingParts)
        {
            this.size = size;
            offset = iOffset + 8 * jOffset + 64 * kOffset;
            this.lowerCorner = lowerCorner;
            this.overridingParts = overridingParts;
        }

        public void ClearChunk()
        {
            size = 0;
            offset = 0;
            lowerCorner = Vector3d.zero;
            for (int i = 0; i < voxelPoints.Length; i++)
                voxelPoints[i].Clear();

            overridingParts = null;
        }

        //Use when locking is unnecessary and only to change size, not part
        public unsafe void SetVoxelPointGlobalIndexNoLock(int zeroBaseIndex, VoxelOrientationPlane plane = VoxelOrientationPlane.FILL_VOXEL, byte location = 255)
        {
            zeroBaseIndex -= offset;
            //voxelPoints[zeroBaseIndex].part = p;
            voxelPoints[zeroBaseIndex].SetPlaneLocation(plane, location);
        }
        
        //Use when certian that locking is unnecessary
        public unsafe void SetVoxelPointGlobalIndexNoLock(int zeroBaseIndex, Part p, VoxelOrientationPlane plane = VoxelOrientationPlane.FILL_VOXEL, byte location = 255)
        {
            zeroBaseIndex -= offset;
            SetPart(p, zeroBaseIndex, plane, location);
        }

        public unsafe void SetVoxelPointGlobalIndexNoLock(int i, int j, int k, Part p, VoxelOrientationPlane plane = VoxelOrientationPlane.FILL_VOXEL, byte location = 255)
        {
            int index = i + 8 * j + 64 * k - offset;
            SetPart(p, index, plane, location);
        }
        //Sets point and ensures that includedParts includes p
        public unsafe void SetVoxelPointGlobalIndex(int zeroBaseIndex, Part p, VoxelOrientationPlane plane = VoxelOrientationPlane.FILL_VOXEL, byte location = 255)
        {
            lock (voxelPoints)
            {
                zeroBaseIndex -= offset;
                SetPart(p, zeroBaseIndex, plane, location);
            }
        }

        public unsafe void SetVoxelPointGlobalIndex(int i, int j, int k, Part p, VoxelOrientationPlane plane = VoxelOrientationPlane.FILL_VOXEL, byte location = 255)
        {
            lock (voxelPoints)
            {
                int index = i + 8 * j + 64 * k - offset;
                SetPart(p, index, plane, location);
            }
        }

        unsafe void SetPart(Part p, int index, VoxelOrientationPlane plane, byte location)
        {
            Part currentPart = voxelPoints[index].part;
            //if we update the plane location with this, then we can consider replacing the part here.  Otherwise, we don't
            bool largerThanLast = voxelPoints[index].SetPlaneLocation(plane, location);
            if ((object)currentPart == null || overridingParts.Contains(p) || (largerThanLast && !overridingParts.Contains(currentPart)))
                voxelPoints[index].part = p;

        }

        public unsafe bool VoxelPointExistsLocalIndex(int zeroBaseIndex)
        {
            return (object)(voxelPoints[zeroBaseIndex].part) != null;
        }

        public unsafe bool VoxelPointExistsLocalIndex(int i, int j, int k)
        {
            int index = i + 8 * j + 64 * k;
            return (object)(voxelPoints[index].part) != null;
        }

        public unsafe bool VoxelPointExistsGlobalIndex(int zeroBaseIndex)
        {
            return (object)(voxelPoints[zeroBaseIndex - offset].part) != null;
        }
        
        public unsafe bool VoxelPointExistsGlobalIndex(int i, int j, int k)
        {
            int index = i + 8 * j + 64 * k - offset;
            return (object)(voxelPoints[index].part) != null;
        }


        public unsafe Part GetVoxelPartGlobalIndex(int zeroBaseIndex)
        {
            Part p = null;
            int index = zeroBaseIndex - offset;
            p = voxelPoints[index].part;
            return p;
        }
        
        public unsafe Part GetVoxelPartGlobalIndex(int i, int j, int k)
        {
            Part p = null;

            int index = i + 8 * j + 64 * k - offset;
            p = voxelPoints[index].part;

            return p;
        }

        public unsafe PartSizePair GetVoxelPartSizePairGlobalIndex(int zeroBaseIndex)
        {
            int index = zeroBaseIndex - offset;
            return voxelPoints[index];
        }
        
        public unsafe PartSizePair GetVoxelPartSizePairGlobalIndex(int i, int j, int k)
        {
            int index = i + 8 * j + 64 * k - offset;

            return voxelPoints[index];
        }
        
        public void VisualizeVoxels(Matrix4x4 vesselLocalToWorldMatrix)
        {
            ClearVisualVoxels();
            visualVoxels = new DebugVisualVoxel[8, 8, 8];
            for(int i = 0; i < 8; i++)
                for(int j = 0; j < 8; j++)
                    for(int k = 0; k < 8; k++)
                    {
                        DebugVisualVoxel vx;
                        //if(voxelPoints[i,j,k] != null)
                        PartSizePair pair = voxelPoints[i + 8 * j + 64 * k];
                        if ((object)pair.part != null)
                        {
                            double elementSize = pair.GetSize();
                            if (elementSize > 1)
                                elementSize = 1;

                            elementSize *= size * 0.5f;
                            vx = new DebugVisualVoxel(vesselLocalToWorldMatrix.MultiplyPoint3x4(lowerCorner + new Vector3d(i, j, k) * size), elementSize);
                            visualVoxels[i, j, k] = vx;
                        }
                    }
        }

        public void ClearVisualVoxels()
        {
            if (visualVoxels != null)
                for (int i = 0; i < 8; i++)
                    for (int j = 0; j < 8; j++)
                        for (int k = 0; k < 8; k++)
                        {
                            DebugVisualVoxel vx = visualVoxels[i, j, k];
                            if (vx != null)
                                GameObject.Destroy(vx.gameObject);
                            vx = null;
                        }
        }

        //~VoxelChunk()
        //{
        //    ClearVisualVoxels();
        //}

        public class PartSizePair
        {
            const float AREA_SCALING = 1f / (255f * 255f * 255f);
            const int LENGTH_OF_VOXEL = 255;
            //const byte UP_MASK = 15;
            //const byte DOWN_MASK = 240;

            public Part part;

            byte xPlaneUp, yPlaneUp, zPlaneUp;
            byte xPlaneDown, yPlaneDown, zPlaneDown;

            public void Clear()
            {
                xPlaneUp = yPlaneUp = zPlaneUp = 0;
                xPlaneDown = yPlaneDown = zPlaneDown = 0;
                part = null;
            }

            public float GetSize()
            {
                int x, y, z;

                x = xPlaneUp + xPlaneDown;
                y = yPlaneUp + yPlaneDown;
                z = zPlaneUp + zPlaneDown;

                if (x > LENGTH_OF_VOXEL)
                    x -= LENGTH_OF_VOXEL;

                if (y > LENGTH_OF_VOXEL)
                    y -= LENGTH_OF_VOXEL;

                if (z > LENGTH_OF_VOXEL)
                    z -= LENGTH_OF_VOXEL;

                if (x == 0 && y == 0 & z == 0)      //if they're all 0, this is 0
                    return 0f;

                //If a plane actually passes through this, that means that any values that are 0 indicate no planes cutting through this, and thus, that they should fill that dimension
                if (x == 0)
                    x = LENGTH_OF_VOXEL;

                if (y == 0)
                    y = LENGTH_OF_VOXEL;

                if (z == 0)
                    z = LENGTH_OF_VOXEL;


                float size = x * y * z;     //so then calc the volume
                size *= AREA_SCALING;       //scale for the 0-15 scaling used for the plane locations

                return size;
            }

            public void SetFilledSides(VoxelOrientationPlane filledPlanes)
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
            public bool SetPlaneLocation(VoxelOrientationPlane plane, byte location)
            {
                bool returnVal = false;
                switch (plane)
                {
                    case VoxelOrientationPlane.X_UP:
                        if (location >= xPlaneUp)
                        {
                            xPlaneUp = location;
                            returnVal = true;
                        }
                        break;

                    case VoxelOrientationPlane.X_DOWN:
                        if (location >= xPlaneDown)
                        {
                            xPlaneDown = location;
                            returnVal = true;
                        }
                        break;

                    case VoxelOrientationPlane.Y_UP:
                        if (location >= yPlaneUp)
                        {
                            yPlaneUp = location;
                            returnVal = true;
                        }
                        break;

                    case VoxelOrientationPlane.Y_DOWN:
                        if (location >= yPlaneDown)
                        {
                            yPlaneDown = location;
                            returnVal = true;
                        }
                        break;

                    case VoxelOrientationPlane.Z_UP:
                        if (location >= zPlaneUp)
                        {
                            zPlaneUp = location;
                            returnVal = true;
                        }
                        break;

                    case VoxelOrientationPlane.Z_DOWN:
                        if (location >= zPlaneDown)
                        {
                            zPlaneDown = location;
                            returnVal = true;
                        }
                        break;

                    case VoxelOrientationPlane.FILL_VOXEL:
                        xPlaneUp = location;
                        yPlaneUp = location;
                        zPlaneUp = location;

                        xPlaneDown = location;
                        yPlaneDown = location;
                        zPlaneDown = location;
                        break;
                }

                return returnVal;
            }
        }
    }

    [Flags]
    enum VoxelOrientationPlane
    {
        NONE = 0,
        X_UP = 1,
        X_DOWN = 2,
        Y_UP = 4,
        Y_DOWN = 8,
        Z_UP = 16,
        Z_DOWN = 32,
        FILL_VOXEL = 64
    }
}
