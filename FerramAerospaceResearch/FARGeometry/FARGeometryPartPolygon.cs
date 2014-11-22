using System;
using System.Collections.Generic;
using UnityEngine;
using KSP;
using FerramAerospaceResearch.FARWing;

namespace FerramAerospaceResearch.FARGeometry
{
    public class FARGeometryPartPolygon
    {
        private List<FARGeometryLineSegment> planformBoundsLines;
        public List<FARGeometryLineSegment> PlanformBoundsLines
        {
            get { return planformBoundsLines; }
        }

        private Vector3d normVec;
        public Vector3d NormVec
        {
            get { return normVec; }
        }

        private Transform parentTransform;
        public Transform ParentTransform
        {
            get { return parentTransform; }
        }

        //Used for sorting parts into various planes
        public double normVecDot = 0;

        public FARGeometryPartPolygon(Part p)
        {
            normVec = p.transform.forward;
            parentTransform = p.transform;

            FARGeometryWingMeshCalculator wingGeoCalc = new FARGeometryWingMeshCalculator(p);
            planformBoundsLines = wingGeoCalc.CalculateWingPlanformPoints();
        }

        public List<Vector3d> GetPolyPointsAsVectors()
        {
            List<Vector3d> verts = new List<Vector3d>();
            for (int i = 0; i < planformBoundsLines.Count; i++)
                if (!verts.Contains(planformBoundsLines[i].point1.point))
                    verts.Add(planformBoundsLines[i].point1.point);
                else if (!verts.Contains(planformBoundsLines[i].point2.point))
                    verts.Add(planformBoundsLines[i].point2.point);

            return verts;
        }

        public void SetParentTransform(Transform t)
        {
            parentTransform = t;
            for (int i = 0; i < planformBoundsLines.Count; i++)
            {
                planformBoundsLines[i].point1.TransformToLocalSpace(t);
                planformBoundsLines[i].point2.TransformToLocalSpace(t);
            }
        }

        public bool PolygonContainsThisPoint(Vector3d testPoint, double verticalClearance)
        {
            if (Math.Abs(testPoint.z) > verticalClearance)   //if the point is too high above or below the polygon, it isn't in the polygon
                return false;

            int counter = 0;
            int i;
            double xinters;
            Vector3d p1, p2;
            List<FARGeometryPoint> planform = new List<FARGeometryPoint>();
            for (i = 0; i < planformBoundsLines.Count; i++)
                if (!planform.Contains(planformBoundsLines[i].point1))
                    planform.Add(planformBoundsLines[i].point1);
                else if (!planform.Contains(planformBoundsLines[i].point2))
                    planform.Add(planformBoundsLines[i].point2);

            p1 = planform[0].point;
            for (i = 1; i <= planform.Count; i++)
            {
                p2 = planform[i % planform.Count].point;
                if (testPoint.y > Math.Min(p1.y, p2.y))
                {
                    if (testPoint.y < Math.Max(p1.y, p2.y))
                    {
                        if (testPoint.x < Math.Max(p1.x, p2.x))
                        {
                            if (p1.y != p2.y)
                            {
                                xinters = (testPoint.y - p1.y) * (p2.x - p1.x) / (p2.y - p1.y) + p1.x;
                                if (p1.x == p2.x || testPoint.x < xinters)
                                    counter++;
                            }
                        }
                    }
                }
                p1 = p2;
            }

            if (counter % 2 == 0)
                return false;
            else
                return true;
        }

        /// <summary>
        /// Removes all line segments associated with this between the specified lines
        /// </summary>
        /// <param name="lineStart"></param>
        /// <param name="lineEnd"></param>
        /// <returns>New index associated with lineEnd</returns>
        public int LinesToRemove(FARGeometryLineSegment lineStart, FARGeometryLineSegment lineEnd)
        {
            int lineStartIndex = planformBoundsLines.IndexOf(lineStart);
            int lineEndIndex = planformBoundsLines.IndexOf(lineEnd);

            planformBoundsLines.RemoveRange(lineStartIndex + 1, lineEndIndex - (lineStartIndex + 1));

            return lineStartIndex + 1;
        }

        public class CompareNormVec : Comparer<FARGeometryPartPolygon>
        {
            public override int Compare(FARGeometryPartPolygon x, FARGeometryPartPolygon y)
            {
                if (x.normVecDot > y.normVecDot)
                    return 1;
                else
                    return -1;
            }
        }
    }
}
