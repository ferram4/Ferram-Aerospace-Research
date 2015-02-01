using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeometry
{
    class VoxelSection
    {
        public Part[, ,] voxelPoints = null;
        public HashSet<Part> includedParts = new HashSet<Part>();

        float size;

        int xLength, yLength, zLength;

        public VoxelSection(float size, int xLength, int yLength, int zLength)
        {
            this.size = size;
            this.xLength = xLength;
            this.yLength = yLength;
            this.zLength = zLength;
            voxelPoints = new Part[xLength, yLength, zLength];
        }

        //Sets point and ensures that includedParts includes p
        public void SetVoxelPoint(int i, int j, int k, Part p)
        {
            voxelPoints[i, j, k] = p;
            if (!includedParts.Contains(p))
                includedParts.Add(p);
        }
    }
}
