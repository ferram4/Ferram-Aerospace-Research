using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FerramAerospaceResearch.FARGeometry
{
    public class FARGeometryLineSegment
    {
        public FARGeometryPoint point1;
        public FARGeometryPoint point2;

        public FARGeometryLineSegment(FARGeometryPoint pt1, FARGeometryPoint pt2)
        {
            point1 = pt1;
            point2 = pt2;
        }

        public FARGeometryPoint OppositePoint(FARGeometryPoint pt)
        {
            if (pt == point1)
                return point2;
            if (pt == point2)
                return point1;
            return null;
        }
    }
}
