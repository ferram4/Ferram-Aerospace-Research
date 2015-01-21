using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FerramAerospaceResearch.FARPartGeoUtil
{
    class CrossSectionEvent : IComparable<CrossSectionEvent>
    {
        public Line line;
        public float point;
        public bool crossSectionCut;

        public int CompareTo(CrossSectionEvent other)
        {
            if (this.point == other.point)
                return 0;
            if (this.point < other.point)
                return -1;
            return 1;
        }
    }
}
