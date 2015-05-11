/*
Ferram Aerospace Research v0.15.1 "Fanno"
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

        double size;
        Vector3d lowerCorner;
        //int iOffset, jOffset, kOffset;
        int offset;

        public VoxelChunk(double size, Vector3d lowerCorner, int iOffset, int jOffset, int kOffset)
        {
            this.size = size;
            offset = iOffset + 8 * jOffset + 64 * kOffset;
            //voxelPoints = new Part[512];
            //voxelSize = new float[512];
            voxelPoints = new PartSizePair[512];
            this.lowerCorner = lowerCorner;
        }

        public void SetChunk(double size, Vector3d lowerCorner, int iOffset, int jOffset, int kOffset)
        {
            this.size = size;
            offset = iOffset + 8 * jOffset + 64 * kOffset;
            this.lowerCorner = lowerCorner;
        }

        public void ClearChunk()
        {
            size = 0;
            offset = 0;
            lowerCorner = Vector3d.zero;
            for (int i = 0; i < voxelPoints.Length; i++)
            {
                voxelPoints[i].part = null;
                voxelPoints[i].SetPlaneLocation(VoxelOrientationPlane.FILL_VOXEL, 0);
            }
        }

        //Use when locking is unnecessary and only to change size, not part
        public unsafe void SetVoxelPointGlobalIndexNoLock(int zeroBaseIndex, VoxelOrientationPlane plane = VoxelOrientationPlane.FILL_VOXEL, byte location = 15)
        {
            zeroBaseIndex -= offset;
            //voxelPoints[zeroBaseIndex].part = p;
            voxelPoints[zeroBaseIndex].SetPlaneLocation(plane, location);
        }
        
        //Use when certian that locking is unnecessary
        public unsafe void SetVoxelPointGlobalIndexNoLock(int zeroBaseIndex, Part p, VoxelOrientationPlane plane = VoxelOrientationPlane.FILL_VOXEL, byte location = 15)
        {
            zeroBaseIndex -= offset;
            voxelPoints[zeroBaseIndex].part = p;
            voxelPoints[zeroBaseIndex].SetPlaneLocation(plane, location);
        }

        public unsafe void SetVoxelPointGlobalIndexNoLock(int i, int j, int k, Part p, VoxelOrientationPlane plane = VoxelOrientationPlane.FILL_VOXEL, byte location = 15)
        {
            int index = i + 8 * j + 64 * k - offset;
            voxelPoints[index].part = p;
            voxelPoints[index].SetPlaneLocation(plane, location);
        }
        //Sets point and ensures that includedParts includes p
        public unsafe void SetVoxelPointGlobalIndex(int zeroBaseIndex, Part p, VoxelOrientationPlane plane = VoxelOrientationPlane.FILL_VOXEL, byte location = 15)
        {
            lock (voxelPoints)
            {
                zeroBaseIndex -= offset;
                voxelPoints[zeroBaseIndex].part = p;
                voxelPoints[zeroBaseIndex].SetPlaneLocation(plane, location);
            }
        }

        public unsafe void SetVoxelPointGlobalIndex(int i, int j, int k, Part p, VoxelOrientationPlane plane = VoxelOrientationPlane.FILL_VOXEL, byte location = 15)
        {
            lock (voxelPoints)
            {
                int index = i + 8 * j + 64 * k - offset;
                voxelPoints[index].part = p;
                voxelPoints[index].SetPlaneLocation(plane, location);
            }
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

        public struct PartSizePair
        {
            const float AREA_SCALING = 1f / (15f * 15f * 15f);
            const byte UP_MASK = 15;
            const byte DOWN_MASK = 240;

            public Part part;

            byte xPlane, yPlane, zPlane;

            public float GetSize()
            {
                int x, y, z;
                int xh, yh, zh;
                int xl, yl, zl;

                xh = xPlane & UP_MASK;      //convert to proper distances
                yh = yPlane & UP_MASK;
                zh = zPlane & UP_MASK;

                xl = (xPlane & DOWN_MASK) >> 4;
                yl = (yPlane & DOWN_MASK) >> 4;
                zl = (zPlane & DOWN_MASK) >> 4;

                x = xh + xl;
                y = yh + yl;
                z = zh + zl;

                if (x > 15)
                    x -= 15;

                if (y > 15)
                    y -= 15;

                if (z > 15)
                    z -= 15;

                if (x == 0 && y == 0 & z == 0)      //if they're all 0, this is 0
                    return 0f;

                //If a plane actually passes through this, that means that any values that are 0 indicate no planes cutting through this, and thus, that they should fill that dimension
                if (x == 0)
                    x = 15;

                if (y == 0)
                    y = 15;

                if (z == 0)
                    z = 15;


                float size = x * y * z;     //so then calc the volume
                size *= AREA_SCALING;       //scale for the 0-15 scaling used for the plane locations

                return size;
            }

            public void SetPlaneLocation(VoxelOrientationPlane plane, byte location)
            {
                int curVal, nextVal;
                switch(plane)
                {
                    case VoxelOrientationPlane.X_UP:
                        curVal = xPlane & UP_MASK;
                        if (location > curVal)
                            xPlane |= location;
                        break;

                    case VoxelOrientationPlane.X_DOWN:
                        curVal = xPlane & DOWN_MASK;
                        nextVal = location << 4;
                        if (nextVal > curVal)
                            xPlane |= (byte)nextVal;
                        break;

                    case VoxelOrientationPlane.Y_UP:
                        curVal = yPlane & UP_MASK;
                        if (location > curVal)
                            yPlane |= location;
                        break;

                    case VoxelOrientationPlane.Y_DOWN:
                        curVal = yPlane & DOWN_MASK;
                        nextVal = location << 4;
                        if (nextVal > curVal)
                            yPlane |= (byte)nextVal;
                        break;

                    case VoxelOrientationPlane.Z_UP:
                        curVal = zPlane & UP_MASK;
                        if (location > curVal)
                            zPlane |= location;
                        break;

                    case VoxelOrientationPlane.Z_DOWN:
                        curVal = zPlane & DOWN_MASK;
                        nextVal = location << 4;
                        if (nextVal > curVal)
                            zPlane |= (byte)nextVal;
                        break;

                    case VoxelOrientationPlane.FILL_VOXEL:
                        xPlane = location;
                        yPlane = location;
                        zPlane = location;
                        break;
                }
            }
        }
    }

    enum VoxelOrientationPlane
    {
        X_UP,
        X_DOWN,
        Y_UP,
        Y_DOWN,
        Z_UP,
        Z_DOWN,
        FILL_VOXEL
    }
}
