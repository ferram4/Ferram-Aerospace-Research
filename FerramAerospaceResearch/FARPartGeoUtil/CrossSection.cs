using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeoUtil
{
    struct CrossSection : IComparable<CrossSection>
    {
        public Vector2d centroid;
        public double area;
        public double station;
        public double radius;

        public int CompareTo(CrossSection other)
        {
            return this.station.CompareTo(other.station);
        }
    }
}
