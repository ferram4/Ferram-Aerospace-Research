using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeoUtil
{
    class VehicleVoxel
    {
        float elementSize;
        VoxelSection[, ,] voxelSections;


        VehicleVoxel(List<Part> partList, Transform basis, float elementSize)
        {
            this.elementSize = elementSize;

            for(int i = 0; i < partList.Count; i++)
            {
                Part p = partList[i];
                p.GetPartOverallMeshBoundsInBasis(basis);
            }
        }
    }
}
