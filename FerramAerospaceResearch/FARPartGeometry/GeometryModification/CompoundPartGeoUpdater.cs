using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FerramAerospaceResearch.FARPartGeometry.GeometryModification
{
    class CompoundPartGeoUpdater : IGeometryUpdater
    {
        CompoundPart part;
        GeometryPartModule geoModule;
        bool attached;

        public CompoundPartGeoUpdater(CompoundPart part, GeometryPartModule geoModule)
        {
            this.part = part;
            this.geoModule = geoModule;
            attached = part.isAttached;
        }

        public void EditorGeometryUpdate()
        {
            if (attached != part.isAttached)
            {
                geoModule.RebuildAllMeshData();
                attached = !attached;
            }
        }

        public void FlightGeometryUpdate()
        {
            if (attached != part.isAttached)
            {
                geoModule.RebuildAllMeshData();
                attached = !attached;
            }
        }
    }
}
