using System;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeometry.GeometryModification
{
    class CompoundPartGeoUpdater : IGeometryUpdater
    {
        CompoundPart part;
        GeometryPartModule geoModule;
        CompoundPart.AttachState lastAttachState;
        Part lastTarget;

        public CompoundPartGeoUpdater(CompoundPart part, GeometryPartModule geoModule)
        {
            this.part = part;
            this.geoModule = geoModule;
            lastAttachState = part.attachState;
            lastTarget = part.target;
        }

        public void EditorGeometryUpdate()
        {
            if (lastAttachState != part.attachState || lastTarget != part.target || !EditorLogic.SortedShipList.Contains(part.target))
            {
                geoModule.RebuildAllMeshData();
                lastAttachState = part.attachState;
                lastTarget = part.target;
            }
        }

        public void FlightGeometryUpdate()
        {
            if (lastAttachState != part.attachState || lastTarget != part.target || !part.vessel.parts.Contains(part.target))
            {
                geoModule.RebuildAllMeshData();
                lastAttachState = part.attachState;
                lastTarget = part.target;
            }
        }
    }
}
