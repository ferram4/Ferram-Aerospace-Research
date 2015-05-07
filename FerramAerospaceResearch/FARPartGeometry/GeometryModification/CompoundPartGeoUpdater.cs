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
            CompoundPartGeoUpdate();
        }

        public void FlightGeometryUpdate()
        {
            CompoundPartGeoUpdate();
        }

        private void CompoundPartGeoUpdate()
        {
            if (lastAttachState != part.attachState || lastTarget != part.target || !EditorLogic.SortedShipList.Contains(part.target))
            {
                geoModule.RebuildAllMeshData();
                lastAttachState = part.attachState;
                lastTarget = part.target;
            }
        }
    }
}
