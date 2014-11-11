using System;
using System.Collections.Generic;
using KSP;
using FerramAerospaceResearch.FARWing;

namespace FerramAerospaceResearch.FARGeometry
{
    public class FARGeometryPartPolygon
    {
        private FARWingPartModule module;
        public FARWingPartModule WingModule
        {
            get { return module; }
        }
        private List<FARGeometryPoint> planformBoundsPoints;
        public List<FARGeometryPoint> PlanformBoundsPoints
        {
            get { return planformBoundsPoints; }
        }

        private List<Vector3d> planformTestPoints;
        public List<Vector3d> PlanformTestPoints
        {
            get { return planformTestPoints; }
        }

        private Vector3d centroid = new Vector3d();
        public Vector3d Centroid
        {
            get { return centroid; }
        }

        private Vector3d normVec;
        public Vector3d NormVec
        {
            get { return normVec; }
        }

        private double area;
        public double Area
        {
            get { return area; }
        }

        //Used for sorting parts into various planes
        public double normVecDot = 0;

        public FARGeometryPartPolygon(FARWingPartModule wingModule)
        {
            module = wingModule;
            normVec = wingModule.transform.forward;

            FARGeometryWingMeshCalculator wingGeoCalc = new FARGeometryWingMeshCalculator(wingModule.part);
            planformBoundsPoints = wingGeoCalc.CalculateWingPlanformPoints();

            planformTestPoints = new List<Vector3d>();
            area = 0;

            //Create the test points for finding nearby (but non-intersecting) polygons and calculate the area
            for(int i = 0; i < PlanformBoundsPoints.Count; i++)
            {
                int ip1 = i + 1;
                if (ip1 == PlanformBoundsPoints.Count)  //if the index is out of bounds, wrap around to the first point
                    ip1 = 0;

                Vector3d pt1 = PlanformBoundsPoints[i].point;
                Vector3d pt2 = PlanformBoundsPoints[ip1].point;

                Vector3d avg = (pt1 + pt2) * 0.5;   //position halfway down the edge

                Vector3d offsetVec = pt2 - pt1;     //first, get a vector from pt1 to pt2
                offsetVec.Normalize();              //normalize the vector

                //Since the points are in CCW order, we must turn this vector 90 degrees clockwise in order to get a line pointing out of the polygon

                offsetVec.z = -offsetVec.x;         //shift the -x value over to z temporarily
                offsetVec.x = offsetVec.y;          
                offsetVec.y = offsetVec.z;
                offsetVec.z = 0;                    //set z to 0, and now the vector has been turned 90 degrees clockwise

                offsetVec *= 0.15;                  //Point shall be 0.15 m away from the line

                planformTestPoints.Add(avg + offsetVec);    //and add the test point

                area += pt1.x * pt2.y - pt2.x * pt1.y;

                centroid += pt1;
            }
            area *= 0.5;    //And finish calculating the area
            centroid /= PlanformBoundsPoints.Count;
        }

        public List<Vector3d> GetPolyPointsAsVectors()
        {
            List<Vector3d> verts = new List<Vector3d>();
            for (int i = 0; i < planformBoundsPoints.Count; i++)
                verts.Add(planformBoundsPoints[i].point);

            return verts;
        }

        private bool PolygonContainsThisPoint(Vector3d testPoint, double verticalClearance)
        {
            if (Math.Abs(testPoint.z) > verticalClearance)   //if the point is too high above or below the polygon, it isn't in the polygon
                return false;

            int counter = 0;
            int i;
            double xinters;
            Vector3d p1, p2;
            List<FARGeometryPoint> planform = PlanformBoundsPoints;

            p1 = planform[0].point;
            for (i = 1; i <= planform.Count; i++)
            {
                p2 = planform[i % planform.Count].point;
                if (testPoint.y > Math.Min(p1.y, p2.y))
                {
                    if (testPoint.y <= Math.Max(p1.y, p2.y))
                    {
                        if (testPoint.x <= Math.Max(p1.x, p2.x))
                        {
                            if (p1.y != p2.y)
                            {
                                xinters = (testPoint.y - p1.y) * (p2.x - p1.x) / (p2.y - p1.y) + p1.x;
                                if (p1.x == p2.x || testPoint.x <= xinters)
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
