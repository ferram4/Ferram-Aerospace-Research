using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FerramAerospaceResearch.FARCollections;

namespace FerramAerospaceResearch.FARGeometry
{
    public class BentleyOttmannEventQueue
    {
        public class Event : IComparable<Event>
        {
            public FARGeometryPoint point;

            public int CompareTo(Event x)
            {
                return this.point.CompareTo(x.point);
            }
        }
        public class LineEndPointEvent : Event
        {
            public bool isLeftEnd = false;
            public LineEndPointEvent otherEnd;
            public FARGeometryLineSegment line;
        }
        public class IntersectionEvent : Event
        {
            public FARGeometryLineSegment above;
            public FARGeometryLineSegment below;
        }
        public class EventComparer : IComparer<Event>
        {
            public int Compare(Event x, Event y)
            {
                return x.point.CompareTo(y.point);
            }
        }
        List<Event> eventQueue;
        int index = 0;

        public int Count { get { return eventQueue.Count - index; } }

        public BentleyOttmannEventQueue(List<FARGeometryLineSegment> lines)
        {
            eventQueue = new List<Event>();
            for (int i = 0; i < lines.Count; i++)
            {
                FARGeometryLineSegment line = lines[i];

                line.SetPoint1ToLeftMost();

                LineEndPointEvent ev1 = new LineEndPointEvent();
                LineEndPointEvent ev2 = new LineEndPointEvent();

                ev1.point = line.point1;
                ev2.point = line.point2;
                ev1.line = line;
                ev2.line = line;
                ev1.otherEnd = ev2;
                ev2.otherEnd = ev1;
                ev1.isLeftEnd = true;

                eventQueue.Add(ev1);
                eventQueue.Add(ev2);
            }

            eventQueue = eventQueue.MergeSort(new EventComparer());
        }

        public void InsertIntersection(FARGeometryPoint intersect, FARGeometryLineSegment above, FARGeometryLineSegment below)
        {
            IntersectionEvent ev = new IntersectionEvent();
            ev.point = intersect;
            ev.above = above;
            ev.below = below;

            for(int i = index; i < eventQueue.Count; i++)
            {
                int cmp = ev.CompareTo(eventQueue[i]);
                if (cmp < 1)
                    continue;
                else if (cmp == 1 && eventQueue[i] is IntersectionEvent)
                {
                    IntersectionEvent prevIntersect = (IntersectionEvent)eventQueue[i];
                    if (prevIntersect.above == above && ev.below == below)  //in this case, we have a repeat intersection; break out of that then
                        break;

                }
                eventQueue.Insert(i, ev);       //This is only called if cmp > 1 or if cmp == 1, but it's not the same intersection
            }
        }

        public Event GetNextEvent()
        {
            Event ev = eventQueue[index];
            index++;

            return ev;
        }
    }
}
