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

        /// <summary>
        /// Determines where a test point is in relation to this line
        /// </summary>
        /// <param name="pt">test point</param>
        /// <returns>Returns 1 if pt is to left, -1 if pt is to right, and 0 if on line</returns>
        public int pointToLeft(FARGeometryPoint pt)
        {
            double val = (point2.point.x - point1.point.x) * (pt.point.y - point1.point.y) - (pt.point.x - point1.point.x) * (point2.point.y - point1.point.y);
            return val.CompareTo(0);
        }

        public void SetPoint1ToLeftMost()
        {
            int cmp = point1.CompareTo(point2);
            if (cmp > 1)
            {
                FARGeometryPoint tmp = point1;
                point1 = point2;
                point2 = tmp;
            }
        }
    }

    public class IsLeftComparer : IComparer<FARGeometryLineSegment>
    {
        public int Compare(FARGeometryLineSegment x, FARGeometryLineSegment y)
        {
            if(x.point1 == y.point1)
            {
                if (x.point2.y < y.point2.y)
                    return 1;
                else
                    return -1;
            }
            return x.pointToLeft(y.point2);
        }
    }
}
