using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using KSP;

namespace ferram4
{
    class FAREditorAeroSim
    {
        public void CalculateExcessPowerPlot(out Dictionary<string, double[]> plotXValues, out Dictionary<string, double[]> plotYValues, double minMach, double maxMach, double minAlt, double maxAlt, CelestialBody body, int contourStep)
        {
            List<ModuleEngines> standardLegacyEngines = new List<ModuleEngines>();
            List<ModuleEnginesFX> standardFXEngines = new List<ModuleEnginesFX>();
            List<PartModule> ajeJetEngines = new List<PartModule>();
            List<PartModule> ajePropEngines = new List<PartModule>();
            List<PartModule> ajeInlets = new List<PartModule>();

            for (int i = 0; i < FARAeroUtil.CurEditorParts.Count; i++)
            {
                Part p = FARAeroUtil.CurEditorParts[i];
                if (p.Modules.Contains("AJEModule"))
                {
                    ajeJetEngines.Add(p.Modules["AJEModule"]);
                    continue;
                }
                else if (p.Modules.Contains("AJEPropeller"))
                {
                    ajePropEngines.Add(p.Modules["AJEPropeller"]);
                    continue;
                }
                else if (p.Modules.Contains("AJEInlet"))
                {
                    ajeInlets.Add(p.Modules["AJEInlet"]);
                    continue;
                }
                else if (p.Modules.Contains("ModuleEngines"))
                {
                    standardLegacyEngines.Add((ModuleEngines)p.Modules["ModuleEngines"]);
                    continue;
                }
                else if (p.Modules.Contains("ModuleEnginesFX"))
                {
                    standardFXEngines.Add((ModuleEnginesFX)p.Modules["ModuleEnginesFX"]);
                    continue;
                }
            }
            double curMach = minMach;
            double curAltitude = minAlt;
            double excessPower = 0;
            double mass, area;  //Establish vehicle characteristics
            Vector3d CoM;
            SetVehicleCharacteristics(out mass, out area, out CoM);

            double lastMach = -1;
            double lastExcessPower = 0;

            double specExcessPower = 0;

            double alpha;

            Dictionary<int, List<double>> xValues = new Dictionary<int, List<double>>();
            Dictionary<int, List<double>> yValues = new Dictionary<int, List<double>>();

            while (curAltitude < maxAlt)
            {
                while (curMach < maxMach || (excessPower < 0 && curMach > 1.2))
                {
                    lastExcessPower = specExcessPower;
                    double vel = Math.Sqrt(FlightGlobals.getExternalTemperature((float)curAltitude, body) * FARAeroUtil.currentBodyAtm.x) * curMach;

                    double drag = IterateToSteadyFlightDrag(out alpha, vel, curAltitude, curMach, body, mass, area, CoM);

                    Vector3d velocity = Vector3d.forward * Math.Cos(alpha) - Vector3d.up * Math.Sin(alpha);
                    double thrust = CalculateThrust(curMach, vel, curAltitude, velocity, body, standardLegacyEngines, standardFXEngines, ajeJetEngines, ajePropEngines, ajeInlets);
                    specExcessPower = vel * (thrust - drag) / mass;

                    int curContour, lastContour;
                    curContour = (int)specExcessPower / contourStep;
                    lastContour = (int)lastExcessPower / contourStep;

                    if (curContour != lastContour)
                    {
                        int contour = Math.Max(curContour, lastContour);
                        double xVal = (contour - lastExcessPower) * (curMach - lastMach) / (specExcessPower - lastExcessPower) + lastMach;

                        if (xValues.ContainsKey(contour))
                        {
                            xValues[contour].Add(xVal);
                            yValues[contour].Add(curAltitude);
                        }
                        else
                        {
                            xValues[contour] = new List<double>();
                            yValues[contour] = new List<double>();

                            xValues[contour].Add(xVal);
                            yValues[contour].Add(curAltitude);
                        }
                    }
                    lastMach = curMach;
                    curMach += 0.05;
                }
                curAltitude += 500;
            }
        }

        private double CalculateThrust(double mach, double vel, double alt, Vector3d velNormVector, CelestialBody body, List<ModuleEngines> standardLegacyEngines,
            List<ModuleEnginesFX> standardFXEngines, List<PartModule> ajeJetEngines, List<PartModule> ajePropEngines, List<PartModule> ajeInlets)
        {
            double thrust = 0;

            thrust += ThrustFromStockEngines((float)vel, standardLegacyEngines);
            thrust += ThrustFromStockEngines((float)vel, standardFXEngines);

            SetAJEInlets(velNormVector, (float)vel, ajeInlets);
            thrust += ThrustFromAJEJets(alt, vel, body, ajeJetEngines);

            return thrust;
        }

        private void SetAJEInlets(Vector3 velVector, float vel, List<PartModule> ajeInlets)
        {
            for (int i = 0; i < ajeInlets.Count; i++)
            {
                PartModule m = ajeInlets[i];
                Type inletType = m.GetType();

                MethodInfo inlet = inletType.GetMethod("IntakeAngle");
                inlet.Invoke(m, new object[] { velVector, vel });
            }
        }

        private double ThrustFromAJEJets(double alt, double vel, CelestialBody body, List<PartModule> ajeJetEngines)
        {
            double thrust = 0;
            for(int i = 0; i < ajeJetEngines.Count; i++)
            {
                PartModule m = ajeJetEngines[i];
                Type engineType = m.GetType();

                MethodInfo inlet = engineType.GetMethod("UpdateInletEffects");
                inlet.Invoke(m, new object[]{FARAeroUtil.CurEditorParts});

                MethodInfo flightCondition = engineType.GetMethod("UpdateFlightCondition");
                flightCondition.Invoke(m, new object[] { alt, vel, body});

                MethodInfo calcThrust = engineType.GetMethod("CalculateThrust");
                calcThrust.Invoke(m, new object[] { 1 });

                if(m.part.Modules.Contains("ModuleEngines"))
                {
                    thrust += ((ModuleEngines)m.part.Modules["ModuleEngines"]).maxThrust;
                }
                else if (m.part.Modules.Contains("ModuleEnginesFX"))
                {
                    thrust += ((ModuleEnginesFX)m.part.Modules["ModuleEnginesFX"]).maxThrust;
                }
            }
            return thrust;
        }

        private double ThrustFromStockEngines(float vel, List<ModuleEngines> engines)
        {
            double thrust = 0;
            for(int i = 0; i < engines.Count; i++)
            {
                ModuleEngines e = engines[i];
                if (e.useVelocityCurve)
                    thrust += e.velocityCurve.Evaluate(vel) * e.maxThrust;
                else
                    thrust += e.maxThrust;
            }
            return thrust;
        }

        private double ThrustFromStockEngines(float vel, List<ModuleEnginesFX> engines)
        {
            double thrust = 0;
            for (int i = 0; i < engines.Count; i++)
            {
                ModuleEnginesFX e = engines[i];
                if (e.useVelocityCurve)
                    thrust += e.velocityCurve.Evaluate(vel) * e.maxThrust;
                else
                    thrust += e.maxThrust;
            }
            return thrust;
        }
        
        private void SetVehicleCharacteristics(out double mass, out double area, out Vector3d CoM)
        {
            mass = 0;
            area = 0;
            CoM = Vector3d.zero;
            for (int i = 0; i < FARAeroUtil.CurEditorParts.Count; i++)
            {
                Part p = FARAeroUtil.CurEditorParts[i];

                if (FARAeroUtil.IsNonphysical(p))
                    continue;
                double partMass = p.mass;
                CoM += partMass * (Vector3d)p.transform.TransformPoint(p.CoMOffset);
                mass += partMass;
                FARWingAerodynamicModel w = p.GetComponent<FARWingAerodynamicModel>();
                if (w != null)
                {
                    area += w.S;
                }
            }
            if (area == 0)
            {
                area = 1;
            }
        }

        private double IterateToSteadyFlightDrag(out double alpha, double u0, double alt, double M, CelestialBody body, double mass, double area, Vector3d CoM)
        {
            double Cl = 0;
            double Cd = 0;
            double effectiveG = CalculateAccelerationDueToGravity(body, alt);     //This is the effect of gravity
            double q = FARAeroUtil.GetCurrentDensity(body, alt) * u0 * u0 * 0.5;
            effectiveG -= u0 * u0 / (alt + body.Radius);                          //This is the effective reduction of gravity due to high velocity
            double neededCl = mass * effectiveG / (q * area);

            alpha = 2;

            double pertCl, nomCm, nomCy, nomCn, nomC_roll;
            int iter = 7;
            for (; ; )
            {
                GetClCdCmSteady(CoM, alpha, 0, 0, 0, 0, 0, M, 0, out Cl, out Cd, out nomCm, out nomCy, out nomCn, out nomC_roll, true, true);

                GetClCdCmSteady(CoM, alpha, 0, 0, 0, 0, 0, M, 0, out pertCl, out Cd, out nomCm, out nomCy, out nomCn, out nomC_roll, true, true);
                if (--iter <= 0 || Math.Abs((Cl - neededCl) / neededCl) < 0.1)
                    break;

                double delta = (neededCl - Cl) / pertCl * FARMathUtil.rad2deg;
                delta = Math.Sign(delta) * Math.Min(0.4f * iter * iter, Math.Abs(delta));
                alpha = Math.Max(-5f, Math.Min(25f, alpha + delta));
            }

            return Cd * area * q;
        }

        public double CalculateAccelerationDueToGravity(CelestialBody body, double alt)
        {
            double radius = body.Radius + alt;
            double mu = body.gravParameter;

            double accel = radius * radius;
            accel = mu / accel;
            return accel;
        }
        
        public void GetClCdCmSteady(Vector3d CoM, double alpha, double beta, double phi, double alphaDot, double betaDot, double phiDot, double M, double pitch, out double Cl, out double Cd, out double Cm, out double Cy, out double Cn, out double C_roll, bool clear, bool reset_stall = false, int flap_setting = 0, bool spoilersDeployed = false, bool vehicleFueled = true)
        {
            Cl = 0;
            Cd = 0;
            Cm = 0;
            Cy = 0;
            Cn = 0;
            C_roll = 0;
            double area = 0;
            double MAC = 0;
            double b_2 = 0;

            alpha *= FARMathUtil.deg2rad;
            beta *= FARMathUtil.deg2rad;
            phi *= FARMathUtil.deg2rad;

            Vector3d forward = Vector3.forward;
            Vector3d up = Vector3.up;
            Vector3d right = Vector3.right;

            if (EditorLogic.fetch.editorType == EditorLogic.EditorMode.VAB)
            {
                forward = Vector3.up;
                up = -Vector3.forward;
            }

            Vector3d AngVel = (phiDot - Math.Sin(alpha) * betaDot) * forward + (Math.Cos(phi) * alphaDot + Math.Cos(alpha) * Math.Sin(phi) * betaDot) * right + (Math.Sin(phi) * alphaDot - Math.Cos(alpha) * Math.Cos(phi) * betaDot) * up;


            Vector3d velocity = forward * Math.Cos(alpha) * Math.Cos(beta) + right * (Math.Sin(phi) * Math.Sin(alpha) * Math.Cos(beta) - Math.Cos(phi) * Math.Sin(beta)) - up * (Math.Cos(phi) * Math.Sin(alpha) * Math.Cos(beta) - Math.Cos(phi) * Math.Sin(beta));

            velocity.Normalize();

            Vector3d liftVector = -forward * Math.Sin(alpha) + right * Math.Sin(phi) * Math.Cos(alpha) - up * Math.Cos(phi) * Math.Cos(alpha);

            Vector3d sideways = Vector3.Cross(velocity, liftVector);

            for (int i = 0; i < FARAeroUtil.CurEditorParts.Count; i++)
            {
                Part p = FARAeroUtil.CurEditorParts[i];

                if (FARAeroUtil.IsNonphysical(p))
                    continue;
                for (int k = 0; k < p.Modules.Count; k++)
                {
                    PartModule m = p.Modules[k];
                    if (m is FARWingAerodynamicModel)
                    {
                        FARWingAerodynamicModel w = m as FARWingAerodynamicModel;
                        if (clear)
                            w.EditorClClear(reset_stall);

                        Vector3 relPos = p.transform.position - CoM;

                        Vector3 vel = velocity + Vector3.Cross(AngVel, relPos);

                        if (w is FARControllableSurface)
                            (w as FARControllableSurface).SetControlStateEditor(CoM, vel, (float)pitch, 0, 0, flap_setting, spoilersDeployed);
                    }
                }
            }
            for (int j = 0; j < 3; j++)
            {
                for (int i = 0; i < FARAeroUtil.CurEditorParts.Count; i++)
                {
                    Part p = FARAeroUtil.CurEditorParts[i];

                    if (FARAeroUtil.IsNonphysical(p))
                        continue;
                    for (int k = 0; k < p.Modules.Count; k++)
                    {
                        PartModule m = p.Modules[k];
                        if (m is FARWingAerodynamicModel)
                        {
                            Vector3 relPos = p.transform.position - CoM;

                            Vector3 vel = velocity + Vector3.Cross(AngVel, relPos);

                            FARWingAerodynamicModel w = m as FARWingAerodynamicModel;
                            w.ComputeClCdEditor(vel, M);
                        }
                    }
                }
            }
            for (int i = 0; i < FARAeroUtil.CurEditorParts.Count; i++)
            {
                Part p = FARAeroUtil.CurEditorParts[i];
                if (FARAeroUtil.IsNonphysical(p))
                    continue;
                for (int k = 0; k < p.Modules.Count; k++)
                {
                    PartModule m = p.Modules[k];
                    if (m is FARWingAerodynamicModel)
                    {

                        FARWingAerodynamicModel w = m as FARWingAerodynamicModel;
                        if (w.isShielded)
                            break;

                        Vector3d relPos = w.GetAerodynamicCenter() - CoM;

                        Vector3d vel = velocity + Vector3d.Cross(AngVel, relPos);

                        w.ComputeClCdEditor(vel, M);

                        double tmpCl = w.GetCl() * w.S;
                        Cl += tmpCl * -Vector3d.Dot(w.GetLiftDirection(), liftVector);
                        Cy += tmpCl * -Vector3d.Dot(w.GetLiftDirection(), sideways);
                        double tmpCd = w.GetCd() * w.S;
                        Cd += tmpCd;
                        Cm += tmpCl * Vector3d.Dot((relPos), velocity) * -Vector3d.Dot(w.GetLiftDirection(), liftVector) + tmpCd * -Vector3d.Dot((relPos), liftVector);
                        Cn += tmpCd * Vector3d.Dot((relPos), sideways) + tmpCl * Vector3d.Dot((relPos), velocity) * -Vector3d.Dot(w.GetLiftDirection(), sideways);
                        C_roll += tmpCl * Vector3d.Dot((relPos), sideways) * -Vector3d.Dot(w.GetLiftDirection(), liftVector);
                        area += w.S;
                        MAC += w.GetMAC() * w.S;
                        b_2 += w.Getb_2() * w.S;
                        break;
                    }
                    else if (m is FARBasicDragModel)
                    {
                        FARBasicDragModel d = m as FARBasicDragModel;

                        if (d.isShielded)
                            break;

                        Vector3d relPos = p.transform.position - CoM;

                        Vector3d vel = velocity + Vector3d.Cross(AngVel, relPos);

                        double tmpCd = d.GetDragEditor(vel, M);
                        Cd += tmpCd;
                        double tmpCl = d.GetLiftEditor();
                        Cl += tmpCl * -Vector3d.Dot(d.GetLiftDirection(), liftVector);
                        Cy += tmpCl * -Vector3d.Dot(d.GetLiftDirection(), sideways);
                        relPos = d.GetCoDEditor() - CoM;
                        Cm += d.GetMomentEditor() + tmpCl * Vector3d.Dot((relPos), velocity) * -Vector3d.Dot(d.GetLiftDirection(), liftVector) + tmpCd * -Vector3d.Dot((relPos), liftVector);
                        Cn += tmpCd * Vector3d.Dot((relPos), sideways) + tmpCl * Vector3d.Dot((relPos), velocity) * -Vector3d.Dot(d.GetLiftDirection(), sideways);
                        C_roll += tmpCl * Vector3d.Dot((relPos), sideways) * -Vector3d.Dot(d.GetLiftDirection(), liftVector);
                        break;
                    }
                }

                Vector3 part_pos = p.transform.TransformPoint(p.CoMOffset) - CoM;
                double partMass = p.mass;
                if (vehicleFueled && p.Resources.Count > 0)
                    partMass += p.GetResourceMass();

                double stock_drag = partMass * p.maximum_drag * FlightGlobals.DragMultiplier * 1000;
                Cd += stock_drag;
                Cm += stock_drag * -Vector3d.Dot(part_pos, liftVector);
                Cn += stock_drag * Vector3d.Dot(part_pos, sideways);
            }
            if (area == 0)
            {
                area = 1;
                b_2 = 1;
                MAC = 1;
            }

            double recipArea = 1 / area;

            MAC *= recipArea;
            b_2 *= recipArea;
            Cl *= recipArea;
            Cd *= recipArea;
            Cm *= recipArea / MAC;
            Cy *= recipArea;
            Cn *= recipArea / b_2;
            C_roll *= recipArea / b_2;
        }
    }
}
