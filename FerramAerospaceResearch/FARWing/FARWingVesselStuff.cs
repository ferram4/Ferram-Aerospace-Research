using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ferram4;
using FerramAerospaceResearch.FARGeometry;

namespace FerramAerospaceResearch.FARWing
{
    public class FARWingVesselStuff
    {
        private List<FARGeometryPartPolygon> wingPolys;

        public FARWingVesselStuff(Vessel v) : this(v.Parts) { }

        public FARWingVesselStuff(List<Part> partList)
        {
            wingPolys = GetAllPolysInWings(partList);
        }

        private List<FARGeometryPartPolygon> GetAllPolysInWings(List<Part> partList)
        {
            List<FARGeometryPartPolygon> polys = new List<FARGeometryPartPolygon>();

            for (int i = 0; i < partList.Count; i++)
            {
                Part p = partList[i];
                FARWingPartModule wingModule = p.GetComponent<FARWingPartModule>();

                if ((object)wingModule != null)
                {
                    polys.Add(wingModule.Poly);
                }
            }

            return polys;
        }
    }
}
