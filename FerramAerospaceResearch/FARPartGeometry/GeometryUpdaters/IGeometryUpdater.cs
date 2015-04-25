using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeometry.GeometryUpdaters
{
    interface IGeometryUpdater
    {
        void EditorGeometryUpdate();

        void FlightGeometryUpdate();
    }
}
