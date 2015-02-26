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
        public float areaDeriv2ToNextSection;   //second derivative of area, which is constant between sections; this is the value between this and the next section
        public HashSet<Part> includedParts;

        public float additionalUnshadowedArea;        //area added to this crosssection that has no area ahead of it
        public Vector3 additonalUnshadowedCentroid;     //centroid of unshadowedArea

        public float removedArea;               //area removed from this particular crosssection, compared to the one in front of it
        public Vector3 removedCentroid;          //centroid of removedArea
    }
}
