using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ferram4;

namespace FerramAerospaceResearch.FARAeroComponents
{
    class EditorAeroCenter
    {
        static EditorAeroCenter instance;
        public static EditorAeroCenter Instance
        {
            get { return instance; }
        }
        
        Vector3 vesselRootLocalAeroCenter;
        public static Vector3 VesselRootLocalAeroCenter
        {
            get { return instance.vesselRootLocalAeroCenter; }
        }
        Vector3 aeroForce;

        List<FARAeroPartModule> _currentAeroModules;
        List<FARAeroSection> _currentAeroSections;

        public EditorAeroCenter()
        {
            instance = this;
        }

        public void UpdateAeroData(VehicleAerodynamics vehicleAero)
        {
            vehicleAero.GetNewAeroData(out _currentAeroModules, out _currentAeroSections);
            UpdateAerodynamicCenter();
        }

        void UpdateAerodynamicCenter()
        {
            FARCenterQuery aeroSection, lift, dummy;
            aeroSection = new FARCenterQuery();
            lift = new FARCenterQuery();
            dummy = new FARCenterQuery();

            Vector3 vel_base, vel_fuzz;

            if (EditorDriver.editorFacility == EditorFacility.SPH)
            {
                vel_base = Vector3.forward;
                vel_fuzz = 0.02f * Vector3.up;
            }
            else
            {
                vel_base = Vector3.up;
                vel_fuzz = -0.02f * Vector3.forward;
            }

            Vector3 vel = (vel_base - vel_fuzz).normalized;

            for(int i = 0; i < _currentAeroSections.Count; i++)
            {
                FARAeroSection section = _currentAeroSections[i];
                section.EditorCalculateAeroForces(1, 0, 10000, 0.005f, vel, aeroSection);
            }

            aeroSection.force = -aeroSection.force;
            aeroSection.torque = -aeroSection.torque;

            vel = (vel_base + vel_fuzz).normalized;

            for (int i = 0; i < _currentAeroSections.Count; i++)
            {
                FARAeroSection section = _currentAeroSections[i];
                section.EditorCalculateAeroForces(1, 0, 10000, 0.005f, vel, aeroSection);
            }

            FARBaseAerodynamics.PrecomputeGlobalCenterOfLift(lift, dummy);

            aeroSection.AddAll(lift);

            aeroForce = aeroSection.force;
            vesselRootLocalAeroCenter = aeroSection.GetMinTorquePos();
            vesselRootLocalAeroCenter = EditorLogic.RootPart.transform.worldToLocalMatrix.MultiplyPoint3x4(vesselRootLocalAeroCenter);
        }
    }
}
