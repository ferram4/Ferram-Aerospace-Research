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
using System.Collections;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeometry
{
    unsafe class VoxelChunk
    {
        private Part[] voxelPoints = null;
        private DebugVisualVoxel[, ,] visualVoxels = null;

        double size;
        Vector3d lowerCorner;
        //int iOffset, jOffset, kOffset;
        int offset;

        public VoxelChunk(double size, Vector3d lowerCorner, int iOffset, int jOffset, int kOffset)
        {
            this.size = size;
            offset = iOffset + 8 * jOffset + 64 * kOffset;
            voxelPoints = new Part[512];
            this.lowerCorner = lowerCorner;
        }


        //Use when certian that locking is unnecessary
        public unsafe void SetVoxelPointGlobalIndexNoLock(int i, int j, int k, Part p)
        {
            voxelPoints[i + 8 * j + 64 * k - offset] =  p;          
        }
        //Sets point and ensures that includedParts includes p
        public unsafe void SetVoxelPointGlobalIndex(int i, int j, int k, Part p)
        {
            lock (voxelPoints)
            {
                voxelPoints[i + 8 * j + 64 * k - offset] = p;
            }
        }

        public unsafe bool VoxelPointExistsLocalIndex(int zeroBaseIndex)
        {
            return voxelPoints[zeroBaseIndex];
        }

        public unsafe bool VoxelPointExistsLocalIndex(int i, int j, int k)
        {
            return voxelPoints[i + 8 * j + 64 * k];
        }

        public unsafe bool VoxelPointExistsGlobalIndex(int zeroBaseIndex)
        {
            return voxelPoints[zeroBaseIndex - offset];
        }
        
        public unsafe bool VoxelPointExistsGlobalIndex(int i, int j, int k)
        {
            return voxelPoints[i + 8 * j + 64 * k - offset];
        }


        public unsafe Part GetVoxelPartGlobalIndex(int zeroBaseIndex)
        {
            Part p = null;
            p = voxelPoints[zeroBaseIndex - offset];
            return p;
        }
        
        public unsafe Part GetVoxelPartGlobalIndex(int i, int j, int k)
        {
            Part p = null;

            p = voxelPoints[i + 8 * j + 64 * k - offset];

            return p;
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
                        if ((object)voxelPoints[i + 8 * j + 64 * k] != null)
                        {
                            vx = new DebugVisualVoxel(vesselLocalToWorldMatrix.MultiplyPoint3x4(lowerCorner + new Vector3d(i, j, k) * size), size * 0.5f);
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

        ~VoxelChunk()
        {
            ClearVisualVoxels();
        }

    }
}
