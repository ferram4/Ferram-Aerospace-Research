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
            }
        }

        private void CalculateIntersection(FARGeometryLineSegment line1, FARGeometryLineSegment line2)
        {
            if (line1 == null || line2 == null)     //This line is on the edge of the group of lines
                return;

            //Solve for an intercept point, then check if that point exists within the x domain each line is defined on; if it is, add a new intersect; if not, return;
        }
    }
}
