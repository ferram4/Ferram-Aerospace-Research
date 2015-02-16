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
        public float deltaAreaDeriv1;   //finite change in first derivative of area at this section, where areaDeriv2 is discontinuous
        public float areaDeriv2ToNextSection;   //second derivative of area, which is constant between sections; this is the value between this and the next section
        public HashSet<Part> includedParts;
    }
}
