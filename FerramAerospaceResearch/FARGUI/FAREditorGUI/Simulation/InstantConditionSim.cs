/*
Ferram Aerospace Research v0.14.6
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
using UnityEngine;
using ferram4;
using FerramAerospaceResearch.FARAeroComponents;

namespace FerramAerospaceResearch.FARGUI.FAREditorGUI.Simulation
{
    class InstantConditionSim
    {
        List<FARAeroSection> _currentAeroSections;
        List<FARAeroPartModule> _currentAeroModules;
        List<FARWingAerodynamicModel> _wingAerodynamicModel;

        double _maxCrossSectionFromBody;
        double _bodyLength;

        public void UpdateAeroData(VehicleAerodynamics vehicleAero, List<FARWingAerodynamicModel> wingAerodynamicModel)
        {
            vehicleAero.GetNewAeroData(out _currentAeroModules, out _currentAeroSections);
            _wingAerodynamicModel = wingAerodynamicModel;
            _maxCrossSectionFromBody = vehicleAero.MaxCrossSectionArea;
            _bodyLength = vehicleAero.Length;
        }

        public double CalculateAccelerationDueToGravity(CelestialBody body, double alt)
        {
            double radius = body.Radius + alt;
            double mu = body.gravParameter;

            double accel = radius * radius;
            accel = mu / accel;
            return accel;
        }

        public void GetClCdCmSteady(InstantConditionSimInput input, out InstantConditionSimOutput output, bool clear, bool reset_stall = false)
        {
            output = new InstantConditionSimOutput();

            double area = 0;
            double MAC = 0;
            double b_2 = 0;

            Vector3d forward = Vector3.forward;
            Vector3d up = Vector3.up;
            Vector3d right = Vector3.right;

            Vector3d CoM = Vector3d.zero;
            double mass = 0;
            List<Part> partsList = EditorLogic.SortedShipList;
            for (int i = 0; i < partsList.Count; i++)
            {
                Part p = partsList[i];

                if (FARAeroUtil.IsNonphysical(p))
                    continue;

                double partMass = p.mass;
                if (p.Resources.Count > 0)
                    partMass += p.GetResourceMass();

                partMass += p.GetModuleMass(p.mass);
                CoM += partMass * (Vector3d)p.transform.TransformPoint(p.CoMOffset);
                mass += partMass;
            }
            CoM /= mass;

            if (EditorDriver.editorFacility == EditorFacility.VAB)
            {
                forward = Vector3.up;
                up = -Vector3.forward;
            }

            double sinAlpha = Math.Sin(input.alpha * Math.PI / 180);
            double cosAlpha = Math.Sqrt(Math.Max(1 - sinAlpha * sinAlpha, 0));

            double sinBeta = Math.Sin(input.beta * Math.PI / 180);
            double cosBeta = Math.Sqrt(Math.Max(1 - sinBeta * sinBeta, 0));

            double sinPhi = Math.Sin(input.phi * Math.PI / 180);
            double cosPhi = Math.Sqrt(Math.Max(1 - sinPhi * sinPhi, 0));

            double alphaDot = input.alphaDot * Math.PI / 180;
            double betaDot = input.betaDot * Math.PI / 180;
            double phiDot = input.phiDot * Math.PI / 180;

            Vector3d AngVel = (phiDot - sinAlpha * betaDot) * forward + (cosPhi * alphaDot + cosAlpha * sinPhi * betaDot) * right + (sinPhi * alphaDot - cosAlpha * cosPhi * betaDot) * up;

            Vector3d velocity = forward * cosAlpha * cosBeta + right * (sinPhi * cosAlpha * cosBeta - cosPhi * sinBeta) - up * cosPhi * (sinAlpha * cosBeta - sinBeta);

            velocity.Normalize();

            //this is negative wrt the ground
            Vector3d liftVector = -forward * sinAlpha + right * sinPhi * cosAlpha - up * cosPhi * cosAlpha;

            Vector3d sideways = Vector3.Cross(velocity, liftVector).normalized;


            for (int i = 0; i < _wingAerodynamicModel.Count; i++)
            {
                FARWingAerodynamicModel w = _wingAerodynamicModel[i];
                if (!(w && w.part))
                    continue;

                if (w.isShielded)
                    continue;

                if (clear)
                    w.EditorClClear(reset_stall);

                //w.ComputeForceEditor(velocity, input.machNumber);     //do this just to get the AC right
                Vector3d relPos = w.GetAerodynamicCenter() - CoM;

                Vector3d vel = velocity + Vector3d.Cross(AngVel, relPos);

                if (w is FARControllableSurface)
                    (w as FARControllableSurface).SetControlStateEditor(CoM, vel, (float)input.pitchValue, 0, 0, input.flaps, input.spoilers);

                Vector3d force = w.ComputeForceEditor(vel.normalized, input.machNumber) * 1000;

                output.Cl += -Vector3d.Dot(force, liftVector);
                output.Cy += -Vector3d.Dot(force, sideways);
                output.Cd += -Vector3d.Dot(force, velocity);

                Vector3d moment = -Vector3d.Cross(relPos, force);

                output.Cm += Vector3d.Dot(moment, sideways);
                output.Cn += -Vector3d.Dot(moment, liftVector);
                output.C_roll += Vector3d.Dot(moment, velocity);

                //w.ComputeClCdEditor(vel.normalized, input.machNumber);

                /*double tmpCl = w.GetCl() * w.S;
                output.Cl += tmpCl * -Vector3d.Dot(w.GetLiftDirection(), liftVector);
                output.Cy += tmpCl * -Vector3d.Dot(w.GetLiftDirection(), sideways);
                double tmpCd = w.GetCd() * w.S;
                output.Cd += tmpCd;
                output.Cm += tmpCl * Vector3d.Dot((relPos), velocity) * -Vector3d.Dot(w.GetLiftDirection(), liftVector) + tmpCd * -Vector3d.Dot((relPos), liftVector);
                output.Cn += tmpCd * Vector3d.Dot((relPos), sideways) + tmpCl * Vector3d.Dot((relPos), velocity) * -Vector3d.Dot(w.GetLiftDirection(), sideways);
                output.C_roll += tmpCl * Vector3d.Dot((relPos), sideways) * -Vector3d.Dot(w.GetLiftDirection(), liftVector);*/
                area += w.S;
                MAC += w.GetMAC() * w.S;
                b_2 += w.Getb_2() * w.S;
            }
            FARCenterQuery center = new FARCenterQuery();
            for (int i = 0; i < _currentAeroSections.Count; i++)
            {
                _currentAeroSections[i].EditorCalculateAeroForces(1, (float)input.machNumber, 10000, 0.005f, velocity.normalized, center);
            }

            Vector3d centerForce = center.force * 1000;

            output.Cl += -Vector3d.Dot(centerForce, liftVector);
            output.Cy += -Vector3d.Dot(centerForce, sideways);
            output.Cd += -Vector3d.Dot(centerForce, velocity);

            Vector3d centerMoment = -Vector3d.Cross(center.GetPos() - CoM, centerForce) + center.torque;

            output.Cm += Vector3d.Dot(centerMoment, sideways);
            output.Cn += -Vector3d.Dot(centerMoment, liftVector);
            output.C_roll += Vector3d.Dot(centerMoment, velocity);
            

            /*for (int i = 0; i < FARAeroUtil.CurEditorParts.Count; i++)
            {
                Part p = FARAeroUtil.CurEditorParts[i];
                if (FARAeroUtil.IsNonphysical(p))
                    continue;

                Vector3 part_pos = p.transform.TransformPoint(p.CoMOffset) - CoM;
                double partMass = p.mass;
                if (p.Resources.Count > 0)
                    partMass += p.GetResourceMass();

                double stock_drag = partMass * p.maximum_drag * FlightGlobals.DragMultiplier * 1000;
                output.Cd += stock_drag;
                output.Cm += stock_drag * -Vector3d.Dot(part_pos, liftVector);
                output.Cn += stock_drag * Vector3d.Dot(part_pos, sideways);
            }*/

            if (area == 0)
            {
                area = _maxCrossSectionFromBody;
                b_2 = 1;
                MAC = _bodyLength;
            }

            double recipArea = 1 / area;

            MAC *= recipArea;
            b_2 *= recipArea;
            output.Cl *= recipArea;
            output.Cd *= recipArea;
            output.Cm *= recipArea / MAC;
            output.Cy *= recipArea;
            output.Cn *= recipArea / b_2;
            output.C_roll *= recipArea / b_2;
        }

        private double neededCl;

        private InstantConditionSimInput iterationInput = new InstantConditionSimInput();
        public InstantConditionSimOutput iterationOutput;

        public void SetState(double machNumber, double Cl, Vector3d CoM, double pitch, int flapSetting, bool spoilers)
        {
            iterationInput.machNumber = machNumber;
            neededCl = Cl;
            iterationInput.pitchValue = pitch;
            iterationInput.flaps = flapSetting;
            iterationInput.spoilers = spoilers;

            for (int i = 0; i < FARAeroUtil.CurEditorWings.Count; i++)
            {
                FARWingAerodynamicModel w = FARAeroUtil.CurEditorWings[i];
                if (w.isShielded)
                    continue;

                if (w is FARControllableSurface)
                    (w as FARControllableSurface).SetControlStateEditor(CoM, Vector3.up, (float)pitch, 0, 0, flapSetting, spoilers);
            }
        }

        public double FunctionIterateForAlpha(double alpha)
        {
            iterationInput.alpha = alpha;
            GetClCdCmSteady(iterationInput, out iterationOutput, true, true);
            return iterationOutput.Cl - neededCl;
        }

    }
}
