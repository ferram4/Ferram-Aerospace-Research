using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeometry
{
    public struct VoxelCrossSection
    {
        public float area;
        public Vector3 centroid;
        public float a;
        public float b;
        //public Dictionary<Part, int> partsRepresented = new Dictionary<Part, int>();
    }
}
