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
        public float area_deriv1;   //first derivative of area
        public float area_deriv2;   //second derivative of area
        //public Dictionary<Part, int> partsRepresented = new Dictionary<Part, int>();
    }
}
