using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeometry.GeometryUpdaters
{
    class StockJettisonTransformGeoUpdater : IGeometryUpdater
    {
        ModuleJettison engineFairing;
        GeometryPartModule geoModule;
        bool fairingVisible;

        public StockJettisonTransformGeoUpdater(ModuleJettison engineFairing, GeometryPartModule geoModule)
        {
            this.engineFairing = engineFairing;
            this.geoModule = geoModule;
            fairingVisible = engineFairing.jettisonTransform.gameObject.activeSelf;
        }

        public void EditorGeometryUpdate()
        {
            Transform t = engineFairing.jettisonTransform;
            if(t == null)
                return;

            GameObject o = t.gameObject;
            if(o != null)
                if (fairingVisible != engineFairing.jettisonTransform.gameObject.activeSelf)
                {
                    geoModule.RebuildAllMeshData();
                    fairingVisible = !fairingVisible;
                }
        }

        public void FlightGeometryUpdate()
        {
            Transform t = engineFairing.jettisonTransform;
            if (t == null)
                return;

            GameObject o = t.gameObject;
            if (o != null)
                if (fairingVisible != engineFairing.jettisonTransform.gameObject.activeSelf)
                {
                    geoModule.RebuildAllMeshData();
                    fairingVisible = !fairingVisible;
                }
        }

    }
}
