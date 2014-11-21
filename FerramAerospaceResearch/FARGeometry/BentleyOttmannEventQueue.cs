using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FerramAerospaceResearch.FARCollections;

namespace FerramAerospaceResearch.FARGeometry
{
    public class BentleyOttmannEventQueue
    {
        class Event
        {
            public FARGeometryPoint point;
        }
        class LineEndPointEvent : Event
        {
            public bool isLeftEnd = false;
            public LineEndPointEvent otherEnd;
            public FARGeometryLineSegment line;
        }
        class IntersectionEvent : Event
        {
            public FARGeometryLineSegment line1;
            public FARGeometryLineSegment line2;
        }
        class EventComparer : IComparer<Event>
        {
            public int Compare(Event x, Event y)
            {
                return x.point.CompareTo(y.point);
            }
        }
        List<Event> eventQueue;

        public BentleyOttmannEventQueue(List<FARGeometryLineSegment> lines)
        {
            eventQueue = new List<Event>();
            for (int i = 0; i < lines.Count; i++)
            {
                FARGeometryLineSegment line = lines[i];
                LineEndPointEvent ev1 = new LineEndPointEvent();
                LineEndPointEvent ev2 = new LineEndPointEvent();

                ev1.point = line.point1;
                ev2.point = line.point2;
                ev1.line = line;
                ev2.line = line;
                ev1.otherEnd = ev2;
                ev2.otherEnd = ev1;
                int cmp = ev1.point.CompareTo(ev2.point);
                if (cmp > 1)
                    ev2.isLeftEnd = true;
                else
                    ev1.isLeftEnd = true;

                eventQueue.Add(ev1);
                eventQueue.Add(ev2);
            }

            eventQueue = eventQueue.MergeSort(new EventComparer());
        }
    }
}
