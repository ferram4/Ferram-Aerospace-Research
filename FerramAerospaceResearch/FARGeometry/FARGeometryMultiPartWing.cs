using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FerramAerospaceResearch.FARGeometry
{
    public class FARGeometryMultiPartWing
    {
        public FARGeometryPartPolygon rootPoly;

        public List<FARGeometryPoint> points;

        public void ExpandWing(List<FARGeometryPartPolygon> polys)
        {
            rootPoly = polys[0];

            for (int i = 0; i < polys.Count; i++)
                polys[i].normVecDot = Math.Abs(Vector3d.Dot(polys[i].NormVec, rootPoly.NormVec));

            polys.Sort(new FARGeometryPartPolygon.CompareNormVec());

            List<FARGeometryPartPolygon> outOfPlanePolys = new List<FARGeometryPartPolygon>();

            int indexOfInPlanePolys = 0;

            for (indexOfInPlanePolys = 0; indexOfInPlanePolys < polys.Count; indexOfInPlanePolys++)
                if (polys[indexOfInPlanePolys].normVecDot < 0.999)
                    outOfPlanePolys.Add(polys[indexOfInPlanePolys]);
                else
                    break;

            polys.RemoveRange(0, indexOfInPlanePolys);
        }
    }
}
