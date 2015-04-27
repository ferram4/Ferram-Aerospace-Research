using System;
using System.Collections.Generic;
using UnityEngine;
using FerramAerospaceResearch.FARAeroComponents;
using ferram4;

namespace FerramAerospaceResearch.FARGUI.FARFlightGUI
{
    class PhysicsCalcs
    {
        NavBall _navball;
        Vessel _vessel;
        FARVesselAero _vesselAero;

        List<FARAeroPartModule> _currentAeroModules;
        List<FARWingAerodynamicModel> _LEGACY_currentWingAeroModel = new List<FARWingAerodynamicModel>();

        Vector3 totalAeroForceVector;
        int intakeAirId;
        double intakeAirDensity = 1;
        bool useWingArea;
        double wingArea = 0;

        VesselFlightInfo vesselInfo;

        public PhysicsCalcs(Vessel vessel, FARVesselAero vesselAerodynamics)
        {
            _vessel = vessel;
            _vesselAero = vesselAerodynamics;

            PartResourceLibrary resLibrary = PartResourceLibrary.Instance;
            PartResourceDefinition r = resLibrary.resourceDefinitions["IntakeAir"];
            if (r != null)
            {
                intakeAirId = r.id;
                intakeAirDensity = r.density;
            }
        }

        public void UpdateAeroModules(List<FARAeroPartModule> newAeroModules)
        {
            _currentAeroModules = newAeroModules;
            _LEGACY_currentWingAeroModel.Clear();
            wingArea = 0;
            useWingArea = false;
            for (int i = 0; i < _vessel.parts.Count; i++)
            {
                Part p = _vessel.parts[i];
                FARWingAerodynamicModel w = p.GetComponent<FARWingAerodynamicModel>();
                if ((object)w != null)
                {
                    _LEGACY_currentWingAeroModel.Add(w);
                    useWingArea = true;
                    wingArea += w.S;
                }
            }
        }

        public VesselFlightInfo UpdatePhysicsParameters()
        {
            Vector3d velVector = _vessel.srf_velocity
                - FARWind.GetWind(_vessel.mainBody, _vessel.rootPart, _vessel.ReferenceTransform.position);
            Vector3d velVectorNorm = velVector.normalized;
            double vesselSpeed = velVector.magnitude;

            vesselInfo = new VesselFlightInfo();

            CalculateTotalAeroForce();
            CalculateForceBreakdown(velVectorNorm, velVector);
            CalculateVesselOrientation(velVectorNorm);
            CalculateEngineAndIntakeBasedParameters(vesselSpeed);
            CalculateBallisticCoefficientAndTermVel();
            CalculateStallFraction();

            return vesselInfo;
        }

        private void CalculateTotalAeroForce()
        {
            totalAeroForceVector = Vector3.zero;
            if (_currentAeroModules != null)
            {
                for (int i = 0; i < _currentAeroModules.Count; i++)
                {
                    FARAeroPartModule m = _currentAeroModules[i];
                    if ((object)m != null)
                        totalAeroForceVector += m.worldSpaceAeroForce;
                }
            }

            for (int i = 0; i < _LEGACY_currentWingAeroModel.Count; i++)
            {
                FARWingAerodynamicModel w = _LEGACY_currentWingAeroModel[i];
                if ((object)w != null)
                    totalAeroForceVector += w.worldSpaceForce;
            }
        }

        private void CalculateForceBreakdown(Vector3d velVectorNorm, Vector3d velVector)
        {
            vesselInfo.dragForce = -Vector3d.Dot(totalAeroForceVector, velVectorNorm);     //reverse along vel normal will be drag

            Vector3d remainderVector = totalAeroForceVector + velVectorNorm * vesselInfo.dragForce;

            vesselInfo.liftForce = -Vector3d.Dot(remainderVector, _vessel.ReferenceTransform.forward);     //forward points down for the vessel, so reverse along that will be lift
            vesselInfo.sideForce = Vector3d.Dot(remainderVector, _vessel.ReferenceTransform.right);        //and the side force

            if (useWingArea)
                vesselInfo.refArea = wingArea;
            else
                vesselInfo.refArea = _vesselAero.MaxCrossSectionArea;

            vesselInfo.dynPres = _vessel.dynamicPressurekPa;

            double invAndDynPresArea = vesselInfo.refArea;
            invAndDynPresArea *= vesselInfo.dynPres;
            invAndDynPresArea = 1 / invAndDynPresArea;

            vesselInfo.dragCoeff = vesselInfo.dragForce * invAndDynPresArea;
            vesselInfo.liftCoeff = vesselInfo.liftForce * invAndDynPresArea;
            vesselInfo.sideCoeff = vesselInfo.sideForce * invAndDynPresArea;

            vesselInfo.liftToDragRatio = vesselInfo.liftForce / vesselInfo.dragForce;
        }

        private void GetNavball()
        {
            if (HighLogic.LoadedSceneIsFlight)
                _navball = FlightUIController.fetch.GetComponentInChildren<NavBall>();
        }

        private void CalculateVesselOrientation(Vector3d velVectorNorm)
        {
            Transform refTransform = _vessel.ReferenceTransform;

            Vector3 tmpVec = refTransform.up * Vector3.Dot(refTransform.up, velVectorNorm) + refTransform.forward * Vector3.Dot(refTransform.forward, velVectorNorm);   //velocity vector projected onto a plane that divides the airplane into left and right halves
            vesselInfo.aoA = Vector3.Dot(tmpVec.normalized, refTransform.forward);
            vesselInfo.aoA = FARMathUtil.rad2deg * Math.Asin(vesselInfo.aoA);
            if (double.IsNaN(vesselInfo.aoA))
                vesselInfo.aoA = 0;

            tmpVec = refTransform.up * Vector3.Dot(refTransform.up, velVectorNorm) + refTransform.right * Vector3.Dot(refTransform.right, velVectorNorm);     //velocity vector projected onto the vehicle-horizontal plane
            vesselInfo.sideslipAngle = Vector3.Dot(tmpVec.normalized, refTransform.right);
            vesselInfo.sideslipAngle = FARMathUtil.rad2deg * Math.Asin(vesselInfo.sideslipAngle);
            if (double.IsNaN(vesselInfo.sideslipAngle))
                vesselInfo.sideslipAngle = 0;

            if (_navball == null)
                GetNavball();
            if (_navball)
            {
                Quaternion vesselRot = Quaternion.Inverse(_navball.relativeGymbal);

                vesselInfo.headingAngle = vesselRot.eulerAngles.y;
                //vesselRot *= Quaternion.Euler(0, -yawAngle, 0);
                //yawAngle = 360 - yawAngle;
                vesselInfo.pitchAngle = (vesselRot.eulerAngles.x > 180) ? (360 - vesselRot.eulerAngles.x) : -vesselRot.eulerAngles.x;
                vesselInfo.rollAngle = (vesselRot.eulerAngles.z > 180) ? (360 - vesselRot.eulerAngles.z) : -vesselRot.eulerAngles.z;
            }
        }

        private void CalculateEngineAndIntakeBasedParameters(double vesselSpeed)
        {
            double totalThrust = 0;
            double totalThrust_Isp = 0;

            double fuelConsumptionVol = 0;
            double airDemandVol = 0;
            double airAvailableVol = 0;

            double invDeltaTime = 1 / TimeWarp.fixedDeltaTime;
            PartResourceLibrary resLibrary = PartResourceLibrary.Instance;


            List<Part> partsList = _vessel.Parts;
            for (int i = 0; i < partsList.Count; i++)
            {
                Part p = partsList[i];

                Rigidbody rb = p.rb;
                if ((object)rb != null)
                {
                    vesselInfo.fullMass += rb.mass;
                    vesselInfo.dryMass += p.mass;
                }

                for (int j = 0; j < p.Modules.Count; j++)
                {
                    PartModule m = p.Modules[j];
                    if (m is ModuleEngines)
                    {
                        ModuleEngines e = (ModuleEngines)m;
                        FuelConsumptionFromEngineModule(e, ref totalThrust, ref totalThrust_Isp, ref fuelConsumptionVol, ref airDemandVol, invDeltaTime);
                    }

                    if (m is ModuleResourceIntake)
                    {
                        ModuleResourceIntake intake = (ModuleResourceIntake)m;
                        if (intake.intakeEnabled)
                        {
                            airAvailableVol += intake.airFlow * intakeAirDensity / invDeltaTime;
                        }
                    }
                }
            }
            vesselInfo.tSFC = totalThrust / totalThrust_Isp;    //first, calculate inv Isp
            vesselInfo.tSFC *= 3600;   //then, convert from 1/s to 1/hr

            vesselInfo.intakeAirFrac = airAvailableVol / airDemandVol;

            vesselInfo.specExcessPower = totalThrust - vesselInfo.dragForce;
            vesselInfo.specExcessPower *= vesselSpeed / vesselInfo.fullMass;

            vesselInfo.velocityLiftToDragRatio = vesselSpeed * vesselInfo.liftToDragRatio;
            double L_D_TSFC = 0;
            double VL_D_TSFC = 0;
            if (vesselInfo.tSFC != 0)
            {
                L_D_TSFC = vesselInfo.liftToDragRatio / vesselInfo.tSFC;
                VL_D_TSFC = vesselInfo.velocityLiftToDragRatio / vesselInfo.tSFC * 3600;
            }

            vesselInfo.range = vesselInfo.fullMass / vesselInfo.dryMass;
            vesselInfo.range = Math.Log(vesselInfo.range);
            vesselInfo.endurance = L_D_TSFC * vesselInfo.range;
            vesselInfo.range *= VL_D_TSFC * 0.001;
        }

        private void FuelConsumptionFromEngineModule(ModuleEngines e, ref double totalThrust, ref double totalThrust_Isp, ref double fuelConsumptionVol, ref double airDemandVol, double invDeltaTime)
        {
            if (e.EngineIgnited && !e.engineShutdown)
            {
                totalThrust += e.finalThrust;
                totalThrust_Isp += e.finalThrust * e.realIsp;
                for (int i = 0; i < e.propellants.Count; i++)
                {
                    Propellant v = e.propellants[i];

                    if (v.id == intakeAirId)
                        airDemandVol += v.currentRequirement;

                    if(!v.ignoreForIsp)
                        fuelConsumptionVol += v.currentRequirement * invDeltaTime;
                }
            }
        }

        private void CalculateBallisticCoefficientAndTermVel()
        {
            double geeForce = FlightGlobals.getGeeForceAtPosition(_vessel.CoM).magnitude;

            vesselInfo.ballisticCoeff = vesselInfo.fullMass * vesselInfo.dynPres / vesselInfo.dragForce * 1000;

            vesselInfo.termVelEst = 2 * vesselInfo.ballisticCoeff * geeForce;
            vesselInfo.termVelEst /= _vessel.atmDensity;
            vesselInfo.termVelEst = Math.Sqrt(vesselInfo.termVelEst);
        }

        private void CalculateStallFraction()
        {
            for (int i = 0; i < _LEGACY_currentWingAeroModel.Count; i++)
                vesselInfo.stallFraction += _LEGACY_currentWingAeroModel[i].GetStall();

            vesselInfo.stallFraction /= (double)_LEGACY_currentWingAeroModel.Count;
        }
    }
}
