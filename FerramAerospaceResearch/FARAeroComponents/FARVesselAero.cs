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
using System.Threading;
using System.Linq;
using UnityEngine;
using FerramAerospaceResearch.FARPartGeometry;
using ferram4;

namespace FerramAerospaceResearch.FARAeroComponents
{
    public class FARVesselAero : MonoBehaviour
    {
        Vessel _vessel;
        VesselType _vType;
        int _voxelCount;

        VehicleVoxel _voxel = null;
        VoxelCrossSection[] _vehicleCrossSection = null;

        internal bool ready = false;

        double length = 0;
        List<FARAeroPartModule> _currentAeroModules;
        List<FARAeroPartModule> _newAeroModules;

        List<FARAeroSection> _currentAeroSections;
        List<FARAeroSection> _newAeroSections;

        int _updateRateLimiter = 0;
        bool _updateQueued = false;

        private void Start()
        {
            _vessel = gameObject.GetComponent<Vessel>();
            VesselUpdate();
            this.enabled = true;
            for(int i = 0; i < _vessel.Parts.Count; i++)
            {
                Part p = _vessel.Parts[i];
                p.maximum_drag = 0;
                p.minimum_drag = 0;
                p.angularDrag = 0;
            }
        }

        private void FixedUpdate()
        {
            if (ready)
            {
                _currentAeroSections = _newAeroSections;
                _newAeroSections = null;

                _currentAeroModules = _newAeroModules;
                _newAeroModules = null;
                ready = false;
            } 
            
            if (FlightGlobals.ready && _currentAeroSections != null)
            {
                float atmDensity = (float)_vessel.atmDensity;

                if (atmDensity <= 0)
                    return;

                float machNumber = (float)FARAeroUtil.GetMachNumber(_vessel.mainBody, _vessel.altitude, _vessel.srfSpeed);
                float skinFrictionDragCoefficient = (float)FARAeroUtil.SkinFrictionDrag(_vessel.atmDensity, length, _vessel.srfSpeed, machNumber, FlightGlobals.getExternalTemperature((float)_vessel.altitude, _vessel.mainBody) + 273.15f);

                Vector3 frameVel = Krakensbane.GetFrameVelocityV3f();

                for (int i = 0; i < _currentAeroModules.Count; i++)
                {
                    FARAeroPartModule m = _currentAeroModules[i];
                    if (m != null)
                        m.UpdateVelocity(frameVel);
                }
                
                for (int i = 0; i < _currentAeroSections.Count; i++)
                    _currentAeroSections[i].CalculateAeroForces(atmDensity, machNumber, skinFrictionDragCoefficient);

                for (int i = 0; i < _currentAeroModules.Count; i++)
                {
                    FARAeroPartModule m = _currentAeroModules[i];
                    if (m != null)
                        m.ApplyForces();
                }
            }
            if (_updateRateLimiter < 20)
            {
                _updateRateLimiter++;
            }
            else if (_updateQueued)
                VesselUpdate();
        }

        public void AnimationVoxelUpdate()
        {
            Debug.Log("AnimUpdate");
            VesselUpdate();
        }

        public void VesselUpdate()
        {
            if(_vessel == null)
                _vessel = gameObject.GetComponent<Vessel>();
            if (_updateRateLimiter < 20)
            {
                _updateQueued = true;
                return;
            }
            else
            {
                _updateRateLimiter = 0;
                _updateQueued = false;
            }
            _vType = _vessel.vesselType;
            _voxelCount = VoxelCountFromType();

            ThreadPool.QueueUserWorkItem(CreateVoxel);

            Debug.Log("Updating vessel voxel for " + _vessel.vesselName);
        }

        //TODO: have this grab from a config file
        private int VoxelCountFromType()
        {
            if (_vType == VesselType.Debris || _vType == VesselType.Unknown)
                return 20000;
            else
                return 125000;
        }

        private void CreateVoxel(object nullObj)
        {
            try
            {
                VehicleVoxel newvoxel = new VehicleVoxel(_vessel.parts, _voxelCount, true, true);

                _vehicleCrossSection = new VoxelCrossSection[newvoxel.MaxArrayLength];
                for (int i = 0; i < _vehicleCrossSection.Length; i++)
                    _vehicleCrossSection[i].includedParts = new List<Part>();

                _voxel = newvoxel;


                CalculateVesselAeroProperties();
                ready = true;
            }
            catch(Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void CalculateVesselAeroProperties()
        {
            int front, back, numSections;
            double sectionThickness, maxCrossSectionArea;

            _voxel.CrossSectionData(_vehicleCrossSection, Vector3.up, out front, out back, out sectionThickness, out maxCrossSectionArea);

            numSections = back - front;
            double invMaxRadFactor = 1f / Math.Sqrt(maxCrossSectionArea / Math.PI);

            double finenessRatio = sectionThickness * numSections * 0.5 * invMaxRadFactor;       //vehicle length / max diameter, as calculated from sect thickness * num sections / (2 * max radius) 

            //skin friction and pressure drag for a body, taken from 1978 USAF Stability And Control DATCOM, Section 4.2.3.1, Paragraph A
            double viscousDragFactor = 0;
            viscousDragFactor = 60 / (finenessRatio * finenessRatio * finenessRatio) + 0.0025 * finenessRatio;     //pressure drag for a subsonic / transonic body due to skin friction
            viscousDragFactor++;

            viscousDragFactor /= (double)numSections;   //fraction of viscous drag applied to each section

            double criticalMachNumber = CalculateCriticalMachNumber(finenessRatio);

            double transonicWaveDragFactor = -sectionThickness * sectionThickness / (2 * Math.PI);

            length = sectionThickness * numSections;

            _newAeroSections = new List<FARAeroSection>();
            HashSet<FARAeroPartModule> tmpAeroModules = new HashSet<FARAeroPartModule>();

            for (int i = 0; i <= numSections; i++)  //index in the cross sections
            {
                int index = i + front;      //index along the actual body

                double prevArea, curArea, nextArea;
               
                curArea = _vehicleCrossSection[index].area;
                if (i == 0)
                    prevArea = 0;
                else
                    prevArea = _vehicleCrossSection[index - 1].area;
                if (i == numSections)
                    nextArea = 0;
                else
                    nextArea = _vehicleCrossSection[index + 1].area;

                FloatCurve xForcePressureAoA0 = new FloatCurve();
                FloatCurve xForcePressureAoA180 = new FloatCurve();

                FloatCurve xForceSkinFriction = new FloatCurve();

                //Potential and Viscous lift calcs
                float areaChange = (float)(curArea - prevArea);
                float areaChangeMax = (float)Math.Min(curArea, prevArea) * 0.15f;

                float sonicBaseDrag = 0.21f;

                sonicBaseDrag *= areaChange;    //area base drag acts over

                if (areaChange > areaChangeMax)
                    areaChange = areaChangeMax;
                else if (areaChange < -areaChangeMax)
                    areaChange = -areaChangeMax;
                else
                    sonicBaseDrag *= Math.Abs(areaChange / areaChangeMax);      //some scaling for small changes in cross-section

                double flatnessRatio = _vehicleCrossSection[index].flatnessRatio;
                if (flatnessRatio >= 1)
                    sonicBaseDrag /= (float)flatnessRatio;
                else
                    sonicBaseDrag *= (float)flatnessRatio;

                float viscCrossflowDrag = (float)(Math.Sqrt(curArea / Math.PI) * sectionThickness * 2.4d);

                double surfaceArea = 2d * Math.Sqrt(curArea * Math.PI); //section circumference
                surfaceArea *= sectionThickness;    //section surface area for viscous effects

                xForceSkinFriction.Add(0f, (float)(surfaceArea * viscousDragFactor), 0, 0);   //subsonic incomp visc drag
                xForceSkinFriction.Add(1f, (float)(surfaceArea * viscousDragFactor), 0, 0);   //transonic visc drag
                xForceSkinFriction.Add(2f, (float)surfaceArea, 0, 0);                     //above Mach 1.4, visc is purely surface drag, no pressure-related components simulated

                float sonicWaveDrag = (float)CalculateTransonicWaveDrag(i, index, numSections, front, sectionThickness * sectionThickness);
                float hypersonicDragForward = (float)CalculateHypersonicDrag(prevArea, curArea, sectionThickness);
                float hypersonicDragBackward = (float)CalculateHypersonicDrag(nextArea, curArea, sectionThickness);

                if (hypersonicDragForward > 0)
                    hypersonicDragForward = 0;
                if (hypersonicDragBackward > 0)
                    hypersonicDragBackward = 0;

                xForcePressureAoA0.Add(25f, hypersonicDragForward, 0f, 0f);
                xForcePressureAoA180.Add(25f, -hypersonicDragBackward, 0f, 0f);

                if (sonicBaseDrag > 0)      //occurs with increase in area; force applied at 180 AoA
                {
                    xForcePressureAoA0.Add((float)criticalMachNumber, hypersonicDragForward * 0.4f, 0f, 0f);    //hypersonic drag used as a proxy for effects due to flow separation
                    xForcePressureAoA180.Add((float)criticalMachNumber, (sonicBaseDrag * 0.1f - hypersonicDragBackward * 0.4f), 0f, 0f);

                    xForcePressureAoA0.Add(1f, sonicWaveDrag + hypersonicDragForward * 0.6f, 0f, 0f);     //positive is force forward; negative is force backward
                    xForcePressureAoA180.Add(1f, -sonicWaveDrag - hypersonicDragBackward * 0.6f + sonicBaseDrag, 0f, 0f);
                }
                else if (sonicBaseDrag < 0)
                {
                    xForcePressureAoA0.Add((float)criticalMachNumber, (sonicBaseDrag * 0.1f + hypersonicDragForward * 0.4f), 0f, 0f);
                    xForcePressureAoA180.Add((float)criticalMachNumber, -hypersonicDragBackward * 0.4f, 0f, 0f);

                    xForcePressureAoA0.Add(1f, sonicWaveDrag + hypersonicDragForward * 0.6f + sonicBaseDrag, 0f, 0f);     //positive is force forward; negative is force backward
                    xForcePressureAoA180.Add(1f, -sonicWaveDrag - hypersonicDragBackward * 0.6f, 0f, 0f);
                }
                else
                {
                    xForcePressureAoA0.Add((float)criticalMachNumber, hypersonicDragForward * 0.4f, 0f, 0f);
                    xForcePressureAoA180.Add((float)criticalMachNumber, -hypersonicDragBackward * 0.4f, 0f, 0f);

                    xForcePressureAoA0.Add(1f, sonicWaveDrag + hypersonicDragForward * 0.6f, 0f, 0f);     //positive is force forward; negative is force backward
                    xForcePressureAoA180.Add(1f, -sonicWaveDrag - hypersonicDragBackward * 0.6f, 0f, 0f);
                }

                Vector3 xRefVector;
                if (index == front || index == back)
                    xRefVector = Vector3.up;
                else
                    xRefVector = (Vector3)(_vehicleCrossSection[index - 1].centroid - _vehicleCrossSection[index + 1].centroid).normalized;

                Vector3 nRefVector = _vehicleCrossSection[index].flatNormalVector;

                Vector3 centroid = _vessel.transform.localToWorldMatrix.MultiplyPoint3x4(_vehicleCrossSection[index].centroid);
                xRefVector = _vessel.transform.localToWorldMatrix.MultiplyVector(xRefVector);
                nRefVector = _vessel.transform.localToWorldMatrix.MultiplyVector(nRefVector);

                List<Part> includedParts = _vehicleCrossSection[index].includedParts;
                List<FARAeroPartModule> includedModules = new List<FARAeroPartModule>();
                List<float> weighting = new List<float>();

                for (int j = 0; j < includedParts.Count;j++)
                {
                    FARAeroPartModule m = includedParts[j].GetComponent<FARAeroPartModule>();
                    if (m != null)
                        includedModules.Add(m);
                }
                for (int j = 0; j < includedModules.Count; j++)
                {
                    weighting.Add(1 / (float)includedModules.Count);
                }
                FARAeroSection section = new FARAeroSection(xForcePressureAoA0, xForcePressureAoA180, xForceSkinFriction, areaChange, viscCrossflowDrag, (float)flatnessRatio,
                    centroid, xRefVector, nRefVector, includedModules, weighting);

                _newAeroSections.Add(section);
                tmpAeroModules.UnionWith(includedModules);
            }
            _newAeroModules = tmpAeroModules.ToList();
        }

        private double CalculateHypersonicDrag(double lowArea, double highArea, double sectionThickness)
        {
            double drag = highArea + lowArea - 2 * Math.Sqrt(highArea * lowArea);
            drag = drag / (drag + sectionThickness * sectionThickness * Math.PI);
            drag *= drag * drag;
            drag = Math.Sqrt(drag);
            drag *= (lowArea - highArea) * 2;
            return drag;        //force is negative 
        }

        private double CalculateTransonicWaveDrag(int i, int index, int numSections, int front, double sectionThicknessSq)
        {
            double lastLj = 0;
            double drag = 0;
            int limDoubleDrag = Math.Min(i, numSections - i);

            for (int j = 0; j <= limDoubleDrag; j++)      //section of influence from ahead and behind
            {
                double thisLj = (j + 0.5) * Math.Log(j + 0.5);
                double tmp = thisLj;
                thisLj -= lastLj;
                lastLj = tmp;

                tmp = Math.Min(_vehicleCrossSection[index + j].areaDeriv2ToNextSection, _vehicleCrossSection[index + j].area * sectionThicknessSq);
                tmp += Math.Min(_vehicleCrossSection[index - j].areaDeriv2ToNextSection, _vehicleCrossSection[index - j].area * sectionThicknessSq);

                drag += tmp * thisLj;
            }
            if(i < numSections - i)
            {
                for (int j = 2 * i + 1; j < numSections; j++)
                {
                    double thisLj = (j - i + 0.5) * Math.Log(j - i + 0.5);
                    double tmp = thisLj;
                    thisLj -= lastLj;
                    lastLj = tmp;

                    tmp = Math.Min(_vehicleCrossSection[j + front].areaDeriv2ToNextSection, _vehicleCrossSection[j + front].area * sectionThicknessSq);

                    drag += tmp * thisLj;
                }
            }
            else if (i > numSections - i)
            {
                for (int j = 0; j < numSections - 2 * i - 1; j++)
                {
                    double thisLj = (i - j + 0.5) * Math.Log(i - j + 0.5);
                    double tmp = thisLj;
                    thisLj -= lastLj;
                    lastLj = tmp;

                    tmp = Math.Min(_vehicleCrossSection[j + front].areaDeriv2ToNextSection, _vehicleCrossSection[j + front].area * sectionThicknessSq);

                    drag += tmp * thisLj;
                }
            }

            drag *= sectionThicknessSq;
            drag /= 2 * Math.PI;
            drag *= Math.Min(_vehicleCrossSection[index].areaDeriv2ToNextSection, _vehicleCrossSection[index].area * sectionThicknessSq);
            return -drag;
        }

        private double CalculateCriticalMachNumber(double finenessRatio)
        {
            if (finenessRatio > 10)
                return 0.975;
            if (finenessRatio < 1.5)
                return 0.335;
            if(finenessRatio > 4)
            {
                if (finenessRatio > 6)
                    return 0.00625 * finenessRatio + 0.9125;

                return 0.025 * finenessRatio + 0.8;
            }
            else if( finenessRatio < 3)
                return 0.33 * finenessRatio - 0.16;

            return 0.07 * finenessRatio + 0.62;
        }

        /*private void UpdateVesselAeroThreadLoop()
       {
           while (!_threadDone)
           {
               lock (this)
               {
                   Monitor.Wait(this);
                   try
                   {
                       VesselAeroDataUpdate();
                   }
                   catch (Exception e)
                   {
                       Debug.LogException(e);
                   }
               }
           }
       }*/
        /*private void VesselAeroDataUpdate()
         {
             //Vector3 angVelDiff = _vessel.angularVelocity - lastVesselAngVel;
             //Quaternion angVelRot = Quaternion.AngleAxis(-(_vessel.angularVelocity.magnitude * TimeWarp.fixedDeltaTime) * Mathf.Rad2Deg * 1.4f, _vessel.angularVelocity);
             Vector3d velocity = _vessel.ReferenceTransform.worldToLocalMatrix.MultiplyVector(_vessel.srf_velocity);
             //lastVesselAngVel = _vessel.angularVelocity;
             if ((velocity.x != 0 || velocity.y != 0 || velocity.z != 0) && _vessel.atmDensity > 0)
             {
                 int front, back;
                 double sectionThickness, maxCrossSectionArea;

                 _voxel.CrossSectionData(_vehicleCrossSection, velocity, out front, out back, out sectionThickness, out maxCrossSectionArea);

                 Vector3d velNorm = velocity.normalized;

                 double lastLj = 0;
                 //float vehicleLength = sectionThickness * Math.Abs(front - back);
                 //float nonZeroCrossSectionEnd = 0;

                 double skinFrictionDragCoefficient = FARAeroUtil.SkinFrictionDrag(_vessel.atmDensity, sectionThickness * (back - front), _vessel.srfSpeed, machNumber, FlightGlobals.getExternalTemperature((float)_vessel.altitude, _vessel.mainBody) + 273.15f);
                 double invMaxRadFactor = 1f / Math.Sqrt(maxCrossSectionArea / Math.PI);

                 double finenessRatio = sectionThickness * (back - front) * 0.5 * invMaxRadFactor;       //vehicle length / max diameter, as calculated from sect thickness * num sections / (2 * max radius) 

                 //double viscousDrag = 0;          //used in calculating base drag at any point

                 //skin friction and pressure drag for a body, taken from 1978 USAF Stability And Control DATCOM, Section 4.2.3.1, Paragraph A
                 double viscousDragFactor = 0;
                 if (machNumber < 1.2)
                     viscousDragFactor = 60 / (finenessRatio * finenessRatio * finenessRatio) + 0.0025 * finenessRatio;     //pressure drag for a subsonic / transonic body
                 if (machNumber > 1)
                     viscousDragFactor *= (machNumber - 1d) * 5d;          //ensures that this value is only skin friction at Mach > 1.2

                 viscousDragFactor++;
                 viscousDragFactor *= skinFrictionDragCoefficient;       //all of which is affected by skin friction drag

                 viscousDragFactor *= sectionThickness;  //increase per section thickness

                 double sectsSinceExposed = 1, sectsSinceRemoved = 1;

                 for (int j = 0; j <= back - front; j++)
                 {
                     VoxelCrossSection currentSection = _vehicleCrossSection[j + front];
                     VoxelCrossSection prevSection;
                     if (j == 0)
                         prevSection = _vehicleCrossSection[j + front];
                     else
                         prevSection = _vehicleCrossSection[j - 1 + front];


                     double nominalDragDivQ = 0d;         //drag, divided by dynamic pressure; will be fed into aeromodules
                     Vector3d nominalLiftDivQ = Vector3d.zero;            //lift at the current AoA

                     double cosAngle;
                     Vector3d liftVecDir;
                     cosAngle = GetCosAoAFromCenterLineAndVel(velNorm, prevSection.centroid, currentSection.centroid, out liftVecDir);

                     //Zero-lift drag calcs
                     //Viscous drag calcs for a body, taken from 1978 USAF Stability And Control DATCOM, Section 4.2.3.1, Paragraph A
                     nominalDragDivQ += SubsonicViscousDrag(currentSection.area, viscousDragFactor);
                         //SubsonicViscousDrag(j, front, maxCrossSectionArea, invMaxRadFactor, ref viscousDrag, viscousDragFactor, ref currentSection);

                     double slenderBodyFactor = 1d;
                     if (finenessRatio < 4)
                         slenderBodyFactor *= finenessRatio * 0.25;

                     //Supersonic Slender Body theory drag, Mach ~0.8 - ~3.5, based on method of NACA TN 4258
                     if (machNumber > 1.2)
                     {
                         double tmp = 1d;
                         if (machNumber > 3.5)
                             tmp = 1.6 - 0.2 * machNumber;

                         tmp *= slenderBodyFactor;
                         if(machNumber < 8)
                             nominalDragDivQ += SupersonicSlenderBodyDrag(j, front, back, sectionThickness, ref lastLj) * tmp;
                           
                     }
                     else if (machNumber > 0.8)
                     {
                         double tmp = 2.5 * machNumber - 2d;
                         tmp *= slenderBodyFactor;
                         nominalDragDivQ += SupersonicSlenderBodyDrag(j, front, back, sectionThickness, ref lastLj) * tmp;
                     }

                     Vector3d momentDivQ;     //used for additional moments that can't be handled by lift and drag alone

                     //Slender Body Lift calcs (Mach number independent)
                     double nomLiftSlend = 0d;

                     if (machNumber < 8)
                     {
                         nomLiftSlend = currentSection.area - prevSection.area;  //calculate area diff, which is the source of slender body potential lift
                         if (nomLiftSlend > currentSection.area * 0.25)
                             nomLiftSlend = currentSection.area * 0.25;      //assume that maximum area change should only be 25% of current area for increasing
                         if (nomLiftSlend < -currentSection.area * 0.1)
                             nomLiftSlend = -currentSection.area * 0.1;      //assume that maximum area change should only be 10% of cur area for decreasing
                         nomLiftSlend = SlenderBodyLift(cosAngle, nomLiftSlend);
                         if(machNumber > 3)
                         {
                             nomLiftSlend *= 1.6 - 0.2 * machNumber;     //account for reduction at hypersonic velocities
                         }
                     }

                     nominalLiftDivQ = nomLiftSlend * liftVecDir;

                     //pertDragDivQ = pertLiftDivQ * (float)Math.Sqrt(Mathf.Clamp01(1 / (cosAngle * cosAngle) - 1));

                     //Newtonian Impact Calculations
                     double nomDragNewt = 0, nomLiftNewt = 0, pertMomentNewt = 0, pertMomentDampNewt = 0;

                     Vector3d unshadowedLiftVec;// = currentSection.centroid - currentSection.additonalUnshadowedCentroid;
                     //double unshadowedAoA = GetCosAoAFromCenterLineAndVel(velNorm, prevSection.additonalUnshadowedCentroid, currentSection.additonalUnshadowedCentroid, out unshadowedLiftVec);

                     //NewtonianImpactDrag(out nomDragNewt, out nomLiftNewt, out pertMomentNewt, out pertMomentDampNewt, currentSection.area, currentSection.additionalUnshadowedArea, sectionThickness * sectsSinceExposed, machNumber, unshadowedLiftVec.sqrMagnitude * Math.PI / currentSection.area);
                     NewtonianImpactDrag2(out nomDragNewt, out nomLiftNewt, out pertMomentNewt, out pertMomentDampNewt, currentSection.area, prevSection.area, sectionThickness, machNumber, cosAngle);
                     if (currentSection.additionalUnshadowedArea > 0)
                         sectsSinceExposed = 1;
                     else
                         sectsSinceExposed++;

                     unshadowedLiftVec = nomLiftNewt * liftVecDir;
                     momentDivQ = Vector3d.Cross(velNorm, liftVecDir) * pertMomentNewt;
                    
                     //Separated flow rearward side calculations
                     double nomDragSep = 0d, nomLiftSep = 0d;

                     Vector3d sepLiftVec;
                     SeparatedFlowDrag(out nomDragSep, out nomLiftSep, currentSection.area, currentSection.removedArea, sectionThickness * sectsSinceRemoved, machNumber, cosAngle);

                     if (currentSection.removedArea > 0)
                         sectsSinceRemoved = 1;
                     else
                         sectsSinceRemoved++;

                     sepLiftVec = nomLiftSep * liftVecDir;

                    
                     Vector3d forceCenter = currentSection.centroid;
                     double denom = (nominalDragDivQ + nomLiftSlend + nomDragNewt + nomLiftNewt + nomDragSep + nomLiftSep);
                     if (denom != 0)
                     {
                         forceCenter *= (nominalDragDivQ + nomLiftSlend);
                         forceCenter += (nomDragNewt + nomLiftNewt) * currentSection.additonalUnshadowedCentroid + (nomDragSep + nomLiftSep) * currentSection.removedCentroid;
                         forceCenter /= denom;
                     }

                     nominalDragDivQ += nomDragNewt + nomDragSep;

                     nominalLiftDivQ += unshadowedLiftVec + sepLiftVec;

                     double frac = 1d / (double)currentSection.includedParts.Count;

                     nominalDragDivQ *= frac;
                     nominalLiftDivQ *= frac;
                     momentDivQ *= frac;
                     pertMomentDampNewt *= frac;


                     for (int i = 0; i < currentSection.includedParts.Count; i++)
                     {
                         Part p = currentSection.includedParts[i];
                         FARAeroPartModule m;
                         if (aeroModules.TryGetValue(p, out m))
                         {
                             m.IncrementAeroForces(velNorm, forceCenter, (float)nominalDragDivQ, nominalLiftDivQ, momentDivQ, (float)pertMomentDampNewt);
                         }
                     }
                 }
             }
             updateModules = true;
         }

         private double SupersonicSlenderBodyDrag(int j, int front, int back, double sectionThickness, ref double lastLj)
         {
             double thisLj = j + 0.5;
             double tmp = Math.Log(thisLj);

             thisLj *= tmp;

             double crossSectionEffect = 0d;
             for (int i = j + front; i <= back; i++)
             {
                 double area1, area2;
                 area1 = Math.Min(_vehicleCrossSection[i].areaDeriv2ToNextSection, _vehicleCrossSection[i].area * sectionThickness * sectionThickness);
                 area2 = Math.Min(_vehicleCrossSection[i - j].areaDeriv2ToNextSection, _vehicleCrossSection[i - j].area * sectionThickness * sectionThickness);
                 crossSectionEffect += area1 * area2;
             }
             double dragDivQ = (thisLj - lastLj) * crossSectionEffect * sectionThickness * sectionThickness / Math.PI;
             lastLj = thisLj;

             return dragDivQ;
         }

         private double SubsonicViscousDrag(double curSectArea, double viscousDragFactor)//int j, int front, double maxCrossSectionArea, double invMaxRadFactor, ref double viscousDrag, double viscousDragFactor, ref VoxelCrossSection currentSection)
         {
             double tmp = curSectArea * Math.PI;
             if (tmp <= 0)
                 return 0;

             double sectionViscDrag = viscousDragFactor * 2d * Math.Sqrt(tmp);   //increase in viscous drag due to viscosity

             /*viscousDrag += sectionViscDrag / maxCrossSectionArea;     //keep track of viscous drag for base drag purposes

             if (j > 0 && baseRadius > 0)
             {
                 float baseDrag = baseRadius * invMaxRadFactor;     //based on ratio of base diameter to max diameter

                 baseDrag *= baseDrag * baseDrag;    //Similarly based on 1978 USAF Stability And Control DATCOM, Section 4.2.3.1, Paragraph A
                 baseDrag *= 0.029f;
                 baseDrag /= (float)Math.Sqrt(viscousDrag);

                 sectionViscDrag += baseDrag * maxCrossSectionArea;     //and bring it up to the same level as all the others
             }*/
        /*

            return sectionViscDrag;
        }

        private double SlenderBodyLift(double cosAngle, double areaChange)
        {
            double cos2angle = cosAngle * cosAngle;
            double sin2angle = 1 - cos2angle;
            if (sin2angle < 0)
                sin2angle = 0;
            double nomPotentialLift = 2d * Math.Sqrt(sin2angle) * cosAngle;     //convert cosAngle into sin(2 angle) using cos^2 + sin^2 = 1 to get sin and sin(2x) = 2 sin(x) cos(x)
            nomPotentialLift *= areaChange;

            //pertLiftperQ = (cos2angle - sin2angle);
            //pertLiftperQ *= areaChange;

            return nomPotentialLift;
        }

        private double GetCosAoAFromCenterLineAndVel(Vector3d velNormVector, Vector3d forwardCentroid, Vector3d rearwardCentroid, out Vector3d resultingLiftVec)
        {
            Vector3d centroidChange = forwardCentroid - rearwardCentroid;   //vector from rearward centroid to forward centroid in vessel coords
            if (centroidChange.IsZero())
            {
                resultingLiftVec = Vector3d.zero;
                return 1;
            }
            centroidChange.Normalize();
            resultingLiftVec = Vector3d.Exclude(velNormVector, centroidChange);
            resultingLiftVec.Normalize();

            return Vector3d.Dot(velNormVector, centroidChange);   //get cos(angle)
        }

        private void NewtonianImpactDrag2(out double nomDragDivQ, out double nomLiftDivQ, out double nomMomentDivQ, out double pertMomentDampDivQ,
            double curArea, double prevArea, double sectionThickness, double machNumber, double cosAngle)
        {
            double cPmax = 1.86;     //max pressure coefficient, TODO: make function of machNumber
            //double areaDiff = curArea - prevArea;
            if (machNumber < 0.8)
            {
                nomDragDivQ = 0;
                nomLiftDivQ = 0;
                //pertDragDivQ = 0;
                //pertLiftDivQ = 0;
                nomMomentDivQ = 0;
                pertMomentDampDivQ = 0;
                return;
            }
            else if (machNumber < 4)
                cPmax *= (0.3125 * machNumber - 0.25);

            double areaSqrt = Math.Sqrt(curArea * prevArea);

            double cos2Angle = cosAngle * cosAngle;
            double sin2Angle = 1 - cos2Angle;
            if (sin2Angle < 0)
                sin2Angle = 0;
            double sinAngle = Math.Sqrt(sin2Angle);

            double fact1 = prevArea + curArea - 2 * areaSqrt;

            double piSectSq = Math.PI * sectionThickness * sectionThickness;

            double xDivQ, nDivQ;

            nDivQ = piSectSq * fact1;

            double liftDenom = 1 / (piSectSq + fact1);

            nDivQ *= liftDenom;
            nDivQ *= cPmax * sinAngle * cosAngle;
            if (nDivQ < 0)
                nDivQ = 0;

            xDivQ = sin2Angle * piSectSq + fact1 * cos2Angle;
            xDivQ *= fact1;
            xDivQ *= liftDenom;
            xDivQ *= cPmax * sectionThickness;
            if (xDivQ < 0)
                xDivQ = 0;

            nomDragDivQ = xDivQ * cosAngle + nDivQ * sinAngle;
            nomLiftDivQ = nDivQ * cosAngle - xDivQ * sinAngle;

            nomMomentDivQ = fact1 * piSectSq * 0.5;
            nomMomentDivQ += curArea * curArea - prevArea * prevArea - 2 * (curArea - prevArea) * areaSqrt;
            nomMomentDivQ *= -sectionThickness * cPmax;
            nomMomentDivQ *= liftDenom;

            pertMomentDampDivQ = -nomMomentDivQ * sectionThickness * (cos2Angle - sin2Angle);
            nomMomentDivQ *= sinAngle * cosAngle;
        }


        private void NewtonianImpactDrag(out double nomDragDivQ, out double nomLiftDivQ, out double nomMomentDivQ, out double pertMomentDampDivQ,
            double overallArea, double exposedArea, double sectionThickness, double machNumber, double centroidDistFactor)
        {
            double cPmax = 1.86;     //max pressure coefficient, TODO: make function of machNumber

            if (machNumber < 0.8 || exposedArea <= 0 || overallArea <= 0)
            {
                nomDragDivQ = 0;
                nomLiftDivQ = 0;
                //pertDragDivQ = 0;
                //pertLiftDivQ = 0;
                nomMomentDivQ = 0;
                pertMomentDampDivQ = 0;
                return;
            }
            else if (machNumber < 4)
                cPmax *= (0.3125 * machNumber - 0.25);

            /*cPmax *= exposedArea;
            nomDragDivQ = cPmax;
            nomLiftDivQ = Mathf.Clamp(1 / (cosAngle * cosAngle) - 1, 0, float.PositiveInfinity);
            nomLiftDivQ = cPmax * (float)Math.Sqrt(nomLiftDivQ);

            pertDragDivQ = nomLiftDivQ * cosAngle;
            pertLiftDivQ = cPmax / (cosAngle * cosAngle);

            float tmpDist = (float)Math.Sqrt(exposedArea / Math.PI);
            nomMomentDivQ = -cPmax* tmpDist;
            pertMomentDampNewt = -nomMomentDivQ * tmpDist;*/
        /*

            double sin2SurfAngle = CalculateAreaFactor(overallArea, exposedArea, sectionThickness);
            double cos2SurfAngle = 1d - sin2SurfAngle;
            if (cos2SurfAngle < 0)
                cos2SurfAngle = 0;
            double cosSurfAngle = Math.Sqrt(cos2SurfAngle);

            nomMomentDivQ = -2d * cPmax * exposedArea * sin2SurfAngle * cosSurfAngle * sectionThickness * Math.PI * centroidDistFactor;

            pertMomentDampDivQ = 2d * Math.PI * cPmax * exposedArea * sectionThickness * (1d - 2d * sin2SurfAngle) * cosSurfAngle;

            nomDragDivQ = sin2SurfAngle * exposedArea;
            nomDragDivQ *= cPmax;

            nomLiftDivQ = cPmax * cosSurfAngle * exposedArea * centroidDistFactor;
        }

        private void SeparatedFlowDrag(out double nomDragDivQ, out double nomLiftDivQ,
            double overallArea, double removedArea, double sectionThickness, double machNumber, double cosAngle)
        {
            double cDMax = 1.2;     //max rearward drag coefficient, TODO: make function of machNumber

            if (machNumber <= 0 || removedArea <= 0 || overallArea <= 0)
            {
                nomDragDivQ = 0d;
                nomLiftDivQ = 0d;
                //pertDragDivQ = 0;
                //pertLiftDivQ = 0;
                return;
            }
            //double normalMach = cosAngle * machNumber;

            //if (normalMach > 1)
            //    cDMax /= (normalMach * normalMach);

            double areaFactor = removedArea / overallArea;
            double sin2Angle = 1d - cosAngle * cosAngle;
            if (removedArea < 0)
                removedArea = 0;

            if (sin2Angle < 0)
                sin2Angle = 0;

            nomDragDivQ = areaFactor * removedArea;
            nomDragDivQ *= cDMax;

            nomLiftDivQ = nomDragDivQ * Math.Sqrt(sin2Angle);//cDMax * sin2Angle * cosAngle * planArea;

            //pertDragDivQ = nomLiftDivQ * 2f;

            //pertLiftDivQ = cD * 0.6666666667f * (cosAngle * cosAngle - sin2Angle);
        }

        private double CalculateAreaFactor(double overallArea, double exposedArea, double sectionThickness)
        {
            double areaFactor = overallArea * exposedArea;
            areaFactor = Math.Sqrt(areaFactor) * 2d;
            areaFactor -= exposedArea;
            areaFactor /= Math.PI;
            areaFactor /= (areaFactor + sectionThickness * sectionThickness);
            return Math.Max(areaFactor, 0d);
        }

        private double BesselFunction1ApproxAboutPi_4(double x)
        {
            double value = x * ((-0.0471455 * x - 0.0238978) * x + 0.51399) - 0.00291724;
            return value;
        }*/
    }
}
