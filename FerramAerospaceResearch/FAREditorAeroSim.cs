/*
Ferram Aerospace Research v0.14.4
Copyright 2014, Michael Ferrara, aka Ferram4

    This file is part of Ferram Aerospace Research.

    Ferram Aerospace Research is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Ferram Aerospace Research is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Ferram Aerospace Research.  If not, see <http://www.gnu.org/licenses/>.

    Serious thanks:		a.g., for tons of bugfixes and code-refactorings
            			Taverius, for correcting a ton of incorrect values
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager 1.5 updates
            			ialdabaoth (who is awesome), who originally created Module Manager
                        Regex, for adding RPM support
            			Duxwing, for copy editing the readme
 * 
 * Kerbal Engineer Redux created by Cybutek, Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License
 *      Referenced for starting point for fixing the "editor click-through-GUI" bug
 *
 * Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/55219
 *
 * Toolbar integration powered by blizzy78's Toolbar plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/60863
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using KSP;

namespace ferram4
{
    class FAREditorAeroSim
    {
        public Texture2D CalculateExcessPowerPlot(double minMach, double maxMach, double minAlt, double maxAlt, CelestialBody body, int width, int height, double maxExcessPower)
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
            double mass, area;  //Establish vehicle characteristics
            Vector3d CoM;
            SetVehicleCharacteristics(out mass, out area, out CoM);
            double specExcessPower = 0;

            double alpha;

            Texture2D texture = new Texture2D(width, height);

            FARAeroUtil.UpdateCurrentActiveBody(body);

            //string s = "";

            int xPixel = 0, yPixel = 0, lastXPixel = 0, lastYPixel = 0;
            while (curMach <= maxMach)
            {
                lastXPixel = xPixel;
                double tmp = (curMach - minMach) / (maxMach - minMach);
                xPixel = (int)(tmp * width);

                bool contAltUntilPosPower = true;
                double estLiftSlope = EstimateLiftSlope(CoM, curMach);

                while (curAltitude <= maxAlt && (contAltUntilPosPower || specExcessPower > 0))
                {
                    double vel = Math.Sqrt((FlightGlobals.getExternalTemperature((float)curAltitude, body) + FARAeroUtil.currentBodyTemp) * FARAeroUtil.currentBodyAtm.x) * curMach;

                    double drag = IterateToSteadyFlightDrag(out alpha, estLiftSlope, vel, curAltitude, curMach, body, mass, area, CoM);
                    alpha = 0;
                    alpha *= FARMathUtil.deg2rad;

                    Vector3d velocityVec = Vector3d.forward * Math.Cos(alpha) - Vector3d.up * Math.Sin(alpha);
                    double thrust = CalculateThrust(curMach, vel, curAltitude, velocityVec, body, standardLegacyEngines, standardFXEngines, ajeJetEngines, ajePropEngines, ajeInlets);
                    specExcessPower = vel * (thrust - drag) / mass;

                    if (specExcessPower > 0)
                        contAltUntilPosPower = false;       //this handles spec power being < 0 for Mach > 1 near the ground

                    //Debug.Log("Mach: " + curMach + " Alt: " + curAltitude + " thrust: " + thrust + " drag: " + drag + " vel: " + vel + " AoA: " + alpha + "\n\r");
                    tmp = (curAltitude - minAlt) / (maxAlt - minAlt);
                    lastYPixel = yPixel;
                    yPixel = (int)(tmp * height);

                    if (xPixel < lastXPixel)
                    {
                        texture.SetPixel(xPixel, yPixel, ColorFromVal((float)maxExcessPower, specExcessPower));
                        curAltitude += 150;
                        continue;
                    }
                    Color topRightColor = ColorFromVal((float)maxExcessPower, specExcessPower);
                    Color bottomLeftColor = texture.GetPixel(lastXPixel, lastYPixel);
                    Color topLeftColor = texture.GetPixel(lastXPixel, yPixel);
                    Color bottomRightColor = texture.GetPixel(xPixel, lastYPixel);

                    for (int i = lastXPixel; i <= xPixel; i++)
                    {
                        float xFrac = (float)(i - lastXPixel) / (float)(xPixel - lastXPixel);
                        for (int j = lastYPixel; j <= yPixel; j++)
                        {
                            float yFrac = (float)(j - lastYPixel) / (float)(yPixel - lastYPixel);
                            texture.SetPixel(i, j, bottomLeftColor + xFrac * (bottomRightColor - bottomLeftColor) + yFrac * (topLeftColor - bottomLeftColor) + xFrac * yFrac * (bottomLeftColor + topRightColor - bottomRightColor - topLeftColor));
                        }
                    }
                    curAltitude += 150;
                }

                if(curAltitude < maxAlt)
                {
                    for (int i = lastXPixel; i <= xPixel; i++)
                        for (int j = yPixel; j < texture.height; j++)
                        {
                            texture.SetPixel(i, j, Color.black);
                        }
                    
                }
                curAltitude = minAlt;
                curMach += 0.025;
            }
            texture.Apply();
            //Debug.Log(s);

            return texture;
        }

        private Color ColorFromVal(float maximumVal, double val)
        {
            float fracMaxVal = (float)(val / maximumVal);

            if (fracMaxVal < 0)
                return Color.black;

            if (fracMaxVal < 0.2f)
                return new Color(0, fracMaxVal * 5, 1);

            if (fracMaxVal < 0.4f)
                return new Color(0, 1, 2 - 5 * fracMaxVal);

            if (fracMaxVal < 0.6f)
                return new Color(fracMaxVal * 5 - 2, 1, 0);

            if(fracMaxVal < 0.8f)
                return new Color(1, 4 - 5 * fracMaxVal, 0);

            if (fracMaxVal < 1)
                return new Color(1, 5 * fracMaxVal - 4, 5 * fracMaxVal - 4);

            return Color.white;
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
                inlet.Invoke(m, new object[] { FARAeroUtil.CurEditorParts });

                MethodInfo flightCondition = engineType.GetMethod("UpdateFlightCondition");
                flightCondition.Invoke(m, new object[] { alt, vel, body});

                MethodInfo calcThrust = engineType.GetMethod("CalculateThrust");
                calcThrust.Invoke(m, new object[] { 1 });

                if(m.part.temperature <= m.part.maxTemp)
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
                if (p.Resources.Count > 0)
                    partMass += p.GetResourceMass();

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

        //This needs heavy optimization for the chart to be usable; it takes too long to hit all the points
        private double IterateToSteadyFlightDrag(out double alpha, double estLiftSlope, double u0, double alt, double M, CelestialBody body, double mass, double area, Vector3d CoM)
        {
            double effectiveG = CalculateAccelerationDueToGravity(body, alt);     //This is the effect of gravity
            double q = FARAeroUtil.GetCurrentDensity(body, alt) * u0 * u0 * 0.5;
            effectiveG -= u0 * u0 / (alt + body.Radius);                          //This is the effective reduction of gravity due to high velocity
            double neededCl = mass * effectiveG * 1000 / (q * area);

            alpha = neededCl / estLiftSlope;

            if (alpha >= 25)
                return Double.PositiveInfinity;

            double lowerAlpha = alpha - 5;
            double upperAlpha = alpha + 5;

            SetState(M, neededCl, CoM, 0, 0, false);
            alpha = FARMathUtil.BrentsMethod(FunctionIterateForAlphaExcessPower, lowerAlpha, upperAlpha, 10, 0.1);
            //double Cd = this.Cd;


            return Cd * area * q * 0.001;
        }

        //Returns Cl per degree
        private double EstimateLiftSlope(Vector3d CoM, double MachNumber)
        {
            double zeroCl, pertCl, tmp;
            GetClCdCmSteady(CoM, 0, MachNumber, out zeroCl, out tmp, true, true, flaps, spoilers);
            GetClCdCmSteady(CoM, 0.1, MachNumber, out pertCl, out tmp, true, true, flaps, spoilers);

            return (pertCl - zeroCl) * 10;
        }

        public double CalculateAccelerationDueToGravity(CelestialBody body, double alt)
        {
            double radius = body.Radius + alt;
            double mu = body.gravParameter;

            double accel = radius * radius;
            accel = mu / accel;
            return accel;
        }
        
        public void GetClCdCmSteady(Vector3d CoM, double alpha, double M, out double Cl, out double Cd, bool clear, bool reset_stall = false, int flap_setting = 0, bool spoilersDeployed = false, bool vehicleFueled = true)
        {
            Cl = 0;
            Cd = 0;
            double area = 0;

            alpha *= FARMathUtil.deg2rad;

            Vector3d forward = Vector3.forward;
            Vector3d up = Vector3.up;
            Vector3d right = Vector3.right;

            if (EditorLogic.fetch.editorType == EditorLogic.EditorMode.VAB)
            {
                forward = Vector3.up;
                up = -Vector3.forward;
            }


            Vector3d velocity = forward * Math.Cos(alpha) - up * Math.Sin(alpha);

            //velocity.Normalize();

            Vector3d liftVector = -forward * Math.Sin(alpha) - up * Math.Cos(alpha);

            Vector3d sideways = Vector3.Cross(velocity, liftVector);

            for (int i = 0; i < FARAeroUtil.CurEditorWings.Count; i++)
            {
                FARWingAerodynamicModel w = FARAeroUtil.CurEditorWings[i];
                if (w.isShielded)
                    continue;

                if (clear)
                    w.EditorClClear(reset_stall);

                if (w is FARControllableSurface)
                    (w as FARControllableSurface).SetControlStateEditor(CoM, velocity, 0, 0, 0, flap_setting, spoilersDeployed);

                w.ComputeClCdEditor(velocity, M);

                double tmpCl = w.GetCl() * w.S;
                Cl += tmpCl * -Vector3d.Dot(w.GetLiftDirection(), liftVector);
                double tmpCd = w.GetCd() * w.S;
                Cd += tmpCd;
                area += w.S;
            }
            for (int i = 0; i < FARAeroUtil.CurEditorOtherDrag.Count; i++)
            {
                FARBasicDragModel d = FARAeroUtil.CurEditorOtherDrag[i];
                if (d.isShielded)
                    continue;


                double tmpCd = d.GetDragEditor(velocity, M);
                Cd += tmpCd;
                double tmpCl = d.GetLiftEditor();
                Cl += tmpCl * -Vector3d.Dot(d.GetLiftDirection(), liftVector);
            }
            /*for (int i = 0; i < FARAeroUtil.CurEditorParts.Count; i++)
            {
                Part p = FARAeroUtil.CurEditorParts[i];
                if (FARAeroUtil.IsNonphysical(p))
                    continue;

                Vector3 part_pos = p.transform.TransformPoint(p.CoMOffset) - CoM;
                double partMass = p.mass;
                if (vehicleFueled && p.Resources.Count > 0)
                    partMass += p.GetResourceMass();

                double stock_drag = partMass * p.maximum_drag * FlightGlobals.DragMultiplier * 1000;
                Cd += stock_drag;
                Cm += stock_drag * -Vector3d.Dot(part_pos, liftVector);
                Cn += stock_drag * Vector3d.Dot(part_pos, sideways);
            }*/
            if (area == 0)
            {
                area = 1;
            }

            double recipArea = 1 / area;

            Cl *= recipArea;
            Cd *= recipArea;
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

            //velocity.Normalize();

            Vector3d liftVector = -forward * Math.Sin(alpha) + right * Math.Sin(phi) * Math.Cos(alpha) - up * Math.Cos(phi) * Math.Cos(alpha);

            Vector3d sideways = Vector3.Cross(velocity, liftVector);

            for (int i = 0; i < FARAeroUtil.CurEditorWings.Count; i++ )
            {
                FARWingAerodynamicModel w = FARAeroUtil.CurEditorWings[i];
                if (w.isShielded)
                    continue;

                if (clear)
                    w.EditorClClear(reset_stall);

                Vector3d relPos = w.GetAerodynamicCenter() - CoM;

                Vector3d vel = velocity + Vector3d.Cross(AngVel, relPos);

                if (w is FARControllableSurface)
                    (w as FARControllableSurface).SetControlStateEditor(CoM, vel, (float)pitch, 0, 0, flap_setting, spoilersDeployed);

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
            }
            for (int i = 0; i < FARAeroUtil.CurEditorOtherDrag.Count; i++)
            {
                FARBasicDragModel d = FARAeroUtil.CurEditorOtherDrag[i];
                if (d.isShielded)
                    continue;

                Vector3d relPos = d.part.transform.position - CoM;

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

            }
            /*for (int i = 0; i < FARAeroUtil.CurEditorParts.Count; i++)
            {
                Part p = FARAeroUtil.CurEditorParts[i];
                if (FARAeroUtil.IsNonphysical(p))
                    continue;

                Vector3 part_pos = p.transform.TransformPoint(p.CoMOffset) - CoM;
                double partMass = p.mass;
                if (vehicleFueled && p.Resources.Count > 0)
                    partMass += p.GetResourceMass();

                double stock_drag = partMass * p.maximum_drag * FlightGlobals.DragMultiplier * 1000;
                Cd += stock_drag;
                Cm += stock_drag * -Vector3d.Dot(part_pos, liftVector);
                Cn += stock_drag * Vector3d.Dot(part_pos, sideways);
            }*/
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

        private double MachNumber;
        private double neededCl;
        Vector3d CoM;
        double pitch;
        int flaps;
        bool spoilers;

        public void SetState(double M, double Cl, Vector3d CoM, double pitch, int flapSetting, bool spoilers)
        {
            MachNumber = M;
            neededCl = Cl;
            this.CoM = CoM;
            this.pitch = pitch;
            flaps = flapSetting;
            this.spoilers = spoilers;
        }

        public double FunctionIterateForAlphaExcessPower(double alpha)
        {
            double Cl;
            GetClCdCmSteady(CoM, alpha, MachNumber, out Cl, out Cd, true, true, flaps, spoilers);
            return Cl - neededCl;
        }


        public double FunctionIterateForAlpha(double alpha)
        {
            double Cl;
            GetClCdCmSteady(CoM, alpha, 0, 0, 0, 0, 0, MachNumber, pitch, out Cl, out Cd, out Cm, out Cy, out Cn, out C_roll, true, true, flaps, spoilers);
            return Cl - neededCl;
        }

        public double Cd, Cm, Cy, Cn, C_roll;
    }
}
