using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeoUtil
{
    struct PartCrossSection : IComparable<PartCrossSection>
    {
        public Vector2d centroid;
        public double area;
        public double station;
        public double radius;

        public int CompareTo(PartCrossSection other)
        {
            return this.station.CompareTo(other.station);
        }
    }
}
