using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FerramAerospaceResearch.FARCollections;

namespace FerramAerospaceResearch.FARGeometry
{
    public class BentleyOttmann
    {
        private LLRedBlackTree<FARGeometryLineSegment> sweepLine;
        private BentleyOttmannEventQueue eventQueue;

        private List<Intersection> finalIntersections;
        private Dictionary<FARGeometryLineSegment, List<Intersection>> lineIntersections;

        class Intersection : IEquatable<Intersection>
        {
            public Intersection(FARGeometryPoint pt, FARGeometryLineSegment line1, FARGeometryLineSegment line2)
            {
                point = pt;
                this.line1 = line1;
                this.line2 = line2;
            }
            public FARGeometryPoint point;
            public FARGeometryLineSegment line1;
            public FARGeometryLineSegment line2;

            public bool Equals(Intersection otherIntersect)
            {
                if(this.line1 == otherIntersect.line1)
                {
                    return this.line2 == otherIntersect.line2;
                }
                else if (this.line1 == otherIntersect.line2)
                {
                    return this.line2 == otherIntersect.line1;
                }
                return false;
            }
        }

        /// <summary>
        /// This merges two polygons using the Bentley-Ottmann Algorithm
        /// </summary>
        public FARGeometryPartPolygon MergePolygons(FARGeometryPartPolygon poly1, FARGeometryPartPolygon poly2)
        {
            poly2.SetParentTransform(poly1.ParentTransform);        //We do this to maintain points in the same coordinate system

            List<FARGeometryLineSegment> lines = new List<FARGeometryLineSegment>(poly1.PlanformBoundsLines);
            lines.AddRange(poly2.PlanformBoundsLines);

            eventQueue = new BentleyOttmannEventQueue(lines);

            sweepLine = new LLRedBlackTree<FARGeometryLineSegment>(new IsLeftComparer());

            finalIntersections = new List<Intersection>();
            lineIntersections = new Dictionary<FARGeometryLineSegment, List<Intersection>>();

            while(eventQueue.Count > 0)
            {
                BentleyOttmannEventQueue.Event newEvent = eventQueue.GetNextEvent();

                if(newEvent is BentleyOttmannEventQueue.LineEndPointEvent)
                {
                    ProcessLineEndPointEvent((BentleyOttmannEventQueue.LineEndPointEvent)newEvent);
                }
                else
                {       //An event that is not a LineEndPointEvent must be an Intersect Event
                    ProcessIntersectEvent((BentleyOttmannEventQueue.IntersectionEvent)newEvent);
                }
            }
            for (int i = 0; i < finalIntersections.Count; i++)
            {
                AdjustLinesAfterCalculatingIntersections(poly1, poly2, finalIntersections[i]);
            }
            FARGeometryPartPolygon returnPoly = new FARGeometryPartPolygon(poly1);
            return returnPoly;
        }

        private void AdjustLinesAfterCalculatingIntersections(FARGeometryPartPolygon poly1, FARGeometryPartPolygon poly2, Intersection intersect)
        {
            FARGeometryLineSegment line1 = intersect.line1;
            FARGeometryLineSegment line2 = intersect.line2;

            SplitLine(line1, intersect);
            SplitLine(line2, intersect);
        }

        private void SplitLine(FARGeometryLineSegment line, Intersection intersect)
        {
            List<Intersection> lineIntersects = lineIntersections[line];          //Get all the intersections for this line
            FARGeometryLineSegment lineRight = new FARGeometryLineSegment(intersect.point, line.point2);  //Split the line by creating a new one that extends from the intersect to point 2 (the rightmost point) of the original line
            line.point2 = intersect.point;     //And then set the rightmost point of the original line to the intersect point

            lineIntersects.Remove(intersect);      //Get rid of this intersect item, since we don't care about it anymore

            List<Intersection> lineRightIntersects = new List<Intersection>();

            for (int i = 0; i < lineIntersects.Count; i++)
            {
                if (lineIntersects[i].point.CompareTo(intersect.point) > 0)        //If this intersect point is to the right of the intersect we care about here, it belongs to the other line
                    lineRightIntersects.Add(lineIntersects[i]);
            }
            for (int i = 0; i < lineRightIntersects.Count; i++)
            {
                Intersection item = lineRightIntersects[i];
                lineIntersects.Remove(item);        //Remove any intersects that shouldn't be there
                if (item.line1 == line)
                    item.line1 = lineRight;        //And change the line reference that the intersection points to
                else
                    item.line2 = lineRight;
            }
        }

        private void DetermineLineInsideOrOutsidePoly(FARGeometryLineSegment line, FARGeometryPartPolygon poly1, FARGeometryPartPolygon poly2)
        {
            /*if (poly1.PlanformBoundsLines.Contains(line1))
            {
                if (poly1.PolygonContainsThisPoint(line2.point1.point, 0.1))        //point 2 is outside the other poly, but point 1 is inside it; replace point 1 with the intersection
                {
                    if (!poly1.PolygonContainsThisPoint(line2.point2.point, 0.1))
                        line2.point1 = intersect.point;
                }
                else if (poly1.PolygonContainsThisPoint(line2.point2.point, 0.1))   //And now, the other way around...
                {
                    if (!poly1.PolygonContainsThisPoint(line2.point1.point, 0.1))
                        line2.point2 = intersect.point;
                }

                if (poly2.PolygonContainsThisPoint(line1.point1.point, 0.1))        //point 2 is outside the other poly, but point 1 is inside it; replace point 1 with the intersection
                {
                    if (!poly2.PolygonContainsThisPoint(line1.point2.point, 0.1))
                        line1.point1 = intersect.point;
                }
                else if (poly2.PolygonContainsThisPoint(line1.point2.point, 0.1))
                {
                    if (!poly2.PolygonContainsThisPoint(line1.point1.point, 0.1))
                        line1.point2 = intersect.point;
                }
            }*/
        }

        private void ProcessIntersectEvent(BentleyOttmannEventQueue.IntersectionEvent intersectEvent)
        {
            FARGeometryLineSegment newBelow = intersectEvent.above;
            FARGeometryLineSegment newAbove = intersectEvent.below;
            if (sweepLine.TrySwapValues(newBelow, newAbove))
            {
                FARGeometryLineSegment aboveNewAbove = sweepLine.Next(newAbove);        //The line below newAbove was the line responsible for the intersection we just processed
                CalculateIntersection(newAbove, aboveNewAbove);                         //Assuming Euclidean geometry, they cannot intersect again, so we only need to check for the new "above"

                FARGeometryLineSegment belowNewBelow = sweepLine.Prev(newBelow);        //Similar logic for newBelow
                CalculateIntersection(newBelow, belowNewBelow);
            }
            Intersection newIntersection = new Intersection(intersectEvent.point, newBelow, newAbove);
            if(!finalIntersections.Contains(newIntersection))
            {
                finalIntersections.Add(new Intersection(intersectEvent.point, newBelow, newAbove));
                if(lineIntersections.ContainsKey(newBelow))
                {
                    lineIntersections[newBelow].Add(newIntersection);
                }
                else
                {
                    lineIntersections.Add(newBelow, new List<Intersection>());
                    lineIntersections[newBelow].Add(newIntersection);
                }

                if (lineIntersections.ContainsKey(newAbove))
                {
                    lineIntersections[newAbove].Add(newIntersection);
                }
                else
                {
                    lineIntersections.Add(newAbove, new List<Intersection>());
                    lineIntersections[newAbove].Add(newIntersection);
                }
            }

        }

        private void ProcessLineEndPointEvent(BentleyOttmannEventQueue.LineEndPointEvent endEvent)
        {
            if (endEvent.isLeftEnd)     //Start of a new line segment
            {
                FARGeometryLineSegment line = endEvent.line;
                sweepLine.Insert(line);
                FARGeometryLineSegment above = sweepLine.Next(line);
                CalculateIntersection(line, above);
                FARGeometryLineSegment below = sweepLine.Prev(line);
                CalculateIntersection(line, below);
            }
            else
                sweepLine.Delete(endEvent.line);
        }

        private void CalculateIntersection(FARGeometryLineSegment line1, FARGeometryLineSegment line2)
        {
            if (line1 == null || line2 == null)     //This line is on the edge of the group of lines
                return;

            //First, decide whether lines are not purely along x;
            double line1xdiff = line2.point1.x - line1.point1.x;
            double line2xdiff = line2.point2.x - line2.point1.x;
            double line1Slope, line2Slope;
            double xSoln, ySoln;

            if (line1xdiff == 0)     //Line 1 is vertical; grab the x value and test to see if line 2 passes through it.
            {
                if (line2xdiff == 0)
                    return;             //Obviously, if both are vertical, they can't intercept (unless they run over each other, which will not happen

                xSoln = line1.point1.x;

                if (line2.point1.x > xSoln)     //Check to make sure that the xSoln is even between those points
                    return;
                if (line2.point2.x < xSoln)
                    return;

                line2Slope = (line2.point2.y - line2.point1.y) / line2xdiff;
                ySoln = line2Slope * (xSoln - line2.point1.x) + line2.point1.y;

                if (line2.point1.y > ySoln)     //Check to make sure that the ySoln is even between those points
                    return;
                if (line2.point2.y < ySoln)
                    return;

                //Add intercept to queue
                AddIntersectionToQueue(line1, line2, new Vector3d(xSoln, ySoln, 0));
                return;
            }
            else if (line2xdiff == 0)     //Line 2 is vertical; grab the x value and test to see if line 1 passes through it.
            {
                if (line1xdiff == 0)
                    return;             //Obviously, if both are vertical, they can't intercept (unless they run over each other, which will not happen

                xSoln = line2.point1.x;

                if (line1.point1.x > xSoln)     //Check to make sure that the xSoln is even between those points
                    return;
                if (line1.point2.x < xSoln)
                    return;

                line1Slope = (line1.point2.y - line1.point1.y) / line1xdiff;
                ySoln = line1Slope * (xSoln - line1.point1.x) + line1.point1.y;

                if (line1.point1.y > ySoln)     //Check to make sure that the ySoln is even between those points
                    return;
                if (line1.point2.y < ySoln)
                    return;

                //Add intercept to queue
                AddIntersectionToQueue(line1, line2, new Vector3d(xSoln, ySoln, 0));
                return;
            }

            line1Slope = (line1.point2.y - line1.point1.y) / line1xdiff;
            line2Slope = (line2.point2.y - line2.point1.y) / line2xdiff;

            xSoln = line1Slope - line2Slope;     //Calculate the x value of the intersection of the lines defined by this
            xSoln = 1 / xSoln;
            double tmp = line1Slope * line1.point1.x - line2Slope * line2.point1.x;
            tmp += line2.point1.y - line1.point1.y;
            xSoln *= tmp;

            if (line1.point1.x > xSoln)         //Check to make sure that it actually happens between the values.
                return;
            if (line1.point2.x < xSoln)
                return;
            if (line2.point1.x > xSoln)
                return;
            if (line2.point2.x < xSoln)
                return;

            ySoln = line1Slope * (xSoln - line1.point1.x) + line1.point1.y;  //and calculate y counterpart
            
            //Add intercept to queue
            AddIntersectionToQueue(line1, line2, new Vector3d(xSoln, ySoln, 0));
            return;
        }


        private void AddIntersectionToQueue(FARGeometryLineSegment line1, FARGeometryLineSegment line2, Vector3d point)
        {
            Part p = line1.point1.associatedPart;
            int isLeft = line1.pointToLeft(line2.point1);
            if(isLeft >= 0)
                eventQueue.InsertIntersection(new FARGeometryPoint(point, p), line1, line2);
            else
                eventQueue.InsertIntersection(new FARGeometryPoint(point, p), line2, line1);

        }
    }
}
