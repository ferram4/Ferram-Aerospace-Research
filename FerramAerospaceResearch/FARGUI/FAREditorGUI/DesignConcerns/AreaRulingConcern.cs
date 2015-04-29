using System;
using System.Collections.Generic;
using PreFlightTests;
using FerramAerospaceResearch.FARAeroComponents;

namespace FerramAerospaceResearch.FARGUI.FAREditorGUI.DesignConcerns
{
    class AreaRulingConcern : DesignConcernBase
    {
        private VehicleAerodynamics _vesselAero;

        public AreaRulingConcern(VehicleAerodynamics vesselAero)
        {
            _vesselAero = vesselAero;
        }

        public override bool TestCondition()
        {
            if (_vesselAero == null)
                return true;

            if (_vesselAero.SonicDragArea * 0.75 < _vesselAero.MaxCrossSectionArea)
                return true;

            return false;
        }

        public override EditorFacilities GetEditorFacilities()
        {
            return base.GetEditorFacilities();
        }
        public override string GetConcernTitle()
        {
            return "High Transonic // Supersonic Drag!";
        }
        public override string GetConcernDescription()
        {
            return "This vehicle's cross-sectional area distribution is insufficiently smooth and//or contains very large changes in area over short distances.";
        }
        public override DesignConcernSeverity GetSeverity()
        {
            return DesignConcernSeverity.WARNING;
        }
    }
}
