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
        }
    }
}
