using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FerramAerospaceResearch.FARAeroComponents;

namespace FerramAerospaceResearch.FARGUI.FAREditorGUI.Simulation
{
    class EditorSimManager
    {
        InstantConditionSim _instantCondition;

        StabilityDerivCalculator _stabDerivCalculator;
        public StabilityDerivCalculator StabDerivCalculator
        {
            get { return _stabDerivCalculator; }
        }

        StabilityDerivLinearSim _stabDerivLinearSim;
        public StabilityDerivLinearSim StabDerivLinearSim
        {
            get { return _stabDerivLinearSim; }
        }

        SweepSim _sweepSim;
        public SweepSim SweepSim
        {
            get { return _sweepSim; }
        }
        EditorAeroCenter _aeroCenter;

        public StabilityDerivOutput vehicleData;

        public EditorSimManager()
        {
            _instantCondition = new InstantConditionSim();
            _stabDerivCalculator = new StabilityDerivCalculator(_instantCondition);
            _stabDerivLinearSim = new StabilityDerivLinearSim(_instantCondition);
            _sweepSim = new SweepSim(_instantCondition);
            _aeroCenter = new EditorAeroCenter();
            vehicleData = new StabilityDerivOutput();
        }

        public void UpdateAeroData(VehicleAerodynamics vehicleAero)
        {
            _instantCondition.UpdateAeroData(vehicleAero);
            _aeroCenter.UpdateAeroData(vehicleAero);
        }
    }
}
