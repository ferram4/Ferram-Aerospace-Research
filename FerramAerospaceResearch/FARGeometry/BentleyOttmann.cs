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


        /// <summary>
        /// This merges two polygons using the Bentley-Ottmann Algorithm
        /// </summary>
        /// <param name="poly1">First poly; this will be used for unified transforms and will contain the output of this method</param>
        /// <param name="poly2">Poly to be merged</param>
        public void MergePolygons(ref FARGeometryPartPolygon poly1, FARGeometryPartPolygon poly2)
        {
            List<FARGeometryLineSegment> lines = new List<FARGeometryLineSegment>(poly1.PlanformBoundsLines);
            lines.AddRange(poly2.PlanformBoundsLines);

            eventQueue = new BentleyOttmannEventQueue(lines);

            sweepLine = new LLRedBlackTree<FARGeometryLineSegment>(new IsLeftComparer());

            while(eventQueue.Count > 0)
            {
                BentleyOttmannEventQueue.Event newEvent = eventQueue.GetNextEvent();
                if(newEvent is BentleyOttmannEventQueue.LineEndPointEvent)
                {
                    ProcessLineEndPointEvent((BentleyOttmannEventQueue.LineEndPointEvent)newEvent);
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
            eventQueue.InsertIntersection(new FARGeometryPoint(point, p), line1, line2);
        }
    }
}
