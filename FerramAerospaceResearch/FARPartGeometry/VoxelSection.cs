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

namespace FerramAerospaceResearch.FARPartGeometry
{
    unsafe class VoxelSection
    {
        //private Part[, ,] voxelPoints = null;
        private byte[,] voxelPoints = null;
        private DebugVisualVoxel[, ,] visualVoxels = null;
        public HashSet<Part> includedParts = new HashSet<Part>();
        private Part firstPart = null;

        float size;
        Vector3 lowerCorner;
        int iOffset, jOffset, kOffset;
        byte xLength, yLength, zLength;

        public VoxelSection(float size, Vector3 lowerCorner, int iOffset, int jOffset, int kOffset)
        {
            this.size = size;
            this.xLength = 8;
            this.yLength = 8;
            this.zLength = 8;
            this.iOffset = iOffset;
            this.jOffset = jOffset;
            this.kOffset = kOffset;
            //voxelPoints = new Part[xLength, yLength, zLength];
            voxelPoints = new byte[xLength, yLength];
            this.lowerCorner = lowerCorner;
        }

        //Use when certian that locking is unnecessary
        public unsafe void SetVoxelPointGlobalIndexNoLock(int i, int j, int k, Part p)
        {

            //voxelPoints[i, j, k] = p;
            voxelPoints[i - iOffset, j - jOffset] |= (byte)(1 << (k - kOffset));
            if (!includedParts.Contains(p))
                includedParts.Add(p);
            if (firstPart == null)
                firstPart = p;
            
        }
        //Sets point and ensures that includedParts includes p
        public unsafe void SetVoxelPointGlobalIndex(int i, int j, int k, Part p)
        {
            lock (voxelPoints)
            {
                //voxelPoints[i, j, k] = p;
                voxelPoints[i - iOffset, j - jOffset] |= (byte)(1 << (k - kOffset));
                if (!includedParts.Contains(p))
                    includedParts.Add(p);
                if (firstPart == null)
                    firstPart = p;
            }
        }

        public unsafe Part GetVoxelPointGlobalIndex(int i, int j, int k)
        {
            Part p = null;
            //lock (voxelPoints)      //Locks are not needed because reading and writing are not done in different threads simultaneously
            //{
                //p = voxelPoints[i, j, k];
                if ((voxelPoints[i - iOffset, j - jOffset] & (1 << (k - kOffset))) != 0)
                    p = firstPart;
            //}
            return p;
        }
        
        //Sets point and ensures that includedParts includes p
        public unsafe void SetVoxelPointLocalIndex(int i, int j, int k, Part p)
        {
            lock (voxelPoints)
            {
                //voxelPoints[i, j, k] = p;
                voxelPoints[i, j] |= (byte)(1 << k);
                if (!includedParts.Contains(p))
                    includedParts.Add(p);
                if (firstPart == null)
                    firstPart = p;
            }
        }

        public unsafe Part GetVoxelPointLocalIndex(int i, int j, int k)
        {
            Part p = null;
            lock (voxelPoints)
            {
                //p = voxelPoints[i, j, k];
                if ((voxelPoints[i, j] & (1 << k)) != 0)
                    p = firstPart;
            }
            return p;
        }

        public void VisualizeVoxels(Vector3 vesselOffset)
        {
            ClearVisualVoxels();
            visualVoxels = new DebugVisualVoxel[xLength, yLength, zLength];
            for(int i = 0; i < xLength; i++)
                for(int j = 0; j < yLength; j++)
                    for(int k = 0; k < zLength; k++)
                    {
                        DebugVisualVoxel vx;
                        //if(voxelPoints[i,j,k] != null)
                        if ((voxelPoints[i, j] & (1 << k)) != 0)
                        {
                            vx = new DebugVisualVoxel(lowerCorner + new Vector3(i, j, k) * size + vesselOffset, size * 0.5f);
                            visualVoxels[i, j, k] = vx;
                        }
                    }
        }

        public void ClearVisualVoxels()
        {
            if (visualVoxels != null)
                for (int i = 0; i < xLength; i++)
                    for (int j = 0; j < yLength; j++)
                        for (int k = 0; k < zLength; k++)
                        {
                            DebugVisualVoxel vx = visualVoxels[i, j, k];
                            if (vx != null)
                                GameObject.Destroy(vx.gameObject);
                            vx = null;
                        }
        }

        ~VoxelSection()
        {
            ClearVisualVoxels();
        }
    }
}
